package com.lux.ticker.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.floatPreferencesKey
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

val Context.symbolDataStore: DataStore<Preferences> by preferencesDataStore(name = "lux_symbols")
val Context.settingsDataStore: DataStore<Preferences> by preferencesDataStore(name = "lux_settings")

class SymbolStore(
    private val data: DataStore<Preferences>,
    private val onChanged: suspend () -> Unit = {},
) {
    private val key = stringPreferencesKey("symbols")

    val symbols: Flow<List<SymbolSpec>> = data.data.map { prefs ->
        SymbolJson.decodeStored(prefs[key])
    }

    suspend fun read(): List<SymbolSpec> = symbols.first()

    suspend fun update(list: List<SymbolSpec>) {
        replace(list)
        onChanged()
    }

    /** Cloud pull. Does not mark the phone as a newer edit. */
    suspend fun replace(list: List<SymbolSpec>) {
        val pinned = SymbolList.ensurePinned(list)
        data.edit { prefs -> prefs[key] = SymbolJson.encode(pinned) }
    }
}

class SettingsStore(private val data: DataStore<Preferences>) {
    private val dockKey = stringPreferencesKey("dock")
    private val speedKey = floatPreferencesKey("speed")
    private val heightKey = floatPreferencesKey("height")
    private val bootKey = booleanPreferencesKey("boot")
    private val overlayKey = booleanPreferencesKey("overlay")
    private val handleKey = booleanPreferencesKey("handle")
    private val leftPadKey = floatPreferencesKey("left_pad")
    private val rightPadKey = floatPreferencesKey("right_pad")
    private val lockScreenKey = booleanPreferencesKey("show_on_lock_screen")
    private val orangeDividersKey = booleanPreferencesKey("orange_dividers")
    private val luxWireKey = booleanPreferencesKey("lux_wire_posts")
    private val luxPollKey = floatPreferencesKey("lux_wire_poll_min")
    private val pinKey = booleanPreferencesKey("pin_btc")
    private val textSizeKey = floatPreferencesKey("text_size")
    private val themeKey = stringPreferencesKey("strip_theme")
    private val appThemeKey = stringPreferencesKey("app_theme")
    private val modeKey = stringPreferencesKey("display_mode")
    private val digestKey = booleanPreferencesKey("lux_wire_digest")
    private val subscriberKey = booleanPreferencesKey("lux_wire_subscriber")
    private val alertsKey = booleanPreferencesKey("price_alerts")
    private val alertsLoudKey = booleanPreferencesKey("price_alerts_loud")
    private val btcAlertKey = floatPreferencesKey("btc_alert_pct")
    private val alertSymbolKey = stringPreferencesKey("alert_symbol")
    private val alertTargetKey = floatPreferencesKey("alert_target")
    private val scrollKey = booleanPreferencesKey("scroll_rtl")
    private val pinSideKey = stringPreferencesKey("pin_side")
    private val alertsListKey = stringPreferencesKey("saved_alerts")
    private val verticalOffsetKey = floatPreferencesKey("vertical_offset")
    private val syncAtKey = androidx.datastore.preferences.core.longPreferencesKey("sync_updated_at")
    private val sessionTagsKey = booleanPreferencesKey("show_session_tags")
    private val symbolColorKey = stringPreferencesKey("symbol_color")
    private val priceColorKey = stringPreferencesKey("price_color")
    private val chartHapticsKey = booleanPreferencesKey("chart_haptics")

    val settings: Flow<TickerSettings> = data.data.map { prefs -> decode(prefs) }

    suspend fun read(): TickerSettings = settings.first()

    suspend fun setDock(dock: Dock) = edit { it[dockKey] = dock.name }

    suspend fun setSpeed(dpPerSec: Float) = edit { it[speedKey] = dpPerSec.coerceIn(12f, 160f) }

    suspend fun setHeight(dp: Float) = edit { it[heightKey] = dp.coerceIn(18f, 40f) }

    suspend fun setStartOnBoot(enabled: Boolean) = edit { it[bootKey] = enabled }

