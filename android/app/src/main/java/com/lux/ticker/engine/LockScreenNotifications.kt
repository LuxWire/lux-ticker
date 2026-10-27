package com.lux.ticker.engine

import android.app.Notification
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.res.Configuration
import android.widget.RemoteViews
import androidx.core.app.NotificationCompat
import com.lux.ticker.MainActivity
import com.lux.ticker.R
import com.lux.ticker.data.LockTickerText
import com.lux.ticker.data.ShadePalette
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeColors
import com.lux.ticker.data.TapeModel

/**
 * The one ticker notification. Android requires a foreground-service notification,
 * so the tape lives there instead of beside a separate "Status bar" row.
 * Stock Pixel hides the overlay on the keyguard. This notification is what the
 * shade and, when allowed, the lock screen show. RemoteViews cannot scroll at
 * the overlay frame rate. [charOffset] steps the text. setSelected is not a
 * remotable TextView method, so a marquee in the layout does not run.
 */
object LockScreenNotifications {
    const val CHANNEL_ID = "lux_ticker_lock"
    /** Posted by 1.3.x beside the service notification. 1.4.0 cancels it. */
    const val LEGACY_NOTIFICATION_ID = 1002

    fun build(
        context: Context,
        tape: TapeModel,
        charOffset: Int = 0,
        pinOnRight: Boolean = false,
        theme: StripTheme = StripTheme.PAPER,
        showOnLockScreen: Boolean = true,
        toggleLabel: String? = null,
        toggleIntent: PendingIntent? = null,
        foreground: Boolean = false,
    ): Notification {
        val night = nightMode(context)
        val open = PendingIntent.getActivity(
            context,
            3,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val collapsedLayout = if (pinOnRight) R.layout.notification_lock_ticker_right else R.layout.notification_lock_ticker
        val collapsed = views(context, collapsedLayout, tape, charOffset, theme, night)
        val expanded = views(context, R.layout.notification_lock_ticker_big, tape, charOffset, theme, night)
        val scrolling = LockTickerText.crawledPlain(tape, charOffset)
        val fallback = scrolling.ifEmpty { LockTickerText.plainPinned(tape) }
        val visibility = if (showOnLockScreen) {
            NotificationCompat.VISIBILITY_PUBLIC
        } else {
            NotificationCompat.VISIBILITY_SECRET
        }
        val builder = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_ticker)
            .setColor(if (night) ShadePalette.SYMBOL else TapeColors.SYMBOL)
            .setContentTitle(LockTickerText.coloredPinned(tape, theme, night))
            .setContentText(fallback)
            .setStyle(NotificationCompat.DecoratedCustomViewStyle())
            .setCustomContentView(collapsed)
            .setCustomBigContentView(expanded)
            .setVisibility(visibility)
            .setOngoing(true)
            .setSilent(true)
            .setOnlyAlertOnce(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setCategory(if (foreground) NotificationCompat.CATEGORY_SERVICE else NotificationCompat.CATEGORY_STATUS)
            .setContentIntent(open)
        if (foreground) {
            builder.setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
        }
        if (toggleLabel != null && toggleIntent != null) {
            builder.addAction(0, toggleLabel, toggleIntent)
        }
        return builder.build()
    }

    fun nightMode(context: Context): Boolean {
        val night = context.resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK
        return night == Configuration.UI_MODE_NIGHT_YES
    }

    private fun views(
        context: Context,
        layout: Int,
        tape: TapeModel,
        charOffset: Int,
        theme: StripTheme,
        night: Boolean,
    ): RemoteViews {
        return RemoteViews(context.packageName, layout).apply {
            setInt(R.id.lock_root, "setBackgroundColor", TapeColors.notificationBand(theme, night))
            setInt(R.id.lock_accent, "setBackgroundColor", TapeColors.forNotification(TapeColors.SYMBOL, theme, night))
            setTextViewText(R.id.lock_btc, LockTickerText.coloredPinned(tape, theme, night))
            setTextViewText(R.id.lock_tape, LockTickerText.coloredCrawled(tape, charOffset, theme, night))
        }
    }
}
