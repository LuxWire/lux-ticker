package com.lux.ticker.widget

import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.Context
import android.content.Intent
import android.util.Log
import androidx.core.content.ContextCompat
import com.lux.ticker.LuxApp
import com.lux.ticker.R
import com.lux.ticker.data.QuoteCache
import com.lux.ticker.data.StripFormatter
import com.lux.ticker.data.PinSide
import com.lux.ticker.engine.TickerService
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.io.File
import java.time.Instant

class TickerWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        refreshWidgets(context, appWidgetManager, appWidgetIds, R.layout.widget_ticker, goAsync())
    }

    override fun onEnabled(context: Context) {
        startTicker(context)
    }
}

class TickerWidgetNarrow : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        refreshWidgets(context, appWidgetManager, appWidgetIds, R.layout.widget_ticker_1x2, goAsync())
    }

    override fun onEnabled(context: Context) {
        startTicker(context)
    }
}

class TickerWidgetSquare : AppWidgetProvider() {
    override fun onUpdate(context: Context, appWidgetManager: AppWidgetManager, appWidgetIds: IntArray) {
        refreshWidgets(context, appWidgetManager, appWidgetIds, R.layout.widget_ticker_2x2, goAsync())
    }

    override fun onEnabled(context: Context) {
        startTicker(context)
    }
}

private fun refreshWidgets(
    context: Context,
    appWidgetManager: AppWidgetManager,
    appWidgetIds: IntArray,
    layout: Int,
    pending: android.content.BroadcastReceiver.PendingResult,
) {
    val app = context.applicationContext
    CoroutineScope(Dispatchers.IO).launch {
        try {
            val container = LuxApp.container(app)
            val symbols = container.symbols.read()
            val quotes = container.quotes.snapshot().ifEmpty {
                QuoteCache(File(app.filesDir, QuoteCache.FILE_NAME)).load()
            }
            val settings = container.settings.read()
            val tape = StripFormatter.fromSettings(symbols, quotes, Instant.now(), settings)
            WidgetRenderer.bind(
                app,
                appWidgetManager,
                appWidgetIds,
                tape,
                settings.orangeDividers,
                settings.stripTheme,
                settings.textSizeSp,
                if (settings.pinSide == PinSide.RIGHT && layout == R.layout.widget_ticker) {
                    R.layout.widget_ticker_pin_right
                } else {
                    layout
                },
            )
            startTicker(app)
        } finally {
            pending.finish()
        }
    }
}

private fun startTicker(context: Context) {
    try {
        ContextCompat.startForegroundService(context, Intent(context, TickerService::class.java))
    } catch (e: Exception) {
        Log.w("LuxTicker", "widget could not start the ticker", e)
    }
}
