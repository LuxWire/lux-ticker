package com.lux.ticker.account

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.net.URLEncoder
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64

/**
 * Auth, profiles, read-only entitlements, and app_settings.
 * SQL migrations live in the Windows repo. This client does not create those tables.
 * Requests use the publishable key or the signed-in access token. There is no secret key.
 */
class SupabaseApi(private val http: OkHttpClient) {
    fun signUp(email: String, password: String, username: String): AuthOutcome {
        val body = buildJsonObject {
            put("email", email)
            put("password", password)
            if (username.isNotBlank()) {
                put("data", buildJsonObject { put("username", username) })
            }
        }
        val url = SupabaseConfig.URL + "/auth/v1/signup?redirect_to=" + enc(SupabaseConfig.REDIRECT)
        return authCall(url, body.toString(), SupabaseConfig.PUBLISHABLE_KEY)
    }

    fun signIn(email: String, password: String): AuthOutcome {
        val body = buildJsonObject {
            put("email", email)
            put("password", password)
        }
        val url = SupabaseConfig.URL + "/auth/v1/token?grant_type=password"
        return authCall(url, body.toString(), SupabaseConfig.PUBLISHABLE_KEY)
    }

    fun refresh(refreshToken: String): AuthOutcome {
        val body = buildJsonObject { put("refresh_token", refreshToken) }
        val url = SupabaseConfig.URL + "/auth/v1/token?grant_type=refresh_token"
        return authCall(url, body.toString(), SupabaseConfig.PUBLISHABLE_KEY)
    }

    fun recover(email: String): AuthOutcome {
        val body = buildJsonObject { put("email", email) }
        val url = SupabaseConfig.URL + "/auth/v1/recover?redirect_to=" + enc(SupabaseConfig.REDIRECT)
        val (code, text) = post(url, body.toString(), SupabaseConfig.PUBLISHABLE_KEY)
        if (code in 200..299) return AuthOutcome.ConfirmEmail
        return AuthOutcome.Failure(AuthJson.publicError(text), fatal = code == 400 || code == 401)
    }

    fun logout(accessToken: String) {
        val url = SupabaseConfig.URL + "/auth/v1/logout"
        runCatching { post(url, "{}", accessToken) }
    }

    fun exchange(code: String, verifier: String): AuthOutcome {
        val body = buildJsonObject {
            put("auth_code", code)
            put("code_verifier", verifier)
        }
        val url = SupabaseConfig.URL + "/auth/v1/token?grant_type=pkce"
        return authCall(url, body.toString(), SupabaseConfig.PUBLISHABLE_KEY)
    }

    fun emailForUsername(username: String): UsernameLookup {
        val url = SupabaseConfig.URL + "/rest/v1/rpc/email_for_username"
        val body = buildJsonObject { put("username", username) }.toString()
        val (code, text) = try {
            post(url, body, SupabaseConfig.PUBLISHABLE_KEY)
        } catch (error: Exception) {
            return UsernameLookup.Failed("Couldn't look up that username.")
        }
        if (code == 404) return UsernameLookup.NotReady
        if (code !in 200..299) return UsernameLookup.Failed(AuthJson.publicError(text))
        val email = AuthJson.parseEmail(text)
        return if (email == null) UsernameLookup.NotFound else UsernameLookup.Found(email)
    }

    fun deleteAccount(accessToken: String): DeleteCall {
        val url = SupabaseConfig.URL + "/rest/v1/rpc/delete_own_account"
        val (code, text) = try {
            post(url, "{}", accessToken)
        } catch (error: Exception) {
            return DeleteCall.Failed("Couldn't reach the account server.")
        }
        return when (code) {
            200, 201, 204 -> DeleteCall.Deleted
            404 -> DeleteCall.NotReady
            401 -> if (AuthJson.userGone(text)) DeleteCall.AlreadyGone else DeleteCall.Unauthorized
            else -> DeleteCall.Failed(AuthJson.publicError(text))
        }
    }

