package com.lux.ticker.ui

object TapeGeometry {
    fun origins(widthPx: Int, pinWidthPx: Int, dividerPx: Int, gapPx: Int): Pair<Int, Int> {
        val origin = (pinWidthPx + dividerPx + gapPx).coerceAtLeast(0)
        val scroll = (widthPx - origin).coerceAtLeast(0)
        return origin to scroll
    }
}
