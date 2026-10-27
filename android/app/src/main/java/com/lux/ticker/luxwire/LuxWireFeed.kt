package com.lux.ticker.luxwire

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.util.Log
import com.lux.ticker.LuxApp
import okhttp3.Request

/**
 * Polls the @LuxWire timeline on X. The first successful poll records the current
 * posts and does not alert. Later polls alert new ids, at most the last 10.
 * Alerts stay off while the Settings switch is off.
 */
class LuxWireFeed(context: Context) {
    private val app = context.applicationContext
    private val prefs = app.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
    private val auth = LuxWireAuth(app)

    suspend fun poll() {
        val settings = LuxApp.container(app).settings.read()
        if (!settings.luxWirePosts) return
        val token = auth.authorization()
        if (token.isNullOrBlank()) {
            Log.i(TAG, "no X token; public posts need a bearer in Settings")
            return
        }
        val client = LuxWireXClient(LuxApp.container(app).http)
        val (userId, posts) = client.latest(token, auth.userId())
        auth.saveUserId(userId)
        val step = LuxWireCursor.step(prefs.getBoolean(PRIMED, false), seen(), posts)
        prefs.edit().putBoolean(PRIMED, step.primed).putStringSet(SEEN, trim(step.seen)).apply()
        if (step.notify.isEmpty()) {
            flushDigest(force = false, subscribed = settings.luxWireSubscriber, digest = settings.luxWireDigest)
            return
        }
        if (settings.luxWireDigest) {
            step.notify.forEach { enqueueDigest(it) }
            flushDigest(force = false, subscribed = settings.luxWireSubscriber, digest = true)
        } else {
            deliver(step.notify, settings.luxWireSubscriber)
        }
    }

    private fun deliver(posts: List<LuxWirePost>, subscribed: Boolean) {
        if (posts.isEmpty()) return
        if (posts.size == 1) {
            val post = posts.first()
            LuxWireNotifier.show(app, post, subscribed, thumb(post.imageUrl))
        } else {
            LuxWireNotifier.showDigest(app, posts, subscribed)
        }
    }

    private fun enqueueDigest(post: LuxWirePost) {
        val current = prefs.getString(DIGEST, "").orEmpty()
        val next = if (current.isBlank()) LuxWirePosts.encode(post) else "$current\n${LuxWirePosts.encode(post)}"
        val editor = prefs.edit().putString(DIGEST, next)
        if (prefs.getLong(DIGEST_AT, 0L) == 0L) editor.putLong(DIGEST_AT, System.currentTimeMillis())
        editor.apply()
    }

    private fun flushDigest(force: Boolean, subscribed: Boolean, digest: Boolean) {
        val pending = pendingDigest()
        if (pending.isEmpty()) return
        val started = prefs.getLong(DIGEST_AT, 0L)
        val ready = force || !digest || LuxWireDigest.due(started, System.currentTimeMillis(), pending.size)
        if (!ready) return
        prefs.edit().remove(DIGEST).remove(DIGEST_AT).apply()
        deliver(pending, subscribed)
    }

    private fun pendingDigest(): List<LuxWirePost> {
        val raw = prefs.getString(DIGEST, "").orEmpty()
        if (raw.isBlank()) return emptyList()
        return raw.lineSequence().mapNotNull { LuxWirePosts.parseLine(it) }.toList()
    }

    private fun seen(): Set<String> = prefs.getStringSet(SEEN, emptySet()).orEmpty()

    private fun trim(ids: Set<String>): Set<String> {
        if (ids.size <= SEEN_CAP) return ids
        return ids.toList().takeLast(SEEN_CAP).toSet()
    }

    private fun thumb(url: String): Bitmap? {
        if (!url.startsWith("https://")) return null
        return runCatching {
            val call = LuxApp.container(app).http.newCall(Request.Builder().url(url).build())
            call.execute().use { response ->
                if (!response.isSuccessful) return null
                val bytes = response.body?.bytes() ?: return null
                if (bytes.size > 2_000_000) return null
                BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
            }
        }.getOrNull()
    }

    private companion object {
        const val TAG = "LuxTicker"
        const val PREFS = "lux_wire_feed"
        const val SEEN = "seen"
        const val PRIMED = "primed"
        const val DIGEST = "digest"
        const val DIGEST_AT = "digest_at"
        const val SEEN_CAP = 400
    }
}
