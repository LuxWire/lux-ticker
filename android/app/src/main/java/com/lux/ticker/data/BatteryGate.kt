package com.lux.ticker.data

import java.util.concurrent.atomic.AtomicInteger

/**
 * Whether the ticker may open the live Bitcoin socket or start another price fetch.
 * Screen off, Doze, and manual mode do not fetch. Battery saver never keeps the socket.
 */
enum class FetchKind {
    /** No fetch and no socket. Wait until the screen, saver, or a tap changes. */
    SLEEP,

    /** One fetch, then wait [FetchPlan.waitMs]. The socket is closed between fetches. */
    INTERVAL,

    /** Live Bitcoin socket, and stocks on the short Live gap. */
    LIVE_SOCKET,

    /** The user tapped Refresh. One fetch, socket stays closed. */
    ONCE,
}

data class FetchPlan(
    val kind: FetchKind,
    val waitMs: Long,
    val holdSocket: Boolean,
    val stockPeriodMs: Long,
) {
    val fetches: Boolean
        get() = kind != FetchKind.SLEEP
}

object BatteryGate {
    fun plan(
        refresh: PriceRefresh,
        screenOn: Boolean,
        powerSave: Boolean,
        deviceIdle: Boolean,
        manual: Boolean,
        userRefresh: Boolean,
    ): FetchPlan {
        if (userRefresh) {
            return FetchPlan(FetchKind.ONCE, 0L, holdSocket = false, stockPeriodMs = 0L)
        }
        if (manual || !screenOn || deviceIdle) {
            return FetchPlan(
                FetchKind.SLEEP,
                PriceRefreshPace.IDLE_RECHECK_MS,
                holdSocket = false,
                stockPeriodMs = PriceRefreshPace.IDLE_RECHECK_MS,
            )
        }
        val stock = PriceRefreshPace.stockPeriodMs(refresh, powerSave)
        if (PriceRefreshPace.usesLiveBitcoinSocket(refresh, powerSave, screenOn, deviceIdle)) {
            return FetchPlan(FetchKind.LIVE_SOCKET, stock, holdSocket = true, stockPeriodMs = stock)
        }
        val wait = PriceRefreshPace.fetchPeriodMs(refresh, stock, powerSave)
        return FetchPlan(FetchKind.INTERVAL, wait, holdSocket = false, stockPeriodMs = wait)
    }

    /** Side feeds such as @LuxWire stay quiet while prices themselves are paused. */
    fun pauseSideFeeds(screenOn: Boolean, powerSave: Boolean, deviceIdle: Boolean): Boolean =
        !screenOn || powerSave || deviceIdle

    /**
     * A tape redraw reports the same settings again. That must not count as a new fetch.
     * The wait ends only when [elapsedMs] reaches [waitMs].
     */
    fun shouldFetch(elapsedMs: Long, waitMs: Long): Boolean = elapsedMs >= waitMs

    /** How many price fetches happen if this plan stays put for [durationMs], including one at the start. */
    fun fetchCount(plan: FetchPlan, durationMs: Long): Int {
        if (!plan.fetches || plan.kind == FetchKind.ONCE) return if (plan.kind == FetchKind.ONCE) 1 else 0
        val gap = if (plan.holdSocket) plan.stockPeriodMs else plan.waitMs
        if (gap <= 0L || durationMs < 0L) return 0
        return (durationMs / gap).toInt() + 1
    }
}

/**
 * Counts CPU wake holds. The ticker acquires one only around a fetch and always releases it.
 * Idle, screen off, and the gap between interval ticks must leave it released.
 */
class WakeLedger {
    private val held = AtomicInteger(0)
    private val acquires = AtomicInteger(0)
    private val releases = AtomicInteger(0)

    fun acquire(reason: String) {
        acquires.incrementAndGet()
        held.incrementAndGet()
    }

    fun release(reason: String) {
        releases.incrementAndGet()
        held.updateAndGet { (it - 1).coerceAtLeast(0) }
    }

    fun releaseAll() {
        if (held.get() > 0) releases.incrementAndGet()
        held.set(0)
    }

    fun isHeld(): Boolean = held.get() > 0

    fun acquireCount(): Int = acquires.get()

    fun releaseCount(): Int = releases.get()
}

object BatterySession {
    suspend fun aroundFetch(ledger: WakeLedger, block: suspend () -> Unit) {
        ledger.acquire("fetch")
        try {
            block()
        } finally {
            ledger.release("fetch")
        }
    }

    fun idle(ledger: WakeLedger) {
        ledger.releaseAll()
    }
}
