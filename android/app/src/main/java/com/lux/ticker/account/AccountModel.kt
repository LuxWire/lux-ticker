package com.lux.ticker.account

import com.lux.ticker.BuildConfig

/**
 * Optional LuxWire account. The project URL and publishable key come from
 * supabase.properties at build time. That file is gitignored. This app does
 * not ship a service-role key and does not include SQL migrations.
 * Tables are profiles, read-only entitlements, and app_settings. The Windows repo owns that schema.
 */
object SupabaseConfig {
    const val URL = BuildConfig.SUPABASE_URL
    const val PUBLISHABLE_KEY = BuildConfig.SUPABASE_ANON_KEY
    const val REDIRECT = "luxwire://auth-callback"
    /** Shared app_settings.app value. The Windows repo owns the table. */
    const val APP_ID = "lux-ticker-android"
    const val SESSION_PREFS = "lux_account_session"
}

enum class AccountTier {
    FREE,
    SIGNED_IN,
    PRO,
    ;

    fun label(): String = when (this) {
        FREE -> "Free"
        SIGNED_IN -> "Signed in"
        PRO -> "Pro"
    }
}

enum class AccountFeature {
    /** Tickers and settings sync. Signed out stays on this phone. */
    CLOUD_SYNC,
    /** Reserved. No Pro tools are shown in this version. */
    PRO_TOOLS,
}

object FeatureGate {
    fun allows(tier: AccountTier, feature: AccountFeature): Boolean = when (feature) {
        AccountFeature.CLOUD_SYNC -> tier != AccountTier.FREE
        AccountFeature.PRO_TOOLS -> tier == AccountTier.PRO
    }

    /** A session with a missing or unknown tier is signed in. Signed out is free. The client never promotes itself to Pro. */
    fun fromProfile(signedIn: Boolean, raw: String?): AccountTier {
        if (!signedIn) return AccountTier.FREE
        return when (raw?.trim()?.lowercase()) {
            "pro" -> AccountTier.PRO
            else -> AccountTier.SIGNED_IN
        }
    }
}

object UsernameRules {
    /** Empty is allowed on sign-up. A non-empty name is 3–24 letters, numbers, or underscores. */
    fun normalize(raw: String): String? {
        val text = raw.trim()
        if (text.isEmpty()) return ""
        if (text.length !in 3..24) return null
        if (!text.all { it.isLetterOrDigit() || it == '_' }) return null
        return text
    }
}

data class AccountSession(
    val userId: String,
    val accessToken: String,
    val refreshToken: String,
    val expiresAtMillis: Long,
    val email: String,
)

data class AccountSnapshot(
    val signedIn: Boolean = false,
    val email: String = "",
    val username: String = "",
    val tier: AccountTier = AccountTier.FREE,
    val message: String = "",
    val error: Boolean = false,
    val busy: Boolean = false,
)