    suspend fun setOverlay(enabled: Boolean) = edit { it[overlayKey] = enabled }

    suspend fun setShowHandle(enabled: Boolean) = edit { it[handleKey] = enabled }

    suspend fun setLeftPad(dp: Float) = edit { it[leftPadKey] = dp.coerceIn(40f, 140f) }

    suspend fun setRightPad(dp: Float) = edit { it[rightPadKey] = dp.coerceIn(48f, 180f) }

    suspend fun setShowOnLockScreen(enabled: Boolean) = edit { it[lockScreenKey] = enabled }

    suspend fun setOrangeDividers(enabled: Boolean) = edit { it[orangeDividersKey] = enabled }

    suspend fun setLuxWirePosts(enabled: Boolean) = edit { it[luxWireKey] = enabled }

    suspend fun setLuxWirePollMinutes(minutes: Int) = edit {
        it[luxPollKey] = minutes.toFloat().coerceIn(2f, 5f)
    }

    suspend fun setPinBtc(enabled: Boolean) = edit { it[pinKey] = enabled }

    suspend fun setTextSize(sp: Float) = edit { it[textSizeKey] = sp.coerceIn(11f, 18f) }

    suspend fun setVerticalOffset(dp: Float) = edit { it[verticalOffsetKey] = VerticalOffset.coerce(dp) }

    /**
     * Does not write a vertical position. A missing key means the user never set
     * one, and the overlay then sits on the real status-bar inset. Writing 0, or
     * the old 30 dp gap, would turn that into a saved value and move the strip.
     */
    suspend fun ensureFactoryDefaults() = Unit

    /** True when any setting other than the sync clock is already on disk. */
    suspend fun hasStoredSettings(): Boolean {
        val prefs = data.data.first()
        return prefs.asMap().keys.any { it.name != syncAtKey.name }
    }

    suspend fun hasStoredVerticalOffset(): Boolean = data.data.first()[verticalOffsetKey] != null

    suspend fun localUpdatedAt(): Long = data.data.first()[syncAtKey] ?: 0L

    suspend fun noteLocalEdit() {
        data.edit { prefs -> prefs[syncAtKey] = System.currentTimeMillis() }
    }

    suspend fun setShowSessionTags(enabled: Boolean) = edit { it[sessionTagsKey] = enabled }

    suspend fun setSymbolColor(color: Int) = edit { it[symbolColorKey] = TapeColorPref.encode(color) }

    suspend fun setPriceColor(color: Int) = edit { it[priceColorKey] = TapeColorPref.encode(color) }

    suspend fun setChartHaptics(enabled: Boolean) = edit { it[chartHapticsKey] = enabled }

    /** Clears saved settings so the next read uses [TickerSettings] defaults. Tickers and X sign-in live elsewhere. */
    suspend fun resetAll() {
        edit { it.clear() }
    }

    suspend fun setStripTheme(theme: StripTheme) = edit { it[themeKey] = theme.name }

    suspend fun setAppTheme(theme: AppTheme) = edit { it[appThemeKey] = theme.name }

    suspend fun setDisplayMode(mode: DisplayMode) = edit { it[modeKey] = mode.name }

    suspend fun setLuxWireDigest(enabled: Boolean) = edit { it[digestKey] = enabled }

    suspend fun setLuxWireSubscriber(enabled: Boolean) = edit { it[subscriberKey] = enabled }

    suspend fun setPriceAlerts(enabled: Boolean) = edit { it[alertsKey] = enabled }

    suspend fun setPriceAlertsLoud(enabled: Boolean) = edit { it[alertsLoudKey] = enabled }

    suspend fun setBtcAlertPct(pct: Float) = edit { it[btcAlertKey] = pct.coerceIn(0.5f, 25f) }

    suspend fun setPriceAlertSymbol(symbol: String) = edit {
        it[alertSymbolKey] = symbol.trim().uppercase().take(16)
    }

    suspend fun setPriceAlertTarget(price: Float) = edit { it[alertTargetKey] = price.coerceIn(0f, 1_000_000f) }

