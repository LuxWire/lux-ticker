package com.lux.ticker.account

import com.lux.ticker.data.AlertJson
import com.lux.ticker.data.AppTheme
import com.lux.ticker.data.DisplayMode
import com.lux.ticker.data.Dock
import com.lux.ticker.data.PinSide
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.SymbolRules
import com.lux.ticker.data.SymbolSpec
import com.lux.ticker.data.TapeColorPref
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.data.VerticalOffset
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import java.time.Instant

/**
 * Last-write-wins payload for app_settings.data.
 * X bearer tokens are not part of [TickerSettings] and are not written here.
 */
object CloudSync {
    enum class Decision { PUSH, PULL, NONE }

    data class Payload(val settings: TickerSettings, val symbols: List<SymbolSpec>?)

    fun decide(
        localAt: Long,
        remoteAt: Long?,
        localBytes: String,
        remoteBytes: String?,
        localHasSettings: Boolean = false,
    ): Decision {
        // sync_updated_at arrived in 1.4.3. A phone that already has settings and no clock
        // must push those settings. Treating the clock as 0 used to pull the server row
        // and replace every local value, including ones the server document never had.
        if (localAt == 0L && localHasSettings) return Decision.PUSH
        if (remoteAt == null || remoteBytes == null) return Decision.PUSH
        if (localAt > remoteAt) return Decision.PUSH
        if (remoteAt > localAt) return Decision.PULL
        return if (localBytes == remoteBytes) Decision.NONE else Decision.PUSH
    }

