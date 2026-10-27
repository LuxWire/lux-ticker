package com.lux.ticker.data

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import com.lux.ticker.engine.OverlayPolicy
import com.lux.ticker.engine.PriceAlertGate
import com.lux.ticker.luxwire.LuxWireCopy
import com.lux.ticker.luxwire.LuxWireDigest
import com.lux.ticker.luxwire.LuxWirePost
import com.lux.ticker.ui.ReleaseNotes
import com.lux.ticker.data.AlertKind
import com.lux.ticker.data.AlertJson
import com.lux.ticker.data.PinSide
import com.lux.ticker.data.SavedAlert
import com.lux.ticker.ui.SettingsMenu
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.time.Instant

class ReleaseLogicTest {
    @Test fun slotTitlesStayAndSubscriberPostsOpenTheSubscribePage() {
        assertEquals("8-K", LuxWireCopy.title("8-K"))
        assertEquals("8-K", LuxWireCopy.title("filing-8k"))
        assertEquals("Monday Edition", LuxWireCopy.title("monday-edition"))
        assertEquals("One Shot", LuxWireCopy.title("one_shot"))
        assertEquals("Scorecard", LuxWireCopy.title("scorecard"))
        assertEquals("@LuxWire", LuxWireCopy.title("FILLER"))
        assertEquals("@LuxWire", LuxWireCopy.title(""))
        val gated = LuxWirePost(
            id = "9",
            text = "Desk",
            url = "https://x.com/LuxWire/status/9",
            subscriberOnly = true,
            slotType = "scorecard",
            imageUrl = "https://x.com/card.jpg",
            articleUrl = "https://x.com/LuxWire/article/9",
        )
        assertEquals("https://x.com/LuxWire/subscribe", LuxWireCopy.openTarget(gated, subscribed = false))
        assertEquals("https://x.com/LuxWire/article/9", LuxWireCopy.openTarget(gated, subscribed = true))
        assertTrue(LuxWireCopy.contentTitle(gated, subscribed = false).contains("Subscribers"))
        assertEquals("Scorecard", LuxWireCopy.contentTitle(gated, subscribed = true))
        assertTrue(LuxWireCopy.shareText(gated).contains("@LuxWire"))
        assertFalse(LuxWireCopy.shareText(gated).contains("AH"))
    }

    @Test fun digestGroupsAfterFifteenMinutes() {
        val start = 1_000L
        assertFalse(LuxWireDigest.due(start, start + 14 * 60_000L, 3))
        assertTrue(LuxWireDigest.due(start, start + LuxWireDigest.WINDOW_MS, 2))
        assertEquals("4 new from LuxWire", LuxWireCopy.digestTitle(4))
        assertFalse(LuxWireDigest.due(0L, 50_000L, 2))
    }

    @Test fun priceAlertsFireOnTheCrossAndCanStayOff() {
        assertFalse(PriceAlerts.crossed(null, 100.0, 90.0))
        assertFalse(PriceAlerts.crossed(80.0, 85.0, 90.0))
        assertTrue(PriceAlerts.crossed(89.0, 90.0, 90.0))
        assertTrue(PriceAlerts.crossed(91.0, 89.0, 90.0))
        val (fire, armed) = PriceAlerts.btcStep(3.2, 3.0, armed = true)
        assertTrue(fire)
        assertFalse(armed)
        val (again, still) = PriceAlerts.btcStep(4.0, 3.0, armed = false)
        assertFalse(again)
        assertFalse(still)
        val (reset, rearmed) = PriceAlerts.btcStep(1.0, 3.0, armed = false)
        assertFalse(reset)
        assertTrue(rearmed)

        val gate = PriceAlertGate()
        val quiet = TickerSettings(priceAlerts = false, btcAlertPct = 3f, priceAlertSymbol = "MSTR", priceAlertTarget = 100f)
        val first = quote("MSTR", 90.0)
        assertTrue(gate.take(quiet, mapOf("MSTR" to first, "BTC" to btc(4.0))).isEmpty())
        val on = quiet.copy(priceAlerts = true)
        assertTrue(gate.take(on, mapOf("MSTR" to quote("MSTR", 101.0), "BTC" to btc(4.0))).any { it.key.startsWith("px:") })
        val btcOnly = gate.take(on, mapOf("BTC" to btc(1.0)))
        assertTrue(btcOnly.isEmpty())
        assertTrue(gate.take(on, mapOf("BTC" to btc(3.4))).any { it.title.startsWith("BTC") })
        assertTrue(PriceAlerts.btcHit(3.4, 3.0).body.contains("Coinbase USD spot"))
        assertFalse(PriceAlerts.btcHit(3.4, 3.0).body.contains("live", ignoreCase = true))

        val many = TickerSettings(
            priceAlerts = true,
            savedAlerts = listOf(
                SavedAlert("mstr", AlertKind.PRICE, "MSTR", 100.0),
                SavedAlert("nvda", AlertKind.PRICE, "NVDA", 50.0),
            ),
        )
        val gate2 = PriceAlertGate()
        gate2.take(many, mapOf("MSTR" to quote("MSTR", 90.0), "NVDA" to quote("NVDA", 40.0)))
        val hits = gate2.take(many, mapOf("MSTR" to quote("MSTR", 101.0), "NVDA" to quote("NVDA", 55.0)))
        assertEquals(setOf("mstr", "nvda"), hits.map { it.alertId }.toSet())
        val encoded = AlertJson.encode(listOf(SavedAlert("mstr", AlertKind.PRICE, "MSTR", 100.0, hitAtMillis = 1_700_000_000_000)))
        assertEquals(1_700_000_000_000L, AlertJson.decode(encoded).single().hitAtMillis)
        assertEquals("Invalid ticker", ReleaseNotes.INVALID_TICKER)
    }

