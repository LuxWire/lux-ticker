package com.lux.ticker.ui

import android.app.Application
import android.content.Intent
import android.net.Uri
import android.os.PowerManager
import android.provider.Settings
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Slider
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lux.ticker.LuxApp
import com.lux.ticker.account.AccountBrowser
import com.lux.ticker.R
import com.lux.ticker.data.AlertKind
import com.lux.ticker.data.AppTheme
import com.lux.ticker.data.DisplayMode
import com.lux.ticker.data.PinSide
import com.lux.ticker.data.PriceAlerts
import com.lux.ticker.data.PriceFormat
import com.lux.ticker.data.SavedAlert
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeColorChoices
import com.lux.ticker.data.TapeColors
import com.lux.ticker.data.SymbolList
import com.lux.ticker.data.SymbolRules
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.data.VerticalOffset
import com.lux.ticker.engine.TickerService
import com.lux.ticker.luxwire.LuxWireAuth
import com.lux.ticker.luxwire.LuxWirePosts
import androidx.core.content.ContextCompat
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

class SettingsViewModel(app: Application) : AndroidViewModel(app) {
    private val store = LuxApp.container(app).settings
    val settings = store.settings.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), TickerSettings())

    fun setSpeed(dpPerSec: Float) = launch { store.setSpeed(dpPerSec) }
    fun setLeftPad(dp: Float) = launch { store.setLeftPad(dp) }
    fun setRightPad(dp: Float) = launch { store.setRightPad(dp) }
    fun setStartOnBoot(enabled: Boolean) = launch { store.setStartOnBoot(enabled) }
    fun setShowHandle(enabled: Boolean) = launch { store.setShowHandle(enabled) }
    fun setShowOnLockScreen(enabled: Boolean) = launch { store.setShowOnLockScreen(enabled) }
    fun setOrangeDividers(enabled: Boolean) = launch { store.setOrangeDividers(enabled) }
    fun setLuxWirePosts(enabled: Boolean) = launch { store.setLuxWirePosts(enabled) }
    fun setLuxWireDigest(enabled: Boolean) = launch { store.setLuxWireDigest(enabled) }
    fun setLuxWireSubscriber(enabled: Boolean) = launch { store.setLuxWireSubscriber(enabled) }
    fun setLuxWirePollMinutes(minutes: Int) = launch { store.setLuxWirePollMinutes(minutes) }
    fun setPinBtc(enabled: Boolean) = launch { store.setPinBtc(enabled) }
    fun setTextSize(sp: Float) = launch { store.setTextSize(sp) }
    fun setVerticalOffset(dp: Float) = launch { store.setVerticalOffset(dp) }
    fun setShowSessionTags(enabled: Boolean) = launch { store.setShowSessionTags(enabled) }
    fun setSymbolColor(color: Int) = launch { store.setSymbolColor(color) }
    fun setPriceColor(color: Int) = launch { store.setPriceColor(color) }
    fun setChartHaptics(enabled: Boolean) = launch { store.setChartHaptics(enabled) }
    fun resetAll() = launch { store.resetAll() }
    fun setStripTheme(theme: StripTheme) = launch { store.setStripTheme(theme) }
    fun setAppTheme(theme: AppTheme) = launch { store.setAppTheme(theme) }
    fun setDisplayMode(mode: DisplayMode) = launch { store.setDisplayMode(mode) }
    fun setPriceAlerts(enabled: Boolean) = launch { store.setPriceAlerts(enabled) }
    fun setPriceAlertsLoud(enabled: Boolean) = launch { store.setPriceAlertsLoud(enabled) }
    fun setBtcAlertPct(pct: Float) = launch { store.setBtcAlertPct(pct) }
    fun setPriceAlertSymbol(symbol: String) = launch { store.setPriceAlertSymbol(symbol) }
    fun setPriceAlertTarget(price: Float) = launch { store.setPriceAlertTarget(price) }
    fun setScrollRightToLeft(enabled: Boolean) = launch { store.setScrollRightToLeft(enabled) }
    fun setPinSide(side: PinSide) = launch { store.setPinSide(side) }
    fun addSavedAlert(symbol: String, target: Double, kind: AlertKind) = launch {
        store.addAlert(
            SavedAlert(
                id = "a${System.nanoTime().toString(36)}",
                kind = kind,
                symbol = symbol,
                threshold = target,
            ),
        )
    }
    fun removeAlert(id: String) = launch { store.removeAlert(id) }

    fun checkLuxWireNow() {
        val app = getApplication<Application>()
        ContextCompat.startForegroundService(
            app,
            Intent(app, TickerService::class.java).setAction(TickerService.ACTION_LUX_WIRE),
        )
    }

    private fun launch(block: suspend () -> Unit) {
        viewModelScope.launch { block() }
    }
}

