package com.lux.ticker.data

import android.text.SpannableStringBuilder
import android.text.Spanned
import android.text.style.ForegroundColorSpan

/**
 * Lock-screen copy. Bitcoin stays first. The rest of the list is cut off after
 * [MAX_ITEMS] so the notification stays a ticker instead of a full page.
 */
object LockTickerText {
    const val MAX_ITEMS = 12
    const val EXPANDED_ITEMS = 24

    fun plainPinned(tape: TapeModel): String = StripFormatter.plain(tape.pinned).ifBlank { "BTC" }

    fun plainScrolling(tape: TapeModel, maxItems: Int = MAX_ITEMS): String {
        if (tape.scrolling.isEmpty()) return ""
        val shown = tape.scrolling.take(maxItems.coerceAtLeast(1))
        val body = StripFormatter.plain(shown)
        return if (tape.scrolling.size > shown.size) "$body  ·  …" else body
    }

    fun plain(tape: TapeModel, maxItems: Int = MAX_ITEMS): String {
        val rest = plainScrolling(tape, maxItems)
        val btc = plainPinned(tape)
        return if (rest.isEmpty()) btc else "$btc  ·  $rest"
    }

    fun coloredPinned(tape: TapeModel, theme: StripTheme = StripTheme.PAPER, night: Boolean = false): CharSequence =
        colorize(tape.pinned.spans, theme, night)

    /**
     * The scrolling half of the lock-screen tape, rotated by [charOffset] so a
     * refreshed notification crawls even when the lock screen will not marquee.
     * Bitcoin is not included.
     */
    fun crawledPlain(tape: TapeModel, charOffset: Int): String {
        val unit = loopPlain(tape)
        if (unit.isEmpty()) return ""
        val start = nonNegativeMod(charOffset, unit.length)
        val doubled = unit + unit
        return doubled.substring(start, start + unit.length)
    }

    fun coloredCrawled(tape: TapeModel, charOffset: Int, theme: StripTheme = StripTheme.PAPER, night: Boolean = false): CharSequence {
        val unit = loopColored(tape, theme, night)
        if (unit.isEmpty()) return ""
        val start = nonNegativeMod(charOffset, unit.length)
        val doubled = SpannableStringBuilder(unit).append(unit)
        return doubled.subSequence(start, start + unit.length)
    }

    private fun nonNegativeMod(value: Int, length: Int): Int {
        if (length <= 0) return 0
        val remainder = value % length
        return if (remainder < 0) remainder + length else remainder
    }

    private fun loopPlain(tape: TapeModel): String {
        if (tape.scrolling.isEmpty()) return ""
        return tape.scrolling.joinToString("  ·  ") { StripFormatter.plain(it) } + "  ·  "
    }

    private fun loopColored(tape: TapeModel, theme: StripTheme, night: Boolean): CharSequence {
        if (tape.scrolling.isEmpty()) return ""
        val builder = SpannableStringBuilder()
        tape.scrolling.forEachIndexed { index, item ->
            if (index > 0) appendGap(builder, theme, night)
            builder.append(colorize(item.spans, theme, night))
        }
        appendGap(builder, theme, night)
        return builder
    }

    fun coloredScrolling(tape: TapeModel, maxItems: Int = MAX_ITEMS, theme: StripTheme = StripTheme.PAPER, night: Boolean = false): CharSequence {
        if (tape.scrolling.isEmpty()) return ""
        val shown = tape.scrolling.take(maxItems.coerceAtLeast(1))
        val builder = SpannableStringBuilder()
        shown.forEachIndexed { index, item ->
            if (index > 0) appendGap(builder, theme, night)
            builder.append(colorize(item.spans, theme, night))
        }
        if (tape.scrolling.size > shown.size) {
            appendGap(builder, theme, night)
            val start = builder.length
            builder.append("…")
            builder.setSpan(
                ForegroundColorSpan(TapeColors.forNotification(TapeColors.DOT, theme, night)),
                start,
                builder.length,
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
            )
        }
        return builder
    }

    private fun appendGap(builder: SpannableStringBuilder, theme: StripTheme, night: Boolean) {
        val start = builder.length
        builder.append("  ·  ")
        builder.setSpan(
            ForegroundColorSpan(TapeColors.forNotification(TapeColors.DOT, theme, night)),
            start,
            builder.length,
            Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
        )
    }

    private fun colorize(spans: List<TapeSpan>, theme: StripTheme, night: Boolean): CharSequence {
        val builder = SpannableStringBuilder()
        spans.forEach { span ->
            val start = builder.length
            builder.append(span.text)
            builder.setSpan(
                ForegroundColorSpan(TapeColors.forNotification(span.color, theme, night)),
                start,
                builder.length,
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
            )
        }
        return builder
    }
}
