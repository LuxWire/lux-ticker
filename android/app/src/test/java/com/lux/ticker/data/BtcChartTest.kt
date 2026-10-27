package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZoneOffset

class BtcChartTest {
    @Test fun paperMatchesTheArticleCard() {
        assertEquals(0xFFF6F2E8.toInt(), PaperPalette.BG)
        assertEquals(0xFFEFEADE.toInt(), PaperPalette.PANEL)
        assertEquals(0xFF282622.toInt(), PaperPalette.INK)
        assertEquals(0xFF161412.toInt(), PaperPalette.BODY)
        assertEquals(0xFF55555A.toInt(), PaperPalette.SUB)
        assertEquals(0xFF78787D.toInt(), PaperPalette.MUTED)
        assertEquals(0xFFF7931A.toInt(), PaperPalette.ACCENT)
        assertEquals(0xFFC2410C.toInt(), PaperPalette.ACCENT_DEEP)
        assertEquals(PaperPalette.BG, TapeColors.PAPER)
        assertEquals(PaperPalette.INK, TapeColors.LOCK_INK)
        assertEquals(PaperPalette.ACCENT, BtcChart.SERIES)
        assertEquals(0xFFF7931A.toInt(), BtcChart.CANDLE_UP)
        assertEquals(PaperPalette.INK, BtcChart.CANDLE_DOWN)
        assertEquals(PaperPalette.ACCENT, TapeColors.SYMBOL)
        assertEquals(PaperPalette.PANEL, TapeColors.forStrip(TapeColors.PIN_WELL, StripTheme.PAPER))
        assertEquals(PaperPalette.INK, TapeColors.forStrip(TapeColors.PRICE, StripTheme.PAPER))
        assertEquals(PaperPalette.GAIN, TapeColors.forStrip(TapeColors.UP, StripTheme.PAPER))
        assertEquals(PaperPalette.LOSS, TapeColors.forStrip(TapeColors.DOWN, StripTheme.PAPER))
        assertEquals(PaperPalette.SUB, TapeColors.forStrip(TapeColors.MUTED, StripTheme.PAPER))
        assertEquals(TapeColors.UP, TapeColors.forStrip(TapeColors.UP, StripTheme.DARK))
        assertEquals(PaperPalette.INK, TapeColors.forLockTicker(TapeColors.PRICE))
        assertEquals(StripTheme.PAPER, AppTheme.PAPER.statusStrip(systemDark = true))
        assertEquals(StripTheme.DARK, AppTheme.DARK.statusStrip(systemDark = false))
        assertEquals(StripTheme.LIGHT, AppTheme.LIGHT.statusStrip(systemDark = true))
        assertEquals(StripTheme.DARK, AppTheme.SYSTEM.statusStrip(systemDark = true))
        assertEquals(StripTheme.PAPER, AppTheme.SYSTEM.statusStrip(systemDark = false))
        val samples = listOf(BtcPoint(0, 1.0), BtcPoint(100, 2.0), BtcPoint(200, 3.0))
        assertEquals(0, BtcChart.nearestIndex(samples, 0f))
        assertEquals(1, BtcChart.nearestIndex(samples, 0.5f))
        assertEquals(2, BtcChart.nearestIndex(samples, 1f))
    }

    private val zone = ZoneId.of("America/New_York")
    private val now = Instant.parse("2026-10-23T20:00:00Z")

    @Test fun presetsCoverTheRangesAndDefaultMath() {
        val day = BtcChart.preset(BtcPreset.DAY, now, zone)
        assertEquals(24 * 3_600_000L, day.endMillis - day.startMillis)
        assertEquals("5m", BtcChart.intervalFor(day.startMillis, day.endMillis))
        assertEquals(60_000L, BtcChart.refreshMillis(day))

        val week = BtcChart.preset(BtcPreset.WEEK, now, zone)
        assertEquals("60m", BtcChart.intervalFor(week.startMillis, week.endMillis))
        assertTrue(BtcChart.plan(week).single() is BtcFetch.Yahoo)

        val month = BtcChart.preset(BtcPreset.MONTH, now, zone)
        assertEquals("Last 30 days", month.label)
        assertEquals("60m", BtcChart.intervalFor(month.startMillis, month.endMillis))

        val six = BtcChart.preset(BtcPreset.SIX_MONTH, now, zone)
        assertEquals("1d", BtcChart.intervalFor(six.startMillis, six.endMillis))
        assertEquals(5 * 60_000L, BtcChart.refreshMillis(six))

        val ytd = BtcChart.preset(BtcPreset.YTD, now, zone)
        val start = Instant.ofEpochMilli(ytd.startMillis).atZone(zone).toLocalDate()
        assertEquals(LocalDate.of(2026, 1, 1), start)
        assertTrue(ytd.label.contains("device time"))

        val year = BtcChart.preset(BtcPreset.YEAR, now, zone)
        assertEquals(365L * 86_400_000L, year.endMillis - year.startMillis)

        val all = BtcChart.preset(BtcPreset.ALL, now, zone)
        assertTrue(all.fullHistory)
        assertEquals(BtcChart.EARLIEST, Instant.ofEpochMilli(all.startMillis).atZone(ZoneOffset.UTC).toLocalDate())
        val plan = BtcChart.plan(all)
        assertTrue(plan.first() is BtcFetch.BlockchainAll)
        assertTrue(plan.last() is BtcFetch.Yahoo)
    }

