package com.lux.ticker.data

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import com.lux.ticker.net.YahooUrls
import com.lux.ticker.ui.TapeGeometry
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.time.Instant
import java.time.ZoneId
import java.time.ZonedDateTime

class SymbolAndTapeTest {
    @Test fun defaultListPinsBtcAndKeepsTheWindowsOrder() {
        assertEquals(46, DefaultSymbols.all.size)
        assertEquals(46, DefaultSymbols.all.map { it.symbol }.distinct().size)
        assertEquals("BTC", DefaultSymbols.all.first().symbol)
        assertEquals(
            listOf(
                "BTC", "IBIT", "MSTR", "3350.T", "MPJPY", "XXI", "ASST", "MARA", "BLSH", "COIN",
                "CLSK", "DJT", "TSLA", "RIOT", "HUT", "XYZ", "ABTC", "GDC", "GLXY", "NXTT", "GEMI",
                "SVIA", "SWC.L", "IREN", "NAKA", "SLE", "STRC", "STRF", "STRK", "STRD", "SATA",
                "^GSPC", "^DJI", "^NDX", "^RUT", "CL=F", "GLD", "SLV", "^VIX", "AAPL", "MSFT",
                "GOOGL", "AMZN", "NVDA", "META", "SPCX",
            ),
            DefaultSymbols.all.map { it.symbol },
        )
        assertEquals("S&P 500", DefaultSymbols.labels.getValue("^GSPC"))
        assertEquals("DOW", DefaultSymbols.labels.getValue("^DJI"))
        assertEquals("NDX", DefaultSymbols.labels.getValue("^NDX"))
        assertEquals("RUT", DefaultSymbols.labels.getValue("^RUT"))
        assertEquals("OIL", DefaultSymbols.labels.getValue("CL=F"))
        assertEquals("VIX", DefaultSymbols.labels.getValue("^VIX"))
        assertFalse(DefaultSymbols.all.drop(1).any { it.symbol == "BTC" })
    }