    fun fetchProfile(session: AccountSession): ProfileRow? {
        val username = readOwn(
            SupabaseConfig.URL + "/rest/v1/profiles?user_id=eq.${enc(session.userId)}&select=username",
            session.accessToken,
            AuthJson::parseUsername,
        )
        val tier = readOwn(
            SupabaseConfig.URL + "/rest/v1/entitlements?user_id=eq.${enc(session.userId)}&select=tier",
            session.accessToken,
            AuthJson::parseTier,
        )
        if (username == null && tier == null) return null
        return ProfileRow(username.orEmpty(), tier)
    }

    fun upsertProfile(session: AccountSession, username: String): ProfileWrite {
        val url = SupabaseConfig.URL + "/rest/v1/profiles?on_conflict=user_id"
        val (code, text) = post(url, profileJson(session.userId, username), session.accessToken, merge = true)
        return when (code) {
            in 200..299 -> ProfileWrite.Saved
            409 -> ProfileWrite.Taken
            else -> ProfileWrite.Failed(AuthJson.publicError(text))
        }
    }

    fun readSettings(session: AccountSession): RemoteSettings? {
        val url = SupabaseConfig.URL +
            "/rest/v1/app_settings?user_id=eq.${enc(session.userId)}" +
            "&app=eq.${SupabaseConfig.APP_ID}&select=data,updated_at"
        val (code, text) = get(url, session.accessToken)
        if (code == 401) throw UnauthorizedException()
        if (code !in 200..299) return null
        return AuthJson.parseSettings(text)
    }

    fun upsertSettings(session: AccountSession, dataJson: String, updatedAtMillis: Long): Boolean {
        val url = SupabaseConfig.URL + "/rest/v1/app_settings?on_conflict=user_id,app"
        val (code, _) = post(url, settingsBody(session.userId, dataJson, updatedAtMillis), session.accessToken, merge = true)
        if (code == 401) throw UnauthorizedException()
        return code in 200..299
    }

    private fun authCall(url: String, body: String, token: String): AuthOutcome {
        val (code, text) = try {
            post(url, body, token)
        } catch (error: Exception) {
            return AuthOutcome.Failure("Couldn't reach the account server.", fatal = false)
        }
        val parsed = AuthJson.parseAuth(text, System.currentTimeMillis())
        if (parsed is AuthOutcome.Session) return parsed
        if (code in 200..299 && parsed is AuthOutcome.ConfirmEmail) return parsed
        if (parsed is AuthOutcome.Failure) return parsed.copy(fatal = code == 400 || code == 401)
        if (code in 200..299) return AuthOutcome.ConfirmEmail
        return AuthOutcome.Failure(AuthJson.publicError(text), fatal = code == 400 || code == 401)
    }

    private fun post(url: String, json: String, token: String, merge: Boolean = false): Pair<Int, String> {
        val builder = Request.Builder()
            .url(url)
            .post(json.toRequestBody(JSON))
        stamp(builder, token, merge)
        return execute(builder.build())
    }

    private fun readOwn(url: String, token: String, parse: (String) -> String?): String? {
        val (code, text) = try {
            get(url, token)
        } catch (error: Exception) {
            return null
        }
        if (code !in 200..299) return null
        return parse(text)
    }

    private fun get(url: String, token: String): Pair<Int, String> {
        val builder = Request.Builder().url(url).get()
        stamp(builder, token, merge = false)
        return execute(builder.build())
    }

    private fun stamp(builder: Request.Builder, token: String, merge: Boolean) {
        builder
            .header("apikey", SupabaseConfig.PUBLISHABLE_KEY)
            .header("Authorization", "Bearer $token")
            .header("Accept", "application/json")
            .header("Content-Type", "application/json")
        if (merge) builder.header("Prefer", "resolution=merge-duplicates,return=minimal")
    }

    private fun execute(request: Request): Pair<Int, String> {
        http.newCall(request).execute().use { response ->
            return response.code to response.body?.string().orEmpty()
        }
    }

