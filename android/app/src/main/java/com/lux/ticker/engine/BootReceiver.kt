package com.lux.ticker.engine

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import androidx.core.content.ContextCompat
import com.lux.ticker.LuxApp
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent?) {
        if (intent?.action != Intent.ACTION_BOOT_COMPLETED) return
        val pending = goAsync()
        val app = context.applicationContext
        CoroutineScope(Dispatchers.IO).launch {
            try {
                val settings = LuxApp.container(app).settings.read()
                if (settings.startOnBoot) {
                    ContextCompat.startForegroundService(app, Intent(app, TickerService::class.java))
                }
            } catch (t: Throwable) {
                Log.w("LuxTicker", "boot start failed", t)
            } finally {
                pending.finish()
            }
        }
    }
}
