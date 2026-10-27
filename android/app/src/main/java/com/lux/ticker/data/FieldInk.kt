package com.lux.ticker.data

/** Typed text and the cursor. Paper and light stay ink. Dark stays cream. A chart page forces paper. */
object FieldInk {
    fun argb(forcePaper: Boolean, paperSurface: Boolean, dark: Boolean): Int = when {
        forcePaper || paperSurface -> PaperPalette.BODY
        dark -> PaperPalette.BG
        else -> PaperPalette.BODY
    }
}
