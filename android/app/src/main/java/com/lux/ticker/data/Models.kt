package com.lux.ticker.data

import kotlin.math.roundToInt

enum class Dock { TOP, BOTTOM }

enum class SessionTag { NONE, PRE, REGULAR, AFTER }

enum class DisplayMode { ALWAYS, LOCK_ONLY, MANUAL }

enum class StripTheme { DARK, LIGHT, PAPER }

/** Tape motion. Missing preference keeps the original right-to-left crawl. */
enum class ScrollDirection { RIGHT_TO_LEFT, LEFT_TO_RIGHT }

/** Where pinned Bitcoin sits. Missing preference keeps the left. */
enum class PinSide { LEFT, RIGHT }

/**
 * Extra gap, in dp, added under the status bar.
 * 0 sits on the bottom edge of the real status bar. Positive moves the strip
 * down. Negative moves it up onto the status bar.
 * A missing preference keeps [DEFAULT_DP]. It is not a hardcoded status-bar height.
 */
object VerticalOffset {
    const val DEFAULT_DP = 0f
    const val MIN_DP = -120f
    const val MAX_DP = 800f

    fun coerce(dp: Float): Float {
        if (dp.isNaN() || dp.isInfinite()) return DEFAULT_DP
        val tenths = (dp.coerceIn(MIN_DP, MAX_DP) * 10f).roundToInt()
        return tenths / 10f
    }

    /** One decimal place, so the field and the slider show the same number. */
    fun format(dp: Float): String {
        val tenths = (coerce(dp) * 10f).roundToInt()
        val negative = tenths < 0
        val abs = if (negative) -tenths else tenths
        return "${if (negative) "-" else ""}${abs / 10}.${abs % 10}"
    }

    /**
     * A finished number. Blank, "-", and a trailing dot are still being typed.
     * Anything else that is not a number is rejected. A real number is clamped.
     */
    fun parse(raw: String): Float? {
        val text = raw.trim()
        if (text.isEmpty() || text == "-" || text.endsWith('.')) return null
        val value = text.toFloatOrNull() ?: return null
        return coerce(value)
    }
}

enum class AlertKind { BTC_PCT, PRICE }

data class SavedAlert(
    val id: String,
    val kind: AlertKind,
    val symbol: String,
    val threshold: Double,
    val hitAtMillis: Long = 0L,
)

/** In-app screens. Missing preference follows the phone. */
enum class AppTheme {
    SYSTEM,
    DARK,
    LIGHT,
    PAPER,
    ;

    fun resolveDark(systemDark: Boolean): Boolean = when (this) {
        DARK -> true
        LIGHT, PAPER -> false
        SYSTEM -> systemDark
    }

    /** Paper cream cards. A light phone uses paper. Explicit Light is a cooler white. */
    fun paperSurface(systemDark: Boolean): Boolean = when (this) {
        PAPER -> true
        LIGHT, DARK -> false
        SYSTEM -> !systemDark
    }

    /** Status-bar and lock-screen tape. Follows this theme, including the phone when System is on. */
    fun statusStrip(systemDark: Boolean): StripTheme = when (this) {
        DARK -> StripTheme.DARK
        LIGHT -> StripTheme.LIGHT
        PAPER -> StripTheme.PAPER
        SYSTEM -> if (systemDark) StripTheme.DARK else StripTheme.PAPER
    }
}

data class SymbolSpec(
    val symbol: String,
    val label: String,
)

data class TickerSettings(
    val dock: Dock = Dock.BOTTOM,
    val speedDpPerSec: Float = 50f,
    val heightDp: Float = 22f,
    val startOnBoot: Boolean = true,
    val overlayEnabled: Boolean = true,
    val showHandle: Boolean = false,
    val leftPadDp: Float = 71f,
    val rightPadDp: Float = 100f,
    val showOnLockScreen: Boolean = true,
    /** Thin orange bar after Bitcoin. Off unless the key is set, including on upgrade. */
    val orangeDividers: Boolean = false,
    /** @LuxWire post alerts. On for this phone unless the key is saved off. */
    val luxWirePosts: Boolean = true,
    /** How often the X timeline is polled while the ticker service is running. */
    val luxWirePollMinutes: Int = 3,
    /** Fixed Bitcoin on the left. On unless the key is saved off. */
    val pinBtc: Boolean = true,
    val textSizeSp: Float = 13f,
    /** Dp added under the real status bar. 0 is flush with that bar until a value is saved. */
    val verticalOffsetDp: Float = VerticalOffset.DEFAULT_DP,
    /** US session labels. On unless the key is saved off. Pre before the open, AH after the close. */
    val showSessionTags: Boolean = true,
    /** Symbol letters on the tape. Orange unless a choice is saved. */
    val symbolColor: Int = TapeColors.SYMBOL,
    /** Price letters on the tape. White unless a choice is saved. */
    val priceColor: Int = TapeColors.PRICE,
    /** Chart scrub ticks. On unless the key is saved off. Ignores the system touch-feedback setting. */
    val chartHaptics: Boolean = true,
    val stripTheme: StripTheme = StripTheme.DARK,
    /** Settings and the rest of the app. System unless a choice is saved. */
    val appTheme: AppTheme = AppTheme.SYSTEM,
    /** Right to left unless a choice is saved. */
    val scrollRightToLeft: Boolean = true,
    /** Bitcoin on the left unless a choice is saved. */
    val pinSide: PinSide = PinSide.LEFT,
    val displayMode: DisplayMode = DisplayMode.ALWAYS,
    /** Group LuxWire posts. Off unless the key is saved on. */
    val luxWireDigest: Boolean = false,
    /** Subscriber posts open the post. On unless the key is saved off. */
    val luxWireSubscriber: Boolean = true,
    /** Price alerts. Off unless the key is saved on. */
    val priceAlerts: Boolean = false,
    val priceAlertsLoud: Boolean = false,
    val btcAlertPct: Float = 3f,
    val priceAlertSymbol: String = "",
    val priceAlertTarget: Float = 0f,
    /** Extra alerts under the add field. Empty until one is saved, aside from a migrated target. */
    val savedAlerts: List<SavedAlert> = emptyList(),
)

data class ParsedQuote(
    val symbol: String,
    val price: Double,
    val previousClose: Double?,
    val changePct: Double?,
    val currency: String,
    val asOfMillis: Long,
    val fetchedAtMillis: Long,
)

class QuoteParseException(message: String) : Exception(message)

class YahooHttpException(val code: Int, symbol: String) : Exception("Yahoo HTTP $code for $symbol")
