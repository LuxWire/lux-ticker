package com.lux.ticker.luxwire

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.OkHttpClient
import okhttp3.Request
import java.net.URLEncoder

/** X API v2 timeline for @LuxWire. Public posts use an app bearer. A user token can see more. */
class LuxWireXClient(private val http: OkHttpClient) {
    fun latest(token: String, knownUserId: String): Pair<String, List<LuxWirePost>> {
        val userId = knownUserId.ifBlank { lookupUserId(token) }
        val tweets = get(userTweetsUrl(userId), token)
        return userId to LuxWireX.parseTimeline(tweets)
    }

    private fun lookupUserId(token: String): String {
        val body = get(USER_LOOKUP, token)
        return LuxWireX.parseUserId(body)
            ?: throw LuxWireHttpException(404, "X did not return the LuxWire user id")
    }

    private fun get(url: String, token: String): String {
        val request = Request.Builder()
            .url(url)
            .header("Authorization", "Bearer $token")
            .build()
        http.newCall(request).execute().use { response ->
            val body = response.body?.string().orEmpty()
            if (!response.isSuccessful) throw LuxWireHttpException(response.code, "X HTTP ${response.code}")
            return body
        }
    }

    companion object {
        const val USER_LOOKUP = "https://api.x.com/2/users/by/username/LuxWire"
        private const val FIELDS =
            "tweet.fields=created_at,text,note_tweet,entities,attachments" +
                "&expansions=attachments.media_keys&media.fields=url,preview_image_url,type&max_results=10"

        fun userTweetsUrl(userId: String): String =
            "https://api.x.com/2/users/${encode(userId)}/tweets?exclude=replies,retweets&$FIELDS"

        private fun encode(value: String): String = URLEncoder.encode(value, Charsets.UTF_8.name())
    }
}

class LuxWireHttpException(val code: Int, message: String) : Exception(message)

object LuxWireX {
    private val json = Json { ignoreUnknownKeys = true }

    fun statusUrl(id: String): String = "https://x.com/LuxWire/status/$id"

    fun parseUserId(body: String): String? {
        val root = runCatching { json.parseToJsonElement(body).jsonObject }.getOrNull() ?: return null
        return root["data"]?.jsonObject?.get("id")?.jsonPrimitive?.contentOrNull?.takeIf { it.isNotBlank() }
    }

    /** Newest first, as X returns them. At most 10. */
    fun parseTimeline(body: String): List<LuxWirePost> {
        val root = runCatching { json.parseToJsonElement(body).jsonObject }.getOrNull() ?: return emptyList()
        val data = root["data"] as? JsonArray ?: return emptyList()
        val media = mediaUrls(root["includes"] as? JsonObject)
        return data.mapNotNull { element ->
            val tweet = element as? JsonObject ?: return@mapNotNull null
            val id = tweet.string("id")
            if (id.isBlank() || !id.all { it.isDigit() }) return@mapNotNull null
            val text = noteText(tweet).ifBlank { tweet.string("text") }.ifBlank { "New post" }
            val keys = tweet["attachments"]?.let { it as? JsonObject }
                ?.get("media_keys")?.let { it as? JsonArray }
                ?.mapNotNull { runCatching { it.jsonPrimitive.content }.getOrNull() }
                .orEmpty()
            val image = keys.firstNotNullOfOrNull { media[it] }.orEmpty()
            LuxWirePost(
                id = id,
                text = text.take(600),
                url = statusUrl(id),
                subscriberOnly = subscriber(tweet),
                imageUrl = image,
                articleUrl = articleUrl(tweet),
            )
        }.take(10)
    }

    private fun mediaUrls(includes: JsonObject?): Map<String, String> {
        val list = includes?.get("media") as? JsonArray ?: return emptyMap()
        val out = LinkedHashMap<String, String>()
        list.forEach { element ->
            val item = element as? JsonObject ?: return@forEach
            val key = item.string("media_key")
            val url = item.string("url").ifBlank { item.string("preview_image_url") }
            if (key.isNotBlank() && url.startsWith("https://")) out[key] = url
        }
        return out
    }

    private fun noteText(tweet: JsonObject): String {
        val note = tweet["note_tweet"] as? JsonObject ?: return ""
        return note.string("text")
    }

    private fun articleUrl(tweet: JsonObject): String {
        val entities = tweet["entities"] as? JsonObject ?: return ""
        val urls = entities["urls"] as? JsonArray ?: return ""
        urls.forEach { element ->
            val expanded = (element as? JsonObject)?.string("expanded_url").orEmpty()
            if (expanded.contains("/article/")) return expanded
        }
        return ""
    }

    private fun subscriber(tweet: JsonObject): Boolean {
        if (tweet["subscriber_only"]?.jsonPrimitive?.booleanOrNull == true) return true
        val visibility = tweet.string("visibility")
        return visibility.startsWith("sub", ignoreCase = true)
    }

    private fun JsonObject.string(key: String): String =
        this[key]?.jsonPrimitive?.contentOrNull.orEmpty()
}

/** Which of the latest posts should notify. The first successful poll only records them. */
object LuxWireCursor {
    const val BACKLOG = 10

    data class Step(val notify: List<LuxWirePost>, val seen: Set<String>, val primed: Boolean)

    fun step(primed: Boolean, seen: Set<String>, newestFirst: List<LuxWirePost>): Step {
        val window = newestFirst.filter { it.id.isNotBlank() }.take(BACKLOG)
        val ids = window.map { it.id }
        if (!primed) return Step(emptyList(), seen + ids, primed = true)
        val notify = window.asReversed().filter { it.id !in seen }.take(BACKLOG)
        return Step(notify, seen + ids, primed = true)
    }
}