    suspend fun setScrollRightToLeft(enabled: Boolean) = edit { it[scrollKey] = enabled }

    suspend fun setPinSide(side: PinSide) = edit { it[pinSideKey] = side.name }

    suspend fun addAlert(alert: SavedAlert) = edit { prefs ->
        val current = alertsFrom(prefs)
        prefs[alertsListKey] = AlertJson.encode(current + alert)
    }

    suspend fun removeAlert(id: String) = edit { prefs ->
        prefs[alertsListKey] = AlertJson.encode(alertsFrom(prefs).filter { it.id != id })
    }

    suspend fun markAlertHit(id: String, atMillis: Long) = edit { prefs ->
        val next = alertsFrom(prefs).map { alert ->
            if (alert.id == id) alert.copy(hitAtMillis = atMillis) else alert
        }
        prefs[alertsListKey] = AlertJson.encode(next)
    }

    private suspend fun edit(
        bump: Boolean = true,
        block: suspend (androidx.datastore.preferences.core.MutablePreferences) -> Unit,
    ) {
        data.edit { prefs ->
            block(prefs)
            if (bump) prefs[syncAtKey] = System.currentTimeMillis()
        }
    }

    /** Last-write-wins pull. The clock stays at the server time so this phone does not immediately win again. */
    suspend fun replaceFromCloud(settings: TickerSettings, updatedAtMillis: Long) {
        edit(bump = false) { prefs ->
            prefs[dockKey] = settings.dock.name
            prefs[speedKey] = settings.speedDpPerSec
            prefs[heightKey] = settings.heightDp
            prefs[bootKey] = settings.startOnBoot
            prefs[overlayKey] = settings.overlayEnabled
            prefs[handleKey] = settings.showHandle
            prefs[leftPadKey] = settings.leftPadDp
            prefs[rightPadKey] = settings.rightPadDp
            prefs[lockScreenKey] = settings.showOnLockScreen
            prefs[orangeDividersKey] = settings.orangeDividers
            prefs[luxWireKey] = settings.luxWirePosts
            prefs[luxPollKey] = settings.luxWirePollMinutes.toFloat()
            prefs[pinKey] = settings.pinBtc
            prefs[textSizeKey] = settings.textSizeSp
            prefs[verticalOffsetKey] = VerticalOffset.coerce(settings.verticalOffsetDp)
            prefs[sessionTagsKey] = settings.showSessionTags
            prefs[symbolColorKey] = TapeColorPref.encode(settings.symbolColor)
            prefs[priceColorKey] = TapeColorPref.encode(settings.priceColor)
            prefs[chartHapticsKey] = settings.chartHaptics
            prefs[themeKey] = settings.stripTheme.name
            prefs[appThemeKey] = settings.appTheme.name
            prefs[scrollKey] = settings.scrollRightToLeft
            prefs[pinSideKey] = settings.pinSide.name
            prefs[modeKey] = settings.displayMode.name
            prefs[digestKey] = settings.luxWireDigest
            prefs[subscriberKey] = settings.luxWireSubscriber
            prefs[alertsKey] = settings.priceAlerts
            prefs[alertsLoudKey] = settings.priceAlertsLoud
            prefs[btcAlertKey] = settings.btcAlertPct
            prefs[alertSymbolKey] = settings.priceAlertSymbol
            prefs[alertTargetKey] = settings.priceAlertTarget
            prefs[alertsListKey] = AlertJson.encode(settings.savedAlerts)
            prefs[syncAtKey] = updatedAtMillis
        }
    }

