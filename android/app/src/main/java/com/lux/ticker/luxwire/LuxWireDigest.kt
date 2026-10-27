package com.lux.ticker.luxwire

object LuxWireDigest {
    const val WINDOW_MS = 15 * 60 * 1000L
    const val NOTIFICATION_ID = 2500

    fun due(startedAtMillis: Long, nowMillis: Long, count: Int): Boolean =
        count > 0 && startedAtMillis > 0L && nowMillis - startedAtMillis >= WINDOW_MS
}
