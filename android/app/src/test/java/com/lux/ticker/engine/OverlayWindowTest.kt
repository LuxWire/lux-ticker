package com.lux.ticker.engine

import android.app.Application
import android.graphics.PixelFormat
import android.view.Display
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.view.WindowManager
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeColors
import com.lux.ticker.data.TapeModel
import com.lux.ticker.data.TickerSettings
import com.lux.ticker.ui.StatusBandLayout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import kotlin.math.roundToInt

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34], application = Application::class)
class OverlayWindowTest {
    @Test fun statusBarBandUsesTheStatusInsetAndLeavesTheMiddleOpen() {
        val context = RuntimeEnvironment.getApplication()
        val windows = context.getSystemService(WindowManager::class.java)
        val overlay = OverlayWindows(context, windows)
        val settings = TickerSettings()
        val inset = 84
        assertEquals(71f, settings.leftPadDp)
        assertEquals(100f, settings.rightPadDp)
        assertEquals(50f, settings.speedDpPerSec)
        assertTrue(settings.startOnBoot)
        assertTrue(settings.showOnLockScreen)
        assertFalse(settings.showHandle)
        assertFalse(settings.orangeDividers)
        assertTrue(settings.luxWirePosts)

        val unlockedSettings = settings.copy(showOnLockScreen = false)
        val tape = overlay.tapeLayoutParams(unlockedSettings, inset)

        assertEquals(StripTheme.DARK, OverlayWindows.overlayStripTheme())
        assertFalse(TapeColors.statusBandFills(StripTheme.DARK))
        assertTrue(TapeColors.statusBandFills(StripTheme.PAPER))
        assertTrue(TapeColors.statusBandFills(StripTheme.LIGHT))
        assertEquals(WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY, tape.type)
        assertEquals(PixelFormat.TRANSLUCENT, tape.format)
        val metrics = context.resources.displayMetrics
        val density = metrics.density
        val row = StatusBandLayout.stripHeightPx(settings.textSizeSp, density, metrics.scaledDensity)
        assertEquals(0f, settings.verticalOffsetDp)
        assertEquals(0f, StatusBandLayout.BELOW_STATUS_GAP_DP)
        assertEquals(0, StatusBandLayout.gapPx(density))
        assertEquals(83, StatusBandLayout.offsetPx(30f, 2.75f))
        assertEquals(row, tape.height)
        assertTrue(tape.height < inset)
        assertEquals(inset, tape.y)
        assertEquals(0, tape.x)
        assertEquals(Gravity.TOP or Gravity.START, tape.gravity)
        assertEquals(0, tape.fitInsetsTypes)
        assertTrue(tape.alpha <= 0.8f)
        assertTrue(has(tape.flags, WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE))
        assertTrue(has(tape.flags, WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL))
        assertTrue(has(tape.flags, WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE))
        assertTrue(has(tape.flags, WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN))
        assertTrue(has(tape.flags, WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS))
        assertFalse(has(tape.flags, WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED))
        assertDoesNotWake(tape.flags)

        val locked = overlay.tapeLayoutParams(settings, inset)
        assertShowsWhenLocked(locked.flags)
        assertTrue(has(locked.flags, WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE))
        assertTrue(has(locked.flags, WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE))
        assertTrue(locked.alpha <= 0.8f)
        assertEquals(row, locked.height)
        assertEquals(inset, locked.y)
        assertDoesNotWake(locked.flags)

        val raised = overlay.tapeLayoutParams(settings.copy(verticalOffsetDp = -48f), inset)
        assertEquals(inset + StatusBandLayout.offsetPx(-48f, density), raised.y)
        assertTrue(raised.y < tape.y)
        val lowered = overlay.tapeLayoutParams(settings.copy(verticalOffsetDp = 200f), inset)
        assertEquals(inset + StatusBandLayout.offsetPx(200f, density), lowered.y)
        assertTrue(lowered.y > tape.y)

        val unset = overlay.tapeLayoutParams(settings, 0)
        assertEquals(row, unset.height)
        assertTrue(unset.y >= 0)
        assertTrue(unset.y < tape.y)

        val handle = overlay.handleLayoutParams(settings, inset)
        assertShowsWhenLocked(handle.flags)
        assertEquals(row, handle.height)
        assertEquals(inset, handle.y)
        assertEquals((8f * density).toInt().coerceAtLeast(1), handle.x)
        assertFalse(has(handle.flags, WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE))
        val hidden = overlay.handleLayoutParams(unlockedSettings, inset)
        assertFalse(has(hidden.flags, WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED))

        val width = context.resources.displayMetrics.widthPixels
        val (left, right) = overlay.contentPads()
        assertEquals(
            StatusBandLayout.pads(width, StatusBandLayout.SIDE_INSET_DP, StatusBandLayout.SIDE_INSET_DP, density),
            left to right,
        )
        assertTrue(StatusBandLayout.midWidth(width, left, right) > 0)

        val pixelDensity = 2.75f
        val pixelWidth = (411f * pixelDensity).roundToInt()
        val (pixelLeft, pixelRight) = StatusBandLayout.pads(pixelWidth, 72f, 100f, pixelDensity)
        assertEquals((72f * pixelDensity).roundToInt(), pixelLeft)
        assertEquals((100f * pixelDensity).roundToInt(), pixelRight)
        assertTrue(StatusBandLayout.midWidth(pixelWidth, pixelLeft, pixelRight) >= (80f * pixelDensity).roundToInt())

        val narrow = (180f * pixelDensity).roundToInt()
        val (narrowLeft, narrowRight) = StatusBandLayout.pads(narrow, 72f, 100f, pixelDensity)
        assertTrue(StatusBandLayout.midWidth(narrow, narrowLeft, narrowRight) >= (80f * pixelDensity).roundToInt() - 1)
        assertTrue(narrowLeft < (72f * pixelDensity).roundToInt())
        assertTrue(StatusBandLayout.pinFits(40, 80))
        assertFalse(StatusBandLayout.pinFits(120, 80))
        assertEquals(inset, StatusBandLayout.statusBarPx(inset, fallbackPx = 24))
        assertEquals(24, StatusBandLayout.statusBarPx(0, fallbackPx = 24))
        assertEquals(inset + 1, StatusBandLayout.topPx(inset, fallbackPx = 24, offsetPx = 1))
        assertEquals(25, StatusBandLayout.topPx(0, fallbackPx = 24, offsetPx = 1))
        assertEquals(inset - 30, StatusBandLayout.topPx(inset, fallbackPx = 24, offsetPx = -30))
        val shortRow = StatusBandLayout.stripHeightPx(13f, pixelDensity, pixelDensity)
        assertTrue(shortRow < (32f * pixelDensity).roundToInt())
    }

