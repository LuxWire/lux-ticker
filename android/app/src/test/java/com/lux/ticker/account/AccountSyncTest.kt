package com.lux.ticker.account

import android.app.Application
import android.content.Intent
import androidx.browser.customtabs.CustomTabsIntent
import com.lux.ticker.BuildConfig
import com.lux.ticker.ui.ReleaseNotes
import com.lux.ticker.data.AlertKind
import com.lux.ticker.data.SavedAlert
import com.lux.ticker.data.SymbolSpec
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.data.VerticalOffset
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import java.io.File

class AccountSyncTest {
    @Test fun lastWriteWinsUsesTheClockThenTheBytes() {
        assertEquals(CloudSync.Decision.PUSH, CloudSync.decide(5, null, "a", null))
        assertEquals(CloudSync.Decision.PUSH, CloudSync.decide(5, 4, "local", "remote"))
        assertEquals(CloudSync.Decision.PULL, CloudSync.decide(4, 5, "local", "remote"))
        assertEquals(CloudSync.Decision.NONE, CloudSync.decide(5, 5, "same", "same"))
        assertEquals(CloudSync.Decision.PUSH, CloudSync.decide(5, 5, "local", "remote"))
        assertEquals(CloudSync.Decision.PUSH, CloudSync.decide(0, 9_000, "local", "remote", localHasSettings = true))
        assertEquals(CloudSync.Decision.PULL, CloudSync.decide(0, 9_000, "local", "remote", localHasSettings = false))
        assertEquals(CloudSync.Decision.PULL, CloudSync.decide(0, 9_000, "local", "remote"))
    }

    @Test fun partialRemoteKeepsEveryUnsetLocalField() {
        val local = TickerSettings(speedDpPerSec = 80f, verticalOffsetDp = 12.5f, dock = com.lux.ticker.data.Dock.BOTTOM)
        val raw = """{"v":1,"settings":{"dock":"TOP"}}"""
        val merged = CloudSync.merge(local, raw)!!
        assertEquals(com.lux.ticker.data.Dock.TOP, merged.settings.dock)
        assertEquals(80f, merged.settings.speedDpPerSec, 0.001f)
        assertEquals(12.5f, merged.settings.verticalOffsetDp, 0.001f)
        assertNull(merged.symbols)
        val decoded = CloudSync.decode(raw)!!
        assertEquals(50f, decoded.settings.speedDpPerSec, 0.001f)
        assertEquals(VerticalOffset.DEFAULT_DP, decoded.settings.verticalOffsetDp, 0.001f)
    }

    @Test fun payloadRoundTripsOffsetAndWatchlistAndLeavesSecretsOut() {
        val settings = TickerSettings(
            verticalOffsetDp = 12.5f,
            speedDpPerSec = 48f,
            savedAlerts = listOf(SavedAlert("a1", AlertKind.PRICE, "NVDA", 200.0)),
        )
        val symbols = listOf(SymbolSpec("BTC", "Bitcoin"), SymbolSpec("MSTR", "Strategy"))
        val raw = CloudSync.encode(settings, symbols)
        assertFalse(raw.contains("bearer"))
        assertFalse(raw.contains("refresh_token"))
        assertFalse(raw.contains("sb_secret"))
        assertFalse(raw.contains("service_role"))
        assertTrue(raw.contains("verticalOffsetDp"))
        val decoded = CloudSync.decode(raw)!!
        val loaded = decoded.symbols!!
        assertEquals(12.5f, decoded.settings.verticalOffsetDp, 0.001f)
        assertEquals(48f, decoded.settings.speedDpPerSec, 0.001f)
        assertEquals("NVDA", decoded.settings.savedAlerts.single().symbol)
        assertEquals(listOf("BTC", "MSTR"), loaded.map { it.symbol })
        assertEquals("Bitcoin", loaded.first().label)
    }

    @Test fun tierGatesSyncAndProWithoutLettingFreeThrough() {
        assertEquals(AccountTier.FREE, FeatureGate.fromProfile(signedIn = false, raw = "pro"))
        assertEquals(AccountTier.SIGNED_IN, FeatureGate.fromProfile(signedIn = true, raw = null))
        assertEquals(AccountTier.SIGNED_IN, FeatureGate.fromProfile(signedIn = true, raw = "signed_in"))
        assertEquals(AccountTier.SIGNED_IN, FeatureGate.fromProfile(signedIn = true, raw = "nope"))
        assertEquals(AccountTier.PRO, FeatureGate.fromProfile(signedIn = true, raw = "PRO"))
        assertFalse(FeatureGate.allows(AccountTier.FREE, AccountFeature.CLOUD_SYNC))
        assertTrue(FeatureGate.allows(AccountTier.SIGNED_IN, AccountFeature.CLOUD_SYNC))
        assertTrue(FeatureGate.allows(AccountTier.PRO, AccountFeature.CLOUD_SYNC))
        assertFalse(FeatureGate.allows(AccountTier.SIGNED_IN, AccountFeature.PRO_TOOLS))
        assertTrue(FeatureGate.allows(AccountTier.PRO, AccountFeature.PRO_TOOLS))
    }

