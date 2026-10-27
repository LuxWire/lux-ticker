package com.lux.ticker.account

import android.content.Context
import android.content.SharedPreferences
import android.os.Build
import android.util.Log
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import java.util.concurrent.ConcurrentHashMap

/**
 * Session tokens live in [EncryptedSharedPreferences].
 * Robolectric has no Android keystore, so tests use a private file of the same name.
 * A device that cannot open the keystore keeps the session in memory for this process only.
 */
class SessionPrefs private constructor(private val store: Store) {
    fun session(): AccountSession? {
        val userId = store.get(KEY_USER).orEmpty()
        val access = store.get(KEY_ACCESS).orEmpty()
        val refresh = store.get(KEY_REFRESH).orEmpty()
        if (userId.isBlank() || access.isBlank() || refresh.isBlank()) return null
        return AccountSession(
            userId = userId,
            accessToken = access,
            refreshToken = refresh,
            expiresAtMillis = store.get(KEY_EXPIRES)?.toLongOrNull() ?: 0L,
            email = store.get(KEY_EMAIL).orEmpty(),
        )
    }

    fun save(session: AccountSession) {
        store.put(
            mapOf(
                KEY_USER to session.userId,
                KEY_ACCESS to session.accessToken,
                KEY_REFRESH to session.refreshToken,
                KEY_EXPIRES to session.expiresAtMillis.toString(),
                KEY_EMAIL to session.email,
            ),
        )
    }

    fun clear() {
        store.put(
            mapOf(
                KEY_USER to null,
                KEY_ACCESS to null,
                KEY_REFRESH to null,
                KEY_EXPIRES to null,
                KEY_EMAIL to null,
                KEY_USERNAME to null,
                KEY_TIER to null,
                KEY_PENDING to null,
                KEY_VERIFIER to null,
            ),
        )
    }

    fun username(): String = store.get(KEY_USERNAME).orEmpty()

    fun setUsername(username: String) = store.put(mapOf(KEY_USERNAME to username))

    fun tier(): String? = store.get(KEY_TIER)

    fun setTier(raw: String?) = store.put(mapOf(KEY_TIER to raw))

    fun pendingUsername(): String = store.get(KEY_PENDING).orEmpty()

    fun setPendingUsername(username: String) = store.put(mapOf(KEY_PENDING to username))

    fun clearPendingUsername() = store.put(mapOf(KEY_PENDING to null))

    fun verifier(): String? = store.get(KEY_VERIFIER)?.takeIf { it.isNotBlank() }

    fun setVerifier(verifier: String) = store.put(mapOf(KEY_VERIFIER to verifier))

    fun clearVerifier() = store.put(mapOf(KEY_VERIFIER to null))

    private interface Store {
        fun get(key: String): String?
        fun put(values: Map<String, String?>)
    }

    private class PrefStore(private val prefs: SharedPreferences) : Store {
        override fun get(key: String): String? = prefs.getString(key, null)

        override fun put(values: Map<String, String?>) {
            val editor = prefs.edit()
            values.forEach { (key, value) ->
                if (value == null) editor.remove(key) else editor.putString(key, value)
            }
            editor.commit()
        }
    }

    private class MemoryStore : Store {
        private val values = ConcurrentHashMap<String, String>()

        override fun get(key: String): String? = values[key]

        override fun put(values: Map<String, String?>) {
            values.forEach { (key, value) ->
                if (value == null) this.values.remove(key) else this.values[key] = value
            }
        }
    }

    companion object {
        private const val KEY_USER = "user_id"
        private const val KEY_ACCESS = "access"
        private const val KEY_REFRESH = "refresh"
        private const val KEY_EXPIRES = "expires_at"
        private const val KEY_EMAIL = "email"
        private const val KEY_USERNAME = "username"
        private const val KEY_TIER = "profile_tier"
        private const val KEY_PENDING = "pending_username"
        private const val KEY_VERIFIER = "pkce_verifier"

        fun open(context: Context): SessionPrefs {
            val app = context.applicationContext
            val prefs = try {
                encrypted(app)
            } catch (error: Throwable) {
                if (Build.FINGERPRINT == "robolectric") {
                    app.getSharedPreferences(SupabaseConfig.SESSION_PREFS, Context.MODE_PRIVATE)
                } else {
                    Log.w("LuxTicker", "account keystore", error)
                    return SessionPrefs(MemoryStore())
                }
            }
            return SessionPrefs(PrefStore(prefs))
        }

        private fun encrypted(context: Context): SharedPreferences {
            val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
            return EncryptedSharedPreferences.create(
                SupabaseConfig.SESSION_PREFS,
                masterKeyAlias,
                context,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
            )
        }
    }
}
