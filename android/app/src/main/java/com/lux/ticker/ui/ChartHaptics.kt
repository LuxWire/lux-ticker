package com.lux.ticker.ui

import android.content.Context
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.view.HapticFeedbackConstants
import android.view.View

/**
 * One tick when the scrubbed index changes.
 * With the app's chart-haptics toggle on, this ignores the system touch-feedback setting.
 */
object ChartHaptics {
    @Suppress("DEPRECATION") // Still the flag that skips Settings → Sound → Touch feedback. Vibrate covers a refusal.
    const val FLAGS: Int =
        HapticFeedbackConstants.FLAG_IGNORE_GLOBAL_SETTING or HapticFeedbackConstants.FLAG_IGNORE_VIEW_SETTING

    const val PULSE_MS = 20L

    fun feedbackConstant(sdkInt: Int): Int =
        if (sdkInt >= 30) HapticFeedbackConstants.CLOCK_TICK else HapticFeedbackConstants.KEYBOARD_TAP

    fun secondaryConstant(sdkInt: Int): Int? =
        if (sdkInt >= 30) HapticFeedbackConstants.SEGMENT_TICK else null

    fun constants(sdkInt: Int): List<Int> {
        val first = feedbackConstant(sdkInt)
        val second = secondaryConstant(sdkInt)
        return if (second == null || second == first) listOf(first) else listOf(first, second)
    }

    fun tick(view: View, enabled: Boolean) {
        if (!enabled) return
        view.isHapticFeedbackEnabled = true
        val played = constants(Build.VERSION.SDK_INT).any { constant ->
            view.performHapticFeedback(constant, FLAGS)
        }
        if (!played) pulse(view.context)
    }

    fun pulse(context: Context) {
        val vibrator = vibrator(context) ?: return
        if (!vibrator.hasVibrator()) return
        val predefined = runCatching {
            vibrator.vibrate(VibrationEffect.createPredefined(VibrationEffect.EFFECT_TICK))
        }
        if (predefined.isFailure) {
            runCatching {
                vibrator.vibrate(VibrationEffect.createOneShot(PULSE_MS, VibrationEffect.DEFAULT_AMPLITUDE))
            }
        }
    }

    private fun vibrator(context: Context): Vibrator? {
        return if (Build.VERSION.SDK_INT >= 31) {
            context.getSystemService(VibratorManager::class.java)?.defaultVibrator
        } else {
            @Suppress("DEPRECATION")
            context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
        }
    }
}