    @Test fun customRangeRejectsABackwardsSpanAndReaches2009() {
        assertNull(BtcChart.custom(LocalDate.of(2026, 10, 23), LocalDate.of(2026, 10, 1), now, zone))
        val early = BtcChart.custom(LocalDate.of(2011, 6, 1), LocalDate.of(2011, 6, 30), now, zone)!!
        assertTrue(early.label.contains("2011"))
        assertTrue(BtcChart.plan(early).first() is BtcFetch.BlockchainSpan)
        val recent = BtcChart.custom(LocalDate.of(2026, 10, 1), LocalDate.of(2026, 10, 23), now, zone)!!
        assertTrue(BtcChart.plan(recent).single() is BtcFetch.Yahoo)
        assertEquals(10.0, BtcChart.changePct(listOf(BtcPoint(1, 100.0), BtcPoint(2, 110.0)))!!, 0.001)
        val drawn = BtcChart.drawPoints(List(1000) { BtcPoint(it.toLong(), it + 1.0) }, max = 10)
        assertEquals(10, drawn.size)
        assertEquals(0L, drawn.first().epochMillis)
        assertEquals(999L, drawn.last().epochMillis)
    }

    @Test fun parsersKeepClosesAndDropEmptyPrints() {
        val yahoo = BtcChartParse.yahoo(
            """
            {"chart":{"result":[{"timestamp":[1410912000,1410998400],
              "indicators":{"quote":[{"close":[457.33,null]}]}}],"error":null}}
            """.trimIndent(),
        )
        assertEquals(1, yahoo.size)
        assertEquals(457.33, yahoo.single().price, 0.001)
        assertEquals(1410912000_000L, yahoo.single().epochMillis)

        val chain = BtcChartParse.blockchain(
            """{"values":[{"x":1230940800,"y":0.0},{"x":1282003200,"y":0.07}]}""",
        )
        assertEquals(1, chain.size)
        assertEquals(0.07, chain.single().price, 0.0001)
        val stitched = BtcChart.stitch(
            listOf(BtcPoint(1_282_003_200_000L, 0.07), BtcPoint(1_500_000_000_000L, 400.0)),
            listOf(BtcPoint(BtcChart.YAHOO_FIRST.toEpochMilli(), 457.0)),
        )
        assertEquals(listOf(0.07, 457.0), stitched.map { it.price })
    }

    @Test fun hourPresetUsesOneMinuteBars() {
        val hour = BtcChart.preset(BtcPreset.HOUR, now, zone)
        assertEquals("1H", BtcPreset.HOUR.label)
        assertEquals(60 * 60_000L, hour.endMillis - hour.startMillis)
        assertEquals("Last 60 minutes", hour.label)
        assertEquals("1m", BtcChart.intervalFor(hour.startMillis, hour.endMillis))
        assertEquals("1-minute candles", BtcChart.intervalCaption("1m"))
        assertEquals("5-minute candles", BtcChart.intervalCaption("5m"))
        assertEquals("hourly candles", BtcChart.intervalCaption("60m"))
        assertEquals("daily candles", BtcChart.intervalCaption("1d"))
        assertEquals(60_000L, BtcChart.refreshMillis(hour))
        val fetch = BtcChart.plan(hour).single() as BtcFetch.Yahoo
        assertEquals("1m", fetch.interval)
        assertEquals("BTC-USD", fetch.symbol)
        assertTrue(BtcChart.yahooUrl(fetch).contains("/BTC-USD?"))
        val other = BtcChart.plan(hour, "MSTR").single() as BtcFetch.Yahoo
        assertEquals("MSTR", other.symbol)
        assertEquals("1m", other.interval)
        assertTrue(BtcChart.yahooUrl(other).contains("/MSTR?"))
        assertTrue(BtcChart.plan(BtcChart.preset(BtcPreset.ALL, now, zone), "NVDA").none { it is BtcFetch.BlockchainAll })
        assertTrue(BtcChart.scrubTick(-1, 0))
        assertFalse(BtcChart.scrubTick(3, 3))
        assertFalse(BtcChart.scrubTick(3, null))
    }

    @Test fun candlesKeepOpenHighLowClose() {
        val bars = BtcChartParse.bars(
            """
            {"chart":{"result":[{"timestamp":[1700000000,1700003600],
              "indicators":{"quote":[{"open":[10.0,20.0],"high":[12.0,25.0],"low":[9.0,18.0],"close":[11.0,19.0]}]}}],"error":null}}
            """.trimIndent(),
        )
        assertEquals(2, bars.size)
        assertEquals(10.0, bars[0].open, 0.001)
        assertEquals(12.0, bars[0].high, 0.001)
        assertEquals(9.0, bars[0].low, 0.001)
        assertEquals(11.0, bars[0].close, 0.001)
        val marks = BtcChartParse.marksFromBars(bars)
        assertEquals(25.0, marks.high!!.price, 0.001)
        assertEquals(9.0, marks.low!!.price, 0.001)
        val many = List(40) { index ->
            YahooBar(
                epochMillis = index.toLong(),
                close = index + 1.0,
                high = index + 5.0,
                low = index.toDouble(),
                open = index + 0.5,
            )
        }
        val drawn = BtcChart.drawBars(many, max = 4)
        assertEquals(4, drawn.size)
        assertEquals(many.first().open, drawn.first().open, 0.001)
        assertEquals(many.last().close, drawn.last().close, 0.001)
        assertTrue(drawn.maxOf { it.high } >= many.maxOf { it.high } - 5.0)
        val label = BtcChart.formatEt(1_700_000_000_000L)
        assertTrue(label.contains("ET"))
        assertTrue(label.contains("2023"))
    }
}
