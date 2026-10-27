package com.lux.ticker.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ShadeContrastTest {
    @Test fun nightSwatchesClearBodyTextOnNavy() {
        val foregrounds = listOf(
            ShadePalette.SYMBOL,
            ShadePalette.INK,
            ShadePalette.GAIN,
            ShadePalette.LOSS,
            ShadePalette.MUTED,
            ShadePalette.DOT,
        )
        foregrounds.forEach { color ->
            val ratio = Wcag.ratio(color, ShadePalette.BG)
            assertTrue("$color is ${"%.2f".format(ratio)} against navy", ratio >= Wcag.BODY_TEXT)
        }
    }

    @Test fun daytimeInkGainAndLossStayReadableAndUnchanged() {
        assertEquals(0xFFF6F2E8.toInt(), PaperPalette.BG)
        assertEquals(0xFFF7931A.toInt(), PaperPalette.ACCENT)
        assertEquals(0xFF282622.toInt(), PaperPalette.INK)
        assertEquals(0xFF0E7A32.toInt(), PaperPalette.GAIN)
        assertEquals(0xFFC62828.toInt(), PaperPalette.LOSS)
        listOf(PaperPalette.INK, PaperPalette.GAIN, PaperPalette.LOSS, PaperPalette.SUB).forEach { color ->
            assertTrue(Wcag.ratio(color, PaperPalette.BG) >= Wcag.BODY_TEXT)
        }
        assertEquals(PaperPalette.BG, TapeColors.notificationBand(StripTheme.PAPER, night = false))
        assertEquals(PaperPalette.ACCENT, TapeColors.forNotification(TapeColors.SYMBOL, StripTheme.PAPER, night = false))
        assertEquals(PaperPalette.INK, TapeColors.forNotification(TapeColors.TAG, StripTheme.PAPER, night = false))
        assertEquals(PaperPalette.INK, TapeColors.forNotification(TapeColors.PRICE, StripTheme.PAPER, night = false))
        assertEquals(PaperPalette.GAIN, TapeColors.forNotification(TapeColors.UP, StripTheme.PAPER, night = false))
        assertEquals(PaperPalette.LOSS, TapeColors.forNotification(TapeColors.DOWN, StripTheme.PAPER, night = false))
        assertEquals(TapeColors.PRICE, TapeColors.forNotification(TapeColors.PRICE, StripTheme.DARK, night = false))
        assertEquals(TapeColors.UP, TapeColors.forNotification(TapeColors.UP, StripTheme.DARK, night = false))
        assertEquals(TapeColors.BG, TapeColors.notificationBand(StripTheme.DARK, night = false))
    }

    @Test fun nightIgnoresThePaperTheme() {
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.LIGHT, night = true))
        assertEquals(ShadePalette.BG, TapeColors.notificationBand(StripTheme.DARK, night = true))
        assertEquals(ShadePalette.SYMBOL, TapeColors.forNotification(TapeColors.SYMBOL, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.INK, TapeColors.forNotification(TapeColors.TAG, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.INK, TapeColors.forNotification(TapeColors.PRICE, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.GAIN, TapeColors.forNotification(TapeColors.UP, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.LOSS, TapeColors.forNotification(TapeColors.DOWN, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.MUTED, TapeColors.forNotification(TapeColors.MUTED, StripTheme.PAPER, night = true))
        assertEquals(ShadePalette.DOT, TapeColors.forNotification(TapeColors.DOT, StripTheme.PAPER, night = true))
    }
}
