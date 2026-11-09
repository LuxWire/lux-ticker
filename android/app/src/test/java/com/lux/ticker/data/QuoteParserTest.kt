package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class QuoteParserTest {
    @Test fun yahooFixtureUsesTheExtendedHoursPrint() {
        val body = resource("fixtures/yahoo_mstr_chart.json")
        val quote = YahooChartParser.parse(body, "MSTR", fetchedAtMillis = 1_792_707_400_000)
        assertEquals("MSTR", quote.symbol)
        assertEquals("USD", quote.currency)
        assertEquals(144.897, quote.price, 1e-9)
        assertEquals(153.48, quote.previousClose!!, 1e-9)
        assertNotEquals(145.8, quote.price, 0.0)
        val change = checkNotNull(quote.changePct)
        val expected = (144.897 - 153.48) / 153.48 * 100.0
        assertEquals(expected, change, 1e-9)
        assertEquals(-5.5922595777951525, change, 1e-9)
        assertEquals(1_792_707_362_000L, quote.asOfMillis)
        assertEquals(1_792_707_400_000L, quote.fetchedAtMillis)
        assertTrue(quote.asOfMillis > 1_792_699_201_000L)
    }

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
        val body = resource("fixtures/coinbase_btc_ticker.json")
        val quote = CoinbaseTickerParser.parse(body, fetchedAtMillis = 99)!!
        assertEquals("BTC", quote.symbol)
        assertEquals(81259.08, quote.price, 0.0)
        assertEquals(84367.06, quote.previousClose!!, 0.0)
        val expected = (81259.08 - 84367.06) / 84367.06 * 100.0
        assertEquals(expected, quote.changePct!!, 1e-9)
        assertEquals("USD", quote.currency)
        assertEquals(Instant.parse("2026-10-22T22:23:08.213459Z").toEpochMilli(), quote.asOfMillis)
    }

    @Test fun coinbaseIgnoresTheSubscriptionAck() {
        val ack = """{"type":"subscriptions","channels":[{"name":"ticker","product_ids":["BTC-USD"]}]}"""
        assertNull(CoinbaseTickerParser.parse(ack, 1))
        assertNull(CoinbaseTickerParser.parse("not-json", 1))
    }

    private fun resource(path: String): String {
        val loader = checkNotNull(javaClass.classLoader)
        return checkNotNull(loader.getResourceAsStream(path)) { path }.bufferedReader().readText()
    }
}
