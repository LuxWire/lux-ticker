package com.lux.ticker.engine

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import com.lux.ticker.MainActivity
import com.lux.ticker.R
import com.lux.ticker.data.PriceAlerts
import com.lux.ticker.data.TapeColors

object PriceAlertNotifier {
    const val QUIET_CHANNEL = "lux_price_alerts"
    const val LOUD_CHANNEL = "lux_price_alerts_loud"
    const val POPUP_CHANNEL = "lux_price_alerts_popup"

    fun ensureChannels(context: Context) {
        val manager = context.getSystemService(NotificationManager::class.java)
        val quiet = NotificationChannel(
            QUIET_CHANNEL,
            context.getString(R.string.channel_alerts_name),
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = context.getString(R.string.channel_alerts_description)
            setSound(null, null)
            enableVibration(false)
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
        }
        val loud = NotificationChannel(
            LOUD_CHANNEL,
            context.getString(R.string.channel_alerts_loud_name),
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = context.getString(R.string.channel_alerts_loud_description)
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
        }
        val popup = NotificationChannel(
            POPUP_CHANNEL,
            context.getString(R.string.channel_alerts_popup_name),
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = context.getString(R.string.channel_alerts_popup_description)
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
        }
        manager.createNotificationChannel(quiet)
        manager.createNotificationChannel(loud)
        manager.createNotificationChannel(popup)
    }

    fun show(context: Context, hit: PriceAlerts.Hit, loud: Boolean) {
        ensureChannels(context)
        val manager = context.getSystemService(NotificationManager::class.java)
        val channel = if (loud) LOUD_CHANNEL else QUIET_CHANNEL
        val open = PendingIntent.getActivity(
            context,
            80,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val id = 3_000 + (hit.key.hashCode() and 0x0FFF)
        val notification = NotificationCompat.Builder(context, channel)
            .setSmallIcon(R.drawable.ic_stat_ticker)
            .setColor(TapeColors.SYMBOL)
            .setContentTitle(hit.title)
            .setContentText(hit.body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(hit.body))
            .setContentIntent(open)
            .setAutoCancel(true)
            .setOnlyAlertOnce(true)
            .setSilent(!loud)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setCategory(NotificationCompat.CATEGORY_STATUS)
            .build()
        manager.notify(id, notification)
        val popup = NotificationCompat.Builder(context, POPUP_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_ticker)
            .setColor(TapeColors.SYMBOL)
            .setContentTitle(hit.title)
            .setContentText(hit.body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(hit.body))
            .setContentIntent(open)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setCategory(NotificationCompat.CATEGORY_STATUS)
            .build()
        manager.notify(id + 20_000, popup)
    }
}
