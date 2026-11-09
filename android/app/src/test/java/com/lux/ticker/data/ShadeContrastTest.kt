package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ShadeContrastTest {
    @Test fun notificationSwatchesClearBodyTextOnInkInEitherMode() {
        val swatches = listOf(
            ShadePalette.EDGE,
            ShadePalette.SYMBOL,
            ShadePalette.INK,
            ShadePalette.GAIN,
            ShadePalette.LOSS,
            ShadePalette.MUTED,
            ShadePalette.DOT,
        )
        listOf(false, true).forEach { night ->
            assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.PAPER, night))
            assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.LIGHT, night))
            assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.DARK, night))
            swatches.forEach { color ->
                val ratio = Wcag.ratio(color, ShadePalette.BG)
                assertTrue("$color is ${"%.2f".format(ratio)} on ink, night=$night", ratio >= Wcag.BODY_TEXT)
            }
            assertEquals(ShadePalette.SYMBOL, TapeColors.forNotification(TapeColors.SYMBOL, StripTheme.PAPER, night))
            assertEquals(ShadePalette.INK, TapeColors.forNotification(TapeColors.PRICE, StripTheme.LIGHT, night))
            assertEquals(ShadePalette.INK, TapeColors.forNotification(TapeColors.TAG, StripTheme.DARK, night))
            assertEquals(ShadePalette.GAIN, TapeColors.forNotification(TapeColors.UP, StripTheme.PAPER, night))
            assertEquals(ShadePalette.LOSS, TapeColors.forNotification(TapeColors.DOWN, StripTheme.PAPER, night))
            assertEquals(ShadePalette.MUTED, TapeColors.forNotification(TapeColors.FLAT, StripTheme.PAPER, night))
            assertEquals(ShadePalette.MUTED, TapeColors.forNotification(TapeColors.MUTED, StripTheme.PAPER, night))
            assertEquals(ShadePalette.DOT, TapeColors.forNotification(TapeColors.DOT, StripTheme.PAPER, night))
        }
        assertEquals(0xFF14130F.toInt(), ShadePalette.BG)
        assertEquals(0xFFEA580C.toInt(), ShadePalette.EDGE)
        assertEquals(0xFFFFC83D.toInt(), ShadePalette.SYMBOL)
        assertEquals(0xFFF4EFE4.toInt(), ShadePalette.INK)
        assertEquals(0xFFFF6B6B.toInt(), ShadePalette.LOSS)
        assertEquals(0xFF4ADE80.toInt(), ShadePalette.GAIN)
        assertEquals(0xFFB7AFA3.toInt(), ShadePalette.MUTED)
    }

    @Test fun savedColorsStayOnlyWhenTheyClearTheInkBand() {
        val gold = TapeColorChoices.GOLD
        assertTrue(Wcag.ratio(gold, ShadePalette.BG) >= Wcag.BODY_TEXT)
        assertEquals(gold, ShadePalette.place(gold, priceLike = false))
        assertEquals(gold, ShadePalette.place(gold, priceLike = true))

        val darkInk = 0xFF1A140C.toInt()
        assertTrue(Wcag.ratio(darkInk, ShadePalette.BG) < Wcag.BODY_TEXT)
        assertEquals(ShadePalette.SYMBOL, ShadePalette.place(darkInk, priceLike = false))
        assertEquals(ShadePalette.INK, ShadePalette.place(darkInk, priceLike = true))
        assertEquals(ShadePalette.SYMBOL, TapeColors.forNotification(TapeColors.INK, StripTheme.PAPER, night = false))
        assertEquals(ShadePalette.INK, TapeColors.forNotification(PaperPalette.INK, StripTheme.PAPER, night = true, priceLike = true))
    }

    @Test fun daytimePaperStripStaysUnchanged() {
        assertEquals(0xFFF6F2E8.toInt(), PaperPalette.BG)
        assertEquals(0xFFF7931A.toInt(), PaperPalette.ACCENT)
        assertEquals(0xFF282622.toInt(), PaperPalette.INK)
        assertEquals(0xFF0E7A32.toInt(), PaperPalette.GAIN)
        assertEquals(0xFFC62828.toInt(), PaperPalette.LOSS)
        listOf(PaperPalette.INK, PaperPalette.GAIN, PaperPalette.LOSS, PaperPalette.SUB).forEach { color ->
            assertTrue(Wcag.ratio(color, PaperPalette.BG) >= Wcag.BODY_TEXT)
        }
        assertEquals(PaperPalette.BG, TapeColors.band(StripTheme.PAPER))
        assertEquals(PaperPalette.INK, TapeColors.forStrip(TapeColors.PRICE, StripTheme.PAPER))
        assertEquals(PaperPalette.ACCENT, TapeColors.forStrip(TapeColors.SYMBOL, StripTheme.PAPER))
        assertEquals(TapeColors.PRICE, TapeColors.forStrip(TapeColors.PRICE, StripTheme.DARK))
        assertEquals(TapeColors.BG, TapeColors.band(StripTheme.DARK))
    }
}
