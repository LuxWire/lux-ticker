package com.lux.ticker.widget

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.text.SpannableStringBuilder
import android.text.Spanned
import android.text.style.ForegroundColorSpan
import android.util.TypedValue
import android.view.View
import android.widget.RemoteViews
import com.lux.ticker.MainActivity
import com.lux.ticker.R
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeColors
import com.lux.ticker.data.TapeItem
import com.lux.ticker.data.TapeModel
import com.lux.ticker.data.TapeSpan

object WidgetRenderer {
    fun updateAll(
        context: Context,
        tape: TapeModel,
        orangeDividers: Boolean = false,
        theme: StripTheme = StripTheme.DARK,
        textSp: Float = 13f,
        pinOnRight: Boolean = false,
    ) {
        val manager = AppWidgetManager.getInstance(context)
        val wide = if (pinOnRight) R.layout.widget_ticker_pin_right else R.layout.widget_ticker
        listOf(
            TickerWidgetProvider::class.java to wide,
            TickerWidgetNarrow::class.java to R.layout.widget_ticker_1x2,
            TickerWidgetSquare::class.java to R.layout.widget_ticker_2x2,
        ).forEach { (type, layout) ->
            val ids = manager.getAppWidgetIds(ComponentName(context, type))
            if (ids.isNotEmpty()) bind(context, manager, ids, tape, orangeDividers, theme, textSp, layout)
        }
    }

    fun bind(
        context: Context,
        manager: AppWidgetManager,
        ids: IntArray,
        tape: TapeModel,
        orangeDividers: Boolean = false,
        theme: StripTheme = StripTheme.DARK,
        textSp: Float = 13f,
        layout: Int = R.layout.widget_ticker,
    ) {
        val light = theme != StripTheme.DARK
        val views = RemoteViews(context.packageName, layout)
        val pinned = tape.pinned.symbol.isNotEmpty()
        views.setViewVisibility(R.id.btc, if (pinned) View.VISIBLE else View.GONE)
        if (pinned) views.setTextViewText(R.id.btc, colorizeItem(tape.pinned, light))
        val stacked = layout == R.layout.widget_ticker_2x2
        val scrolling = colorizeItems(tape.scrolling, if (stacked) "\n" else "  ·  ", light)
        val tapeText = if (stacked || orangeDividers || tape.scrolling.isEmpty() || !pinned) {
            scrolling
        } else {
            val dot = SpannableStringBuilder("  ·  ")
            dot.setSpan(
                ForegroundColorSpan(TapeColors.forStrip(TapeColors.DOT, light)),
                0,
                dot.length,
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
            )
            dot.append(scrolling)
        }
        views.setTextViewText(R.id.tape, tapeText)
        views.setViewVisibility(
            R.id.pin_divider,
            if (orangeDividers && pinned) View.VISIBLE else View.GONE,
        )
        views.setTextViewTextSize(R.id.btc, TypedValue.COMPLEX_UNIT_SP, textSp)
        views.setTextViewTextSize(R.id.tape, TypedValue.COMPLEX_UNIT_SP, textSp)
        views.setInt(R.id.widget_root, "setBackgroundColor", TapeColors.band(theme))
        if (!stacked) views.setBoolean(R.id.tape, "setSelected", true)
        val open = PendingIntent.getActivity(
            context,
            7,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        views.setOnClickPendingIntent(R.id.widget_root, open)
        views.setOnClickPendingIntent(R.id.btc, open)
        views.setOnClickPendingIntent(R.id.tape, open)
        manager.updateAppWidget(ids, views)
    }

    private fun colorizeItem(item: TapeItem, light: Boolean): CharSequence {
        val builder = SpannableStringBuilder()
        append(builder, item.spans, light)
        return builder
    }

    private fun colorizeItems(items: List<TapeItem>, separator: String, light: Boolean): CharSequence {
        val builder = SpannableStringBuilder()
        items.forEachIndexed { index, item ->
            if (index > 0) {
                val start = builder.length
                builder.append(separator)
                builder.setSpan(
                    ForegroundColorSpan(TapeColors.forStrip(TapeColors.DOT, light)),
                    start,
                    builder.length,
                    Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
                )
            }
            append(builder, item.spans, light)
        }
        return builder
    }

    private fun append(builder: SpannableStringBuilder, spans: List<TapeSpan>, light: Boolean) {
        spans.forEach { span ->
            val start = builder.length
            builder.append(span.text)
            builder.setSpan(
                ForegroundColorSpan(TapeColors.forStrip(span.color, light)),
                start,
                builder.length,
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
            )
        }
    }
}