    private fun decode(prefs: Preferences): TickerSettings {
        val fallback = TickerSettings()
        val dock = if (prefs[dockKey] == Dock.TOP.name) Dock.TOP else fallback.dock
        return TickerSettings(
            dock = dock,
            speedDpPerSec = (prefs[speedKey] ?: fallback.speedDpPerSec).coerceIn(12f, 160f),
            heightDp = (prefs[heightKey] ?: fallback.heightDp).coerceIn(18f, 40f),
            startOnBoot = prefs[bootKey] ?: fallback.startOnBoot,
            overlayEnabled = prefs[overlayKey] ?: fallback.overlayEnabled,
            showHandle = prefs[handleKey] ?: fallback.showHandle,
            leftPadDp = (prefs[leftPadKey] ?: fallback.leftPadDp).coerceIn(40f, 140f),
            rightPadDp = (prefs[rightPadKey] ?: fallback.rightPadDp).coerceIn(48f, 180f),
            showOnLockScreen = prefs[lockScreenKey] ?: fallback.showOnLockScreen,
            // Missing key stays off, so an upgrade does not grow an orange bar.
            orangeDividers = prefs[orangeDividersKey] ?: fallback.orangeDividers,
            luxWirePosts = prefs[luxWireKey] ?: fallback.luxWirePosts,
            luxWirePollMinutes = when (prefs[luxPollKey]?.toInt()) {
                2 -> 2
                5 -> 5
                else -> fallback.luxWirePollMinutes
            },
            pinBtc = prefs[pinKey] ?: fallback.pinBtc,
            textSizeSp = (prefs[textSizeKey] ?: fallback.textSizeSp).coerceIn(11f, 18f),
            verticalOffsetDp = VerticalOffset.coerce(prefs[verticalOffsetKey] ?: fallback.verticalOffsetDp),
            showSessionTags = prefs[sessionTagsKey] ?: fallback.showSessionTags,
            symbolColor = TapeColorPref.decode(prefs[symbolColorKey], fallback.symbolColor),
            priceColor = TapeColorPref.decode(prefs[priceColorKey], fallback.priceColor),
            chartHaptics = prefs[chartHapticsKey] ?: fallback.chartHaptics,
            stripTheme = when (prefs[themeKey]) {
                StripTheme.LIGHT.name -> StripTheme.LIGHT
                StripTheme.PAPER.name -> StripTheme.PAPER
                else -> fallback.stripTheme
            },
            appTheme = when (prefs[appThemeKey]) {
                AppTheme.DARK.name -> AppTheme.DARK
                AppTheme.LIGHT.name -> AppTheme.LIGHT
                AppTheme.PAPER.name -> AppTheme.PAPER
                else -> fallback.appTheme
            },
            scrollRightToLeft = prefs[scrollKey] ?: fallback.scrollRightToLeft,
            pinSide = if (prefs[pinSideKey] == PinSide.RIGHT.name) PinSide.RIGHT else fallback.pinSide,
            displayMode = when (prefs[modeKey]) {
                DisplayMode.LOCK_ONLY.name -> DisplayMode.LOCK_ONLY
                DisplayMode.MANUAL.name -> DisplayMode.MANUAL
                else -> fallback.displayMode
            },
            luxWireDigest = prefs[digestKey] ?: fallback.luxWireDigest,
            luxWireSubscriber = prefs[subscriberKey] ?: fallback.luxWireSubscriber,
            priceAlerts = prefs[alertsKey] ?: fallback.priceAlerts,
            priceAlertsLoud = prefs[alertsLoudKey] ?: fallback.priceAlertsLoud,
            btcAlertPct = (prefs[btcAlertKey] ?: fallback.btcAlertPct).coerceIn(0.5f, 25f),
            priceAlertSymbol = prefs[alertSymbolKey] ?: fallback.priceAlertSymbol,
            priceAlertTarget = (prefs[alertTargetKey] ?: fallback.priceAlertTarget).coerceIn(0f, 1_000_000f),
            savedAlerts = alertsFrom(prefs),
        )
    }

    private fun alertsFrom(prefs: Preferences): List<SavedAlert> {
        val raw = prefs[alertsListKey]
        if (raw != null) return AlertJson.decode(raw)
        val symbol = (prefs[alertSymbolKey] ?: "").trim().uppercase()
        val target = prefs[alertTargetKey] ?: 0f
        if (symbol.isEmpty() || target <= 0f) return emptyList()
        return listOf(
            SavedAlert(
                id = "legacy-$symbol",
                kind = AlertKind.PRICE,
                symbol = symbol,
                threshold = target.toDouble(),
            ),
        )
    }
}
