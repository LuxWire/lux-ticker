package com.lux.ticker.engine

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.app.KeyguardManager
import android.content.Intent
import android.content.res.Configuration
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.PowerManager
import android.provider.Settings
import android.util.Log
import com.lux.ticker.LuxApp
import com.lux.ticker.R
import com.lux.ticker.data.LockTickerText
import com.lux.ticker.data.NotificationCrawl
import com.lux.ticker.data.StripFormatter
import com.lux.ticker.data.TapeModel
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.luxwire.LuxWireFeed
import com.lux.ticker.luxwire.LuxWireNotifier
import com.lux.ticker.widget.WidgetRenderer
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.time.Instant

class TickerService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val kicks = MutableStateFlow(0)
    private var engine: QuoteEngine? = null
    private var overlay: OverlayWindows? = null
    private var renderJob: Job? = null
    private var noteKey = ""
    private var widgetAt = 0L
    private var widgetSymbols = 0
    private var widgetDividers = false
    private var widgetTheme = ""
    private var widgetPin = ""
    private var widgetText = 0f
    private val alerts = PriceAlertGate()
    private var tapeShift = 0
    private var latestSettings = TickerSettings()
    private var latestTape = TapeModel.EMPTY
    private var latestGranted = false
    private var legacyCleared = false
    private var foregroundPosted = false
    private var screenInteractive: () -> Boolean = { true }
    private var keyguardLocked: () -> Boolean = { false }

    override fun onCreate() {
        super.onCreate()
        createChannel()
        val container = LuxApp.container(this)
        val power = getSystemService(POWER_SERVICE) as PowerManager
        val keyguard = getSystemService(KeyguardManager::class.java)
        screenInteractive = { power.isInteractive }
        keyguardLocked = { keyguard.isKeyguardLocked }
        engine = QuoteEngine(
            yahoo = container.yahoo,
            webSocketHttp = container.webSocketHttp,
            quotes = container.quotes,
            symbols = container.symbols,
            screenOn = { power.isInteractive },
            feed = container.feed,
        ).also { it.start(scope) }
        overlay = OverlayWindows(this, getSystemService(WINDOW_SERVICE) as android.view.WindowManager)
        latestGranted = Settings.canDrawOverlays(this)
        startAsForeground(TickerSettings(), latestGranted)
        renderJob = scope.launch {
            runCatching { LuxApp.container(this@TickerService).settings.ensureFactoryDefaults() }
            renderLoop()
        }
        scope.launch {
            while (isActive) {
                delay(30_000)
                kicks.value = kicks.value + 1
            }
        }
        scope.launch {
            while (isActive) {
                delay(NotificationCrawl.delayMs(latestSettings.speedDpPerSec))
                if (!NotificationCrawl.shouldAdvance(screenInteractive())) continue
                val step = if (latestSettings.scrollRightToLeft) 1 else -1
                tapeShift += step
                publishTape(latestSettings, latestTape, latestGranted, tapeShift)
            }
        }
        scope.launch {
            val feed = LuxWireFeed(this@TickerService)
            while (isActive) {
                withContext(Dispatchers.IO) {
                    try {
                        feed.poll()
                    } catch (e: Exception) {
                        Log.w("LuxTicker", "lux wire feed", e)
                    }
                }
                val minutes = try {
                    LuxApp.container(this@TickerService).settings.read().luxWirePollMinutes
                } catch (e: Exception) {
                    3
                }
                delay(minutes.coerceIn(2, 5) * 60_000L)
            }
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_HIDE -> scope.launch { LuxApp.container(this@TickerService).settings.setOverlay(false) }
            ACTION_SHOW -> scope.launch { LuxApp.container(this@TickerService).settings.setOverlay(true) }
            ACTION_REFRESH -> engine?.requestRefresh()
            ACTION_LUX_WIRE -> scope.launch(Dispatchers.IO) {
                try {
                    LuxWireFeed(this@TickerService).poll()
                } catch (e: Exception) {
                    Log.w("LuxTicker", "lux wire feed", e)
                }
            }
            ACTION_STOP -> {
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
                return START_NOT_STICKY
            }
        }
        kicks.value = kicks.value + 1
        return START_STICKY
    }

    override fun onDestroy() {
        renderJob?.cancel()
        engine?.stop()
        overlay?.remove()
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        manager.cancel(LockScreenNotifications.LEGACY_NOTIFICATION_ID)
        scope.cancel()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?) = null

    override fun onConfigurationChanged(newConfig: Configuration) {
        super.onConfigurationChanged(newConfig)
        overlay?.onDisplayChanged()
        kicks.value = kicks.value + 1
    }

    private suspend fun renderLoop() {
        val container = LuxApp.container(this)
        combine(
            container.settings.settings,
            container.symbols.symbols,
            container.quotes.quotes,
            kicks,
        ) { settings, symbols, quotes, _ ->
            Triple(settings, symbols, quotes)
        }.collect { (settings, symbols, quotes) ->
            val tape = StripFormatter.fromSettings(symbols, quotes, Instant.now(), settings)
            latestSettings = settings
            latestTape = tape
            engine?.setDisplayMode(settings.displayMode)
            noteNetwork()
            val granted = Settings.canDrawOverlays(this)
            latestGranted = granted
            overlay?.apply(settings, tape, granted, keyguardLocked())
            publishTape(settings, tape, granted, tapeShift)
            publishWidget(tape, symbols.size, settings)
            alerts.take(settings, quotes).forEach { hit ->
                PriceAlertNotifier.show(this, hit, settings.priceAlertsLoud)
                if (hit.alertId.isNotEmpty()) {
                    val at = System.currentTimeMillis()
                    scope.launch {
                        LuxApp.container(this@TickerService).settings.markAlertHit(hit.alertId, at)
                    }
                }
            }
        }
    }

    private fun publishTape(settings: TickerSettings, tape: TapeModel, granted: Boolean, shift: Int) {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        if (!legacyCleared) {
            manager.cancel(LockScreenNotifications.LEGACY_NOTIFICATION_ID)
            legacyCleared = true
        }
        val night = systemNight()
        val chrome = settings.appTheme.statusStrip(night)
        val showing = settings.overlayEnabled && granted
        val key = "${settings.overlayEnabled}|${settings.showOnLockScreen}|$granted|${settings.pinSide.name}|${chrome.name}|night=$night|" +
            LockTickerText.plainPinned(tape) + "|" + LockTickerText.crawledPlain(tape, shift)
        if (key == noteKey) return
        noteKey = key
        val toggle = PendingIntent.getService(
            this,
            2,
            Intent(this, TickerService::class.java).setAction(if (showing) ACTION_HIDE else ACTION_SHOW),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val notification = LockScreenNotifications.build(
            context = this,
            tape = tape,
            charOffset = shift,
            pinOnRight = settings.pinSide == com.lux.ticker.data.PinSide.RIGHT,
            theme = chrome,
            showOnLockScreen = settings.showOnLockScreen,
            toggleLabel = if (showing) "Hide tape" else "Show tape",
            toggleIntent = toggle,
            foreground = true,
        )
        if (!foregroundPosted) {
            if (Build.VERSION.SDK_INT >= 34) {
                startForeground(NOTIF_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE)
            } else {
                startForeground(NOTIF_ID, notification)
            }
            foregroundPosted = true
        } else {
            manager.notify(NOTIF_ID, notification)
        }
    }

    private fun systemNight(): Boolean {
        val night = resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK
        return night == Configuration.UI_MODE_NIGHT_YES
    }

    private fun noteNetwork() {
        val manager = getSystemService(ConnectivityManager::class.java)
        val caps = manager.activeNetwork?.let { manager.getNetworkCapabilities(it) }
        val online = caps?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) == true
        LuxApp.container(this).feed.networkUp.value = online
    }

    private fun publishWidget(tape: TapeModel, symbolCount: Int, settings: TickerSettings) {
        val now = System.currentTimeMillis()
        val theme = settings.stripTheme.name
        val pin = settings.pinSide.name
        val same = symbolCount == widgetSymbols &&
            settings.orangeDividers == widgetDividers &&
            theme == widgetTheme &&
            pin == widgetPin &&
            settings.textSizeSp == widgetText &&
            now - widgetAt < 15_000L
        if (same) return
        widgetAt = now
        widgetSymbols = symbolCount
        widgetDividers = settings.orangeDividers
        widgetTheme = theme
        widgetPin = pin
        widgetText = settings.textSizeSp
        WidgetRenderer.updateAll(
            this,
            tape,
            settings.orangeDividers,
            settings.stripTheme,
            settings.textSizeSp,
            settings.pinSide == com.lux.ticker.data.PinSide.RIGHT,
        )
    }

    private fun startAsForeground(settings: TickerSettings, granted: Boolean) {
        publishTape(settings, TapeModel.EMPTY, granted, tapeShift)
    }

    private fun createChannel() {
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        val lock = NotificationChannel(
            LockScreenNotifications.CHANNEL_ID,
            getString(R.string.channel_lock_name),
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = getString(R.string.channel_lock_description)
            setSound(null, null)
            enableVibration(false)
            setShowBadge(false)
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
        }
        manager.createNotificationChannel(lock)
        runCatching { manager.deleteNotificationChannel(OLD_CHANNEL) }
        LuxWireNotifier.ensureChannel(this)
        PriceAlertNotifier.ensureChannels(this)
    }

    companion object {
        const val ACTION_SHOW = "com.lux.ticker.SHOW"
        const val ACTION_HIDE = "com.lux.ticker.HIDE"
        const val ACTION_REFRESH = "com.lux.ticker.REFRESH"
        const val ACTION_STOP = "com.lux.ticker.STOP"
        const val ACTION_LUX_WIRE = "com.lux.ticker.LUX_WIRE"
        private const val OLD_CHANNEL = "lux_ticker"
        private const val NOTIF_ID = 1001
    }
}
