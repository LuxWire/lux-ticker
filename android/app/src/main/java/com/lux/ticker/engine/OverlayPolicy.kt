package com.lux.ticker.engine

import com.lux.ticker.data.DisplayMode
import com.lux.ticker.data.TickerSettings

object OverlayPolicy {
    fun showOverlay(settings: TickerSettings, granted: Boolean, keyguardLocked: Boolean): Boolean {
        if (!granted || !settings.overlayEnabled) return false
        return when (settings.displayMode) {
            DisplayMode.ALWAYS, DisplayMode.MANUAL -> true
            DisplayMode.LOCK_ONLY -> keyguardLocked
        }
    }
}
