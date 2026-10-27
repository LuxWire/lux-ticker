package com.lux.ticker.luxwire

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.view.View
import android.widget.RemoteViews
import androidx.core.app.NotificationCompat
import com.lux.ticker.R
import com.lux.ticker.data.TapeColors

/** Paper toast. Status-bar icon is the white LW. The shade shows the orange LW tile. */
object LuxWireNotifier {
    const val CHANNEL_ID = "lux_wire_posts"

    fun ensureChannel(context: Context) {
        val manager = context.getSystemService(NotificationManager::class.java)
        val channel = NotificationChannel(
            CHANNEL_ID,
            context.getString(R.string.channel_lux_wire_name),
            NotificationManager.IMPORTANCE_DEFAULT,
        ).apply {
            description = context.getString(R.string.channel_lux_wire_description)
            setShowBadge(true)
            lockscreenVisibility = Notification.VISIBILITY_PUBLIC
        }
        manager.createNotificationChannel(channel)
    }

    fun show(context: Context, post: LuxWirePost, subscribed: Boolean, image: Bitmap? = null) {
        ensureChannel(context)
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.notify(notificationId(post.id), build(context, post, subscribed, image))
    }

    fun showDigest(context: Context, posts: List<LuxWirePost>, subscribed: Boolean) {
        if (posts.isEmpty()) return
        if (posts.size == 1) {
            show(context, posts.first(), subscribed, null)
            return
        }
        ensureChannel(context)
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.notify(LuxWireDigest.NOTIFICATION_ID, buildDigest(context, posts, subscribed))
    }

    fun build(context: Context, post: LuxWirePost, subscribed: Boolean = true, image: Bitmap? = null): Notification {
        val title = LuxWireCopy.contentTitle(post, subscribed)
        val body = post.text.ifBlank { "New post" }
        val open = pendingView(context, LuxWireCopy.openTarget(post, subscribed), notificationId(post.id))
        val share = sharePending(context, LuxWireCopy.shareText(post), notificationId(post.id) + 20_000)
        val large = image ?: BitmapFactory.decodeResource(context.resources, R.drawable.ic_lux_wire)
        val collapsed = chrome(context, title, body, post.subscriberOnly && !subscribed)
        val builder = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_ticker)
            .setLargeIcon(large)
            .setColor(TapeColors.SYMBOL)
            .setContentTitle(title)
            .setContentText(body)
            .setCustomContentView(collapsed)
            .setContentIntent(open)
            .setAutoCancel(true)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setCategory(NotificationCompat.CATEGORY_SOCIAL)
            .setOnlyAlertOnce(false)
            .addAction(0, "Share", share)
        if (image != null) {
            builder.setStyle(
                NotificationCompat.BigPictureStyle()
                    .bigPicture(image)
                    .setBigContentTitle(title)
                    .setSummaryText(body),
            )
        } else {
            builder.setStyle(NotificationCompat.BigTextStyle().bigText(body))
        }
        return builder.build()
    }

    fun buildDigest(context: Context, posts: List<LuxWirePost>, subscribed: Boolean): Notification {
        val title = LuxWireCopy.digestTitle(posts.size)
        val body = LuxWireCopy.digestBody(posts)
        val newest = posts.last()
        val gated = posts.any { it.subscriberOnly && !subscribed }
        val open = pendingView(context, LuxWireCopy.openTarget(newest, subscribed), LuxWireDigest.NOTIFICATION_ID)
        val share = sharePending(context, LuxWireCopy.shareText(newest), LuxWireDigest.NOTIFICATION_ID + 1)
        val collapsed = chrome(context, title, body, gated)
        return NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_ticker)
            .setLargeIcon(BitmapFactory.decodeResource(context.resources, R.drawable.ic_lux_wire))
            .setColor(TapeColors.SYMBOL)
            .setContentTitle(title)
            .setContentText(body)
            .setCustomContentView(collapsed)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setContentIntent(open)
            .setAutoCancel(true)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setCategory(NotificationCompat.CATEGORY_SOCIAL)
            .setOnlyAlertOnce(false)
            .addAction(0, "Share", share)
            .build()
    }

    fun notificationId(postId: String): Int = 2_000 + (postId.hashCode() and 0x0FFF)

    private fun chrome(context: Context, title: String, body: String, showBadge: Boolean): RemoteViews {
        val views = RemoteViews(context.packageName, R.layout.notification_lux_wire)
        views.setTextViewText(R.id.lux_title, title)
        views.setTextViewText(R.id.lux_body, body)
        views.setViewVisibility(R.id.lux_badge, if (showBadge) View.VISIBLE else View.GONE)
        return views
    }

    private fun pendingView(context: Context, url: String, request: Int): PendingIntent =
        PendingIntent.getActivity(
            context,
            request,
            LuxWirePosts.viewIntent(url),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )

    private fun sharePending(context: Context, text: String, request: Int): PendingIntent {
        val send = Intent(Intent.ACTION_SEND).apply {
            type = "text/plain"
            putExtra(Intent.EXTRA_TEXT, text)
        }
        val chooser = Intent.createChooser(send, "Share").addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        return PendingIntent.getActivity(
            context,
            request,
            chooser,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
    }
}
