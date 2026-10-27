package com.lux.ticker.ui

import android.app.Application
import android.graphics.Bitmap
import android.view.View.MeasureSpec
import com.lux.ticker.data.ParsedQuote
import com.lux.ticker.data.SessionTag
import com.lux.ticker.data.StripFormatter
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.SymbolSpec
import com.lux.ticker.data.TapeColors
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import kotlin.math.roundToInt
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import java.io.File
import java.time.Instant

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34], application = Application::class)
class TickerStripViewTest {
    @Test fun btcPinOccupiesTheLeftAndTheStripPaints() {
        val context = RuntimeEnvironment.getApplication()
        val view = TickerStripView(context)
        val font = listOf(
            File("src/main/res/font/jetbrains_mono_bold.ttf"),
            File("app/src/main/res/font/jetbrains_mono_bold.ttf"),
        ).firstOrNull { it.exists() }
        if (font != null) {
            view.useTypeface(android.graphics.Typeface.createFromFile(font))
        }
        val metrics = view.resources.displayMetrics
        metrics.density = 3f
        metrics.scaledDensity = 3f
        metrics.densityDpi = 480
        val now = Instant.parse("2026-10-22T21:28:00Z")
        val symbols = listOf(
            SymbolSpec("BTC", "BTC"),
            SymbolSpec("IBIT", "IBIT"),
            SymbolSpec("MSTR", "MSTR"),
            SymbolSpec("NVDA", "NVDA"),
            SymbolSpec("STRF", "STRF"),
            SymbolSpec("MARA", "MARA"),
            SymbolSpec("3350.T", "3350.T"),
            SymbolSpec("SPCX", "SPCX"),
        )
        val quotes = mapOf(
            "BTC" to px("BTC", 108412.50, 1.84, "USD", now),
            "IBIT" to px("IBIT", 46.59, 0.76, "USD", now),
            "MSTR" to px("MSTR", 352.18, 5.12, "USD", now),
            "NVDA" to px("NVDA", 195.85, 0.80, "USD", now),
            "STRF" to px("STRF", 112.40, -0.22, "USD", now),
            "MARA" to px("MARA", 18.06, -1.47, "USD", now),
            "3350.T" to px("3350.T", 842.0, 2.38, "JPY", now),
            "SPCX" to px("SPCX", 214.90, 0.88, "USD", now),
        )
        // 5:28 p.m. New York is after the cash close. The session is AFTER, and US names print AH.
        val tape = StripFormatter.build(symbols, quotes, now)
        assertEquals("BTC", tape.pinned.symbol)
        assertTrue(tape.scrolling.none { it.symbol == "BTC" })
        assertEquals(SessionTag.AFTER, com.lux.ticker.data.MarketSession.tag("MSTR", now))
        view.setTape(tape)
        view.setSpeedDpPerSec(0f)
        view.setScrollingEnabled(false)
        val width = 1800
        val height = 66
        view.measure(
            MeasureSpec.makeMeasureSpec(width, MeasureSpec.EXACTLY),
            MeasureSpec.makeMeasureSpec(height, MeasureSpec.EXACTLY),
        )
        view.layout(0, 0, width, height)
        val (origin, scroll) = TapeGeometry.origins(view.width, view.pinWidthPx, view.dividerPx, view.gapPx)
        assertTrue(view.pinWidthPx > 0)
        assertEquals(origin, view.scrollOriginPx())
        assertEquals(scroll, view.scrollViewportWidthPx())
        assertTrue(view.scrollOriginPx() > view.pinWidthPx)
        assertEquals(width, view.scrollOriginPx() + view.scrollViewportWidthPx())

        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        view.draw(android.graphics.Canvas(bitmap))
        val pixels = IntArray(width * height)
        bitmap.getPixels(pixels, 0, width, 0, 0, width, height)
        val colored = pixels.count { (it and 0x00FFFFFF) != 0 }
        assertTrue("expected the pin and tape to paint, colored=$colored", colored > 50)
        var pinInk = 0
        val pinColumns = view.pinWidthPx.coerceAtMost(width)
        for (y in 0 until height) {
            for (x in 0 until pinColumns) {
                if ((pixels[y * width + x] and 0x00FFFFFF) != 0) pinInk++
            }
        }
        assertTrue("pinned column should paint, ink=$pinInk", pinInk > 10)
        writePng(bitmap)
    }

