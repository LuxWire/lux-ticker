package com.lux.ticker.data

/**
 * Shade and lock-screen motion. RemoteViews cannot run a frame-by-frame animation,
 * so the tape advances one character at a time. One mono character is about 8 dp,
 * which keeps 50 dp/s near a 160 ms step. Android may still coalesce updates.
 */
object NotificationCrawl {
    const val MIN_MS = 120L
    const val MAX_MS = 480L

    fun delayMs(speedDpPerSec: Float): Long {
        val ms = (8f / speedDpPerSec.coerceIn(12f, 160f) * 1000f).toLong()
        return ms.coerceIn(MIN_MS, MAX_MS)
    }

    /** The shade is not the keyguard. Crawl whenever the screen is on. */
    fun shouldAdvance(screenInteractive: Boolean): Boolean = screenInteractive
}
