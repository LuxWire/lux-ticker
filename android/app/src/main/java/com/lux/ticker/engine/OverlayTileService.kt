package com.lux.ticker.engine

import android.content.Intent
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService
import androidx.core.content.ContextCompat
import com.lux.ticker.LuxApp
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

/** Quick Settings tile. Shows or hides the status-bar strip. */
class OverlayTileService : TileService() {
    override fun onStartListening() {
        CoroutineScope(Dispatchers.IO).launch {
            val enabled = runCatching { LuxApp.container(this@OverlayTileService).settings.read().overlayEnabled }
                .getOrDefault(false)
            publish(enabled)
        }
    }

    override fun onClick() {
        CoroutineScope(Dispatchers.IO).launch {
            val store = LuxApp.container(this@OverlayTileService).settings
            val next = !store.read().overlayEnabled
            store.setOverlay(next)
            val app = applicationContext
            try {
                ContextCompat.startForegroundService(app, Intent(app, TickerService::class.java))
            } catch (_: Exception) {
                // The tile still reflects the saved choice. The next open starts the service.
            }
            publish(next)
        }
    }

    private fun publish(enabled: Boolean) {
        val tile = qsTile ?: return
        tile.state = if (enabled) Tile.STATE_ACTIVE else Tile.STATE_INACTIVE
        tile.label = "LuxWire Ticker"
        tile.updateTile()
    }
}
