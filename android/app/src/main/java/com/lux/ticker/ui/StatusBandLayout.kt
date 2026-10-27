package com.lux.ticker.ui

import com.lux.ticker.data.VerticalOffset
import kotlin.math.roundToInt

/**
 * The launcher overlay is one text row flush under the system status bar.
 * y=0 is the top of the screen, so the status-bar inset is added once.
 * The strip is only as tall as the prices. A status-bar-tall window centered
 * the text a row too low and left the date and weather showing through.
 */
object StatusBandLayout {
    const val DEFAULT_LEFT_DP = 71f
    const val DEFAULT_RIGHT_DP = 100f
    const val MIN_MID_DP = 80f
    const val TEXT_SP = 13f
    /** Extra gap under the status bar when the user has not saved one. */
    const val BELOW_STATUS_GAP_DP = VerticalOffset.DEFAULT_DP
    /** Text inset from the screen edges. */
    const val SIDE_INSET_DP = 12f

    fun statusBarPx(statusBarInsetPx: Int, fallbackPx: Int): Int {
        if (statusBarInsetPx > 0) return statusBarInsetPx
        return fallbackPx.coerceAtLeast(1)
    }

    /**
     * One status-bar height. The view inset, the window-metrics inset, and the
     * cutout top describe the same bar. The largest of them wins. They are not
     * added together, and a 0 from a restart does not replace a real measurement
     * that was already passed in as [knownPx].
     */
    fun singleStatusBarPx(viewInsetPx: Int, metricsInsetPx: Int, cutoutTopPx: Int, resourcePx: Int, knownPx: Int = 0): Int {
        val measured = maxOf(viewInsetPx, metricsInsetPx, cutoutTopPx, knownPx)
        if (measured > 0) return measured
        return resourcePx.coerceAtLeast(0)
    }

    /**
     * Top of the ticker in window coordinates.
     * [systemShiftPx] is how far below the requested y the window actually
     * appeared. When that shift is about one status bar, the platform already
     * placed the window under the status bar, and adding the bar again drops
     * the strip onto the widgets. [userOffsetPx] is the saved gap only.
     */
    fun placeTopPx(statusBarPx: Int, userOffsetPx: Int, systemShiftPx: Int = 0): Int {
        val bar = statusBarPx.coerceAtLeast(0)
        val alreadyApplied = bar > 0 && systemShiftPx >= bar / 2
        return (if (alreadyApplied) 0 else bar) + userOffsetPx
    }

    /**
     * How far the system moved the window below the y we asked for.
     * A screen position of 0 while we asked for something lower means the
     * window has not been placed yet.
     */
    fun noteShift(requestedY: Int, screenY: Int): Int? {
        if (screenY == 0 && requestedY > 0) return null
        return (screenY - requestedY).coerceAtLeast(0)
    }

    /**
     * Top of the ticker, from the top of the screen.
     * One status-bar inset plus [offsetPx]. Not twice: the overlay is not already
     * pushed below the status bar. [offsetPx] may be negative, which pulls the
     * strip up onto the status bar.
     */
    fun topPx(statusBarInsetPx: Int, fallbackPx: Int, offsetPx: Int): Int {
        return placeTopPx(statusBarPx(statusBarInsetPx, fallbackPx), offsetPx, systemShiftPx = 0)
    }

    /** Signed dp offset in whole pixels. */
    fun offsetPx(offsetDp: Float, density: Float): Int =
        (offsetDp * density.coerceAtLeast(0.5f)).roundToInt()

    /** Default extra gap, in whole pixels. A missing preference uses [VerticalOffset.DEFAULT_DP]. */
    fun gapPx(density: Float): Int = offsetPx(VerticalOffset.DEFAULT_DP, density)

    /** Height of the price row. Independent of the status-bar inset. */
    fun stripHeightPx(textSp: Float, density: Float, scaledDensity: Float): Int {
        val scale = scaledDensity.coerceAtLeast(density.coerceAtLeast(0.5f))
        val textPx = textSp.coerceIn(11f, 18f) * scale
        val pad = 4f * density.coerceAtLeast(0.5f)
        return (textPx * 1.15f + pad).roundToInt().coerceAtLeast(1)
    }

    /**
     * Left and right padding in pixels. Configured padding grows to cover a
     * display cutout on that side, then shrinks only as far as the cutout so
     * the middle band stays at least [MIN_MID_DP] when the screen allows it.
     */
    fun pads(
        widthPx: Int,
        leftDp: Float,
        rightDp: Float,
        density: Float,
        cutoutLeftPx: Int = 0,
        cutoutRightPx: Int = 0,
    ): Pair<Int, Int> {
        if (widthPx <= 0) return 0 to 0
        val scale = density.coerceAtLeast(0.5f)
        val wantedLeft = dp(leftDp, scale).coerceAtLeast(0)
        val wantedRight = dp(rightDp, scale).coerceAtLeast(0)
        val minLeft = cutoutLeftPx.coerceIn(0, widthPx)
        val minRight = cutoutRightPx.coerceIn(0, (widthPx - minLeft).coerceAtLeast(0))
        var left = wantedLeft.coerceAtLeast(minLeft)
        var right = wantedRight.coerceAtLeast(minRight)
        val minMid = dp(MIN_MID_DP, scale)
        if (left + right + minMid <= widthPx) return left to right
        val budget = (widthPx - minMid - minLeft - minRight).coerceAtLeast(0)
        val extraLeftWant = (wantedLeft - minLeft).coerceAtLeast(0)
        val extraRightWant = (wantedRight - minRight).coerceAtLeast(0)
        val extraSum = extraLeftWant + extraRightWant
        val extraLeft = if (extraSum == 0) 0 else budget * extraLeftWant / extraSum
        val extraRight = if (extraSum == 0) 0 else budget - extraLeft
        left = minLeft + extraLeft
        right = minRight + extraRight
        return left to right
    }

    fun midWidth(widthPx: Int, leftPx: Int, rightPx: Int): Int =
        (widthPx - leftPx - rightPx).coerceAtLeast(0)

    /** Bitcoin stays fixed only when its block fits in the middle band. */
    fun pinFits(pinWidthPx: Int, midWidthPx: Int): Boolean =
        pinWidthPx in 1..midWidthPx

    private fun dp(value: Float, density: Float): Int = (value * density).roundToInt()
}