    class UnauthorizedException : Exception("unauthorized")

    companion object {
        private val JSON = "application/json; charset=utf-8".toMediaType()

        fun enc(value: String): String = URLEncoder.encode(value, Charsets.UTF_8.name())

        /** This project enables X OAuth 2.0 as provider `x`, not the older `twitter` provider. */
        fun providerSlug(provider: String): String = when (provider.lowercase()) {
            "x", "twitter" -> "x"
            else -> provider.lowercase()
        }

        fun authorizeUrl(provider: String, codeChallenge: String): String {
            return SupabaseConfig.URL + "/auth/v1/authorize" +
                "?provider=" + enc(providerSlug(provider)) +
                "&redirect_to=" + enc(SupabaseConfig.REDIRECT) +
                "&code_challenge=" + enc(codeChallenge) +
                "&code_challenge_method=s256"
        }

        /** Profile writes send user_id and username. profiles.user_id is the primary key. Entitlements.tier is read-only. */
        fun profileJson(userId: String, username: String): String = buildJsonObject {
            put("user_id", userId)
            put("username", username)
        }.toString()

        fun settingsBody(userId: String, dataJson: String, updatedAtMillis: Long): String {
            val data = Json.parseToJsonElement(dataJson)
            return buildJsonObject {
                put("user_id", userId)
                put("app", SupabaseConfig.APP_ID)
                put("data", data)
                put("updated_at", CloudSync.formatTime(updatedAtMillis))
            }.toString()
        }
    }
}

object AccountPkce {
    fun verifier(): String {
        val bytes = ByteArray(32)
        SecureRandom().nextBytes(bytes)
        return base64Url(bytes)
    }

    fun challenge(verifier: String): String {
        val digest = MessageDigest.getInstance("SHA-256").digest(verifier.toByteArray(Charsets.US_ASCII))
        return base64Url(digest)
    }

    private fun base64Url(bytes: ByteArray): String =
        Base64.getUrlEncoder().withoutPadding().encodeToString(bytes)
}

sealed class AuthOutcome {
    data class Session(val session: AccountSession) : AuthOutcome()
    data object ConfirmEmail : AuthOutcome()
    data class Failure(val message: String, val fatal: Boolean = false) : AuthOutcome()
}

sealed class UsernameLookup {
    data class Found(val email: String) : UsernameLookup()
    data object NotFound : UsernameLookup()
    data object NotReady : UsernameLookup()
    data class Failed(val message: String) : UsernameLookup()
}

sealed class DeleteCall {
    data object Deleted : DeleteCall()
    data object NotReady : DeleteCall()
    data object Unauthorized : DeleteCall()
    data object AlreadyGone : DeleteCall()
    data class Failed(val message: String) : DeleteCall()
}

sealed class ProfileWrite {
    data object Saved : ProfileWrite()
    data object Taken : ProfileWrite()
    data class Failed(val message: String) : ProfileWrite()
}

data class ProfileRow(val username: String, val tier: String?)

data class RemoteSettings(val rawData: String, val updatedAtMillis: Long)

object AuthJson {
    private val json = Json { ignoreUnknownKeys = true }

