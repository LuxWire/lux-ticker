package com.lux.ticker.engine

import android.content.Context
import android.content.Intent
import android.graphics.PixelFormat
import android.os.Build
import android.util.Log
import android.view.Gravity
import android.view.View
import android.view.WindowInsets
import android.view.WindowManager
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeModel
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.ui.StatusBandLayout
import com.lux.ticker.ui.TickerStripView

/**
 * A full-width text row flush under the system status bar. Layout y=0 is the
 * top of the screen (a y of 0 covers the clock), so the real status-bar inset
 * is applied once. The Display vertical offset is an extra gap under that bar.
 * It stays 0 until the user sets one. Negative pulls the strip up onto the status bar.
 * The window is only as tall as the prices. The
 * background is transparent, and the tape paints no theme band. Prices stay
 * light with a shadow. The lock-screen strip is a separate notification and
 * is not drawn here. The tape is not touchable. An optional handle on the
 * right of the strip is the only touch target.
 *
 * [WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED] is applied only when
 * [TickerSettings.showOnLockScreen] is on. The flag does not wake the phone.
 * Stock Pixel System UI can still force-hide this overlay on the keyguard.
 */
class OverlayWindows(
    private val context: Context,
    private val windows: WindowManager,
) {
    private var tape: TickerStripView? = null
    private var handle: View? = null
    private var tapeAdded = false
    private var handleAdded = false
    private var statusBarPx = 0
    private var cutoutLeft = 0
    private var cutoutRight = 0
    /** Extra pixels the platform added under the y we requested. One status bar means do not add it again. */
    private var systemShiftPx = 0
    private var latestSettings: TickerSettings? = null
    private var latestModel: TapeModel? = null
    private var latestGranted = false
    private var latestKeyguard = false

    fun apply(settings: TickerSettings, model: TapeModel, granted: Boolean, keyguardLocked: Boolean = false) {
        latestSettings = settings
        latestModel = model
        latestGranted = granted
        latestKeyguard = keyguardLocked
        if (!OverlayPolicy.showOverlay(settings, granted, keyguardLocked)) {
            remove()
            return
        }
        try {
            val strip = tape ?: TickerStripView(context).also {
                it.importantForAccessibility = View.IMPORTANT_FOR_ACCESSIBILITY_NO
                it.isClickable = false
                it.isFocusable = false
                it.isFocusableInTouchMode = false
                it.setOnApplyWindowInsetsListener { _, insets ->
                    acceptInsets(insets)
                    insets
                }
                tape = it
            }
            val (left, right) = contentPads()
            strip.setStatusBandPx(left, right)
            strip.setOrangeDividers(settings.orangeDividers)
            strip.setTextSizeSp(settings.textSizeSp)
            strip.setStripTheme(overlayStripTheme())
            strip.setScrollRightToLeft(settings.scrollRightToLeft)
            strip.setPinOnRight(settings.pinSide == com.lux.ticker.data.PinSide.RIGHT)
            strip.setTape(model)
            strip.setSpeedDpPerSec(settings.speedDpPerSec)
            // Always push y. addView can be followed by a system relayout that drops
            // the offset, and a matching layout key used to skip the correction on
            // resume, rotation, and the next start of the service.
            val params = tapeLayoutParams(settings, currentStatusBarPx())
            if (!tapeAdded) {
                windows.addView(strip, params)
                tapeAdded = true
                strip.requestApplyInsets()
            } else {
                windows.updateViewLayout(strip, params)
            }
            rememberSystemShift(strip, params.y)
            if (settings.showHandle) {
                ensureHandle(settings)
            } else {
                removeHandle()
            }
        } catch (e: Exception) {
            Log.w(TAG, "overlay", e)
            remove()
        }
    }

    fun remove() {
        removeTape()
        removeHandle()
    }

    internal fun testingStatusBar(px: Int) {
        statusBarPx = px
    }

    /** A later inset of 0, as on keyguard or before the first layout, must not wipe [statusBarPx]. */
    internal fun testingReportInset(topPx: Int) {
        if (topPx > 0) statusBarPx = topPx
    }

    /** Platform shift observed after layout, in pixels. Tests set the Pixel 10 Pro XL case directly. */
    internal fun testingSystemShift(px: Int) {
        systemShiftPx = px
    }

    /**
     * Rotation, dark mode, and other display changes. The last real status-bar
     * height stays. Zeroing it used to lay the strip out with the smaller
     * resource height, then jump when the inset came back.
     */
    fun onDisplayChanged() {
        val settings = latestSettings ?: return
        val model = latestModel ?: return
        apply(settings, model, latestGranted, latestKeyguard)
    }

    internal fun actualTapeY(): Int? =
        (tape?.layoutParams as? WindowManager.LayoutParams)?.y

    /** What the window manager currently has, after something else wrote y. */
    internal fun testingForceTapeY(y: Int) {
        val view = tape ?: return
        val params = (view.layoutParams as? WindowManager.LayoutParams)?.also { it.y = y } ?: return
        windows.updateViewLayout(view, params)
    }

    internal fun tapeLayoutParams(settings: TickerSettings, statusBarInsetPx: Int): WindowManager.LayoutParams {
        val height = stripHeightPx(settings)
        val top = topPx(settings, statusBarInsetPx)
        return baseParams(settings, WindowManager.LayoutParams.MATCH_PARENT, height).apply {
            y = top
            flags = flags or WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE
            // Android drops touches that pass through an opaque untrusted overlay.
            // At or below the platform obscuring limit, taps reach the app behind the strip.
            alpha = PASS_THROUGH_ALPHA
        }
    }

    internal fun handleLayoutParams(
        settings: TickerSettings,
        statusBarInsetPx: Int,
    ): WindowManager.LayoutParams {
        val density = context.resources.displayMetrics.density
        val height = stripHeightPx(settings)
        val top = topPx(settings, statusBarInsetPx)
        val width = (8f * density).toInt().coerceAtLeast(1)
        val margin = (8f * density).toInt().coerceAtLeast(1)
        return baseParams(settings, width, height).apply {
            gravity = Gravity.TOP or Gravity.END
            x = margin
            y = top
        }
    }

    internal fun contentPads(): Pair<Int, Int> {
        val metrics = context.resources.displayMetrics
        return StatusBandLayout.pads(
            widthPx = metrics.widthPixels.coerceAtLeast(1),
            leftDp = StatusBandLayout.SIDE_INSET_DP,
            rightDp = StatusBandLayout.SIDE_INSET_DP,
            density = metrics.density,
            cutoutLeftPx = cutoutLeft,
            cutoutRightPx = cutoutRight,
        )
    }

    private fun topPx(settings: TickerSettings, statusBarInsetPx: Int): Int {
        val offset = StatusBandLayout.offsetPx(settings.verticalOffsetDp, context.resources.displayMetrics.density)
        val bar = StatusBandLayout.singleStatusBarPx(
            viewInsetPx = statusBarInsetPx,
            metricsInsetPx = metricsStatusBarPx(),
            cutoutTopPx = 0,
            resourcePx = fallbackStatusBarPx(),
        )
        return StatusBandLayout.placeTopPx(bar, offset, systemShiftPx)
    }

    private fun rememberSystemShift(view: View, requestedY: Int) {
        if (Build.FINGERPRINT == "robolectric") return
        view.post {
            if (!tapeAdded) return@post
            val loc = IntArray(2)
            view.getLocationOnScreen(loc)
            val shift = StatusBandLayout.noteShift(requestedY, loc[1]) ?: return@post
            if (shift == systemShiftPx) return@post
            systemShiftPx = shift
            val settings = latestSettings ?: return@post
            val again = tapeLayoutParams(settings, currentStatusBarPx())
            if (again.y == requestedY) return@post
            runCatching { windows.updateViewLayout(view, again) }
        }
    }

    private fun metricsStatusBarPx(): Int {
        if (Build.VERSION.SDK_INT < 30) return 0
        return runCatching {
            val insets = windows.currentWindowMetrics.windowInsets
            val bars = insets.getInsetsIgnoringVisibility(WindowInsets.Type.statusBars()).top
            val cutout = insets.displayCutout?.safeInsetTop ?: 0
            maxOf(bars, cutout)
        }.getOrDefault(0)
    }

    private fun stripHeightPx(settings: TickerSettings): Int {
        val metrics = context.resources.displayMetrics
        return StatusBandLayout.stripHeightPx(settings.textSizeSp, metrics.density, metrics.scaledDensity)
    }

    private fun ensureHandle(settings: TickerSettings) {
        val view = handle ?: HandleView(context).also { handle = it }
        val params = handleLayoutParams(settings, currentStatusBarPx())
        if (!handleAdded) {
            windows.addView(view, params)
            handleAdded = true
        } else {
            windows.updateViewLayout(view, params)
        }
    }

    private fun removeTape() {
        val view = tape
        if (tapeAdded && view != null) {
            runCatching { windows.removeView(view) }
        }
        tapeAdded = false
    }

    private fun removeHandle() {
        val view = handle
        if (handleAdded && view != null) {
            runCatching { windows.removeView(view) }
        }
        handleAdded = false
    }

    private fun acceptInsets(insets: WindowInsets) {
        val bars = if (Build.VERSION.SDK_INT >= 30) {
            insets.getInsets(WindowInsets.Type.statusBars()).top
        } else {
            @Suppress("DEPRECATION")
            insets.systemWindowInsetTop
        }
        val cut = insets.displayCutout
        val top = maxOf(bars, cut?.safeInsetTop ?: 0)
        val left = cut?.safeInsetLeft ?: 0
        val right = cut?.safeInsetRight ?: 0
        if (top == statusBarPx && left == cutoutLeft && right == cutoutRight) return
        // A 0 inset on keyguard or before the first layout must not forget the bar.
        if (top > 0) statusBarPx = top
        cutoutLeft = left
        cutoutRight = right
        val settings = latestSettings ?: return
        val model = latestModel ?: return
        tape?.post { apply(settings, model, latestGranted, latestKeyguard) }
    }

    private fun currentStatusBarPx(): Int = statusBarPx

    private fun fallbackStatusBarPx(): Int = dimen("status_bar_height", 24f)

    private fun baseParams(settings: TickerSettings, width: Int, height: Int): WindowManager.LayoutParams {
        val params = WindowManager.LayoutParams(
            width,
            height,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            baseFlags(settings.showOnLockScreen),
            PixelFormat.TRANSLUCENT,
        )
        params.gravity = Gravity.TOP or Gravity.START
        params.x = 0
        params.y = 0
        if (Build.VERSION.SDK_INT >= 30) {
            params.setFitInsetsTypes(0)
            params.setFitInsetsSides(0)
            params.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_ALWAYS
        } else if (Build.VERSION.SDK_INT >= 28) {
            params.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES
        }
        return params
    }

    /**
     * Lock-screen visibility is opt-in. [WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON]
     * stays off so the strip never wakes the phone. On a stock Pixel, System UI
     * may still hide non-system overlays while the keyguard is up.
     */
    @Suppress("DEPRECATION")
    private fun baseFlags(showOnLockScreen: Boolean): Int {
        var flags = WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
            WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL or
            WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or
            WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS or
            WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED
        if (showOnLockScreen) {
            flags = flags or WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED
        }
        return flags
    }

    private fun dimen(name: String, fallbackDp: Float): Int {
        val id = context.resources.getIdentifier(name, "dimen", "android")
        if (id > 0) return context.resources.getDimensionPixelSize(id)
        return (fallbackDp * context.resources.displayMetrics.density).toInt()
    }

    private class HandleView(context: Context) : View(context) {
        init {
            contentDescription = "Open LuxWire Ticker"
            setBackgroundColor(0xCCF7931A.toInt())
            setOnClickListener {
                val intent = Intent(context, com.lux.ticker.MainActivity::class.java)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                runCatching { context.startActivity(intent) }
            }
        }
    }

    companion object {
        private const val TAG = "LuxTicker"
        private const val PASS_THROUGH_ALPHA = 0.8f

        /** Launcher status tape. Light prices, no opaque fill. */
        fun overlayStripTheme(): StripTheme = StripTheme.DARK
    }
}