    @Test fun btcStaysPinnedAndIsNotPartOfTheScrollingList() {
        val start = DefaultSymbols.all
        assertEquals("BTC", SymbolList.pinned(start).symbol)
        assertTrue(SymbolList.scrolling(start).none { it.symbol == "BTC" })
        assertEquals(start.map { it.symbol }, SymbolList.remove(start, "BTC").map { it.symbol })

        val without = start.filter { it.symbol != "BTC" }
        val restored = SymbolList.ensurePinned(without)
        assertEquals("BTC", restored.first().symbol)
        assertEquals(without.map { it.symbol }, restored.drop(1).map { it.symbol })

        val buried = listOf(SymbolSpec("MSTR", "MSTR"), SymbolSpec("BTC", "Bitcoin"), SymbolSpec("IBIT", "IBIT"))
        val pinned = SymbolList.ensurePinned(buried)
        assertEquals(listOf("BTC", "MSTR", "IBIT"), pinned.map { it.symbol })
        assertEquals("Bitcoin", pinned.first().label)

        val moved = SymbolList.moveScrolling(start, 0, 2)
        assertEquals("BTC", moved.first().symbol)
        assertEquals(listOf("MSTR", "3350.T", "IBIT"), moved.drop(1).take(3).map { it.symbol })
        assertTrue(moved.drop(1).none { it.symbol == "BTC" })
        assertEquals(start, SymbolList.moveScrolling(start, 0, 0))
        assertEquals(SymbolList.ensurePinned(start), SymbolList.moveScrolling(start, -1, 1))

        var only = start
        SymbolList.scrolling(start).forEach { spec -> only = SymbolList.remove(only, spec.symbol) }
        assertEquals(listOf("BTC"), only.map { it.symbol })
        assertEquals(listOf("BTC", "GLD"), SymbolList.add(only, SymbolSpec("GLD", "GLD")).map { it.symbol })
        assertEquals(listOf("BTC", "GLD"), SymbolList.add(listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("GLD", "GLD")), SymbolSpec("BTC", "BTC")).map { it.symbol })
    }

    @Test fun storedJsonPinsBtcOnTheWayInAndOut() = runBlocking {
        val encoded = SymbolJson.encode(listOf(SymbolSpec("MSTR", "MSTR"), SymbolSpec("IBIT", "IBIT")))
        val round = SymbolJson.decodeOrNull(encoded)
        assertEquals(listOf("MSTR", "IBIT"), round!!.map { it.symbol })
        assertEquals(listOf("BTC", "AAPL"), SymbolJson.decodeStored("""[{"symbol":"AAPL","label":"AAPL"}]""").map { it.symbol })
        assertEquals(DefaultSymbols.all.map { it.symbol }, SymbolJson.decodeStored("not json").map { it.symbol })
        assertEquals(listOf("BTC"), SymbolJson.decodeStored("[]").map { it.symbol })
        assertNull(SymbolJson.decodeOrNull("{"))

        val ampersand = SymbolJson.decodeOrNull(SymbolJson.encode(listOf(SymbolSpec("^GSPC", "S&P 500"))))
        assertEquals("S&P 500", ampersand!!.single().label)

        val file = File.createTempFile("lux-symbols", ".preferences_pb")
        file.delete()
        val store = SymbolStore(PreferenceDataStoreFactory.create { file })
        store.update(listOf(SymbolSpec("MSTR", "MSTR"), SymbolSpec("IBIT", "IBIT")))
        assertEquals(listOf("BTC", "MSTR", "IBIT"), store.read().map { it.symbol })
        store.update(SymbolList.moveScrolling(DefaultSymbols.all, 0, 2))
        assertEquals(listOf("BTC", "MSTR", "3350.T", "IBIT"), store.read().take(4).map { it.symbol })
    }

    @Test fun tapeAndWidgetKeepBtcFixedOnTheLeft() {
        val instant = ZonedDateTime.of(2026, 10, 22, 17, 28, 0, 0, ZoneId.of("America/New_York")).toInstant()
        val symbols = listOf(
            SymbolSpec("MSTR", "MSTR"),
            SymbolSpec("BTC", "BTC"),
            SymbolSpec("IBIT", "IBIT"),
            SymbolSpec("3350.T", "3350.T"),
            SymbolSpec("ABTC", "ABTC"),
        )
        val quotes = mapOf(
            "BTC" to quote("BTC", 108412.50, 1.84, "USD", instant),
            "MSTR" to quote("MSTR", 352.18, 5.12, "USD", instant),
            "IBIT" to quote("IBIT", 46.59, 0.76, "USD", instant),
            "3350.T" to quote("3350.T", 842.0, 2.38, "JPY", instant),
            "ABTC" to quote("ABTC", 3.0, -1.0, "USD", instant),
        )
        val tape = StripFormatter.build(symbols, quotes, instant)
        assertEquals("BTC", tape.pinned.symbol)
        assertTrue(tape.pinned.emphasized)
        assertEquals(listOf("MSTR", "IBIT", "3350.T", "ABTC"), tape.scrolling.map { it.symbol })
        assertTrue(tape.scrolling.none { it.emphasized || it.symbol == "BTC" })
        assertTrue(tape.pinned.spans.none { it.text.contains("AH") || it.text.contains("PRE") })
        val mstr = StripFormatter.plain(tape.scrolling.first { it.symbol == "MSTR" })
        assertTrue(mstr.startsWith("MSTR "))
        assertTrue(mstr.contains("352.18"))
        assertTrue(mstr.contains("AH"))
        assertTrue(tape.scrolling.first { it.symbol == "3350.T" }.spans.none { it.text.contains("AH") || it.text.contains("PRE") })
        val quiet = StripFormatter.build(symbols, quotes, instant, showSessionTags = false)
        assertFalse(StripFormatter.plain(quiet.scrolling.first { it.symbol == "MSTR" }).contains("AH"))
        assertTrue(StripFormatter.plain(tape.pinned).startsWith("BTC"))
        assertTrue(StripFormatter.plain(tape.scrolling.first { it.symbol == "3350.T" }).contains("¥842"))

        val widget = WidgetContent.from(tape)
        assertTrue(widget.pinned.startsWith("BTC "))
        assertFalse(Regex("""(^|\s)BTC(\s|$)""").containsMatchIn(widget.scrolling))
        assertTrue(widget.scrolling.contains("MSTR "))
        assertTrue(widget.scrolling.contains("ABTC "))
        assertTrue(widget.scrolling.contains("AH"))
        assertFalse(widget.pinned.contains("AH"))
        assertTrue(widget.scrolling.split("  ·  ").none { it.startsWith("BTC") })
    }

    @Test fun pinReservesTheLeftEdgeAndTheScrollGetsTheRemainder() {
        val (origin, scroll) = TapeGeometry.origins(widthPx = 1000, pinWidthPx = 220, dividerPx = 1, gapPx = 8)
        assertEquals(229, origin)
        assertEquals(771, scroll)
        assertTrue(origin > 220)
        assertEquals(1000, origin + scroll)
        val narrow = TapeGeometry.origins(widthPx = 100, pinWidthPx = 220, dividerPx = 1, gapPx = 8)
        assertEquals(229, narrow.first)
        assertEquals(0, narrow.second)
    }

    @Test fun formatterColorsStaleAndSessionTags() {
        val regular = ZonedDateTime.of(2026, 10, 22, 15, 0, 0, 0, ZoneId.of("America/New_York")).toInstant()
        val up = StripFormatter.build(
            listOf(SymbolSpec("MSTR", "MSTR")),
            mapOf("MSTR" to quote("MSTR", 110.0, 1.5, "USD", regular)),
            regular,
        )
        val pct = up.scrolling.first().spans.last()
        assertEquals(TapeColors.UP, pct.color)
        assertTrue(pct.text.contains("+1.50%"))
        assertTrue(up.scrolling.first().spans.none { it.text.contains("AH") || it.text.contains("PRE") })

        val down = quote("STRF", 90.0, -0.25, "USD", regular)
        val downItem = StripFormatter.item(SymbolSpec("STRF", "STRF"), down, SessionTag.REGULAR, regular, false)
        assertEquals(TapeColors.DOWN, downItem.spans.last().color)

        val staleAt = regular.plusMillis(60_000)
        val stale = StripFormatter.item(
            SymbolSpec("MSTR", "MSTR"),
            quote("MSTR", 1.0, 0.0, "USD", regular),
            SessionTag.REGULAR,
            staleAt,
            false,
        )
        assertTrue(stale.stale)
        val freshBtc = StripFormatter.item(DefaultSymbols.btc, quote("BTC", 1.0, 0.0, "USD", regular), SessionTag.NONE, regular.plusMillis(5_000), true)
        assertFalse(freshBtc.stale)
        val oldBtc = StripFormatter.item(DefaultSymbols.btc, quote("BTC", 1.0, 0.0, "USD", regular), SessionTag.NONE, regular.plusMillis(25_000), true)
        assertTrue(oldBtc.stale)
    }

    @Test fun priceFormatAndYahooUrl() {
        assertEquals("1,234.50", PriceFormat.formatPrice(1234.5, "USD"))
        assertEquals("¥842", PriceFormat.formatPrice(842.0, "JPY"))
        assertEquals("0.5000", PriceFormat.formatPrice(0.5, "USD"))
        assertEquals("+1.50%", PriceFormat.formatPct(1.5))
        assertEquals("-0.25%", PriceFormat.formatPct(-0.25))
        assertEquals("+0.00%", PriceFormat.formatPct(0.0))
        assertEquals(
            "https://query1.finance.yahoo.com/v8/finance/chart/%5EGSPC?interval=1m&range=1d&includePrePost=true",
            YahooUrls.chart("^GSPC"),
        )
        assertTrue(YahooUrls.chart("CL=F").contains("CL%3DF"))
    }

    @Test fun factoryDefaultsApplyOnlyWhenAKeyIsMissing() = runBlocking {
        val file = File.createTempFile("lux-settings", ".preferences_pb")
        file.delete()
        val store = SettingsStore(PreferenceDataStoreFactory.create { file })
        val initial = store.read()
        assertEquals(50f, initial.speedDpPerSec)
        assertEquals(0f, initial.verticalOffsetDp)
        assertTrue(initial.showSessionTags)
        assertTrue(initial.chartHaptics)
        assertEquals(com.lux.ticker.data.TapeColors.SYMBOL, initial.symbolColor)
        assertEquals(com.lux.ticker.data.TapeColors.PRICE, initial.priceColor)
        assertEquals(71f, initial.leftPadDp)
        assertEquals(100f, initial.rightPadDp)
        assertTrue(initial.startOnBoot)
        assertTrue(initial.showOnLockScreen)
        assertFalse(initial.showHandle)
        assertFalse(initial.orangeDividers)
        assertTrue(initial.luxWirePosts)
        store.setShowOnLockScreen(false)
        store.setLeftPad(80f)
        store.setRightPad(110f)
        store.setSpeed(48f)
        store.setVerticalOffset(-40.04f)
        store.setOrangeDividers(true)
        val saved = store.read()
        assertFalse(saved.showOnLockScreen)
        assertEquals(80f, saved.leftPadDp)
        assertEquals(110f, saved.rightPadDp)
        assertEquals(48f, saved.speedDpPerSec)
        assertEquals(-40f, saved.verticalOffsetDp)
        assertTrue(saved.orangeDividers)
        store.setVerticalOffset(900f)
        assertEquals(800f, store.read().verticalOffsetDp)
        store.setVerticalOffset(-200f)
        assertEquals(-120f, store.read().verticalOffsetDp)
        assertTrue(saved.startOnBoot)
        store.setOrangeDividers(false)
        store.setShowOnLockScreen(true)
        store.setLuxWirePosts(false)
        val again = store.read()
        assertFalse(again.orangeDividers)
        assertTrue(again.showOnLockScreen)
        assertFalse(again.luxWirePosts)
        assertEquals(48f, again.speedDpPerSec)
        store.setShowSessionTags(false)
        store.setSymbolColor(com.lux.ticker.data.TapeColors.PRICE)
        store.setChartHaptics(false)
        assertFalse(store.read().chartHaptics)
        store.resetAll()
        val reset = store.read()
        assertEquals(50f, reset.speedDpPerSec)
        assertEquals(0f, reset.verticalOffsetDp)
        assertTrue(store.localUpdatedAt() > 0L)
        assertTrue(reset.showSessionTags)
        assertTrue(reset.chartHaptics)
        assertEquals(com.lux.ticker.data.TapeColors.SYMBOL, reset.symbolColor)
        assertEquals(com.lux.ticker.data.TapeColors.PRICE, reset.priceColor)
    }

    @Test fun freshInstallStoresTheDefaultOffsetWithoutClaimingANewerEdit() = runBlocking {
        val file = File.createTempFile("lux-offset", ".preferences_pb")
        file.delete()
        val store = SettingsStore(PreferenceDataStoreFactory.create { file })
        assertFalse(store.hasStoredVerticalOffset())
        assertEquals(0L, store.localUpdatedAt())
        store.ensureFactoryDefaults()
        assertFalse(store.hasStoredVerticalOffset())
        assertEquals(VerticalOffset.DEFAULT_DP, store.read().verticalOffsetDp)
        assertEquals(0L, store.localUpdatedAt())
        store.setVerticalOffset(12.5f)
        assertEquals(12.5f, store.read().verticalOffsetDp)
        val edited = store.localUpdatedAt()
        assertTrue(edited > 0L)
        store.ensureFactoryDefaults()
        assertEquals(12.5f, store.read().verticalOffsetDp)
        assertEquals(edited, store.localUpdatedAt())
    }

    @Test fun quoteCacheRoundTrip() {
        val file = File.createTempFile("lux-quotes", ".json")
        val cache = QuoteCache(file)
        cache.save(
            mapOf(
                "MSTR" to ParsedQuote("MSTR", 10.5, 10.0, 5.0, "USD", 20, 30),
                "BTC" to ParsedQuote("BTC", 80_000.0, null, null, "USD", 1, 2),
            ),
        )
        val loaded = QuoteCache(file).load()
        assertEquals(10.5, loaded.getValue("MSTR").price, 0.0)
        assertEquals(5.0, loaded.getValue("MSTR").changePct!!, 0.0)
        assertNull(loaded.getValue("BTC").changePct)
        assertEquals(80_000.0, loaded.getValue("BTC").price, 0.0)
    }

    private fun quote(symbol: String, price: Double, pct: Double, currency: String, now: Instant): ParsedQuote {
        val previous = if (pct == 0.0) price else price / (1.0 + pct / 100.0)
        return ParsedQuote(symbol, price, previous, pct, currency, now.toEpochMilli(), now.toEpochMilli())
    }
}
