package com.lux.ticker.luxwire

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import java.security.MessageDigest
import java.security.SecureRandom
import java.net.URLEncoder
import java.util.Base64

/** OAuth 2.0 PKCE for a user token. Subscriber posts need this. The client id is not a secret. */
object LuxWireOAuth {
    const val REDIRECT = "luxwireticker://oauth"
    const val AUTHORIZE = "https://x.com/i/oauth2/authorize"
    const val TOKEN = "https://api.x.com/2/oauth2/token"
    const val SCOPE = "tweet.read users.read offline.access"

    data class Token(val access: String, val refresh: String, val expiresAtMillis: Long)

    fun verifier(): String {
        val bytes = ByteArray(32)
        SecureRandom().nextBytes(bytes)
        return base64Url(bytes)
    }

    fun challenge(verifier: String): String {
        val digest = MessageDigest.getInstance("SHA-256").digest(verifier.toByteArray(Charsets.US_ASCII))
        return base64Url(digest)
    }

    fun authorizeUrl(clientId: String, state: String, codeChallenge: String): String {
        return AUTHORIZE +
            "?response_type=code" +
            "&client_id=" + enc(clientId) +
            "&redirect_uri=" + enc(REDIRECT) +
            "&scope=" + enc(SCOPE) +
            "&state=" + enc(state) +
            "&code_challenge=" + enc(codeChallenge) +
            "&code_challenge_method=S256"
    }

    fun parseToken(body: String, nowMillis: Long): Token? {
        val root = runCatching { Json.parseToJsonElement(body).jsonObject }.getOrNull() ?: return null
        val access = root["access_token"]?.jsonPrimitive?.contentOrNull.orEmpty()
        if (access.isBlank()) return null
        val refresh = root["refresh_token"]?.jsonPrimitive?.contentOrNull.orEmpty()
        val expiresIn = root["expires_in"]?.jsonPrimitive?.contentOrNull?.toLongOrNull() ?: 7200L
        return Token(access, refresh, nowMillis + expiresIn * 1000L)
    }

    private fun enc(value: String): String = URLEncoder.encode(value, Charsets.UTF_8.name())

    private fun base64Url(bytes: ByteArray): String =
        Base64.getUrlEncoder().withoutPadding().encodeToString(bytes)
}