    @Test fun drawnYFollowsTheDisplayedOffsetAcrossResumeRestartAndRotation() {
        val context = RuntimeEnvironment.getApplication()
        val windows = RecordingWindows()
        val overlay = OverlayWindows(context, windows)
        val settings = TickerSettings()
        val density = context.resources.displayMetrics.density
        val inset = 84
        val expected = inset + StatusBandLayout.offsetPx(settings.verticalOffsetDp, density)
        assertEquals(inset, expected)
        assertEquals(expected, overlay.tapeLayoutParams(settings, inset).y)

        overlay.testingStatusBar(inset)
        overlay.apply(settings, TapeModel.EMPTY, granted = true)
        assertEquals(expected, overlay.actualTapeY())
        assertEquals(expected, windows.y)

        overlay.testingForceTapeY(0)
        assertEquals(0, overlay.actualTapeY())
        overlay.apply(settings, TapeModel.EMPTY, granted = true)
        assertEquals(expected, overlay.actualTapeY())
        assertTrue(windows.updates >= 1)

        overlay.remove()
        overlay.apply(settings, TapeModel.EMPTY, granted = true)
        assertEquals(expected, overlay.actualTapeY())

        val restarted = OverlayWindows(context, RecordingWindows())
        restarted.testingStatusBar(inset)
        restarted.apply(settings, TapeModel.EMPTY, granted = true)
        assertEquals(expected, restarted.actualTapeY())

        overlay.onDisplayChanged()
        assertEquals(overlay.tapeLayoutParams(settings, inset).y, overlay.actualTapeY())
        overlay.testingStatusBar(120)
        overlay.apply(settings, TapeModel.EMPTY, granted = true)
        assertEquals(overlay.tapeLayoutParams(settings, 120).y, overlay.actualTapeY())

        val decimal = settings.copy(verticalOffsetDp = 12.5f)
        overlay.apply(decimal, TapeModel.EMPTY, granted = true)
        assertEquals(overlay.tapeLayoutParams(decimal, 120).y, overlay.actualTapeY())
        assertEquals(120 + StatusBandLayout.offsetPx(12.5f, density), overlay.actualTapeY())
    }

