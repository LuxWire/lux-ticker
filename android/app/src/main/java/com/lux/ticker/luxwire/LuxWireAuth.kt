package com.lux.ticker.luxwire

import android.content.Context
import com.lux.ticker.BuildConfig
import com.lux.ticker.LuxApp
import okhttp3.FormBody
import okhttp3.Request

/**
 * Tokens stay in this app's private storage. Nothing is compiled into the APK.
 * An optional bearer or client id in local.properties is copied into BuildConfig at build time
 * and is empty unless the person building set it.
 */
class LuxWireAuth(context: Context) {
    private val app = context.applicationContext
    private val prefs = app.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun bearer(): String = prefs.getString(BEARER, "").orEmpty().ifBlank { BuildConfig.X_BEARER }

    fun clientId(): String = prefs.getString(CLIENT_ID, "").orEmpty().ifBlank { BuildConfig.X_CLIENT_ID }

    fun hasBearer(): Boolean = stripBearer(bearer()).isNotBlank()

    fun hasUserToken(): Boolean = prefs.getString(ACCESS, "").orEmpty().isNotBlank()

    fun saveBearer(value: String) {
        prefs.edit().putString(BEARER, stripBearer(value)).apply()
    }

    fun saveClientId(value: String) {
        prefs.edit().putString(CLIENT_ID, value.trim()).apply()
    }

    fun signOut() {
        prefs.edit().remove(ACCESS).remove(REFRESH).remove(EXPIRES).remove(VERIFIER).remove(STATE).apply()
    }

    /** Opens the X consent page. Null when no client id is saved. */
    fun beginSignIn(): String? {
        val id = clientId().trim()
        if (id.isBlank()) return null
        val verifier = LuxWireOAuth.verifier()
        val state = LuxWireOAuth.verifier()
        prefs.edit().putString(VERIFIER, verifier).putString(STATE, state).apply()
        return LuxWireOAuth.authorizeUrl(id, state, LuxWireOAuth.challenge(verifier))
    }

    fun exchange(code: String, state: String) {
        val expected = prefs.getString(STATE, "").orEmpty()
        val verifier = prefs.getString(VERIFIER, "").orEmpty()
        prefs.edit().remove(STATE).remove(VERIFIER).apply()
        if (expected.isBlank() || expected != state || verifier.isBlank()) {
            throw LuxWireHttpException(400, "X sign-in state did not match")
        }
        val body = FormBody.Builder()
            .add("grant_type", "authorization_code")
            .add("code", code)
            .add("redirect_uri", LuxWireOAuth.REDIRECT)
            .add("client_id", clientId())
            .add("code_verifier", verifier)
            .build()
        val token = postToken(body) ?: throw LuxWireHttpException(401, "X did not return a token")
        save(token)
    }

    /** User token when signed in, otherwise the app bearer. Null when neither is set. */
    fun authorization(): String? {
        val access = prefs.getString(ACCESS, "").orEmpty()
        if (access.isNotBlank()) {
            val expires = prefs.getLong(EXPIRES, 0L)
            if (expires > System.currentTimeMillis() + 60_000L) return access
            val refresh = prefs.getString(REFRESH, "").orEmpty()
            if (refresh.isNotBlank() && clientId().isNotBlank()) {
                val refreshed = runCatching { refresh(refresh) }.getOrNull()
                if (refreshed != null) return refreshed
            }
            if (expires > System.currentTimeMillis()) return access
        }
        val appBearer = stripBearer(bearer())
        return appBearer.ifBlank { null }
    }

    fun userId(): String = prefs.getString(USER_ID, "").orEmpty()

    fun saveUserId(id: String) {
        if (id.isNotBlank()) prefs.edit().putString(USER_ID, id).apply()
    }

    private fun refresh(refresh: String): String? {
        val body = FormBody.Builder()
            .add("grant_type", "refresh_token")
            .add("refresh_token", refresh)
            .add("client_id", clientId())
            .build()
        val token = postToken(body) ?: return null
        save(token)
        return token.access
    }

    private fun postToken(body: FormBody): LuxWireOAuth.Token? {
        val request = Request.Builder().url(LuxWireOAuth.TOKEN).post(body).build()
        val http = LuxApp.container(app).http
        http.newCall(request).execute().use { response ->
            val text = response.body?.string().orEmpty()
            if (!response.isSuccessful) return null
            return LuxWireOAuth.parseToken(text, System.currentTimeMillis())
        }
    }

    private fun save(token: LuxWireOAuth.Token) {
        prefs.edit()
            .putString(ACCESS, token.access)
            .putString(REFRESH, token.refresh)
            .putLong(EXPIRES, token.expiresAtMillis)
            .apply()
    }

    private fun stripBearer(value: String): String {
        val trimmed = value.trim()
        return if (trimmed.startsWith("Bearer ", ignoreCase = true)) trimmed.substring(7).trim() else trimmed
    }

    companion object {
        private const val PREFS = "lux_wire_auth"
        private const val BEARER = "bearer"
        private const val CLIENT_ID = "client_id"
        private const val ACCESS = "access"
        private const val REFRESH = "refresh"
        private const val EXPIRES = "expires"
        private const val VERIFIER = "verifier"
        private const val STATE = "state"
        private const val USER_ID = "user_id"
    }
}
