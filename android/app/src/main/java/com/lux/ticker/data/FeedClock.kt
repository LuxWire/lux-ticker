package com.lux.ticker.data

import kotlinx.coroutines.flow.MutableStateFlow

/** In-memory poll health. Ages on screen come from cached quote timestamps. */
class FeedClock {
    val attemptAt = MutableStateFlow(0L)
    val stockFailed = MutableStateFlow(false)
    val networkUp = MutableStateFlow(true)

    fun noteStockBatch(ok: Int, failed: Int, at: Long = System.currentTimeMillis()) {
        attemptAt.value = at
        if (ok > 0) stockFailed.value = false
        else if (failed > 0) stockFailed.value = true
    }
}
