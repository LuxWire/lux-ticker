package com.lux.ticker.data

/**
 * LuxWire article card colors, sampled from the C3 paper cards.
 * These values are literal. Paper surfaces do not use Material dynamic color.
 */
object PaperPalette {
    const val BG = 0xFFF6F2E8.toInt()
    const val PANEL = 0xFFEFEADE.toInt()
    const val INK = 0xFF282622.toInt()
    const val BODY = 0xFF161412.toInt()
    const val SUB = 0xFF55555A.toInt()
    const val MUTED = 0xFF78787D.toInt()
    const val RULE = 0xFFD7D7DA.toInt()
    const val RULE_SOFT = 0xFFC8C3B9.toInt()
    const val ACCENT = 0xFFF7931A.toInt()
    const val ACCENT_DEEP = 0xFFC2410C.toInt()
    /** Green and red that stay readable on cream. Not the bright-on-black pair. */
    const val GAIN = 0xFF0E7A32.toInt()
    const val LOSS = 0xFFC62828.toInt()
}
