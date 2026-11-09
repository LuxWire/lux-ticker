package com.lux.ticker.engine

import android.app.Application
import android.app.Notification
import android.graphics.Bitmap
import android.graphics.Canvas
import android.text.Spanned
import android.text.TextUtils.TruncateAt
import android.text.style.ForegroundColorSpan
import android.view.View
import android.widget.TextView
import java.io.File
import kotlin.math.abs
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import com.lux.ticker.R
import com.lux.ticker.data.PaperPalette
import com.lux.ticker.data.ParsedQuote
import com.lux.ticker.data.SessionTag
import com.lux.ticker.data.ShadePalette
import com.lux.ticker.data.StripFormatter
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeColors
import com.lux.ticker.data.TapeModel
import com.lux.ticker.data.Wcag
import com.lux.ticker.data.SymbolSpec
import java.time.Instant

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34], application = Application::class)
class LockScreenNotificationTest {
    @Test fun appLabelIsOneWord() {
        val context = RuntimeEnvironment.getApplication()
        assertEquals("LuxWire Ticker", context.getString(R.string.app_name))
        assertEquals("LuxWire Ticker", context.getString(R.string.channel_name))
        assertEquals("LuxWire Ticker", context.getString(R.string.channel_lock_name))
        assertEquals("LuxWire Ticker", context.getString(R.string.tile_label))
        assertFalse(context.getString(R.string.app_name).contains("Lux Wire"))
    }

    @Test fun publicNotificationShowsPricesNotTheServiceLine() {
        val context = RuntimeEnvironment.getApplication()
        val now = Instant.parse("2026-10-22T21:28:00Z")
        val tape = StripFormatter.build(
            listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("IBIT", "IBIT"), SymbolSpec("MSTR", "MSTR")),
            mapOf(
                "BTC" to ParsedQuote("BTC", 108412.5, 106000.0, 1.84, "USD", now.toEpochMilli(), now.toEpochMilli()),
                "IBIT" to ParsedQuote("IBIT", 46.59, 46.0, 0.76, "USD", now.toEpochMilli(), now.toEpochMilli()),
                "MSTR" to ParsedQuote("MSTR", 352.18, 340.0, 5.12, "USD", now.toEpochMilli(), now.toEpochMilli()),
            ),
            now,
        )
        val notification = LockScreenNotifications.build(context, tape)
        assertEquals(Notification.VISIBILITY_PUBLIC, notification.visibility)
        assertEquals(LockScreenNotifications.CHANNEL_ID, notification.channelId)
        assertTrue((notification.flags and Notification.FLAG_ONGOING_EVENT) != 0)
        assertEquals(Notification.CATEGORY_STATUS, notification.category)

        val title = notification.extras.getCharSequence(Notification.EXTRA_TITLE).toString()
        val text = notification.extras.getCharSequence(Notification.EXTRA_TEXT).toString()
        assertTrue(title.contains("BTC"))
        assertTrue(title.contains("108,412.50"))
        assertTrue(text.contains("IBIT"))
        assertTrue(text.contains("MSTR"))
        assertFalse(title.contains("Status bar"))
        assertFalse(text.contains("lock screen"))

        val content = notification.contentView
        assertNotNull(content)
        assertEquals(R.layout.notification_lock_ticker, content.layoutId)
        val big = notification.bigContentView
        assertNotNull(big)
        assertEquals(R.layout.notification_lock_ticker_big, big.layoutId)
        val host = Robolectric.buildActivity(android.app.Activity::class.java).setup().get()
        val root = content.apply(host, host.findViewById(android.R.id.content))
        val btc = root.findViewById<TextView>(R.id.lock_btc)
        val tapeView = root.findViewById<TextView>(R.id.lock_tape)
        assertTrue(btc.text.toString().contains("BTC"))
        assertTrue(btc.text.toString().contains("108,412.50"))
        assertTrue(tapeView.text.toString().contains("IBIT"))
        assertFalse(tapeView.text.toString().startsWith("BTC"))
        assertEquals(TruncateAt.MARQUEE, tapeView.ellipsize)
        val service = LockScreenNotifications.build(context, tape, foreground = true)
        assertFalse(service.extras.getCharSequence(Notification.EXTRA_TEXT).toString().contains("Status bar"))
        assertEquals(Notification.VISIBILITY_PUBLIC, service.visibility)
        val secret = LockScreenNotifications.build(context, tape, showOnLockScreen = false)
        assertEquals(Notification.VISIBILITY_SECRET, secret.visibility)
        assertEquals(ShadePalette.BG, (root.background as android.graphics.drawable.ColorDrawable).color)
        assertEquals(0, root.paddingTop)
        assertEquals(root.paddingTop, root.paddingBottom)
        val spans = (btc.text as Spanned).getSpans(0, btc.text.length, ForegroundColorSpan::class.java)
        val colors = spans.map { it.foregroundColor }.toSet()
        assertTrue(colors.contains(ShadePalette.SYMBOL))
        assertTrue(colors.contains(ShadePalette.INK))
        assertTrue(colors.contains(ShadePalette.GAIN))
        assertFalse(colors.contains(PaperPalette.GAIN))
        assertFalse(colors.contains(TapeColors.PRICE))
        val dark = LockScreenNotifications.build(context, tape, theme = StripTheme.DARK)
        val darkRoot = dark.contentView.apply(host, host.findViewById(android.R.id.content))
        assertEquals(ShadePalette.BG, (darkRoot.background as android.graphics.drawable.ColorDrawable).color)
        val darkColors = (darkRoot.findViewById<TextView>(R.id.lock_btc).text as Spanned)
            .getSpans(0, darkRoot.findViewById<TextView>(R.id.lock_btc).text.length, ForegroundColorSpan::class.java)
            .map { it.foregroundColor }
            .toSet()
        assertTrue(darkColors.contains(ShadePalette.INK))
        assertTrue(darkColors.contains(ShadePalette.GAIN))
        val bigRoot = big.apply(host, host.findViewById(android.R.id.content))
        assertEquals(ShadePalette.BG, (bigRoot.background as android.graphics.drawable.ColorDrawable).color)
        assertEquals(bigRoot.paddingTop, bigRoot.paddingBottom)

