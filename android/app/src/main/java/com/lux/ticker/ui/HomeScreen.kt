package com.lux.ticker.ui

import android.Manifest
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.AndroidViewModel
import android.app.Application
import androidx.lifecycle.viewModelScope
import com.lux.ticker.LuxApp
import com.lux.ticker.data.FeedHonesty
import com.lux.ticker.data.StripFormatter
import com.lux.ticker.engine.TickerService
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import java.time.Instant

class HomeViewModel(app: Application) : AndroidViewModel(app) {
    private val container = LuxApp.container(app)
    val settings = container.settings.settings.stateIn(
        viewModelScope,
        SharingStarted.WhileSubscribed(5_000),
        com.lux.ticker.data.TickerSettings(),
    )
    val symbols = container.symbols.symbols.stateIn(
        viewModelScope,
        SharingStarted.WhileSubscribed(5_000),
        com.lux.ticker.data.DefaultSymbols.all,
    )
    val quotes = container.quotes.quotes
    val networkUp = container.feed.networkUp
    val stockFailed = container.feed.stockFailed

    fun setOverlay(enabled: Boolean) {
        viewModelScope.launch { container.settings.setOverlay(enabled) }
    }

    fun refresh(context: android.content.Context) {
        androidx.core.content.ContextCompat.startForegroundService(
            context,
            android.content.Intent(context, TickerService::class.java).setAction(TickerService.ACTION_REFRESH),
        )
    }
}

@Composable
fun HomeRoute(onEdit: () -> Unit, onSettings: () -> Unit, onChart: () -> Unit) {
    val context = LocalContext.current
    val viewModel: HomeViewModel = viewModel()
    var overlayGranted by remember { mutableStateOf(Settings.canDrawOverlays(context)) }
    var notificationsGranted by remember { mutableStateOf(notificationsAllowed(context)) }
    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                overlayGranted = Settings.canDrawOverlays(context)
                notificationsGranted = notificationsAllowed(context)
                ContextCompat.startForegroundService(
                    context,
                    Intent(context, TickerService::class.java).setAction(TickerService.ACTION_REFRESH),
                )
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }
    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {
        notificationsGranted = notificationsAllowed(context)
    }
    HomeScreen(
        viewModel = viewModel,
        overlayGranted = overlayGranted,
        notificationsGranted = notificationsGranted,
        onRequestOverlay = { openOverlaySettings(context) },
        onRequestNotifications = {
            if (Build.VERSION.SDK_INT >= 33) {
                permissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        },
        onEdit = onEdit,
        onSettings = onSettings,
        onChart = onChart,
    )
}

