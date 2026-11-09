package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PriceRefreshTest {
    @Test fun liveKeepsTodaysCadenceAndAChoiceReplacesEveryFetch() {
        assertEquals(PriceRefresh.LIVE, PriceRefresh.fromStored(null))
        assertEquals(PriceRefresh.LIVE, PriceRefresh.fromStored("nope"))
        assertEquals(PriceRefresh.M5, PriceRefresh.fromStored("M5"))
        assertEquals(0, PriceRefresh.LIVE.minutes)
        assertEquals(60_000L, PriceRefresh.M1.intervalMs)
        assertEquals(3 * 60_000L, PriceRefresh.M3.intervalMs)
        assertEquals(5 * 60_000L, PriceRefresh.M5.intervalMs)
        assertEquals(10 * 60_000L, PriceRefresh.M10.intervalMs)
        assertEquals(30 * 60_000L, PriceRefresh.M30.intervalMs)

        assertTrue(PriceRefreshPace.usesLiveBitcoinSocket(PriceRefresh.LIVE))
        assertFalse(PriceRefreshPace.usesLiveBitcoinSocket(PriceRefresh.M1))
        assertEquals(PriceRefreshPace.LIVE_STOCK_MS, PriceRefreshPace.stockPeriodMs(PriceRefresh.LIVE))
        assertEquals(13_000L, PriceRefreshPace.stockPeriodMs(PriceRefresh.LIVE))

        PriceRefresh.entries.filter { it != PriceRefresh.LIVE }.forEach { choice ->
            assertEquals(choice.intervalMs, PriceRefreshPace.stockPeriodMs(choice))
            assertEquals(choice.intervalMs, PriceRefreshPace.fetchPeriodMs(choice, 60_000L))
            assertEquals(choice.intervalMs, PriceRefreshPace.fetchPeriodMs(choice, 5 * 60_000L))
            assertTrue(PriceRefreshPace.stockPeriodMs(choice) >= choice.intervalMs)
            assertTrue(PriceRefreshPace.fetchPeriodMs(choice, 60_000L) >= choice.intervalMs)
            assertFalse(PriceRefreshPace.usesLiveBitcoinSocket(choice))
        }

        assertEquals(60_000L, PriceRefreshPace.fetchPeriodMs(PriceRefresh.LIVE, 60_000L))
        assertEquals(5 * 60_000L, PriceRefreshPace.fetchPeriodMs(PriceRefresh.LIVE, 5 * 60_000L))
    }

    @Test fun failureBackoffCannotPollFasterThanTheChoice() {
        assertEquals(15_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.LIVE, 0L))
        assertEquals(30_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.LIVE, 15_000L))
        assertEquals(300_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.LIVE, 300_000L))
        assertEquals(60_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.M1, 0L))
        assertEquals(120_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.M1, 60_000L))
        assertEquals(30 * 60_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.M30, 0L))
        assertEquals(30 * 60_000L, PriceRefreshPace.stockBackoffMs(PriceRefresh.M30, 15_000L))
        assertTrue(PriceRefreshPace.stockBackoffMs(PriceRefresh.M10, 15_000L) >= PriceRefresh.M10.intervalMs)
    }
}
