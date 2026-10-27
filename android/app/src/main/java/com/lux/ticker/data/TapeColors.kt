package com.lux.ticker.data

object TapeColors {
    const val BG = 0xFF000000.toInt()
    const val PAPER = PaperPalette.BG
    /** Prices on the paper lock-screen ticker. White prices disappear on cream. */
    const val LOCK_INK = PaperPalette.INK
    const val STRIP_LIGHT = 0xFFFFFFFF.toInt()
    const val INK = 0xFF1A140C.toInt()
    const val PIN_WELL = 0xFF14110C.toInt()
    const val SYMBOL = PaperPalette.ACCENT
    const val SYMBOL_PIN = PaperPalette.ACCENT
    const val PRICE = 0xFFFFFFFF.toInt()
    const val UP = 0xFF3DDC6A.toInt()
    const val DOWN = 0xFFFF5A5A.toInt()
    const val FLAT = 0xFFB0B0B0.toInt()
    const val DOT = 0xFF8A8A8A.toInt()
    const val TAG = 0xFFC8C8C8.toInt()
    const val MUTED = 0xFF9A9A9A.toInt()
    const val DIVIDER = 0xFFFFB000.toInt()
    const val STALE_ALPHA = 102

    fun band(theme: StripTheme): Int = when (theme) {
        StripTheme.PAPER -> PAPER
        StripTheme.LIGHT -> STRIP_LIGHT
        StripTheme.DARK -> BG
    }

    /**
     * The launcher status tape paints a fill only for light and paper.
     * Dark paints nothing, so the wallpaper shows through.
     */
    fun statusBandFills(theme: StripTheme): Boolean = theme != StripTheme.DARK

    /** Light and paper strips keep the orange marks and swap the paper and ink. */
    fun forStrip(color: Int, theme: StripTheme): Int {
        if (theme == StripTheme.DARK) return color
        if (theme == StripTheme.PAPER) return forPaper(color)
        return when (color) {
            PRICE, TAG -> INK
            UP -> PaperPalette.GAIN
            DOWN -> PaperPalette.LOSS
            FLAT, DOT, MUTED -> 0xFF5C564C.toInt()
            BG -> band(theme)
            PIN_WELL -> 0xFFECECEC.toInt()
            else -> color
        }
    }

    private fun forPaper(color: Int): Int = when (color) {
        PRICE, TAG -> PaperPalette.INK
        UP -> PaperPalette.GAIN
        DOWN -> PaperPalette.LOSS
        FLAT, DOT, MUTED -> PaperPalette.SUB
        BG -> PaperPalette.BG
        PIN_WELL -> PaperPalette.PANEL
        else -> color
    }

    fun forStrip(color: Int, light: Boolean): Int =
        forStrip(color, if (light) StripTheme.LIGHT else StripTheme.DARK)

    /** Lock-screen tape uses the same colors as the status-bar strip for [theme]. */
    fun forLockTicker(color: Int, theme: StripTheme = StripTheme.PAPER): Int = forStrip(color, theme)

    /**
     * Shade and lock notification. Night follows the phone, not the in-app theme,
     * so a paper setting still goes navy when the system is dark.
     */
    fun notificationBand(theme: StripTheme, night: Boolean): Int =
        if (night) ShadePalette.BG else band(theme)

    fun forNotification(color: Int, theme: StripTheme, night: Boolean): Int =
        if (night) ShadePalette.map(color) else forLockTicker(color, theme)
}

/**
 * Pull-down tape when the phone is in night mode.
 * Every swatch is at least 4.5:1 against [BG].
 */
object ShadePalette {
    const val BG = 0xFF0E1520.toInt()
    const val SYMBOL = 0xFFFFD15C.toInt()
    const val INK = 0xFFF4EFE4.toInt()
    const val GAIN = 0xFF7DFFA3.toInt()
    const val LOSS = 0xFFFF8A80.toInt()
    const val MUTED = 0xFFC8C0B4.toInt()
    const val DOT = 0xFFB7AFA3.toInt()

    fun map(color: Int): Int = when (color) {
        TapeColors.SYMBOL -> SYMBOL
        TapeColors.PRICE, TapeColors.TAG -> INK
        TapeColors.UP -> GAIN
        TapeColors.DOWN -> LOSS
        TapeColors.FLAT, TapeColors.MUTED -> MUTED
        TapeColors.DOT -> DOT
        TapeColors.BG -> BG
        else -> color
    }
}

/** WCAG 2.1 relative-luminance contrast. */
object Wcag {
    const val BODY_TEXT = 4.5

    fun ratio(foreground: Int, background: Int): Double {
        val lighter = maxOf(luminance(foreground), luminance(background))
        val darker = minOf(luminance(foreground), luminance(background))
        return (lighter + 0.05) / (darker + 0.05)
    }

    private fun luminance(color: Int): Double {
        val r = channel((color shr 16) and 0xFF)
        val g = channel((color shr 8) and 0xFF)
        val b = channel(color and 0xFF)
        return 0.2126 * r + 0.7152 * g + 0.0722 * b
    }

    private fun channel(component: Int): Double {
        val s = component / 255.0
        return if (s <= 0.04045) s / 12.92 else Math.pow((s + 0.055) / 1.055, 2.4)
    }
}

/** Swatches for the symbol and price color settings. */
object TapeColorChoices {
    const val GOLD = 0xFFE8C547.toInt()

    val swatches = listOf(
        TapeColors.SYMBOL to "Orange",
        TapeColors.PRICE to "White",
        PaperPalette.INK to "Ink",
        PaperPalette.GAIN to "Green",
        PaperPalette.LOSS to "Red",
        GOLD to "Gold",
    )
}

object TapeColorPref {
    fun encode(color: Int): String = String.format(java.util.Locale.US, "#%08X", color)

    fun decode(raw: String?, fallback: Int): Int {
        val text = raw?.trim()?.removePrefix("#") ?: return fallback
        val value = text.toLongOrNull(16) ?: return fallback
        return when (text.length) {
            6 -> (0xFF000000L or value).toInt()
            8 -> value.toInt()
            else -> fallback
        }
    }
}
