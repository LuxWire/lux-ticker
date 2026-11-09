package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ShadeContrastTest {
    @Test fun inkSwatchesClearBodyTextOnBand() {
        val foregrounds = listOf(
            ShadePalette.SYMBOL,
            ShadePalette.INK,
            ShadePalette.GAIN,
            ShadePalette.LOSS,
            ShadePalette.MUTED,
            ShadePalette.DOT,
            ShadePalette.EDGE,
        )
        foregrounds.forEach { color ->
            val ratio = Wcag.ratio(color, ShadePalette.BG)
            assertTrue("$color is ${"%.2f".format(ratio)} against ink band", ratio >= Wcag.BODY_TEXT)
        }
    }

    @Test fun paperStripColorsStayReadableAndUnchanged() {
        assertEquals(0xFFF6F2E8.toInt(), PaperPalette.BG)
        assertEquals(0xFFF7931A.toInt(), PaperPalette.ACCENT)
        assertEquals(0xFF282622.toInt(), PaperPalette.INK)
        assertEquals(0xFF0E7A32.toInt(), PaperPalette.GAIN)
        assertEquals(0xFFC62828.toInt(), PaperPalette.LOSS)
        listOf(PaperPalette.INK, PaperPalette.GAIN, PaperPalette.LOSS, PaperPalette.SUB).forEach { color ->
            assertTrue(Wcag.ratio(color, PaperPalette.BG) >= Wcag.BODY_TEXT)
        }
    }

    @Test fun notificationAlwaysUsesInkBand() {
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.PAPER, night = false))
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.LIGHT, night = false))
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.DARK, night = false))
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.SYMBOL, TapeColors.forNotification(TapeColors.SYMBOL, StripTheme.PAPER, night = false))
        assertEquals(ShadePalette.INK, TapeColors.forNotification(TapeColors.TAG, StripTheme.PAPER, night = false))
        assertEquals(ShadePalette.INK, TapeColors.forNotification(TapeColors.PRICE, StripTheme.DARK, night = false))
        assertEquals(ShadePalette.GAIN, TapeColors.forNotification(TapeColors.UP, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.LOSS, TapeColors.forNotification(TapeColors.DOWN, StripTheme.LIGHT, night = false))
        assertEquals(ShadePalette.MUTED, TapeColors.forNotification(TapeColors.MUTED, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.DOT, TapeColors.forNotification(TapeColors.DOT, StripTheme.PAPER, night = true))
    }

    @Test fun customColorUnderContrastFallsBack() {
        val faint = 0xFF2A2820.toInt() // near the ink band
        assertTrue(Wcag.ratio(faint, ShadePalette.BG) < Wcag.BODY_TEXT)
        assertEquals(ShadePalette.SYMBOL, ShadePalette.place(faint, priceLike = false))
        assertEquals(ShadePalette.INK, ShadePalette.place(faint, priceLike = true))
        val bright = 0xFFE8E4D8.toInt()
        assertTrue(Wcag.ratio(bright, ShadePalette.BG) >= Wcag.BODY_TEXT)
        assertEquals(bright, ShadePalette.place(bright, priceLike = false))
    }
}