    /**
     * Applies only JSON keys that are present. A missing key keeps [local].
     * Decode-then-replace used to fill those gaps with factory defaults and write them.
     */
    fun merge(local: TickerSettings, raw: String): Payload? {
        val root = runCatching { kotlinx.serialization.json.Json.parseToJsonElement(raw) as? JsonObject }.getOrNull()
            ?: return null
        val settingsObj = root["settings"] as? JsonObject ?: return null
        fun has(key: String) = settingsObj.containsKey(key)
        val settings = local.copy(
            dock = if (!has("dock")) local.dock else if (settingsObj.str("dock") == Dock.TOP.name) Dock.TOP else Dock.BOTTOM,
            speedDpPerSec = if (!has("speedDpPerSec")) local.speedDpPerSec else settingsObj.flt("speedDpPerSec", local.speedDpPerSec).coerceIn(12f, 160f),
            heightDp = if (!has("heightDp")) local.heightDp else settingsObj.flt("heightDp", local.heightDp).coerceIn(18f, 40f),
            startOnBoot = if (!has("startOnBoot")) local.startOnBoot else settingsObj.bool("startOnBoot", local.startOnBoot),
            overlayEnabled = if (!has("overlayEnabled")) local.overlayEnabled else settingsObj.bool("overlayEnabled", local.overlayEnabled),
            showHandle = if (!has("showHandle")) local.showHandle else settingsObj.bool("showHandle", local.showHandle),
            leftPadDp = if (!has("leftPadDp")) local.leftPadDp else settingsObj.flt("leftPadDp", local.leftPadDp).coerceIn(40f, 140f),
            rightPadDp = if (!has("rightPadDp")) local.rightPadDp else settingsObj.flt("rightPadDp", local.rightPadDp).coerceIn(48f, 180f),
            showOnLockScreen = if (!has("showOnLockScreen")) local.showOnLockScreen else settingsObj.bool("showOnLockScreen", local.showOnLockScreen),
            orangeDividers = if (!has("orangeDividers")) local.orangeDividers else settingsObj.bool("orangeDividers", local.orangeDividers),
            luxWirePosts = if (!has("luxWirePosts")) local.luxWirePosts else settingsObj.bool("luxWirePosts", local.luxWirePosts),
            luxWirePollMinutes = if (!has("luxWirePollMinutes")) local.luxWirePollMinutes else settingsObj.int("luxWirePollMinutes", local.luxWirePollMinutes).let {
                when (it) {
                    2 -> 2
                    5 -> 5
                    else -> 3
                }
            },
            pinBtc = if (!has("pinBtc")) local.pinBtc else settingsObj.bool("pinBtc", local.pinBtc),
            textSizeSp = if (!has("textSizeSp")) local.textSizeSp else settingsObj.flt("textSizeSp", local.textSizeSp).coerceIn(11f, 18f),
            verticalOffsetDp = if (!has("verticalOffsetDp")) local.verticalOffsetDp else VerticalOffset.coerce(settingsObj.flt("verticalOffsetDp", local.verticalOffsetDp)),
            showSessionTags = if (!has("showSessionTags")) local.showSessionTags else settingsObj.bool("showSessionTags", local.showSessionTags),
            symbolColor = if (!has("symbolColor")) local.symbolColor else TapeColorPref.decode(settingsObj.str("symbolColor"), local.symbolColor),
            priceColor = if (!has("priceColor")) local.priceColor else TapeColorPref.decode(settingsObj.str("priceColor"), local.priceColor),
            chartHaptics = if (!has("chartHaptics")) local.chartHaptics else settingsObj.bool("chartHaptics", local.chartHaptics),
            stripTheme = if (!has("stripTheme")) local.stripTheme else when (settingsObj.str("stripTheme")) {
                StripTheme.LIGHT.name -> StripTheme.LIGHT
                StripTheme.PAPER.name -> StripTheme.PAPER
                StripTheme.DARK.name -> StripTheme.DARK
                else -> local.stripTheme
            },
            appTheme = if (!has("appTheme")) local.appTheme else when (settingsObj.str("appTheme")) {
                AppTheme.DARK.name -> AppTheme.DARK
                AppTheme.LIGHT.name -> AppTheme.LIGHT
                AppTheme.PAPER.name -> AppTheme.PAPER
                AppTheme.SYSTEM.name -> AppTheme.SYSTEM
                else -> local.appTheme
            },
            scrollRightToLeft = if (!has("scrollRightToLeft")) local.scrollRightToLeft else settingsObj.bool("scrollRightToLeft", local.scrollRightToLeft),
            pinSide = if (!has("pinSide")) local.pinSide else if (settingsObj.str("pinSide") == PinSide.RIGHT.name) PinSide.RIGHT else PinSide.LEFT,
            displayMode = if (!has("displayMode")) local.displayMode else when (settingsObj.str("displayMode")) {
                DisplayMode.LOCK_ONLY.name -> DisplayMode.LOCK_ONLY
                DisplayMode.MANUAL.name -> DisplayMode.MANUAL
                DisplayMode.ALWAYS.name -> DisplayMode.ALWAYS
                else -> local.displayMode
            },
            luxWireDigest = if (!has("luxWireDigest")) local.luxWireDigest else settingsObj.bool("luxWireDigest", local.luxWireDigest),
            luxWireSubscriber = if (!has("luxWireSubscriber")) local.luxWireSubscriber else settingsObj.bool("luxWireSubscriber", local.luxWireSubscriber),
            priceAlerts = if (!has("priceAlerts")) local.priceAlerts else settingsObj.bool("priceAlerts", local.priceAlerts),
            priceAlertsLoud = if (!has("priceAlertsLoud")) local.priceAlertsLoud else settingsObj.bool("priceAlertsLoud", local.priceAlertsLoud),
            btcAlertPct = if (!has("btcAlertPct")) local.btcAlertPct else settingsObj.flt("btcAlertPct", local.btcAlertPct).coerceIn(0.5f, 25f),
            priceAlertSymbol = if (!has("priceAlertSymbol")) local.priceAlertSymbol else settingsObj.str("priceAlertSymbol") ?: local.priceAlertSymbol,
            priceAlertTarget = if (!has("priceAlertTarget")) local.priceAlertTarget else settingsObj.flt("priceAlertTarget", local.priceAlertTarget).coerceIn(0f, 1_000_000f),
            savedAlerts = if (!has("savedAlerts")) local.savedAlerts else AlertJson.decode(settingsObj.str("savedAlerts")),
        )
        val symbols = if (!root.containsKey("symbols")) null else symbolsOf(root["symbols"])
        return Payload(settings, symbols)
    }

