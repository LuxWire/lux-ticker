package com.lux.ticker.luxwire

import java.util.Locale

object LuxWireCopy {
    fun title(slotType: String): String {
        val key = slotType.trim().lowercase(Locale.US).replace('_', '-').replace(' ', '-')
        return when {
            key.contains("8-k") || key.contains("8k") -> "8-K"
            key.contains("monday") -> "Monday Edition"
            key.contains("one-shot") || key.contains("oneshot") -> "One Shot"
            key.contains("scorecard") -> "Scorecard"
            else -> "@LuxWire"
        }
    }

    fun contentTitle(post: LuxWirePost, subscribed: Boolean): String {
        val base = title(post.slotType)
        return if (post.subscriberOnly && !subscribed) "$base · Subscribers" else base
    }

    /** Article URL when one was sent. Otherwise the status URL. Gated posts open subscribe. */
    fun openTarget(post: LuxWirePost, subscribed: Boolean): String {
        if (post.subscriberOnly && !subscribed) return LuxWirePosts.SUBSCRIBE
        val article = post.articleUrl.trim()
        if (article.startsWith("http://") || article.startsWith("https://")) return article
        return post.url.ifBlank { LuxWirePosts.PROFILE }
    }

    fun shareText(post: LuxWirePost): String = buildString {
        append(post.text.ifBlank { "New post" })
        append('\n')
        append(post.url.ifBlank { LuxWirePosts.PROFILE })
        append("\n@LuxWire")
    }

    fun digestTitle(count: Int): String = "$count new from LuxWire"

    fun digestBody(posts: List<LuxWirePost>): String =
        posts.take(4).joinToString("\n") { it.text.ifBlank { "New post" } }
}