    @Test fun feedsNameTheSourceAndNeverCallStocksLive() {
        assertEquals("Coinbase USD spot · 12s ago", FeedHonesty.btc(12_000L, online = true))
        assertTrue(FeedHonesty.btc(30_000L, online = true).contains("cached"))
        assertEquals("Yahoo · updated · 40s ago", FeedHonesty.stocks(40_000L, online = true))
        val delayed = FeedHonesty.stocks(20 * 60_000L, online = true)
        assertTrue(delayed.contains("delayed"))
        assertFalse(delayed.contains("live", ignoreCase = true))
        assertTrue(FeedHonesty.stocks(5_000L, online = false).contains("delayed"))
        assertEquals("108,412.50", PriceFormat.formatPrice(108412.5012, "USD"))
        assertFalse(PriceFormat.formatPrice(108412.5012, "USD").contains("5012"))
    }

    @Test fun pinCanScrollAndAfterHoursStillHasNoAh() {
        val now = Instant.parse("2026-10-22T21:28:00Z")
        val symbols = listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("MSTR", "MSTR"))
        val quotes = mapOf(
            "BTC" to ParsedQuote("BTC", 100.0, null, 1.0, "USD", now.toEpochMilli(), now.toEpochMilli()),
            "MSTR" to ParsedQuote("MSTR", 10.0, 9.0, 1.0, "USD", now.toEpochMilli() - 120_000L, now.toEpochMilli() - 120_000L),
        )
        val loose = StripFormatter.build(symbols, quotes, now, pinBtc = false)
        assertEquals("", loose.pinned.symbol)
        assertEquals(listOf("BTC", "MSTR"), loose.scrolling.map { it.symbol })
        val stale = StripFormatter.plain(loose.scrolling.first { it.symbol == "MSTR" })
        assertTrue(stale.contains("°"))
        assertTrue(stale.contains("AH"))
        assertFalse(StripFormatter.plain(loose.scrolling.first { it.symbol == "BTC" }).contains("AH"))
        val hidden = StripFormatter.build(symbols, quotes, now, pinBtc = false, showSessionTags = false)
        assertFalse(StripFormatter.plain(hidden.scrolling.first { it.symbol == "MSTR" }).contains("AH"))
        val fixed = StripFormatter.build(symbols, quotes, now, pinBtc = true)
        assertEquals("BTC", fixed.pinned.symbol)
        assertTrue(fixed.scrolling.none { it.symbol == "BTC" })
    }

    @Test fun overlayFollowsBatteryModeAndCopyStaysHonest() {
        val settings = TickerSettings()
        assertEquals(DisplayMode.ALWAYS, settings.displayMode)
        assertEquals(AppTheme.SYSTEM, settings.appTheme)
        assertFalse(AppTheme.SYSTEM.resolveDark(systemDark = false))
        assertTrue(AppTheme.SYSTEM.resolveDark(systemDark = true))
        assertTrue(AppTheme.DARK.resolveDark(systemDark = false))
        assertFalse(AppTheme.LIGHT.resolveDark(systemDark = true))
        assertFalse(AppTheme.PAPER.resolveDark(systemDark = true))
        assertTrue(AppTheme.PAPER.paperSurface(systemDark = true))
        assertTrue(AppTheme.SYSTEM.paperSurface(systemDark = false))
        assertFalse(AppTheme.LIGHT.paperSurface(systemDark = false))
        assertTrue(settings.scrollRightToLeft)
        assertEquals(PinSide.LEFT, settings.pinSide)
        assertTrue(settings.savedAlerts.isEmpty())
        assertTrue(settings.pinBtc)
        assertFalse(settings.luxWireDigest)
        assertFalse(settings.priceAlerts)
        assertTrue(settings.luxWireSubscriber)
        assertTrue(OverlayPolicy.showOverlay(settings, granted = true, keyguardLocked = false))
        assertFalse(OverlayPolicy.showOverlay(settings.copy(displayMode = DisplayMode.LOCK_ONLY), true, false))
        assertTrue(OverlayPolicy.showOverlay(settings.copy(displayMode = DisplayMode.LOCK_ONLY), true, true))
        assertFalse(OverlayPolicy.showOverlay(settings.copy(displayMode = DisplayMode.MANUAL, overlayEnabled = false), true, false))
        val notes = ReleaseNotes.WHATS_NEW + ReleaseNotes.ABOUT + ReleaseNotes.WHY_NOTIFICATIONS
        assertTrue(ReleaseNotes.WHATS_NEW.startsWith("1.4\n"))
        assertTrue(notes.contains("An update keeps settings that were already saved"))
        val versions = ReleaseNotes.WHATS_NEW.split("\n\n").map { it.substringBefore('\n') }
        assertEquals(listOf("1.4", "1.3", "1.2", "1.1", "1.0"), versions)
        assertEquals(versions.distinct().size, versions.size)
        versions.zipWithNext().forEach { (newer, older) ->
            val next = newer.split('.').map { it.toInt() }
            val prev = older.split('.').map { it.toInt() }
            val higher = next.zip(prev).firstOrNull { (a, b) -> a != b }?.let { (a, b) -> a > b } ?: false
            assertTrue("$newer should be newer than $older", higher)
        }
        ReleaseNotes.WHATS_NEW.split("\n").filter { it.isNotBlank() && !it.first().isDigit() }.forEach { line ->
            assertFalse("bullet is a paragraph: $line", Regex("""[A-Za-z]\. """).containsMatchIn(line))
        }
        assertTrue(notes.contains("#161412"))
        assertTrue(TickerSettings().chartHaptics)
        assertTrue(notes.contains("1H"))
        assertTrue(notes.contains("Candles"))
        assertTrue(notes.contains("Vertical position"))
        assertTrue(notes.contains("0.3 dp"))
        assertTrue(notes.contains("flush under the status bar"))
        assertTrue(notes.contains("just under the status bar"))
        assertTrue(notes.contains("transparent"))
        assertTrue(notes.contains("status-bar tape"))
        assertTrue(notes.contains("#F6F2E8"))
        assertTrue(notes.contains("#F7931A"))
        assertTrue(notes.contains("Bitcoin chart"))
        assertTrue(notes.contains("X feed"))
        assertTrue(settings.luxWirePosts)
        assertEquals(3, settings.luxWirePollMinutes)
        assertTrue(notes.contains("LuxWire Ticker"))
        assertFalse(notes.contains("Lux Wire"))
        assertTrue(notes.contains("sideload"))
        assertTrue(notes.contains("no billing"))
        assertEquals(
            listOf("Tickers", "Display", "Alerts", "Account", "Battery", "What's new", "Contact / About"),
            SettingsMenu.entries.map { it.title },
        )
        assertEquals(SettingsMenu.entries.map { it.route }.distinct().size, SettingsMenu.entries.size)
        assertFalse(notes.contains("Jonathan"))
        assertFalse(ReleaseNotes.WHATS_NEW.contains("live LuxWire", ignoreCase = true))
        assertEquals("No tickers yet. Add one above.", ReleaseNotes.EMPTY_TICKERS)
        assertEquals("Couldn't refresh prices. Try again.", ReleaseNotes.REFRESH_ERROR)
        assertEquals("Invalid ticker", ReleaseNotes.INVALID_TICKER)
        assertFalse(notes.contains("Lux Ticker"))
    }

    @Test fun appThemeStaysSystemUntilAChoiceIsSaved() = runBlocking {
        val file = File.createTempFile("lux-theme", ".preferences_pb")
        file.delete()
        val store = SettingsStore(PreferenceDataStoreFactory.create { file })
        assertEquals(AppTheme.SYSTEM, store.read().appTheme)
        store.setAppTheme(AppTheme.DARK)
        assertEquals(AppTheme.DARK, store.read().appTheme)
        store.setAppTheme(AppTheme.LIGHT)
        assertEquals(AppTheme.LIGHT, store.read().appTheme)
        store.setAppTheme(AppTheme.PAPER)
        assertEquals(AppTheme.PAPER, store.read().appTheme)
        store.setStripTheme(StripTheme.PAPER)
        assertEquals(StripTheme.PAPER, store.read().stripTheme)
        assertTrue(store.read().scrollRightToLeft)
        store.setScrollRightToLeft(false)
        store.setPinSide(PinSide.RIGHT)
        assertFalse(store.read().scrollRightToLeft)
        assertEquals(PinSide.RIGHT, store.read().pinSide)
        store.addAlert(SavedAlert("a1", AlertKind.PRICE, "NVDA", 200.0))
        assertEquals("NVDA", store.read().savedAlerts.single().symbol)
        store.markAlertHit("a1", 50L)
        assertEquals(50L, store.read().savedAlerts.single().hitAtMillis)
        store.removeAlert("a1")
        assertTrue(store.read().savedAlerts.isEmpty())
    }

    private fun quote(symbol: String, price: Double) =
        ParsedQuote(symbol, price, price, 0.0, "USD", 1L, 1L)

    private fun btc(pct: Double) =
        ParsedQuote("BTC", 100.0, null, pct, "USD", 1L, 1L)
}