    fun encode(settings: TickerSettings, symbols: List<SymbolSpec>): String = buildJsonObject {
        put("v", 1)
        put("symbols", buildJsonArray {
            symbols.forEach { spec ->
                add(buildJsonObject {
                    put("symbol", spec.symbol)
                    put("label", spec.label)
                })
            }
        })
        put("settings", buildJsonObject {
            put("dock", settings.dock.name)
            put("speedDpPerSec", num(settings.speedDpPerSec))
            put("heightDp", num(settings.heightDp))
            put("startOnBoot", settings.startOnBoot)
            put("overlayEnabled", settings.overlayEnabled)
            put("showHandle", settings.showHandle)
            put("leftPadDp", num(settings.leftPadDp))
            put("rightPadDp", num(settings.rightPadDp))
            put("showOnLockScreen", settings.showOnLockScreen)
            put("orangeDividers", settings.orangeDividers)
            put("luxWirePosts", settings.luxWirePosts)
            put("luxWirePollMinutes", settings.luxWirePollMinutes)
            put("pinBtc", settings.pinBtc)
            put("textSizeSp", num(settings.textSizeSp))
            put("verticalOffsetDp", num(VerticalOffset.coerce(settings.verticalOffsetDp)))
            put("showSessionTags", settings.showSessionTags)
            put("symbolColor", TapeColorPref.encode(settings.symbolColor))
            put("priceColor", TapeColorPref.encode(settings.priceColor))
            put("chartHaptics", settings.chartHaptics)
            put("stripTheme", settings.stripTheme.name)
            put("appTheme", settings.appTheme.name)
            put("scrollRightToLeft", settings.scrollRightToLeft)
            put("pinSide", settings.pinSide.name)
            put("displayMode", settings.displayMode.name)
            put("luxWireDigest", settings.luxWireDigest)
            put("luxWireSubscriber", settings.luxWireSubscriber)
            put("priceAlerts", settings.priceAlerts)
            put("priceAlertsLoud", settings.priceAlertsLoud)
            put("btcAlertPct", num(settings.btcAlertPct))
            put("priceAlertSymbol", settings.priceAlertSymbol)
            put("priceAlertTarget", num(settings.priceAlertTarget))
            put("savedAlerts", AlertJson.encode(settings.savedAlerts))
        })
    }.toString()

