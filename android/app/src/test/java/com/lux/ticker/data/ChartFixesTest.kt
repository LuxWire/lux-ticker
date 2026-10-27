package com.lux.ticker.data

import android.view.HapticFeedbackConstants
import com.lux.ticker.ui.ChartHaptics
import com.lux.ticker.ui.ReleaseNotes
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ChartFixesTest {
    @Test fun fieldInkStaysBlackOnPaperEvenWhenTheAppThemeIsDark() {
        assertEquals(PaperPalette.BODY, FieldInk.argb(forcePaper = true, paperSurface = false, dark = true))
        assertEquals(PaperPalette.BODY, FieldInk.argb(forcePaper = false, paperSurface = true, dark = true))
        assertEquals(PaperPalette.BG, FieldInk.argb(forcePaper = false, paperSurface = false, dark = true))
        assertEquals(PaperPalette.BODY, FieldInk.argb(forcePaper = false, paperSurface = false, dark = false))
        assertEquals(0xFF161412.toInt(), PaperPalette.BODY)
        assertEquals(0xFFF6F2E8.toInt(), PaperPalette.BG)
    }

    @Test fun searchNormalizesSuggestsAndNamesTheMiss() {
        assertEquals("GLD", ChartSearch.normalize("  gld "))
        assertEquals("Enter a ticker.", ChartSearch.formatError(""))
        assertEquals(ReleaseNotes.INVALID_TICKER, ChartSearch.formatError("BAD SYMBOL"))
        assertNull(ChartSearch.formatError("BRK.B"))
        assertEquals("No chart for XYZ.", ChartSearch.unknownMessage("XYZ"))
        val known = listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("GLD", "GLD"), SymbolSpec("MSTR", "MSTR"))
        assertEquals(listOf("GLD"), ChartSearch.suggestions("gl", known).map { it.symbol })
        assertTrue(ChartSearch.suggestions("  ", known).isEmpty())
        assertTrue(ChartSearch.suggestions("zzz", known).isEmpty())
        val many = (1..8).map { SymbolSpec("A$it", "A$it") }
        assertEquals(ChartSearch.LIMIT, ChartSearch.suggestions("A", many).size)
    }

    @Test fun scrubHapticsIgnoreTheSystemTouchSetting() {
        assertEquals(HapticFeedbackConstants.CLOCK_TICK, ChartHaptics.feedbackConstant(30))
        assertEquals(HapticFeedbackConstants.CLOCK_TICK, ChartHaptics.feedbackConstant(34))
        assertEquals(HapticFeedbackConstants.KEYBOARD_TAP, ChartHaptics.feedbackConstant(29))
        assertEquals(HapticFeedbackConstants.SEGMENT_TICK, ChartHaptics.secondaryConstant(30))
        assertNull(ChartHaptics.secondaryConstant(29))
        assertEquals(
            listOf(HapticFeedbackConstants.CLOCK_TICK, HapticFeedbackConstants.SEGMENT_TICK),
            ChartHaptics.constants(30),
        )
        assertEquals(listOf(HapticFeedbackConstants.KEYBOARD_TAP), ChartHaptics.constants(29))
        @Suppress("DEPRECATION")
        assertTrue(ChartHaptics.FLAGS and HapticFeedbackConstants.FLAG_IGNORE_GLOBAL_SETTING != 0)
        assertTrue(ChartHaptics.FLAGS and HapticFeedbackConstants.FLAG_IGNORE_VIEW_SETTING != 0)
        assertTrue(TickerSettings().chartHaptics)
    }
}