@Composable
internal fun HomeScreen(
    viewModel: HomeViewModel,
    overlayGranted: Boolean,
    notificationsGranted: Boolean,
    onRequestOverlay: () -> Unit,
    onRequestNotifications: () -> Unit,
    onEdit: () -> Unit,
    onSettings: () -> Unit,
    onChart: () -> Unit,
) {
    val context = LocalContext.current
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val symbols by viewModel.symbols.collectAsStateWithLifecycle()
    val quotes by viewModel.quotes.collectAsStateWithLifecycle()
    val networkUp by viewModel.networkUp.collectAsStateWithLifecycle()
    val stockFailed by viewModel.stockFailed.collectAsStateWithLifecycle()
    val systemDark = isSystemInDarkTheme()
    val now = System.currentTimeMillis()
    val tape = remember(
        symbols,
        quotes,
        settings.pinBtc,
        settings.showSessionTags,
        settings.symbolColor,
        settings.priceColor,
    ) {
        StripFormatter.fromSettings(symbols, quotes, Instant.now(), settings)
    }
    var strip by remember { mutableStateOf<TickerStripView?>(null) }
    val stocksOnline = networkUp && !stockFailed
    Column(
        modifier = Modifier
            .fillMaxSize()
            .windowInsetsPadding(WindowInsets.safeDrawing)
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text("LuxWire Ticker", style = MaterialTheme.typography.headlineMedium)
        Text(
            "Prices scroll in a short row under the status bar. Move it in Settings, Display, Vertical position. The wallpaper shows through. Bitcoin stays put when it fits. Taps pass through.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Button(onClick = onChart, modifier = Modifier.fillMaxWidth().height(48.dp)) {
            Text("Bitcoin chart")
        }
        if (!overlayGranted) {
            Card(
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.primary),
            ) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Display over other apps", style = MaterialTheme.typography.titleMedium)
                    Text(
                        ReleaseNotes.WHY_OVERLAY + " Android opens a system page next. Turn LuxWire Ticker on there, then come back.",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    Button(onClick = onRequestOverlay, modifier = Modifier.fillMaxWidth()) {
                        Text("Allow display over other apps")
                    }
                }
            }
        }
        if (!notificationsGranted) {
            Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Notifications", style = MaterialTheme.typography.titleMedium)
                    Text(
                        ReleaseNotes.WHY_NOTIFICATIONS + " The status-bar control stays quiet.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    OutlinedButton(onClick = onRequestNotifications, modifier = Modifier.fillMaxWidth()) {
                        Text("Allow notifications")
                    }
                }
            }
        }
        Text("Preview", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.primary)
        AndroidView(
            factory = { TickerStripView(it).also { created -> strip = created } },
            update = { view ->
                strip = view
                view.setStatusBandDp(StatusBandLayout.SIDE_INSET_DP, StatusBandLayout.SIDE_INSET_DP)
                view.setOrangeDividers(settings.orangeDividers)
                view.setTextSizeSp(settings.textSizeSp)
                view.setStripTheme(settings.appTheme.statusStrip(systemDark))
                view.setScrollRightToLeft(settings.scrollRightToLeft)
                view.setPinOnRight(settings.pinSide == com.lux.ticker.data.PinSide.RIGHT)
                view.setTape(StripFormatter.fromSettings(symbols, quotes, Instant.now(), settings))
                view.setSpeedDpPerSec(settings.speedDpPerSec)
            },
            modifier = Modifier
                .fillMaxWidth()
                .height(32.dp),
        )
        Text(
            "On the phone this strip sits under the status bar. Display sets that gap. Until you set one, it sits directly under the status bar. The background is transparent. This preview follows the app theme. The lock screen stays paper.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            FeedHonesty.btc(FeedHonesty.btcAgeMillis(quotes, now), networkUp),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            FeedHonesty.stocks(FeedHonesty.stockAgeMillis(quotes, now), stocksOnline),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        if (!networkUp || stockFailed) {
            Text(ReleaseNotes.REFRESH_ERROR, color = MaterialTheme.colorScheme.error)
        }
        Row(verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text("Overlay")
                Text(
                    when {
                        !overlayGranted -> "Waiting for permission"
                        settings.overlayEnabled -> "Showing over other apps"
                        else -> "Hidden"
                    },
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Switch(
                checked = settings.overlayEnabled,
                onCheckedChange = { want ->
                    if (want && !overlayGranted) onRequestOverlay()
                    viewModel.setOverlay(want)
                },
            )
        }
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            Button(onClick = onEdit, modifier = Modifier.weight(1f)) { Text("Tickers") }
            OutlinedButton(onClick = onSettings, modifier = Modifier.weight(1f)) { Text("Settings") }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedButton(
                onClick = { viewModel.refresh(context) },
                modifier = Modifier.weight(1f),
            ) { Text("Refresh") }
            OutlinedButton(
                onClick = { strip?.let { StripShare.share(context, it) } },
                modifier = Modifier.weight(1f),
            ) { Text("Share") }
        }
        Spacer(Modifier.height(8.dp))
        Text(
            if (tape.pinned.stale || tape.scrolling.any { it.stale }) {
                "A dim price with ° is cached. The last price stays until the next update."
            } else {
                "A failed fetch keeps the last price. Cached prices dim."
            },
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

private fun notificationsAllowed(context: Context): Boolean {
    if (Build.VERSION.SDK_INT < 33) return true
    return ContextCompat.checkSelfPermission(
        context,
        Manifest.permission.POST_NOTIFICATIONS,
    ) == PackageManager.PERMISSION_GRANTED
}

private fun openOverlaySettings(context: Context) {
    val primary = Intent(
        Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
        Uri.parse("package:${context.packageName}"),
    )
    try {
        context.startActivity(primary)
    } catch (e: ActivityNotFoundException) {
        context.startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION))
    }
}