    fun decode(raw: String): Payload? {
        val root = runCatching { kotlinx.serialization.json.Json.parseToJsonElement(raw) as? JsonObject }.getOrNull()
            ?: return null
        val settingsObj = root["settings"] as? JsonObject ?: return null
        val fallback = TickerSettings()
        val settings = TickerSettings(
            dock = if (settingsObj.str("dock") == Dock.TOP.name) Dock.TOP else fallback.dock,
            speedDpPerSec = settingsObj.flt("speedDpPerSec", fallback.speedDpPerSec).coerceIn(12f, 160f),
            heightDp = settingsObj.flt("heightDp", fallback.heightDp).coerceIn(18f, 40f),
            startOnBoot = settingsObj.bool("startOnBoot", fallback.startOnBoot),
            overlayEnabled = settingsObj.bool("overlayEnabled", fallback.overlayEnabled),
            showHandle = settingsObj.bool("showHandle", fallback.showHandle),
            leftPadDp = settingsObj.flt("leftPadDp", fallback.leftPadDp).coerceIn(40f, 140f),
            rightPadDp = settingsObj.flt("rightPadDp", fallback.rightPadDp).coerceIn(48f, 180f),
            showOnLockScreen = settingsObj.bool("showOnLockScreen", fallback.showOnLockScreen),
            orangeDividers = settingsObj.bool("orangeDividers", fallback.orangeDividers),
            luxWirePosts = settingsObj.bool("luxWirePosts", fallback.luxWirePosts),
            luxWirePollMinutes = settingsObj.int("luxWirePollMinutes", fallback.luxWirePollMinutes).let {
                when (it) {
                    2 -> 2
                    5 -> 5
                    else -> fallback.luxWirePollMinutes
                }
            },
            pinBtc = settingsObj.bool("pinBtc", fallback.pinBtc),
            textSizeSp = settingsObj.flt("textSizeSp", fallback.textSizeSp).coerceIn(11f, 18f),
            verticalOffsetDp = VerticalOffset.coerce(settingsObj.flt("verticalOffsetDp", fallback.verticalOffsetDp)),
            showSessionTags = settingsObj.bool("showSessionTags", fallback.showSessionTags),
            symbolColor = TapeColorPref.decode(settingsObj.str("symbolColor"), fallback.symbolColor),
            priceColor = TapeColorPref.decode(settingsObj.str("priceColor"), fallback.priceColor),
            chartHaptics = settingsObj.bool("chartHaptics", fallback.chartHaptics),
            stripTheme = when (settingsObj.str("stripTheme")) {
                StripTheme.LIGHT.name -> StripTheme.LIGHT
                StripTheme.PAPER.name -> StripTheme.PAPER
                else -> fallback.stripTheme
            },
            appTheme = when (settingsObj.str("appTheme")) {
                AppTheme.DARK.name -> AppTheme.DARK
                AppTheme.LIGHT.name -> AppTheme.LIGHT
                AppTheme.PAPER.name -> AppTheme.PAPER
                else -> fallback.appTheme
            },
            scrollRightToLeft = settingsObj.bool("scrollRightToLeft", fallback.scrollRightToLeft),
            pinSide = if (settingsObj.str("pinSide") == PinSide.RIGHT.name) PinSide.RIGHT else fallback.pinSide,
            displayMode = when (settingsObj.str("displayMode")) {
                DisplayMode.LOCK_ONLY.name -> DisplayMode.LOCK_ONLY
                DisplayMode.MANUAL.name -> DisplayMode.MANUAL
                else -> fallback.displayMode
            },
            luxWireDigest = settingsObj.bool("luxWireDigest", fallback.luxWireDigest),
            luxWireSubscriber = settingsObj.bool("luxWireSubscriber", fallback.luxWireSubscriber),
            priceAlerts = settingsObj.bool("priceAlerts", fallback.priceAlerts),
            priceAlertsLoud = settingsObj.bool("priceAlertsLoud", fallback.priceAlertsLoud),
            btcAlertPct = settingsObj.flt("btcAlertPct", fallback.btcAlertPct).coerceIn(0.5f, 25f),
            priceAlertSymbol = settingsObj.str("priceAlertSymbol") ?: fallback.priceAlertSymbol,
            priceAlertTarget = settingsObj.flt("priceAlertTarget", fallback.priceAlertTarget).coerceIn(0f, 1_000_000f),
            savedAlerts = AlertJson.decode(settingsObj.str("savedAlerts")),
        )
        return Payload(settings, symbolsOf(root["symbols"]))
    }

    private fun symbolsOf(node: kotlinx.serialization.json.JsonElement?): List<SymbolSpec> {
        val array = node as? JsonArray ?: return emptyList()
        return array.mapNotNull { element ->
            val obj = element as? JsonObject ?: return@mapNotNull null
            val symbol = obj.str("symbol") ?: return@mapNotNull null
            if (!SymbolRules.isValid(symbol)) return@mapNotNull null
            SymbolSpec(symbol, obj.str("label") ?: symbol)
        }
    }

    fun formatTime(millis: Long): String = Instant.ofEpochMilli(millis).toString()

    fun parseTime(raw: String?): Long? {
        val text = raw?.trim().orEmpty()
        if (text.isEmpty()) return null
        text.toLongOrNull()?.let { value ->
            return if (value < 10_000_000_000L) value * 1000L else value
        }
        return runCatching { Instant.parse(text).toEpochMilli() }.getOrNull()
    }

    private fun num(value: Float): Double = value.toString().toDouble()

    private fun JsonObject.str(key: String): String? {
        val primitive = this[key] as? JsonPrimitive ?: return null
        return primitive.content
    }

    private fun JsonObject.bool(key: String, fallback: Boolean): Boolean {
        val primitive = this[key] as? JsonPrimitive ?: return fallback
        return primitive.content.toBooleanStrictOrNull() ?: fallback
    }

    private fun JsonObject.flt(key: String, fallback: Float): Float {
        val primitive = this[key] as? JsonPrimitive ?: return fallback
        return primitive.content.toFloatOrNull() ?: fallback
    }

    private fun JsonObject.int(key: String, fallback: Int): Int {
        val primitive = this[key] as? JsonPrimitive ?: return fallback
        return primitive.content.toIntOrNull() ?: fallback
    }
}