@Composable
fun SettingsMenuRoute(onBack: () -> Unit, onOpen: (String) -> Unit) {
    val viewModel: SettingsViewModel = viewModel()
    var confirmReset by remember { mutableStateOf(false) }
    if (confirmReset) {
        AlertDialog(
            onDismissRequest = { confirmReset = false },
            title = { Text("Reset everything?") },
            text = {
                Text(
                    "Every setting goes back to the 1.4.8 defaults. Speed is 50 dp/s. Vertical position sits directly under the status bar. Pre and AH stay on. Chart haptics stay on. Saved price alerts are cleared. Your tickers and X sign-in stay.",
                )
            },
            confirmButton = {
                TextButton(onClick = {
                    confirmReset = false
                    viewModel.resetAll()
                }) { Text("Reset everything") }
            },
            dismissButton = { TextButton(onClick = { confirmReset = false }) { Text("Cancel") } },
        )
    }
    SettingsPage(title = "Settings", onBack = onBack) {
        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(bottom = 12.dp)) {
            Image(
                painter = painterResource(R.drawable.ic_lux_wire),
                contentDescription = "LuxWire",
                modifier = Modifier.size(48.dp),
            )
            Column(Modifier.padding(start = 12.dp)) {
                Text("LuxWire Ticker", style = MaterialTheme.typography.titleLarge)
                Text(ReleaseNotes.VERSION, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
        SettingsMenu.entries.forEach { item ->
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { onOpen(item.route) }
                    .padding(vertical = 14.dp),
            ) {
                Text(item.title, style = MaterialTheme.typography.titleMedium)
                Text(
                    item.detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            HorizontalDivider(color = MaterialTheme.colorScheme.outline)
        }
        OutlinedButton(
            onClick = { confirmReset = true },
            modifier = Modifier.fillMaxWidth().padding(top = 16.dp),
        ) { Text("Reset everything") }
    }
}

@Composable
fun SettingsTickersRoute(onBack: () -> Unit, onChart: (String) -> Unit) {
    val viewModel: SettingsViewModel = viewModel()
    val editor: EditorViewModel = viewModel()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val symbols by editor.symbols.collectAsStateWithLifecycle()
    val quotes by editor.quotes.collectAsStateWithLifecycle()
    val message by editor.message.collectAsStateWithLifecycle()
    val busy by editor.busy.collectAsStateWithLifecycle()
    var adding by remember { mutableStateOf(false) }
    var draft by remember { mutableStateOf("") }
    if (adding) {
        AlertDialog(
            onDismissRequest = { adding = false },
            title = { Text("Add ticker") },
            text = {
                Column {
                    LuxOutlinedField(
                        value = draft,
                        onValueChange = { draft = it.uppercase() },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                        label = { Text("Symbol") },
                    )
                    if (message != null) {
                        Text(
                            message!!,
                            color = MaterialTheme.colorScheme.error,
                            modifier = Modifier.padding(top = 8.dp),
                        )
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = { editor.add(draft) },
                    enabled = draft.isNotBlank() && !busy,
                ) { Text(if (busy) "…" else "Add ticker") }
            },
            dismissButton = {
                TextButton(onClick = { adding = false }) { Text("Close") }
            },
        )
    }
    SettingsPage(title = "Tickers", onBack = onBack) {
        Text(
            "The list is the tape. Add ticker only adds a symbol. Bitcoin stays on the list.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        SettingSwitch(
            title = "Pin Bitcoin",
            body = "On, Bitcoin stays put. Off, it scrolls with the other names.",
            checked = settings.pinBtc,
            onCheckedChange = viewModel::setPinBtc,
            onReset = { viewModel.setPinBtc(true) },
        )
        ResetRow("Bitcoin side") { viewModel.setPinSide(PinSide.LEFT) }
        Text(
            "Left or right of the status-bar strip. The other names scroll on the opposite side.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        ModeButton("Left", settings.pinSide == PinSide.LEFT) { viewModel.setPinSide(PinSide.LEFT) }
        ModeButton("Right", settings.pinSide == PinSide.RIGHT) { viewModel.setPinSide(PinSide.RIGHT) }
        ResetRow("Scroll") { viewModel.setScrollRightToLeft(true) }
        ModeButton("Right to left", settings.scrollRightToLeft) { viewModel.setScrollRightToLeft(true) }
        ModeButton("Left to right", !settings.scrollRightToLeft) { viewModel.setScrollRightToLeft(false) }
        OutlinedButton(
            onClick = { adding = true },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text("Add ticker") }
        symbols.forEach { spec ->
            val quote = quotes[spec.symbol]
            val pinned = spec.symbol == SymbolList.PINNED
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f).clickable { onChart(spec.symbol) }) {
                    Text(spec.label, fontFamily = TapeFont, style = MaterialTheme.typography.titleMedium)
                    Text(
                        if (quote == null) "—" else PriceFormat.formatPrice(quote.price, quote.currency),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                if (!pinned) {
                    TextButton(onClick = { editor.moveBy(spec.symbol, -1) }) { Text("Up") }
                    TextButton(onClick = { editor.moveBy(spec.symbol, 1) }) { Text("Down") }
                    TextButton(onClick = { editor.remove(spec.symbol) }) { Text("Remove ticker") }
                }
            }
            HorizontalDivider(color = MaterialTheme.colorScheme.outline)
        }
    }
}

@Composable
fun SettingsDisplayRoute(onBack: () -> Unit) {
    val viewModel: SettingsViewModel = viewModel()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    var textSize by remember(settings.textSizeSp) { mutableFloatStateOf(settings.textSizeSp) }
    var speed by remember(settings.speedDpPerSec) { mutableFloatStateOf(settings.speedDpPerSec) }
    var offset by remember { mutableFloatStateOf(settings.verticalOffsetDp) }
    var offsetText by remember { mutableStateOf(VerticalOffset.format(settings.verticalOffsetDp)) }
    var draggingOffset by remember { mutableStateOf(false) }
    var editingOffset by remember { mutableStateOf(false) }
    LaunchedEffect(settings.verticalOffsetDp) {
        if (!draggingOffset && !editingOffset) {
            offset = settings.verticalOffsetDp
            offsetText = VerticalOffset.format(settings.verticalOffsetDp)
        }
    }
    val defaults = TickerSettings()
    SettingsPage(title = "Display", onBack = onBack) {
        ResetRow("App") { viewModel.setAppTheme(defaults.appTheme) }
        Text(
            "System follows the phone. On a light phone that is paper. Light is a cooler white. Paper is the LuxWire card. Dark is the night surface.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp, bottom = 8.dp),
        )
        ModeButton("System", settings.appTheme == AppTheme.SYSTEM) { viewModel.setAppTheme(AppTheme.SYSTEM) }
        ModeButton("Dark", settings.appTheme == AppTheme.DARK) { viewModel.setAppTheme(AppTheme.DARK) }
        ModeButton("Light", settings.appTheme == AppTheme.LIGHT) { viewModel.setAppTheme(AppTheme.LIGHT) }
        ModeButton("Paper", settings.appTheme == AppTheme.PAPER) { viewModel.setAppTheme(AppTheme.PAPER) }
        Spacer(Modifier.height(16.dp))
        ResetRow("Text size  ${textSize.roundToInt()} sp") {
            textSize = defaults.textSizeSp
            viewModel.setTextSize(defaults.textSizeSp)
        }
        Slider(
            value = textSize,
            onValueChange = { textSize = it },
            onValueChangeFinished = { viewModel.setTextSize(textSize) },
            valueRange = 11f..18f,
        )
        ResetRow("Speed  ${speed.roundToInt()} dp/s") {
            speed = defaults.speedDpPerSec
            viewModel.setSpeed(defaults.speedDpPerSec)
        }
        Slider(
            value = speed,
            onValueChange = { speed = it },
            onValueChangeFinished = { viewModel.setSpeed(speed) },
            valueRange = 12f..160f,
        )
        ResetRow("Vertical position  ${VerticalOffset.format(offset)} dp") {
            draggingOffset = false
            editingOffset = false
            offset = defaults.verticalOffsetDp
            offsetText = VerticalOffset.format(defaults.verticalOffsetDp)
            viewModel.setVerticalOffset(defaults.verticalOffsetDp)
        }
        Text(
            "From the bottom of the status bar. 0 sits directly under it. Negative moves the strip up onto the status bar. Positive moves it down. Type a number or drag the slider.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp, bottom = 8.dp),
        )
        Row(verticalAlignment = Alignment.CenterVertically) {
            LuxOutlinedField(
                value = offsetText,
                onValueChange = { raw ->
                    val cleaned = raw.filter { it.isDigit() || it == '.' || it == '-' }
                    editingOffset = true
                    val inProgress = cleaned.isEmpty() || cleaned == "-" || cleaned.endsWith('.')
                    val parsed = if (inProgress) null else VerticalOffset.parse(cleaned)
                    if (parsed == null) {
                        offsetText = cleaned
                    } else {
                        offset = parsed
                        offsetText = if (cleaned.toFloatOrNull() == parsed) cleaned else VerticalOffset.format(parsed)
                        viewModel.setVerticalOffset(parsed)
                    }
                },
                label = { Text("dp") },
                modifier = Modifier
                    .width(120.dp)
                    .onFocusChanged { state ->
                        if (!state.isFocused && editingOffset) {
                            val parsed = VerticalOffset.parse(offsetText)
                            editingOffset = false
                            if (parsed == null) {
                                offsetText = VerticalOffset.format(offset)
                            } else {
                                offset = parsed
                                offsetText = VerticalOffset.format(parsed)
                                viewModel.setVerticalOffset(parsed)
                            }
                        }
                    },
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Decimal,
                    imeAction = ImeAction.Done,
                ),
                keyboardActions = KeyboardActions(onDone = {
                    val parsed = VerticalOffset.parse(offsetText)
                    editingOffset = false
                    if (parsed == null) {
                        offsetText = VerticalOffset.format(offset)
                    } else {
                        offset = parsed
                        offsetText = VerticalOffset.format(parsed)
                        viewModel.setVerticalOffset(parsed)
                    }
                }),
            )
            Slider(
                value = offset.coerceIn(VerticalOffset.MIN_DP, VerticalOffset.MAX_DP),
                onValueChange = {
                    draggingOffset = true
                    editingOffset = false
                    offset = VerticalOffset.coerce(it)
                    offsetText = VerticalOffset.format(offset)
                    viewModel.setVerticalOffset(offset)
                },
                onValueChangeFinished = {
                    viewModel.setVerticalOffset(offset)
                    draggingOffset = false
                },
                valueRange = VerticalOffset.MIN_DP..VerticalOffset.MAX_DP,
                modifier = Modifier.weight(1f).padding(start = 8.dp),
            )
        }
        ResetRow("Symbol color") { viewModel.setSymbolColor(defaults.symbolColor) }
        TapeColorChoices.swatches.forEach { (color, label) ->
            ModeButton(label, settings.symbolColor == color) { viewModel.setSymbolColor(color) }
        }
        ResetRow("Price color") { viewModel.setPriceColor(defaults.priceColor) }
        TapeColorChoices.swatches.forEach { (color, label) ->
            ModeButton(label, settings.priceColor == color) { viewModel.setPriceColor(color) }
        }
        SettingSwitch(
            title = "Pre and AH",
            body = "On. US names show Pre from 4:00 to 9:30 a.m. New York, and AH after the close. Off hides both labels. Bitcoin and non-US names stay untagged.",
            checked = settings.showSessionTags,
            onCheckedChange = viewModel::setShowSessionTags,
            onReset = { viewModel.setShowSessionTags(defaults.showSessionTags) },
        )
        SettingSwitch(
            title = "Chart haptics",
            body = "On. Dragging a chart ticks at each point and each candle, even when the phone's touch feedback is off.",
            checked = settings.chartHaptics,
            onCheckedChange = viewModel::setChartHaptics,
            onReset = { viewModel.setChartHaptics(defaults.chartHaptics) },
        )
        ResetRow("Strip") { viewModel.setStripTheme(defaults.stripTheme) }
        Text(
            "The home strip stays transparent so the wallpaper shows through. Vertical position moves that strip. The lock screen follows the app theme. This choice colors the home-screen widgets.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 4.dp, bottom = 8.dp),
        )
        ModeButton("Dark", settings.stripTheme == StripTheme.DARK) { viewModel.setStripTheme(StripTheme.DARK) }
        ModeButton("Light", settings.stripTheme == StripTheme.LIGHT) { viewModel.setStripTheme(StripTheme.LIGHT) }
        ModeButton("Paper", settings.stripTheme == StripTheme.PAPER) { viewModel.setStripTheme(StripTheme.PAPER) }
        SettingSwitch(
            title = "Orange dividers",
            body = "Off unless you turn it on. A thin orange line after Bitcoin. Off, that gap is a dot.",
            checked = settings.orangeDividers,
            onCheckedChange = viewModel::setOrangeDividers,
            onReset = { viewModel.setOrangeDividers(defaults.orangeDividers) },
        )
    }
}

@Composable
fun SettingsAlertsRoute(onBack: () -> Unit) {
    val context = LocalContext.current
    val viewModel: SettingsViewModel = viewModel()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val auth = remember { LuxWireAuth(context) }
    var bearer by remember { mutableStateOf(auth.bearer()) }
    var clientId by remember { mutableStateOf(auth.clientId()) }
    var signedIn by remember { mutableStateOf(auth.hasUserToken()) }
    var authNote by remember { mutableStateOf<String?>(null) }
    var btcBand by remember(settings.btcAlertPct) { mutableFloatStateOf(settings.btcAlertPct) }
    var alertSymbol by remember(settings.priceAlertSymbol) { mutableStateOf(settings.priceAlertSymbol) }
    var alertTarget by remember(settings.priceAlertTarget) {
        mutableStateOf(if (settings.priceAlertTarget <= 0f) "" else settings.priceAlertTarget.toString())
    }
    SettingsPage(title = "Alerts", onBack = onBack) {
        Text(
            ReleaseNotes.WHY_NOTIFICATIONS,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        SettingSwitch(
            title = "@LuxWire posts",
            body = "On unless you turn it off. New @LuxWire posts are read from X. A tap opens that post. There is no file handoff and no charge.",
            checked = settings.luxWirePosts,
            onCheckedChange = viewModel::setLuxWirePosts,
            onReset = { viewModel.setLuxWirePosts(true) },
        )
        SettingSwitch(
            title = "Quiet digest",
            body = "Off unless you turn it on. Groups posts into one shade item about every 15 minutes: \"N new from LuxWire\".",
            checked = settings.luxWireDigest,
            onCheckedChange = viewModel::setLuxWireDigest,
            onReset = { viewModel.setLuxWireDigest(false) },
        )
        SettingSwitch(
            title = "LuxWire subscriber",
            body = "On, a subscriber post opens that post. Off, the tap opens the subscribe page instead of a dead end.",
            checked = settings.luxWireSubscriber,
            onCheckedChange = viewModel::setLuxWireSubscriber,
            onReset = { viewModel.setLuxWireSubscriber(true) },
        )
        ResetRow("Poll") { viewModel.setLuxWirePollMinutes(3) }
        ModeButton("Every 2 minutes", settings.luxWirePollMinutes == 2) { viewModel.setLuxWirePollMinutes(2) }
        ModeButton("Every 3 minutes", settings.luxWirePollMinutes == 3) { viewModel.setLuxWirePollMinutes(3) }
        ModeButton("Every 5 minutes", settings.luxWirePollMinutes == 5) { viewModel.setLuxWirePollMinutes(5) }
        Text(
            "Public posts need an X bearer token. Sign in to X with a client id so subscriber posts your account can see are included. The first check records the current posts and does not alert.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 8.dp),
        )
        LuxOutlinedField(
            value = bearer,
            onValueChange = { bearer = it },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
            label = { Text("X bearer token") },
            visualTransformation = PasswordVisualTransformation(),
        )
        OutlinedButton(
            onClick = {
                auth.saveBearer(bearer)
                authNote = if (auth.hasBearer()) "Bearer saved." else "Bearer cleared."
            },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text("Save bearer") }
        LuxOutlinedField(
            value = clientId,
            onValueChange = { clientId = it },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
            label = { Text("X client id") },
        )
        OutlinedButton(
            onClick = { auth.saveClientId(clientId) },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text("Save client id") }
        OutlinedButton(
            onClick = {
                auth.saveClientId(clientId)
                val url = auth.beginSignIn()
                if (url == null) {
                    authNote = "Add an X client id first."
                } else {
                    context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
                }
            },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text(if (signedIn) "Sign in to X again" else "Sign in to X") }
        if (signedIn) {
            OutlinedButton(
                onClick = {
                    auth.signOut()
                    signedIn = false
                    authNote = "Signed out of X. Public posts still use the bearer."
                },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            ) { Text("Sign out of X") }
        }
        OutlinedButton(
            onClick = viewModel::checkLuxWireNow,
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text("Check @LuxWire now") }
        if (authNote != null) {
            Text(
                authNote!!,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp),
            )
        }
        Spacer(Modifier.height(16.dp))
        Text("Price alerts", style = MaterialTheme.typography.titleMedium)
        SettingSwitch(
            title = "Price alerts",
            body = "Off unless you turn it on. The BTC band fires from the slider until you save a BTC alert. Saved alerts are the list below.",
            checked = settings.priceAlerts,
            onCheckedChange = viewModel::setPriceAlerts,
            onReset = { viewModel.setPriceAlerts(false) },
        )
        SettingSwitch(
            title = "Loud alerts",
            body = "Off keeps the quiet channel. On uses a channel that can make a sound.",
            checked = settings.priceAlertsLoud,
            onCheckedChange = viewModel::setPriceAlertsLoud,
            onReset = { viewModel.setPriceAlertsLoud(false) },
        )
        ResetRow("BTC ± ${btcBand.roundToInt()}%") {
            btcBand = 3f
            viewModel.setBtcAlertPct(3f)
        }
        Slider(
            value = btcBand,
            onValueChange = { btcBand = it },
            onValueChangeFinished = { viewModel.setBtcAlertPct(btcBand) },
            valueRange = 1f..15f,
        )
        OutlinedButton(
            onClick = { viewModel.addSavedAlert("BTC", btcBand.toDouble(), AlertKind.BTC_PCT) },
            modifier = Modifier.fillMaxWidth().padding(top = 4.dp),
        ) { Text("Add BTC alert") }
        ResetRow("Add a new alert") {
            alertSymbol = ""
            alertTarget = ""
            viewModel.setPriceAlertSymbol("")
            viewModel.setPriceAlertTarget(0f)
        }
        LuxOutlinedField(
            value = alertSymbol,
            onValueChange = { alertSymbol = it.uppercase() },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            label = { Text("Symbol") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Ascii),
        )
        LuxOutlinedField(
            value = alertTarget,
            onValueChange = { alertTarget = it },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
            label = { Text("Target price") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
        )
        OutlinedButton(
            onClick = {
                val name = SymbolRules.normalize(alertSymbol)
                val price = alertTarget.toDoubleOrNull() ?: 0.0
                if (SymbolRules.isValid(name) && price > 0.0) {
                    viewModel.addSavedAlert(name, price, AlertKind.PRICE)
                    alertSymbol = ""
                    alertTarget = ""
                } else {
                    alertSymbol = name
                }
            },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text("Add alert") }
        if (alertSymbol.isNotBlank() && !SymbolRules.isValid(SymbolRules.normalize(alertSymbol))) {
            Text(
                ReleaseNotes.INVALID_TICKER,
                color = MaterialTheme.colorScheme.error,
                modifier = Modifier.padding(top = 8.dp),
            )
        }
        Text("Saved alerts", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 16.dp))
        if (settings.savedAlerts.isEmpty()) {
            Text(
                "No saved alerts yet.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp),
            )
        }
        settings.savedAlerts.forEach { alert ->
            SavedAlertRow(alert, onRemove = { viewModel.removeAlert(alert.id) })
        }
    }
}

@Composable
fun SettingsBatteryRoute(onBack: () -> Unit) {
    val context = LocalContext.current
    val viewModel: SettingsViewModel = viewModel()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val power = context.getSystemService(PowerManager::class.java)
    val batteryFree = power.isIgnoringBatteryOptimizations(context.packageName)
    SettingsPage(title = "Battery", onBack = onBack) {
        Text(
            "Always overlay keeps the status-bar strip while the screen is on. Lock screen only hides that strip until the phone is locked. Manual refresh stops the steady poll and keeps the last prices until you tap Refresh.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(8.dp))
        ModeButton("Always overlay", settings.displayMode == DisplayMode.ALWAYS) {
            viewModel.setDisplayMode(DisplayMode.ALWAYS)
        }
        ModeButton("Lock screen only", settings.displayMode == DisplayMode.LOCK_ONLY) {
            viewModel.setDisplayMode(DisplayMode.LOCK_ONLY)
        }
        ModeButton("Manual refresh", settings.displayMode == DisplayMode.MANUAL) {
            viewModel.setDisplayMode(DisplayMode.MANUAL)
        }
        ResetRow("Overlay mode") { viewModel.setDisplayMode(DisplayMode.ALWAYS) }
        Text(
            ReleaseNotes.WHY_OVERLAY,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 8.dp),
        )
        Text(
            "Add the LuxWire Ticker tile in Quick Settings to show or hide the strip.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 8.dp),
        )
        SettingSwitch(
            title = "Start when the phone boots",
            body = "The tape comes back after a restart. Turn this off if you only want it while the app is open.",
            checked = settings.startOnBoot,
            onCheckedChange = viewModel::setStartOnBoot,
            onReset = { viewModel.setStartOnBoot(true) },
        )
        SettingSwitch(
            title = "Show on lock screen",
            body = ReleaseNotes.WHY_LOCK + " Turn this off to hide the tape on the lock screen. The shade still shows it, because Android keeps one notification for the ticker.",
            checked = settings.showOnLockScreen,
            onCheckedChange = viewModel::setShowOnLockScreen,
            onReset = { viewModel.setShowOnLockScreen(true) },
        )
        SettingSwitch(
            title = "Edge handle",
            body = "Off unless you turn it on. A small handle on the right of the strip, under the status bar, opens LuxWire Ticker. With this off, the strip takes no taps at all.",
            checked = settings.showHandle,
            onCheckedChange = viewModel::setShowHandle,
            onReset = { viewModel.setShowHandle(false) },
        )
        Text(
            if (batteryFree) {
                "Battery optimization is already off for LuxWire Ticker. The strip comes back after a restart when start on boot is on."
            } else {
                "If the strip stops after the screen has been off, open the system battery list and set LuxWire Ticker to Unrestricted. The app does not ask Android for a direct exemption."
            },
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 8.dp),
        )
        OutlinedButton(
            onClick = {
                context.startActivity(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))
            },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
        ) { Text("Open battery settings") }
    }
}

@Composable
fun SettingsWhatsNewRoute(onBack: () -> Unit) {
    SettingsPage(title = "What's new", onBack = onBack) {
        Text(ReleaseNotes.DATED, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        ReleaseNotes.WHATS_NEW.split("\n\n").forEach { block ->
            val lines = block.split("\n").filter { it.isNotBlank() }
            if (lines.isEmpty()) return@forEach
            Text(lines.first(), style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 16.dp))
            lines.drop(1).forEach { line ->
                Text(
                    "• $line",
                    style = MaterialTheme.typography.bodyMedium,
                    modifier = Modifier.padding(top = 4.dp),
                )
            }
        }
    }
}

@Composable
fun SettingsAboutRoute(onBack: () -> Unit) {
    val context = LocalContext.current
    SettingsPage(title = "Contact / About", onBack = onBack) {
        Text("Contact", style = MaterialTheme.typography.titleMedium)
        TextButton(onClick = { context.startActivity(LuxWirePosts.viewIntent(LuxWirePosts.PROFILE)) }) {
            Text(ReleaseNotes.CONTACT)
        }
        Text("About", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 8.dp))
        Text(ReleaseNotes.ABOUT, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(top = 8.dp))
        TextButton(onClick = { AccountBrowser.open(context, ReleaseNotes.PRIVACY_URL) }) {
            Text("Privacy")
        }
        TextButton(onClick = { AccountBrowser.open(context, ReleaseNotes.TERMS_URL) }) {
            Text("Terms")
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun SettingsPage(title: String, onBack: () -> Unit, content: @Composable () -> Unit) {
    Scaffold(
        modifier = Modifier.windowInsetsPadding(WindowInsets.safeDrawing),
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = { Text(title) },
                navigationIcon = { TextButton(onClick = onBack) { Text("Back") } },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 8.dp),
        ) {
            content()
            Spacer(Modifier.height(24.dp))
        }
    }
}

@Composable
private fun ModeButton(label: String, selected: Boolean, onClick: () -> Unit) {
    OutlinedButton(onClick = onClick, modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)) {
        Text(if (selected) "$label · on" else label)
    }
}

@Composable
private fun SavedAlertRow(alert: SavedAlert, onRemove: () -> Unit) {
    val title = when (alert.kind) {
        AlertKind.BTC_PCT -> "BTC ±${PriceAlerts.formatBand(alert.threshold)}%"
        AlertKind.PRICE -> "${alert.symbol} ${PriceFormat.formatPrice(alert.threshold, "USD")}"
    }
    val hit = if (alert.hitAtMillis <= 0L) "Not hit" else "Hit ${PriceAlerts.formatHit(alert.hitAtMillis)}"
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.titleMedium)
            Text(hit, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        TextButton(onClick = onRemove) { Text("Remove") }
    }
    HorizontalDivider(color = MaterialTheme.colorScheme.outline)
}

@Composable
private fun ResetRow(title: String, onReset: () -> Unit) {
    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Text(title, style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
        TextButton(onClick = onReset) { Text("Reset") }
    }
}

@Composable
private fun SettingSwitch(
    title: String,
    body: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    onReset: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f).padding(end = 12.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium)
            Text(body, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        TextButton(onClick = onReset) { Text("Reset") }
        Switch(checked = checked, onCheckedChange = onCheckedChange)
    }
}