        val pinnedRight = LockScreenNotifications.build(context, tape, pinOnRight = true)
        assertEquals(R.layout.notification_lock_ticker_right, pinnedRight.contentView.layoutId)
        val rightRoot = pinnedRight.contentView.apply(host, host.findViewById(android.R.id.content))
        assertEquals(ShadePalette.BG, (rightRoot.background as android.graphics.drawable.ColorDrawable).color)
        assertEquals(ShadePalette.EDGE, (rightRoot.findViewById<TextView>(R.id.lock_accent).background as android.graphics.drawable.ColorDrawable).color)

        val moved = LockScreenNotifications.build(context, tape, charOffset = 24)
        val movedRoot = moved.contentView.apply(host, host.findViewById(android.R.id.content))
        val movedTape = movedRoot.findViewById<TextView>(R.id.lock_tape)
        val movedBtc = movedRoot.findViewById<TextView>(R.id.lock_btc)
        assertTrue(movedBtc.text.toString().contains("BTC"))
        assertNotEquals(tapeView.text.toString(), movedTape.text.toString())
        assertFalse(LockScreenNotifications.nightMode(context))
        assertEquals(ShadePalette.BG, context.getColor(R.color.lux_ticker_shade_bg))
        assertEquals(ShadePalette.SYMBOL, context.getColor(R.color.lux_ticker_shade_accent))
        assertEquals(ShadePalette.INK, context.getColor(R.color.lux_ticker_shade_ink))
        assertEquals(ShadePalette.EDGE, context.getColor(R.color.lux_ticker_shade_edge))
    }

    @Test
    @Config(qualifiers = "night")
    fun nightShadeUsesTheSameInkBand() {
        val context = RuntimeEnvironment.getApplication()
        assertTrue(LockScreenNotifications.nightMode(context))
        assertEquals(ShadePalette.BG, context.getColor(R.color.lux_ticker_shade_bg))
        assertEquals(ShadePalette.SYMBOL, context.getColor(R.color.lux_ticker_shade_accent))
        assertEquals(ShadePalette.INK, context.getColor(R.color.lux_ticker_shade_ink))
        assertEquals(ShadePalette.GAIN, context.getColor(R.color.lux_ticker_shade_gain))
        assertEquals(ShadePalette.LOSS, context.getColor(R.color.lux_ticker_shade_loss))

        val now = Instant.parse("2026-10-26T21:28:00Z")
        val pinned = StripFormatter.item(
            SymbolSpec("BTC", "BTC"),
            ParsedQuote("BTC", 108412.5, 106000.0, 1.84, "USD", now.toEpochMilli(), now.toEpochMilli()),
            SessionTag.NONE,
            now,
            emphasized = true,
        )
        val afterHours = StripFormatter.item(
            SymbolSpec("MSTR", "MSTR"),
            ParsedQuote("MSTR", 300.0, 340.0, -5.12, "USD", now.toEpochMilli(), now.toEpochMilli()),
            SessionTag.AFTER,
            now,
            emphasized = false,
        )
        val tape = TapeModel(pinned, listOf(afterHours))
        val notification = LockScreenNotifications.build(context, tape, theme = StripTheme.PAPER)
        val host = Robolectric.buildActivity(android.app.Activity::class.java).setup().get()
        val root = notification.contentView.apply(host, host.findViewById(android.R.id.content))
        assertEquals(ShadePalette.BG, (root.background as android.graphics.drawable.ColorDrawable).color)
        val accent = root.findViewById<TextView>(R.id.lock_accent)
        assertEquals(ShadePalette.EDGE, (accent.background as android.graphics.drawable.ColorDrawable).color)
        val btc = root.findViewById<TextView>(R.id.lock_btc)
        val colors = (btc.text as Spanned).getSpans(0, btc.text.length, ForegroundColorSpan::class.java)
            .map { it.foregroundColor }
            .toSet()
        assertTrue(colors.contains(ShadePalette.SYMBOL))
        assertTrue(colors.contains(ShadePalette.INK))
        assertTrue(colors.contains(ShadePalette.GAIN))
        assertFalse(colors.contains(PaperPalette.INK))
        assertFalse(colors.contains(PaperPalette.LOSS))
        assertFalse(colors.contains(TapeColors.SYMBOL))
        val tapeView = root.findViewById<TextView>(R.id.lock_tape)
        val tapeColors = (tapeView.text as Spanned).getSpans(0, tapeView.text.length, ForegroundColorSpan::class.java)
            .map { it.foregroundColor }
            .toSet()
        assertTrue(tapeView.text.toString().contains("AH"))
        assertTrue(tapeColors.contains(ShadePalette.INK))
        assertTrue(tapeColors.contains(ShadePalette.LOSS))
        assertFalse(tapeColors.contains(PaperPalette.INK))
        val big = notification.bigContentView.apply(host, host.findViewById(android.R.id.content))
        assertEquals(ShadePalette.BG, (big.background as android.graphics.drawable.ColorDrawable).color)
        val right = LockScreenNotifications.build(context, tape, pinOnRight = true, theme = StripTheme.LIGHT)
        val rightRoot = right.contentView.apply(host, host.findViewById(android.R.id.content))
        assertEquals(ShadePalette.BG, (rightRoot.background as android.graphics.drawable.ColorDrawable).color)
        assertEquals(ShadePalette.EDGE, notification.color)
        assertEquals(0, root.paddingTop)
        assertEquals(root.paddingTop, root.paddingBottom)
    }

    @Test fun lightNotificationTapeIsCenteredInTheSystemSlot() {
        val bitmap = renderSlot(night = false)
        writeNotificationPng(bitmap, "notification-ticker-light.png")
    }

    @Test
    @Config(qualifiers = "night")
    fun nightNotificationTapeIsCenteredInTheSystemSlot() {
        val bitmap = renderSlot(night = true)
        writeNotificationPng(bitmap, "notification-ticker-night.png")
    }

    private fun renderSlot(night: Boolean): Bitmap {
        val context = RuntimeEnvironment.getApplication()
        assertEquals(night, LockScreenNotifications.nightMode(context))
        val now = Instant.parse("2026-10-22T21:28:00Z")
        val tape = StripFormatter.build(
            listOf(SymbolSpec("BTC", "BTC"), SymbolSpec("IBIT", "IBIT"), SymbolSpec("MSTR", "MSTR")),
            mapOf(
                "BTC" to ParsedQuote("BTC", 108412.5, 106000.0, 1.84, "USD", now.toEpochMilli(), now.toEpochMilli()),
                "IBIT" to ParsedQuote("IBIT", 46.59, 46.0, 0.76, "USD", now.toEpochMilli(), now.toEpochMilli()),
                "MSTR" to ParsedQuote("MSTR", 352.18, 340.0, -5.12, "USD", now.toEpochMilli(), now.toEpochMilli()),
            ),
            now,
        )
        val notification = LockScreenNotifications.build(context, tape, theme = StripTheme.PAPER)
        val host = Robolectric.buildActivity(android.app.Activity::class.java).setup().get()
        val root = notification.contentView.apply(host, host.findViewById(android.R.id.content))
        val density = context.resources.displayMetrics.density
        val width = (360 * density).toInt()
        val slot = (48 * density).toInt()
        root.measure(
            View.MeasureSpec.makeMeasureSpec(width, View.MeasureSpec.EXACTLY),
            View.MeasureSpec.makeMeasureSpec(slot, View.MeasureSpec.EXACTLY),
        )
        root.layout(0, 0, width, slot)
        val btc = root.findViewById<TextView>(R.id.lock_btc)
        val above = btc.top
        val below = slot - btc.bottom
        assertTrue("text should sit below the top of the band", above > 0)
        assertTrue("space under the text should match the space above", abs(above - below) <= (2 * density).toInt().coerceAtLeast(2))
        assertEquals(ShadePalette.BG, (root.background as android.graphics.drawable.ColorDrawable).color)
        val bitmap = Bitmap.createBitmap(width, slot, Bitmap.Config.ARGB_8888)
        root.draw(Canvas(bitmap))
        return bitmap
    }

    private fun writeNotificationPng(bitmap: Bitmap, name: String) {
        val dirs = listOf(
            File(System.getProperty("lux.screenshot.dir") ?: "build/reports/screenshots"),
        )
        dirs.forEach { dir ->
            runCatching {
                if (dir.mkdirs() || dir.isDirectory) {
                    File(dir, name).outputStream().use { out ->
                        bitmap.compress(Bitmap.CompressFormat.PNG, 100, out)
                    }
                }
            }
        }
    }
}
