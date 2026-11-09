package com.lux.ticker.data

/**
 * How often prices are fetched. Live keeps the cadence this build already used.
 * A minute choice is the wait for every price fetch, so nothing polls faster than that.
 * A missing preference stays [LIVE].
 */
enum class PriceRefresh(val minutes: Int, val label: String) {
    LIVE(0, "Live"),
    M1(1, "Every 1 min"),
    M3(3, "Every 3 min"),
    M5(5, "Every 5 min"),
    M10(10, "Every 10 min"),
    M30(30, "Every 30 min"),
    ;

    val intervalMs: Long
        get() = minutes * 60_000L

    companion object {
        fun fromStored(raw: String?): PriceRefresh =
            entries.firstOrNull { it.name == raw } ?: LIVE
    }
}

object PriceRefreshPace {
    /** Yahoo batch gap while Price refresh is Live. */
    const val LIVE_STOCK_MS = 13_000L

    const val LIVE_BACKOFF_START_MS = 15_000L
    const val LIVE_BACKOFF_CAP_MS = 300_000L

    /**
     * Slowest Live is allowed to go while Android battery saver is on.
     * A saved choice that is already slower than this stays slower.
     */
    const val SAVER_FLOOR_MS = 15 * 60_000L

    /** How often an idle loop may look again. Not a fetch. */
    const val IDLE_RECHECK_MS = 15 * 60_000L

    /**
     * The Coinbase socket stays open only for Live, with the screen on,
     * and only when Android is not in battery saver or Doze.
     */
    fun usesLiveBitcoinSocket(
        refresh: PriceRefresh,
        powerSave: Boolean = false,
        screenOn: Boolean = true,
        deviceIdle: Boolean = false,
    ): Boolean = refresh == PriceRefresh.LIVE && screenOn && !powerSave && !deviceIdle

    /**
     * Wait before the next fetch on a path that already has a Live cadence.
     * Live keeps [livePeriodMs]. Any other choice uses that choice.
     * Battery saver never lets that wait be shorter than [SAVER_FLOOR_MS].
     */
    fun fetchPeriodMs(refresh: PriceRefresh, livePeriodMs: Long, powerSave: Boolean = false): Long {
        val chosen = if (refresh == PriceRefresh.LIVE) livePeriodMs else refresh.intervalMs
        return if (powerSave) chosen.coerceAtLeast(SAVER_FLOOR_MS) else chosen
    }

    fun stockPeriodMs(refresh: PriceRefresh, powerSave: Boolean = false): Long =
        fetchPeriodMs(refresh, LIVE_STOCK_MS, powerSave)

    /**
     * Yahoo failure backoff. Live keeps the 15 second ramp, capped at 5 minutes.
     * A chosen interval is a floor, so a rate limit cannot poll faster than the choice.
     * Battery saver raises that floor to [SAVER_FLOOR_MS].
     */
    fun stockBackoffMs(refresh: PriceRefresh, previousMs: Long, powerSave: Boolean = false): Long {
        val ramp = if (previousMs <= 0L) {
            if (refresh == PriceRefresh.LIVE) LIVE_BACKOFF_START_MS else refresh.intervalMs
        } else {
            (previousMs * 2).coerceAtMost(LIVE_BACKOFF_CAP_MS)
        }
        val paced = if (refresh == PriceRefresh.LIVE) ramp else ramp.coerceAtLeast(refresh.intervalMs)
        return if (powerSave) paced.coerceAtLeast(SAVER_FLOOR_MS) else paced
    }
}