    @Test fun authJsonDistinguishesSessionConfirmAndError() {
        val now = 1_700_000_000_000L
        val session = AuthJson.parseAuth(
            """{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600,"user":{"id":"user-1","email":"a@b.c"}}""",
            now,
        )
        val signedIn = session as AuthOutcome.Session
        assertEquals("user-1", signedIn.session.userId)
        assertEquals("a@b.c", signedIn.session.email)
        assertEquals(now + 3_600_000L, signedIn.session.expiresAtMillis)
        assertTrue(AuthJson.parseAuth("""{"email":"a@b.c","confirmation_sent_at":"t"}""", now) is AuthOutcome.ConfirmEmail)
        val failure = AuthJson.parseAuth("""{"error":"invalid_grant","error_description":"Invalid login credentials"}""", now)
        assertEquals("Invalid login credentials", (failure as AuthOutcome.Failure).message)
        assertEquals("a@b.c", AuthJson.parseEmail("\"a@b.c\""))
        assertNull(AuthJson.parseEmail("null"))
        assertEquals("a@b.c", AuthJson.parseEmail("""{"email":"a@b.c"}"""))
    }

    @Test fun oauthUrlUsesTheDeepLinkAndDoesNotCarryTheKey() {
        val url = SupabaseApi.authorizeUrl("x", "challenge-value")
        assertTrue(url.contains("redirect_to="))
        assertTrue(url.contains("luxwire"))
        assertTrue(url.contains("auth-callback"))
        assertTrue(url.contains("code_challenge="))
        assertTrue(url.contains("code_challenge_method=s256"))
        assertTrue(url.contains("provider=x"))
        assertFalse(url.contains("provider=twitter"))
        assertTrue(SupabaseConfig.PUBLISHABLE_KEY.isEmpty() || !url.contains(SupabaseConfig.PUBLISHABLE_KEY))
        assertFalse(url.contains("sb_secret"))
        assertEquals("google", SupabaseApi.providerSlug("google"))
        assertEquals("x", SupabaseApi.providerSlug("x"))
        assertEquals("x", SupabaseApi.providerSlug("twitter"))
        val google = SupabaseApi.authorizeUrl("google", "challenge-value")
        assertTrue(google.contains("provider=google"))
        assertTrue(google.contains("code_challenge_method=s256"))
        assertTrue(google.contains("redirect_to="))
        assertTrue(google.contains("/auth/v1/authorize"))
        assertTrue(SupabaseConfig.PUBLISHABLE_KEY.isEmpty() || !google.contains(SupabaseConfig.PUBLISHABLE_KEY))
        val verifier = AccountPkce.verifier()
        val challenge = AccountPkce.challenge(verifier)
        assertTrue(challenge.length >= 43)
        assertFalse(challenge.contains("="))
        assertFalse(challenge.contains("+"))
        assertFalse(challenge == verifier)
        val profile = SupabaseApi.profileJson("user-1", "lux")
        assertTrue(profile.contains("\"user_id\""))
        assertTrue(profile.contains("username"))
        assertFalse(profile.contains("\"id\""))
        assertFalse(profile.contains("tier"))
        assertFalse(profile.contains("pro"))
        val body = SupabaseApi.settingsBody("user-1", """{"v":1}""", 1_700_000_000_000L)
        assertTrue(body.contains("lux-ticker-android"))
        assertFalse(body.contains("tier"))
        assertEquals(VerticalOffset.DEFAULT_DP, 0f, 0.001f)
    }