    @Test fun darkStatusBandLeavesWallpaperPixelsUntouched() {
        val context = RuntimeEnvironment.getApplication()
        val view = TickerStripView(context)
        val metrics = view.resources.displayMetrics
        metrics.density = 3f
        metrics.scaledDensity = 3f
        val now = Instant.parse("2026-10-22T21:28:00Z")
        val tape = StripFormatter.build(
            listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("GLD", "GLD")),
            mapOf(
                "BTC" to px("BTC", 81519.27, 0.52, "USD", now),
                "GLD" to px("GLD", 366.08, -0.79, "USD", now),
            ),
            now,
        )
        view.setTape(tape)
        view.setScrollingEnabled(false)
        view.setStatusBandDp(71f, 100f)
        view.setStripTheme(StripTheme.DARK)
        val width = 1200
        val height = 80
        view.measure(
            MeasureSpec.makeMeasureSpec(width, MeasureSpec.EXACTLY),
            MeasureSpec.makeMeasureSpec(height, MeasureSpec.EXACTLY),
        )
        view.layout(0, 0, width, height)
        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        val sentinel = 0x01000000
        bitmap.eraseColor(sentinel)
        view.draw(android.graphics.Canvas(bitmap))
        val left = (71f * metrics.density).roundToInt()
        assertEquals(sentinel, bitmap.getPixel(4, height / 2))
        assertEquals(sentinel, bitmap.getPixel(left - 2, 2))
        var paper = 0
        var black = 0
        var ink = 0
        for (y in 0 until height) {
            for (x in 0 until width) {
                val pixel = bitmap.getPixel(x, y)
                if (pixel == TapeColors.PAPER) paper++
                if (pixel == TapeColors.BG) black++
                if (pixel != sentinel) ink++
            }
        }
        assertEquals(0, paper)
        assertEquals(0, black)
        assertTrue("light prices should paint over the clear strip, ink=$ink", ink > 20)

        view.setStripTheme(StripTheme.PAPER)
        val paperBitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        view.draw(android.graphics.Canvas(paperBitmap))
        var cream = 0
        for (y in 0 until height) {
            for (x in left until width - (100f * metrics.density).roundToInt()) {
                if (paperBitmap.getPixel(x, y) == TapeColors.PAPER) cream++
            }
        }
        assertTrue("in-app paper preview still fills its band, cream=$cream", cream > 50)
    }

    @Test fun orangeDividerIsOffUntilTurnedOn() {
        val context = RuntimeEnvironment.getApplication()
        val view = TickerStripView(context)
        val metrics = view.resources.displayMetrics
        metrics.density = 3f
        metrics.scaledDensity = 3f
        val now = Instant.parse("2026-10-22T21:28:00Z")
        val tape = StripFormatter.build(
            listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("IBIT", "IBIT")),
            mapOf(
                "BTC" to px("BTC", 108412.50, 1.84, "USD", now),
                "IBIT" to px("IBIT", 46.59, 0.76, "USD", now),
            ),
            now,
        )
        view.setTape(tape)
        view.setScrollingEnabled(false)
        view.setStatusBandDp(71f, 100f)
        view.setOrangeDividers(false)
        val width = 1400
        val height = 72
        view.measure(
            MeasureSpec.makeMeasureSpec(width, MeasureSpec.EXACTLY),
            MeasureSpec.makeMeasureSpec(height, MeasureSpec.EXACTLY),
        )
        view.layout(0, 0, width, height)
        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        view.draw(android.graphics.Canvas(bitmap))
        assertEquals(0, view.dividerPx)
        assertTrue(view.pinWidthPx > 0)
        view.setOrangeDividers(true)
        view.draw(android.graphics.Canvas(bitmap))
        assertTrue(view.dividerPx > 0)
    }

    private fun writePng(bitmap: Bitmap) {
        val dirs = mutableListOf<File>()
        System.getProperty("lux.screenshot.dir")?.let { dirs += File(it) }
        dirs += File("/opt/cursor/artifacts")
        dirs.forEach { dir ->
            runCatching {
                if (dir.mkdirs() || dir.isDirectory) {
                    File(dir, "ticker-strip.png").outputStream().use { out ->
                        bitmap.compress(Bitmap.CompressFormat.PNG, 100, out)
                    }
                }
            }
        }
    }

    private fun px(symbol: String, price: Double, pct: Double, currency: String, now: Instant): ParsedQuote {
        val previous = price / (1.0 + pct / 100.0)
        return ParsedQuote(symbol, price, previous, pct, currency, now.toEpochMilli(), now.toEpochMilli())
    }
}
