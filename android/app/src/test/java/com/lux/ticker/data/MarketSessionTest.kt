package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.Instant
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.ZonedDateTime

class MarketSessionTest {
    private val ny: ZoneId = ZoneId.of("America/New_York")

    @Test fun regularPreAfterOvernightWeekendHolidayAndEarlyClose() {
        assertEquals(SessionTag.REGULAR, tag("MSTR", 2026, 10, 22, 15, 30))
        assertEquals(SessionTag.PRE, tag("MSTR", 2026, 10, 22, 8, 0))
        assertEquals(SessionTag.PRE, tag("AAPL", 2026, 10, 22, 4, 0))
        assertEquals(SessionTag.AFTER, tag("AAPL", 2026, 10, 22, 3, 59))
        assertEquals(SessionTag.REGULAR, tag("NVDA", 2026, 10, 22, 9, 30))
        assertEquals(SessionTag.AFTER, tag("NVDA", 2026, 10, 22, 16, 0))
        assertEquals(SessionTag.AFTER, tag("NVDA", 2026, 10, 22, 16, 30))
        assertEquals(SessionTag.AFTER, tag("NVDA", 2026, 10, 22, 22, 15))
        assertEquals(SessionTag.AFTER, tag("SPCX", 2026, 10, 24, 12, 0))
        assertEquals(SessionTag.AFTER, tag("MSTR", 2026, 7, 3, 11, 0))
        assertEquals(SessionTag.REGULAR, tag("MSTR", 2026, 11, 27, 12, 30))
        assertEquals(SessionTag.REGULAR, tag("MSTR", 2026, 11, 27, 12, 59))
        assertEquals(SessionTag.AFTER, tag("MSTR", 2026, 11, 27, 13, 0))
    }

    @Test fun newYorkDstIsApplied() {
        val winter = ZonedDateTime.of(2026, 1, 15, 9, 30, 0, 0, ny)
        val summer = ZonedDateTime.of(2026, 7, 15, 9, 30, 0, 0, ny)
        assertEquals(ZoneOffset.ofHours(-5), winter.offset)
        assertEquals(ZoneOffset.ofHours(-4), summer.offset)
        assertEquals(SessionTag.REGULAR, MarketSession.tag("MSTR", winter.toInstant()))
        assertEquals(SessionTag.REGULAR, MarketSession.tag("MSTR", summer.toInstant()))
        assertEquals(SessionTag.PRE, tag("MSTR", 2026, 1, 15, 8, 30))
    }

    @Test fun cryptoAndNonUsNeverGetASessionTag() {
        val times = listOf(
            at(2026, 10, 22, 15, 30),
            at(2026, 10, 22, 8, 0),
            at(2026, 10, 22, 22, 0),
            at(2026, 10, 24, 12, 0),
            at(2026, 7, 3, 11, 0),
        )
        listOf("BTC", "ETH-USD", "3350.T", "SWC.L").forEach { symbol ->
            times.forEach { instant ->
                assertEquals(symbol, SessionTag.NONE, MarketSession.tag(symbol, instant))
            }
        }
    }

    private fun tag(symbol: String, y: Int, m: Int, d: Int, h: Int, min: Int): SessionTag =
        MarketSession.tag(symbol, at(y, m, d, h, min))

    private fun at(y: Int, m: Int, d: Int, h: Int, min: Int): Instant =
        ZonedDateTime.of(y, m, d, h, min, 0, 0, ny).toInstant()
}
