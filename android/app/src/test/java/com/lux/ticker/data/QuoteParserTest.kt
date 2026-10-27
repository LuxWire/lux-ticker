package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class QuoteParserTest {
    @Test fun yahooSkipsNullClosesAndFallsBackWhenBarsAreMissing() {
        val withNulls = """
            {"chart":{"result":[{"meta":{"currency":"USD","previousClose":10,"chartPreviousClose":10,"regularMarketPrice":11},
            "timestamp":[100,200,300],"indicators":{"quote":[{"close":[9.5,null,11.5]}]}}],"error":null}}
        """.trimIndent()
        val parsed = YahooChartParser.parse(withNulls, "IBIT", 50)
        assertEquals(11.5, parsed.price, 0.0)
        assertEquals(300_000L, parsed.asOfMillis)
        assertEquals(15.0, parsed.changePct!!, 1e-9)

        val fallback = """
            {"chart":{"result":[{"meta":{"currency":"JPY","regularMarketPrice":269,"previousClose":275,"chartPreviousClose":275,"regularMarketTime":100}}],"error":null}}
        """.trimIndent()
        val tokyo = YahooChartParser.parse(fallback, "3350.T", 80)
        assertEquals(269.0, tokyo.price, 0.0)
        assertEquals("JPY", tokyo.currency)
        assertEquals(100_000L, tokyo.asOfMillis)
    }

    @Test fun yahooErrorPayloadThrowsTheDescription() {
        val body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}"""
        val error = runCatching { YahooChartParser.parse(body, "NOTREALXYZ", 1) }.exceptionOrNull()
        assertTrue(error is QuoteParseException)
        assertTrue(error!!.message!!.contains("No data found"))
    }

    @Test fun coinbaseFixtureParsesTheLiveTicker() {
        // Synthetic ticker frame (made-up numbers, real Coinbase shape).
        val body = """{"type":"ticker","product_id":"BTC-USD","price":"90000.00","open_24h":"100000.00","time":"2026-10-22T22:23:08.213459Z"}"""
        val quote = CoinbaseTickerParser.parse(body, fetchedAtMillis = 99)!!
        assertEquals("BTC", quote.symbol)
        assertEquals(90000.00, quote.price, 0.0)
        assertEquals(100000.00, quote.previousClose!!, 0.0)
        val expected = (90000.00 - 100000.00) / 100000.00 * 100.0
        assertEquals(expected, quote.changePct!!, 1e-9)
        assertEquals("USD", quote.currency)
        assertEquals(Instant.parse("2026-10-22T22:23:08.213459Z").toEpochMilli(), quote.asOfMillis)
    }

    @Test fun coinbaseIgnoresTheSubscriptionAck() {
        val ack = """{"type":"subscriptions","channels":[{"name":"ticker","product_ids":["BTC-USD"]}]}"""
        assertNull(CoinbaseTickerParser.parse(ack, 1))
        assertNull(CoinbaseTickerParser.parse("not-json", 1))
    }

}
