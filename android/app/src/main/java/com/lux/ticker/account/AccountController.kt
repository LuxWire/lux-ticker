package com.lux.ticker.account

import android.content.Context
import android.net.Uri
import android.util.Log
import com.lux.ticker.BuildConfig
import com.lux.ticker.data.SettingsStore
import com.lux.ticker.data.SymbolStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Optional sign-in. The ticker keeps working when this stays signed out.
 * Sync is offline-first: DataStore remains the source of truth, and a signed-in
 * tier pushes or pulls app_settings with last-write-wins.
 */
class AccountController(
    context: Context,
    http: OkHttpClient,
    private val settings: SettingsStore,
    private val symbols: SymbolStore,
) {
    private val appContext = context.applicationContext
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val prefs by lazy { SessionPrefs.open(appContext) }
    private val api = SupabaseApi(http)
    private val started = AtomicBoolean(false)
    private val syncLock = Mutex()
    private val dirty = AtomicBoolean(false)
    private var ackedAt = Long.MIN_VALUE
    private var ackedBytes: String? = null
    private val seenCodes = ArrayDeque<String>()

    private val _snapshot = MutableStateFlow(AccountSnapshot())
    val snapshot: StateFlow<AccountSnapshot> = _snapshot.asStateFlow()

    @OptIn(FlowPreview::class)
    fun start() {
        if (!started.compareAndSet(false, true)) return
        scope.launch {
            refreshSnapshot(loadProfile = true)
            settings.settings.debounce(1_500).collect {
                syncOnce()
            }
        }
        scope.launch {
            while (isActive) {
                delay(120_000)
                syncOnce()
            }
        }
        scope.launch {
            AccountEvents.uris.collect { uri ->
                if (uri != null) {
                    AccountEvents.consume()
                    runCatching { completeOAuth(uri) }
                        .onFailure { error ->
                            Log.w(TAG, "oauth", error)
                            message("Couldn't finish sign-in.", error = true)
                        }
                }
            }
        }
    }

    suspend fun signUp(email: String, password: String, usernameRaw: String) = work {
        val emailText = email.trim()
        if (!validEmail(emailText) || password.length < 6) {
            message("Enter an email and a password of at least 6 characters.", error = true)
            return@work
        }
        val username = UsernameRules.normalize(usernameRaw)
        if (username == null) {
            message("Usernames are 3 to 24 letters, numbers, or underscores.", error = true)
            return@work
        }
        if (username.isNotEmpty()) prefs.setPendingUsername(username)
        when (val outcome = api.signUp(emailText, password, username)) {
            is AuthOutcome.ConfirmEmail -> message("Check your email, then sign in.")
            is AuthOutcome.Failure -> {
                prefs.clearPendingUsername()
                applyAuth(outcome)
            }
            else -> applyAuth(outcome)
        }
    }

    suspend fun signIn(email: String, password: String) = work {
        val emailText = email.trim()
        if (!validEmail(emailText) || password.isEmpty()) {
            message("Enter an email and a password.", error = true)
            return@work
        }
        applyAuth(api.signIn(emailText, password))
    }

    suspend fun signInWithUsername(usernameRaw: String, password: String) = work {
        val username = UsernameRules.normalize(usernameRaw)
        if (username.isNullOrEmpty() || password.isEmpty()) {
            message("Enter a username and a password.", error = true)
            return@work
        }
        when (val lookup = api.emailForUsername(username)) {
            is UsernameLookup.Found -> applyAuth(api.signIn(lookup.email, password))
            UsernameLookup.NotFound -> message("No account uses that username.", error = true)
            UsernameLookup.NotReady -> message("Username sign-in is not ready yet. Use your email.", error = true)
            is UsernameLookup.Failed -> message(lookup.message, error = true)
        }
    }

    suspend fun forgot(email: String) = work {
        val emailText = email.trim()
        if (!validEmail(emailText)) {
            message("Enter the email on the account.", error = true)
            return@work
        }
        when (val outcome = api.recover(emailText)) {
            AuthOutcome.ConfirmEmail -> message("Check your email for a reset link.")
            is AuthOutcome.Failure -> message(outcome.message, error = true)
            is AuthOutcome.Session -> applyAuth(outcome)
        }
    }

    suspend fun signOut() {
        val session = runCatching { prefs.session() }.getOrNull()
        runCatching { prefs.clear() }
        refreshSnapshot(loadProfile = false)
        message("Signed out. The ticker stays on this phone.")
        if (session != null) {
            scope.launch { runCatching { api.logout(session.accessToken) } }
        }
    }

    suspend fun deleteAccount() = work {
        val session = validSession()
        if (session == null) {
            message("Sign in before deleting the account.", error = true)
            return@work
        }
        when (val result = api.deleteAccount(session.accessToken)) {
            DeleteCall.Deleted -> {
                prefs.clear()
                refreshSnapshot(loadProfile = false)
                message("Account deleted. The ticker stays on this phone.")
            }
            DeleteCall.NotReady -> message("Account deletion is not ready on the server yet. You are still signed in.", error = true)
            DeleteCall.Unauthorized -> retryDelete(session)
            DeleteCall.AlreadyGone -> {
                prefs.clear()
                refreshSnapshot(loadProfile = false)
                message("This account is already gone. You are signed out on this phone.")
            }
            is DeleteCall.Failed -> message(result.message.ifBlank { "Couldn't delete the account. You are still signed in." }, error = true)
        }
    }

    suspend fun saveUsername(usernameRaw: String) = work {
        val username = UsernameRules.normalize(usernameRaw)
        if (username == null) {
            message("Usernames are 3 to 24 letters, numbers, or underscores.", error = true)
            return@work
        }
        if (username.isEmpty()) {
            message("Enter a username.", error = true)
            return@work
        }
        val session = validSession()
        if (session == null) {
            message("Sign in before saving a username.", error = true)
            return@work
        }
        when (val write = api.upsertProfile(session, username)) {
            ProfileWrite.Saved -> {
                prefs.setUsername(username)
                refreshSnapshot(loadProfile = false)
                message("Username saved.")
            }
            ProfileWrite.Taken -> message("That username is taken.", error = true)
            is ProfileWrite.Failed -> message(write.message, error = true)
        }
    }

    /** Null only if the Google and X buttons are turned off in this build. */
    fun beginOAuth(provider: String): String? {
        if (!BuildConfig.ACCOUNT_OAUTH) return null
        val verifier = AccountPkce.verifier()
        prefs.setVerifier(verifier)
        return SupabaseApi.authorizeUrl(provider, AccountPkce.challenge(verifier))
    }

    private suspend fun retryDelete(session: AccountSession) {
        val refreshed = api.refresh(session.refreshToken)
        if (refreshed !is AuthOutcome.Session) {
            if (refreshed is AuthOutcome.Failure && refreshed.fatal) {
                prefs.clear()
                refreshSnapshot(loadProfile = false)
                message("This account is already gone. You are signed out on this phone.")
            } else {
                message("Couldn't delete the account. You are still signed in.", error = true)
            }
            return
        }
        prefs.save(refreshed.session)
        when (val again = api.deleteAccount(refreshed.session.accessToken)) {
            DeleteCall.Deleted, DeleteCall.AlreadyGone -> {
                prefs.clear()
                refreshSnapshot(loadProfile = false)
                message(
                    if (again is DeleteCall.Deleted) "Account deleted. The ticker stays on this phone."
                    else "This account is already gone. You are signed out on this phone.",
                )
            }
            DeleteCall.NotReady -> message("Account deletion is not ready on the server yet. You are still signed in.", error = true)
            else -> message("Couldn't delete the account. You are still signed in.", error = true)
        }
    }

    private suspend fun completeOAuth(uri: Uri) {
        val key = uri.getQueryParameter("code") ?: uri.toString()
        synchronized(seenCodes) {
            if (key in seenCodes) return
            seenCodes.addLast(key)
            while (seenCodes.size > 8) seenCodes.removeFirst()
        }
        val error = uri.getQueryParameter("error_description") ?: uri.getQueryParameter("error")
        if (!error.isNullOrBlank()) {
            message(error.take(140), error = true)
            return
        }
        val code = uri.getQueryParameter("code")
        if (code.isNullOrBlank()) {
            message("Sign-in did not return a code.", error = true)
            return
        }
        val verifier = prefs.verifier()
        prefs.clearVerifier()
        if (verifier.isNullOrBlank()) {
            message("Sign-in expired. Try again.", error = true)
            return
        }
        applyAuth(api.exchange(code, verifier))
    }

    private suspend fun applyAuth(outcome: AuthOutcome) {
        when (outcome) {
            is AuthOutcome.Session -> {
                prefs.save(outcome.session)
                if (prefs.tier().isNullOrBlank()) prefs.setTier("signed_in")
                val pending = prefs.pendingUsername()
                if (pending.isNotBlank()) {
                    when (val write = api.upsertProfile(outcome.session, pending)) {
                        ProfileWrite.Saved -> {
                            prefs.setUsername(pending)
                            prefs.clearPendingUsername()
                        }
                        ProfileWrite.Taken -> message("That username is taken.", error = true)
                        is ProfileWrite.Failed -> Unit
                    }
                }
                refreshSnapshot(loadProfile = true)
                syncOnce()
                if (_snapshot.value.message.isBlank() || !_snapshot.value.error) {
                    message("Signed in.")
                }
            }
            AuthOutcome.ConfirmEmail -> message("Check your email, then sign in.")
            is AuthOutcome.Failure -> message(outcome.message, error = true)
        }
    }

    private suspend fun refreshSnapshot(loadProfile: Boolean) {
        val session = runCatching { prefs.session() }.getOrNull()
        if (loadProfile && session != null) {
            val profile = runCatching { api.fetchProfile(session) }.getOrNull()
            if (profile != null) {
                if (profile.username.isNotBlank()) prefs.setUsername(profile.username)
                prefs.setTier(profile.tier ?: "signed_in")
            }
        }
        val current = runCatching { prefs.session() }.getOrNull()
        val tier = FeatureGate.fromProfile(current != null, if (current == null) null else prefs.tier())
        _snapshot.update { previous ->
            previous.copy(
                signedIn = current != null,
                email = current?.email.orEmpty(),
                username = if (current == null) "" else prefs.username(),
                tier = tier,
            )
        }
    }

    private suspend fun validSession(): AccountSession? {
        val session = prefs.session() ?: return null
        if (session.expiresAtMillis - 60_000L > System.currentTimeMillis()) return session
        return when (val refreshed = api.refresh(session.refreshToken)) {
            is AuthOutcome.Session -> {
                prefs.save(refreshed.session)
                refreshed.session
            }
            is AuthOutcome.Failure -> {
                if (refreshed.fatal) {
                    prefs.clear()
                    refreshSnapshot(loadProfile = false)
                    message("Sign in again.", error = true)
                    null
                } else {
                    session
                }
            }
            AuthOutcome.ConfirmEmail -> session
        }
    }

    suspend fun syncOnce() {
        if (!syncLock.tryLock()) {
            dirty.set(true)
            return
        }
        try {
            do {
                dirty.set(false)
                syncBody()
            } while (dirty.get())
        } finally {
            syncLock.unlock()
        }
    }

    private suspend fun syncBody() {
        val session = try {
            validSession()
        } catch (error: Exception) {
            Log.w(TAG, "session", error)
            return
        } ?: return
        val tier = FeatureGate.fromProfile(true, prefs.tier())
        if (!FeatureGate.allows(tier, AccountFeature.CLOUD_SYNC)) return
        try {
            val remote = api.readSettings(session)
            val localSymbols = symbols.read()
            val localSettings = settings.read()
            var localBytes = CloudSync.encode(localSettings, localSymbols)
            var localAt = settings.localUpdatedAt()
            val remoteAt = remote?.updatedAtMillis
            val hasLocal = settings.hasStoredSettings()
            if (localAt == ackedAt && localBytes == ackedBytes && localAt != 0L) return
            val decision = CloudSync.decide(localAt, remoteAt, localBytes, remote?.rawData, hasLocal)
            when (decision) {
                CloudSync.Decision.PULL -> {
                    val stamp = remoteAt ?: return
                    val raw = remote.rawData
                    val merged = CloudSync.merge(localSettings, raw) ?: return
                    settings.replaceFromCloud(merged.settings, stamp)
                    merged.symbols?.let { symbols.replace(it) }
                    ackedAt = stamp
                    ackedBytes = CloudSync.encode(settings.read(), symbols.read())
                }
                CloudSync.Decision.NONE -> {
                    ackedAt = localAt
                    ackedBytes = localBytes
                }
                CloudSync.Decision.PUSH -> {
                    if (localAt == 0L) {
                        settings.noteLocalEdit()
                        localAt = settings.localUpdatedAt()
                        localBytes = CloudSync.encode(settings.read(), symbols.read())
                    }
                    if (api.upsertSettings(session, localBytes, localAt)) {
                        ackedAt = localAt
                        ackedBytes = localBytes
                    }
                }
            }
        } catch (error: Exception) {
            Log.w(TAG, "sync", error)
        }
    }

    private suspend fun work(block: suspend () -> Unit) {
        withContext(Dispatchers.IO) {
            _snapshot.update { it.copy(busy = true) }
            try {
                block()
            } catch (error: Exception) {
                Log.w(TAG, "account", error)
                message("Couldn't reach the account server.", error = true)
            } finally {
                _snapshot.update { it.copy(busy = false) }
            }
        }
    }

    private fun message(text: String, error: Boolean = false) {
        _snapshot.update { it.copy(message = text, error = error) }
    }

    private fun validEmail(email: String): Boolean = email.contains('@') && !email.contains(' ') && email.length <= 200

    companion object {
        private const val TAG = "LuxTicker"
    }
}
