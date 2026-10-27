package com.lux.ticker.luxwire

import android.content.Intent
import android.net.Uri

/**
 * One @LuxWire post from the X timeline. Public posts alert with an app bearer.
 * Subscriber posts alert when a signed-in X account can see them.
 * There is no billing and no file handoff.
 */
data class LuxWirePost(
    val id: String,
    val text: String,
    val url: String,
    val subscriberOnly: Boolean = false,
    val slotType: String = "",
    val imageUrl: String = "",
    val articleUrl: String = "",
)

object LuxWirePosts {
    const val PROFILE = "https://x.com/LuxWire"
    const val SUBSCRIBE = "https://x.com/LuxWire/subscribe"

    fun sample(): LuxWirePost = LuxWirePost(
        id = "sim-${System.currentTimeMillis()}",
        text = "New post",
        url = PROFILE,
    )

    fun parseLine(line: String): LuxWirePost? {
        val trimmed = line.trim()
        if (trimmed.isEmpty()) return null
        if (trimmed.startsWith("http://") || trimmed.startsWith("https://")) {
            return LuxWirePost(
                id = statusIdFrom(trimmed).ifBlank { trimmed },
                text = "New post",
                url = trimmed,
            )
        }
        if (!trimmed.startsWith("{")) return null
        val id = jsonString(trimmed, "id").orEmpty()
        val statusId = jsonString(trimmed, "status_id").orEmpty().ifBlank { jsonString(trimmed, "statusId").orEmpty() }
        val text = jsonString(trimmed, "text").orEmpty()
            .ifBlank { jsonString(trimmed, "text_preview").orEmpty() }
            .ifBlank { jsonString(trimmed, "textPreview").orEmpty() }
            .ifBlank { "New post" }
        val rawUrl = jsonString(trimmed, "url").orEmpty()
        val url = openUrl(rawUrl, statusId.ifBlank { id })
        val resolvedId = id.ifBlank { statusId }.ifBlank { statusIdFrom(url) }.ifBlank { url }
        if (resolvedId.isBlank()) return null
        val visibility = jsonString(trimmed, "visibility").orEmpty()
        val subscriber = jsonBool(trimmed, "subscriber_only") || visibility.startsWith("sub", ignoreCase = true)
        val slot = jsonString(trimmed, "slot_type").orEmpty().ifBlank { jsonString(trimmed, "slotType").orEmpty() }
        val image = listOf("image", "image_url", "imageUrl", "card_image", "thumbnail", "card")
            .firstNotNullOfOrNull { jsonString(trimmed, it) }
            .orEmpty()
        val article = jsonString(trimmed, "article_url").orEmpty()
            .ifBlank { jsonString(trimmed, "articleUrl").orEmpty() }
            .ifBlank { jsonString(trimmed, "article").orEmpty() }
        return LuxWirePost(
            id = resolvedId,
            text = text,
            url = url,
            subscriberOnly = subscriber,
            slotType = slot,
            imageUrl = image,
            articleUrl = article,
        )
    }

    fun encode(post: LuxWirePost): String = buildString {
        append("{\"id\":\"")
        append(escape(post.id))
        append("\",\"text\":\"")
        append(escape(post.text))
        append("\",\"url\":\"")
        append(escape(post.url))
        append("\",\"slot_type\":\"")
        append(escape(post.slotType))
        append("\",\"image\":\"")
        append(escape(post.imageUrl))
        append("\",\"article_url\":\"")
        append(escape(post.articleUrl))
        append("\",\"subscriber_only\":")
        append(if (post.subscriberOnly) "true" else "false")
        append('}')
    }

    fun openUrl(rawUrl: String, statusOrId: String): String {
        val url = rawUrl.trim()
        if (url.startsWith("http://") || url.startsWith("https://")) return url
        val status = statusOrId.trim()
        if (status.all { it.isDigit() } && status.isNotEmpty()) return "https://x.com/i/status/$status"
        return PROFILE
    }

    /** https link. The X app claims x.com when it is installed; otherwise the browser opens it. */
    fun viewIntent(url: String): Intent =
        Intent(Intent.ACTION_VIEW, Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)

    private fun statusIdFrom(url: String): String =
        Regex("""/status/(\d+)""").find(url)?.groupValues?.get(1).orEmpty()

    private fun jsonString(json: String, key: String): String? {
        val match = Regex(""""${Regex.escape(key)}"\s*:\s*"((?:\\.|[^"\\])*)"""").find(json) ?: return null
        return match.groupValues[1]
            .replace("\\\"", "\"")
            .replace("\\n", "\n")
            .replace("\\\\", "\\")
    }

    private fun jsonBool(json: String, key: String): Boolean {
        val match = Regex(""""${Regex.escape(key)}"\s*:\s*(true|false)""").find(json)
        return match?.groupValues?.get(1) == "true"
    }

    private fun escape(value: String): String =
        value.replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n")
}