    fun parseAuth(body: String, nowMillis: Long): AuthOutcome {
        if (body.contains("access_token") && body.contains("refresh_token")) {
            val root = runCatching { json.parseToJsonElement(body).jsonObject }.getOrNull()
            val token = root?.get("access_token")?.jsonPrimitive?.contentOrNull.orEmpty()
            val refresh = root?.get("refresh_token")?.jsonPrimitive?.contentOrNull.orEmpty()
            if (token.isNotBlank() && refresh.isNotBlank() && root != null) {
                val user = root["user"] as? JsonObject
                val userId = user?.get("id")?.jsonPrimitive?.contentOrNull
                    ?: root["id"]?.jsonPrimitive?.contentOrNull
                    ?: ""
                if (userId.isBlank()) return Failure("Sign-in did not return an account id.")
                val email = user?.get("email")?.jsonPrimitive?.contentOrNull
                    ?: root["email"]?.jsonPrimitive?.contentOrNull
                    ?: ""
                val expiresIn = root["expires_in"]?.jsonPrimitive?.contentOrNull?.toLongOrNull()
                val expiresAt = root["expires_at"]?.jsonPrimitive?.contentOrNull?.toLongOrNull()
                val expiry = when {
                    expiresIn != null -> nowMillis + expiresIn * 1000L
                    expiresAt != null && expiresAt < 10_000_000_000L -> expiresAt * 1000L
                    expiresAt != null -> expiresAt
                    else -> nowMillis + 3_600_000L
                }
                return AuthOutcome.Session(
                    AccountSession(userId, token, refresh, expiry, email),
                )
            }
        }
        val root = runCatching { json.parseToJsonElement(body).jsonObject }.getOrNull()
        if (root != null && (root["error"] != null || root["error_description"] != null || root["msg"] != null || root["message"] != null)) {
            return AuthOutcome.Failure(publicError(body))
        }
        val email = root?.get("email")?.jsonPrimitive?.contentOrNull
            ?: (root?.get("user") as? JsonObject)?.get("email")?.jsonPrimitive?.contentOrNull
        if (!email.isNullOrBlank()) return AuthOutcome.ConfirmEmail
        return AuthOutcome.Failure(publicError(body))
    }

    fun parseEmail(body: String): String? {
        val trimmed = body.trim()
        if (trimmed.isEmpty() || trimmed == "null") return null
        val element = runCatching { json.parseToJsonElement(trimmed) }.getOrNull() ?: return null
        val raw = when (element) {
            is JsonPrimitive -> element.contentOrNull
            is JsonObject -> element["email"]?.jsonPrimitive?.contentOrNull
            else -> null
        }?.trim().orEmpty()
        return raw.takeIf { it.contains('@') }
    }

    fun parseUsername(body: String): String? = firstString(body, "username")

    fun parseTier(body: String): String? = firstString(body, "tier")

    private fun firstString(body: String, key: String): String? {
        val array = runCatching { json.parseToJsonElement(body) as? JsonArray }.getOrNull() ?: return null
        val row = array.firstOrNull() as? JsonObject ?: return null
        return row[key]?.jsonPrimitive?.contentOrNull?.takeIf { it.isNotBlank() }
    }

    fun parseSettings(body: String): RemoteSettings? {
        val array = runCatching { json.parseToJsonElement(body) as? JsonArray }.getOrNull() ?: return null
        val row = array.firstOrNull() as? JsonObject ?: return null
        val data = row["data"] ?: return null
        val raw = when (data) {
            is JsonPrimitive -> data.contentOrNull.orEmpty()
            else -> data.toString()
        }
        if (raw.isBlank()) return null
        val stamp = row["updated_at"] as? JsonPrimitive
        val updated = CloudSync.parseTime(stamp?.contentOrNull) ?: return null
        return RemoteSettings(raw, updated)
    }

    fun publicError(body: String): String {
        if (body.contains("access_token") || body.contains("refresh_token")) return "Sign-in failed."
        val root = runCatching { json.parseToJsonElement(body).jsonObject }.getOrNull()
        val message = root?.get("error_description")?.jsonPrimitive?.contentOrNull
            ?: root?.get("msg")?.jsonPrimitive?.contentOrNull
            ?: root?.get("message")?.jsonPrimitive?.contentOrNull
            ?: root?.get("error")?.jsonPrimitive?.contentOrNull
            ?: body
        val clean = message.replace('\n', ' ').trim()
        if (clean.isEmpty() || clean.startsWith("<")) return "The account server rejected that."
        return clean.take(140)
    }

    fun userGone(body: String): Boolean {
        val text = body.lowercase()
        return text.contains("user not found") || text.contains("user_not_found")
    }

    private fun Failure(message: String): AuthOutcome = AuthOutcome.Failure(message)
}