    @Test fun doubledStatusBarPlusTheOldFactoryGapIsTheWidgetOverlap() {
        val density = 3f
        val statusBarPx = 128
        val factoryGapPx = StatusBandLayout.offsetPx(30f, density)
        val oldRequested = statusBarPx + factoryGapPx
        val visual = oldRequested + statusBarPx
        assertEquals(115, (visual / density).roundToInt())

        val flush = StatusBandLayout.placeTopPx(statusBarPx, userOffsetPx = 0, systemShiftPx = statusBarPx)
        assertEquals(0, flush)
        assertEquals(statusBarPx, flush + statusBarPx)

        val saved = StatusBandLayout.offsetPx(12.5f, density)
        assertEquals(statusBarPx + saved, StatusBandLayout.placeTopPx(statusBarPx, saved, systemShiftPx = 0))
        assertEquals(saved, StatusBandLayout.placeTopPx(statusBarPx, saved, systemShiftPx = statusBarPx))
        assertEquals(128, StatusBandLayout.singleStatusBarPx(viewInsetPx = 0, metricsInsetPx = 128, cutoutTopPx = 80, resourcePx = 24))
        assertEquals(128, StatusBandLayout.singleStatusBarPx(viewInsetPx = 128, metricsInsetPx = 128, cutoutTopPx = 80, resourcePx = 24))
        assertEquals(24, StatusBandLayout.singleStatusBarPx(0, 0, 0, 24))
        assertEquals(128, StatusBandLayout.singleStatusBarPx(0, 0, 0, 24, knownPx = 128))
        assertNull(StatusBandLayout.noteShift(requestedY = 128, screenY = 0))
        assertEquals(128, StatusBandLayout.noteShift(requestedY = 128, screenY = 256))
    }

    @Test fun aSystemShiftDoesNotStackAndASavedGapStays() {
        val context = RuntimeEnvironment.getApplication()
        val overlay = OverlayWindows(context, RecordingWindows())
        val density = context.resources.displayMetrics.density
        val inset = 128
        overlay.testingStatusBar(inset)
        overlay.testingSystemShift(inset)
        overlay.apply(TickerSettings(), TapeModel.EMPTY, granted = true)
        assertEquals(0, overlay.actualTapeY())

        val saved = TickerSettings(verticalOffsetDp = 12.5f)
        val offset = StatusBandLayout.offsetPx(12.5f, density)
        overlay.apply(saved, TapeModel.EMPTY, granted = true)
        assertEquals(offset, overlay.actualTapeY())

        overlay.testingSystemShift(0)
        overlay.apply(saved, TapeModel.EMPTY, granted = true)
        assertEquals(inset + offset, overlay.actualTapeY())
        overlay.onDisplayChanged()
        assertEquals(inset + offset, overlay.actualTapeY())

        overlay.testingReportInset(0)
        overlay.apply(saved, TapeModel.EMPTY, granted = true)
        assertEquals(inset + offset, overlay.actualTapeY())
    }

    @Suppress("DEPRECATION")
    private fun assertShowsWhenLocked(flags: Int) {
        assertTrue(has(flags, WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED))
    }

    @Suppress("DEPRECATION")
    private fun assertDoesNotWake(flags: Int) {
        assertEquals(0, flags and WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON)
        assertEquals(0, flags and WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        assertEquals(0, flags and WindowManager.LayoutParams.FLAG_DISMISS_KEYGUARD)
    }

    private fun has(flags: Int, flag: Int): Boolean = (flags and flag) != 0

    private class RecordingWindows : WindowManager {
        var y: Int? = null
        var updates = 0

        override fun addView(view: View, params: ViewGroup.LayoutParams) {
            view.layoutParams = params
            y = (params as WindowManager.LayoutParams).y
        }

        override fun updateViewLayout(view: View, params: ViewGroup.LayoutParams) {
            view.layoutParams = params
            y = (params as WindowManager.LayoutParams).y
            updates += 1
        }

        override fun removeView(view: View) = Unit

        override fun removeViewImmediate(view: View) = Unit

        override fun getDefaultDisplay(): Display = throw UnsupportedOperationException()
    }
}
