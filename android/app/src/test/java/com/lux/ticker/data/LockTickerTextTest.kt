package com.lux.ticker.data

import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class LockTickerTextTest {
    private val now = Instant.parse("2026-10-22T21:28:00Z")

    @Test fun bitcoinLeadsAndTheListStopsMidTape() {
        val tape = StripFormatter.build(DefaultSymbols.all, emptyMap(), now)
        val line = LockTickerText.plain(tape)
        assertTrue(line.startsWith("BTC"))
        assertTrue(line.contains("IBIT"))
        assertTrue(line.contains("TSLA"))
        assertTrue(line.endsWith("…"))
        assertFalse(line.contains("RIOT"))
        assertFalse(line.contains("SPCX"))
        val scrolling = LockTickerText.plainScrolling(tape)
        assertFalse(scrolling.startsWith("BTC"))
    }

    @Test fun crawledTapeMovesAndLeavesBitcoinPinnedOut() {
        val tape = StripFormatter.build(DefaultSymbols.all.take(4), emptyMap(), now)
        val start = LockTickerText.crawledPlain(tape, 0)
        val moved = LockTickerText.crawledPlain(tape, 8)
        assertTrue(start.startsWith("IBIT"))
        assertFalse(start.startsWith("BTC"))
        assertNotEquals(start, moved)
        assertFalse(moved.startsWith("BTC"))
        assertTrue(start.contains("MSTR"))
        assertTrue(moved.contains("MSTR") || moved.contains("3350"))
    }

    @Test fun aShortTapeHasNoEllipsis() {
        val tape = StripFormatter.build(DefaultSymbols.all.take(3), emptyMap(), now)
        val scrolling = LockTickerText.plainScrolling(tape)
        assertTrue(scrolling.contains("IBIT"))
        assertTrue(scrolling.contains("MSTR"))
        assertFalse(scrolling.contains("…"))
        assertTrue(LockTickerText.plain(tape).startsWith("BTC"))
    }
}
