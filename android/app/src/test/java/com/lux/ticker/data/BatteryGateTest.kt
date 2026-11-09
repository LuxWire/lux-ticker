package com.lux.ticker.data

import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BatteryGateTest {
    @Test fun liveOnScreenKeepsTheSocketAndTheShortStockGap() {
        val plan = onScreen(PriceRefresh.LIVE)
        assertEquals(FetchKind.LIVE_SOCKET, plan.kind)
        assertTrue(plan.holdSocket)
        assertEquals(13_000L, plan.stockPeriodMs)
        assertEquals(5, BatteryGate.fetchCount(plan, 60_000L))
        assertTrue(BatteryGate.fetchCount(plan, 60_000L) > BatteryGate.fetchCount(onScreen(PriceRefresh.M1), 60_000L))
    }

    @Test fun aMinuteChoiceWaitsTheFullIntervalAndDropsTheSocket() {
        val five = onScreen(PriceRefresh.M5)
        assertEquals(FetchKind.INTERVAL, five.kind)
        assertFalse(five.holdSocket)
        assertEquals(5 * 60_000L, five.waitMs)
        assertEquals(5 * 60_000L, five.stockPeriodMs)
        assertFalse(BatteryGate.shouldFetch(elapsedMs = 30_000L, waitMs = five.waitMs))
        assertFalse(BatteryGate.shouldFetch(elapsedMs = 4 * 60_000L, waitMs = five.waitMs))
        assertTrue(BatteryGate.shouldFetch(elapsedMs = five.waitMs, waitMs = five.waitMs))
        assertEquals(4, BatteryGate.fetchCount(five, 15 * 60_000L))
        assertEquals(1, BatteryGate.fetchCount(five, 4 * 60_000L))
        PriceRefresh.entries.filter { it != PriceRefresh.LIVE }.forEach { choice ->
            val plan = onScreen(choice)
            assertFalse(plan.holdSocket)
            assertEquals(choice.intervalMs, plan.waitMs)
            assertEquals(1, BatteryGate.fetchCount(plan, choice.intervalMs - 1))
        }
    }

    @Test fun screenOffDozeAndManualDoNotFetchOrHoldTheSocket() {
        val off = BatteryGate.plan(PriceRefresh.LIVE, screenOn = false, powerSave = false, deviceIdle = false, manual = false, userRefresh = false)
        val doze = BatteryGate.plan(PriceRefresh.M1, screenOn = true, powerSave = false, deviceIdle = true, manual = false, userRefresh = false)
        val manual = BatteryGate.plan(PriceRefresh.LIVE, screenOn = true, powerSave = false, deviceIdle = false, manual = true, userRefresh = false)
        listOf(off, doze, manual).forEach { plan ->
            assertEquals(FetchKind.SLEEP, plan.kind)
            assertFalse(plan.fetches)
            assertFalse(plan.holdSocket)
            assertEquals(0, BatteryGate.fetchCount(plan, 60 * 60_000L))
        }
        val tap = BatteryGate.plan(PriceRefresh.LIVE, screenOn = false, powerSave = true, deviceIdle = true, manual = true, userRefresh = true)
        assertEquals(FetchKind.ONCE, tap.kind)
        assertFalse(tap.holdSocket)
        assertEquals(1, BatteryGate.fetchCount(tap, 60 * 60_000L))
    }

    @Test fun batterySaverClosesTheLiveStreamAndNeverPollsFasterThanTheFloor() {
        val savedLive = BatteryGate.plan(PriceRefresh.LIVE, screenOn = true, powerSave = true, deviceIdle = false, manual = false, userRefresh = false)
        assertEquals(FetchKind.INTERVAL, savedLive.kind)
        assertFalse(savedLive.holdSocket)
        assertEquals(PriceRefreshPace.SAVER_FLOOR_MS, savedLive.waitMs)
        assertEquals(1, BatteryGate.fetchCount(savedLive, PriceRefreshPace.SAVER_FLOOR_MS - 1))
        assertTrue(BatteryGate.fetchCount(onScreen(PriceRefresh.LIVE), 15 * 60_000L) > BatteryGate.fetchCount(savedLive, 15 * 60_000L))

        val oneMinute = BatteryGate.plan(PriceRefresh.M1, screenOn = true, powerSave = true, deviceIdle = false, manual = false, userRefresh = false)
        assertEquals(PriceRefreshPace.SAVER_FLOOR_MS, oneMinute.waitMs)
        assertFalse(oneMinute.holdSocket)

        val halfHour = BatteryGate.plan(PriceRefresh.M30, screenOn = true, powerSave = true, deviceIdle = false, manual = false, userRefresh = false)
        assertEquals(30 * 60_000L, halfHour.waitMs)
        assertFalse(halfHour.holdSocket)

        assertTrue(BatteryGate.pauseSideFeeds(screenOn = false, powerSave = false, deviceIdle = false))
        assertTrue(BatteryGate.pauseSideFeeds(screenOn = true, powerSave = true, deviceIdle = false))
        assertTrue(BatteryGate.pauseSideFeeds(screenOn = true, powerSave = false, deviceIdle = true))
        assertFalse(BatteryGate.pauseSideFeeds(screenOn = true, powerSave = false, deviceIdle = false))
    }

    @Test fun aFetchReleasesTheWakeAndIdleStaysReleased() {
        val ledger = WakeLedger()
        assertFalse(ledger.isHeld())
        var inside = false
        runBlocking {
            BatterySession.aroundFetch(ledger) {
                inside = ledger.isHeld()
                assertEquals(1, ledger.acquireCount())
            }
        }
        assertTrue(inside)
        assertFalse(ledger.isHeld())
        assertTrue(ledger.releaseCount() >= 1)

        val broken = WakeLedger()
        try {
            runBlocking {
                BatterySession.aroundFetch(broken) { throw IllegalStateException("fetch failed") }
            }
        } catch (_: IllegalStateException) {
        }
        assertFalse(broken.isHeld())

        val leaked = WakeLedger()
        leaked.acquire("socket")
        assertTrue(leaked.isHeld())
        BatterySession.idle(leaked)
        assertFalse(leaked.isHeld())
        BatterySession.idle(leaked)
        assertFalse(leaked.isHeld())
        assertEquals(0, BatteryGate.fetchCount(
            BatteryGate.plan(PriceRefresh.LIVE, screenOn = false, powerSave = false, deviceIdle = false, manual = false, userRefresh = false),
            durationMs = PriceRefreshPace.IDLE_RECHECK_MS,
        ))
    }

    @Test fun theShadeCrawlSleepsWhileTheScreenIsOff() {
        val crawling = NotificationCrawl.paceMs(50f, screenInteractive = true)
        val asleep = NotificationCrawl.paceMs(50f, screenInteractive = false)
        assertTrue(crawling < 1_000L)
        assertEquals(PriceRefreshPace.IDLE_RECHECK_MS, asleep)
        assertTrue(asleep > 60_000L)
        assertFalse(NotificationCrawl.shouldAdvance(screenInteractive = false))
    }

    private fun onScreen(refresh: PriceRefresh) = BatteryGate.plan(
        refresh,
        screenOn = true,
        powerSave = false,
        deviceIdle = false,
        manual = false,
        userRefresh = false,
    )
}