    @Test fun oauthButtonsAreOnAndTheClientKeyIsPublishable() {
        assertTrue(BuildConfig.ACCOUNT_OAUTH)
        assertEquals("1.4.8", BuildConfig.VERSION_NAME)
        assertEquals(31, BuildConfig.VERSION_CODE)
        assertEquals("https://luxwire.github.io/privacy.html", ReleaseNotes.PRIVACY_URL)
        assertEquals("https://luxwire.github.io/terms.html", ReleaseNotes.TERMS_URL)
        assertEquals(BuildConfig.SUPABASE_URL, SupabaseConfig.URL)
        assertEquals(BuildConfig.SUPABASE_ANON_KEY, SupabaseConfig.PUBLISHABLE_KEY)
        assertFalse(SupabaseConfig.PUBLISHABLE_KEY.contains("sb_secret"))
        val tree = sourceTree()
        assertFalse(tree.contains("sb_secret"))
        assertFalse(tree.contains("service_role"))
        assertTrue(tree.contains("supabase.properties"))
        assertTrue(tree.contains("SUPABASE_ANON_KEY"))
        assertTrue(tree.contains("Continue with Google"))
        assertTrue(tree.contains("Continue with X"))
        assertTrue(tree.contains("CustomTabsIntent"))
        assertTrue(tree.contains("grant_type=pkce"))
        assertTrue(tree.contains(ReleaseNotes.PRIVACY_URL))
        assertTrue(tree.contains(ReleaseNotes.TERMS_URL))
        assertFalse(tree.contains("luxwire-legal"))
        assertTrue(tree.contains("android.support.customtabs.action.CustomTabsService"))
        assertTrue(tree.contains("email_for_username"))
        assertTrue(tree.contains("delete_own_account"))
        assertTrue(tree.contains("lux-ticker-android"))
        assertTrue(tree.contains("/rest/v1/entitlements?"))
        assertTrue(tree.contains("profiles?user_id=eq."))
        assertTrue(tree.contains("on_conflict=user_id"))
        assertFalse(tree.contains("profiles?id=eq."))
        assertFalse(tree.contains("on_conflict=id"))
        assertFalse(tree.contains("delete_my_account"))
        assertFalse(tree.contains("entitlements?on_conflict"))
        assertFalse(tree.contains("ticker_android"))
        assertEquals("pro", AuthJson.parseTier("""[{"tier":"pro"}]"""))
        assertEquals("lux", AuthJson.parseUsername("""[{"username":"lux"}]"""))
        assertNull(AuthJson.parseTier("[]"))
        assertEquals("", UsernameRules.normalize("  "))
        assertEquals("lux_1", UsernameRules.normalize("lux_1"))
        assertNull(UsernameRules.normalize("no"))
        assertNull(UsernameRules.normalize("has space"))
    }

    private fun sourceTree(): String {
        val roots = listOf(File("src/main"), File("app/src/main"))
        val gradle = listOf(File("build.gradle.kts"), File("app/build.gradle.kts"))
        val files = roots.filter { it.isDirectory }.flatMap { root ->
            root.walkTopDown().filter { it.isFile && it.extension in setOf("kt", "xml") }.toList()
        } + gradle.filter { it.isFile }
        return files.joinToString("\n") { it.readText() }
    }
}

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34], application = Application::class)
class SessionPrefsTest {
    @Test fun sessionRoundTripsAndClearRemovesIt() {
        val context = RuntimeEnvironment.getApplication()
        val prefs = SessionPrefs.open(context)
        assertNull(prefs.session())
        val session = AccountSession("user-1", "access-1", "refresh-1", 50L, "a@b.c")
        prefs.save(session)
        prefs.setUsername("lux")
        prefs.setTier("signed_in")
        val loaded = SessionPrefs.open(context).session()
        assertEquals("user-1", loaded!!.userId)
        assertEquals("access-1", loaded.accessToken)
        assertEquals("refresh-1", loaded.refreshToken)
        assertEquals("a@b.c", loaded.email)
        assertEquals("lux", SessionPrefs.open(context).username())
        assertEquals("signed_in", SessionPrefs.open(context).tier())
        prefs.clear()
        assertNull(SessionPrefs.open(context).session())
        assertEquals("", SessionPrefs.open(context).username())
    }

    @Test fun oauthOpensInACustomTab() {
        val intent = AccountBrowser.tabIntent()
        assertEquals(Intent.ACTION_VIEW, intent.action)
        assertTrue((intent.flags and Intent.FLAG_ACTIVITY_NEW_TASK) != 0)
        assertEquals(
            CustomTabsIntent.SHOW_PAGE_TITLE,
            intent.getIntExtra(CustomTabsIntent.EXTRA_TITLE_VISIBILITY_STATE, CustomTabsIntent.NO_TITLE),
        )
        assertEquals(CustomTabsIntent.SHARE_STATE_OFF, intent.getIntExtra(CustomTabsIntent.EXTRA_SHARE_STATE, -1))
    }
}
