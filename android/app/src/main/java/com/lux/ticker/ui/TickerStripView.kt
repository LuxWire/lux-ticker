package com.lux.ticker.ui

import android.content.Context
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Typeface
import android.util.AttributeSet
import android.util.TypedValue
import android.view.Choreographer
import android.view.View
import androidx.core.content.res.ResourcesCompat
import kotlin.math.ceil
import com.lux.ticker.R
import com.lux.ticker.data.StripTheme
import com.lux.ticker.data.TapeColors
import com.lux.ticker.data.TapeItem
import com.lux.ticker.data.TapeModel
import com.lux.ticker.data.TapeSpan

/**
 * Full-width tape. Bitcoin is drawn fixed on the left. Every other name scrolls
 * in the remaining width, advanced by Choreographer so the motion stays on frames.
 */
class TickerStripView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
) : View(context, attrs) {
    private val choreographer = Choreographer.getInstance()
    private var typeface: Typeface = Typeface.MONOSPACE
    private var tape: TapeModel = TapeModel.EMPTY
    private var speedPxPerSec = 50f * resources.displayMetrics.density
    private var orangeDividers = false
    private var textSp = StatusBandLayout.TEXT_SP
    private var stripTheme = StripTheme.DARK
    private var scrollRightToLeft = true
    private var pinOnRight = false
    private var scrollingEnabled = true
    private var offset = 0f
    private var lastFrameNs = 0L
    private var running = false
    /** Robolectric runs a newly posted frame callback before doFrame returns, so the loop never ends. */
    private val frameClock = android.os.Build.FINGERPRINT != "robolectric"
    private var scrollBitmap: Bitmap? = null
    private var scrollLoopWidth = 1f
    private var bakedKey = 0
    private var bakedHeight = 0

    internal var pinWidthPx: Int = 0
        private set
    internal var dividerPx: Int = 1
        private set
    internal var gapPx: Int = 0
        private set

    private var statusBand = false
    private var bandLeftPx = 0
    private var bandRightPx = 0
    private var bandLeftDp = -1f
    private var bandRightDp = -1f
    private var scrollLeadPin = false

    private val fill = Paint().apply { style = Paint.Style.FILL }
    private val text = Paint(Paint.ANTI_ALIAS_FLAG or Paint.SUBPIXEL_TEXT_FLAG).apply {
        isLinearText = true
    }

    private val frameCallback = object : Choreographer.FrameCallback {
        override fun doFrame(frameTimeNanos: Long) {
            val previous = lastFrameNs
            lastFrameNs = frameTimeNanos
            if (previous != 0L && scrollingEnabled) {
                val dt = ((frameTimeNanos - previous) / 1_000_000_000f).coerceIn(0f, 0.1f)
                if (scrollLoopWidth > 1f) {
                    val direction = if (scrollRightToLeft) 1f else -1f
                    offset = (offset + dt * speedPxPerSec * direction) % scrollLoopWidth
                    if (offset < 0f) offset += scrollLoopWidth
                }
            }
            invalidate()
            if (running && frameClock) choreographer.postFrameCallback(this)
        }
    }

    init {
        setWillNotDraw(false)
        setBackgroundColor(TapeColors.BG)
        typeface = loadFont()
        text.typeface = typeface
    }

    fun setTape(model: TapeModel) {
        val scrollChanged = tape.scrolling != model.scrolling || (statusBand && tape.pinned != model.pinned)
        tape = model
        if (scrollChanged) {
            bakedKey = 0
        }
        if (width > 0) rebuildPin(height)
        invalidate()
    }

    /** Pixel insets for the status-bar band. The clock and system icons stay clear. */
    fun setStatusBandPx(leftPx: Int, rightPx: Int) {
        statusBand = true
        bandLeftDp = -1f
        bandRightDp = -1f
        if (bandLeftPx == leftPx && bandRightPx == rightPx) return
        bandLeftPx = leftPx.coerceAtLeast(0)
        bandRightPx = rightPx.coerceAtLeast(0)
        bakedKey = 0
        setBackgroundColor(Color.TRANSPARENT)
        invalidate()
    }

    /** Dp insets resolved against this view's width, for the in-app preview. */
    fun setStatusBandDp(leftDp: Float, rightDp: Float) {
        statusBand = true
        bandLeftDp = leftDp
        bandRightDp = rightDp
        bakedKey = 0
        setBackgroundColor(Color.TRANSPARENT)
        invalidate()
    }

    fun setSpeedDpPerSec(dpPerSec: Float) {
        speedPxPerSec = dpPerSec * resources.displayMetrics.density
    }

    /** Orange bar after Bitcoin. Off draws the same dot used between the other names. */
    fun setTextSizeSp(sp: Float) {
        val next = sp.coerceIn(11f, 18f)
        if (textSp == next) return
        textSp = next
        bakedKey = 0
        invalidate()
    }

    fun setStripTheme(theme: StripTheme) {
        if (stripTheme == theme) return
        stripTheme = theme
        bakedKey = 0
        if (!statusBand) setBackgroundColor(TapeColors.band(theme))
        invalidate()
    }

    fun setLightStrip(light: Boolean) {
        setStripTheme(if (light) StripTheme.LIGHT else StripTheme.DARK)
    }

    /** Right to left is the original crawl. Left to right reverses the step. */
    fun setScrollRightToLeft(enabled: Boolean) {
        scrollRightToLeft = enabled
    }

    /** Pin Bitcoin on the right of the status band. Scrolling names use the other side. */
    fun setPinOnRight(enabled: Boolean) {
        if (pinOnRight == enabled) return
        pinOnRight = enabled
        invalidate()
    }

    fun setOrangeDividers(enabled: Boolean) {
        if (orangeDividers == enabled) return
        orangeDividers = enabled
        bakedKey = 0
        if (height > 0) rebuildPin(height)
        invalidate()
    }

    fun setScrollingEnabled(enabled: Boolean) {
        scrollingEnabled = enabled
        if (!enabled) lastFrameNs = 0L
    }

    internal fun useTypeface(face: Typeface) {
        typeface = face
        text.typeface = face
        bakedKey = 0
        invalidate()
    }

    internal fun scrollOriginPx(): Int {
        val (origin, _) = TapeGeometry.origins(width, pinWidthPx, dividerPx, gapPx)
        return origin
    }

    internal fun scrollViewportWidthPx(): Int {
        val (_, scroll) = TapeGeometry.origins(width, pinWidthPx, dividerPx, gapPx)
        return scroll
    }

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()
        running = true
        lastFrameNs = 0L
        if (frameClock) choreographer.postFrameCallback(frameCallback)
    }

    override fun onDetachedFromWindow() {
        running = false
        choreographer.removeFrameCallback(frameCallback)
        super.onDetachedFromWindow()
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        bakedKey = 0
        rebuildPin(h)
    }

    override fun onDraw(canvas: Canvas) {
        val h = height
        val w = width
        if (w <= 0 || h <= 0) return
        if (statusBand) {
            drawStatusBand(canvas, w, h)
            return
        }
        text.clearShadowLayer()
        canvas.drawColor(TapeColors.band(stripTheme))
        if (tape.pinned.symbol.isEmpty()) {
            pinWidthPx = 0
            dividerPx = 0
            gapPx = 0
        } else {
            rebuildPin(h)
            drawPin(canvas, h)
        }
        ensureScroll(h)
        val origin = scrollOriginPx()
        val viewport = scrollViewportWidthPx()
        val bitmap = scrollBitmap
        if (bitmap == null || viewport <= 0 || scrollLoopWidth <= 1f) return
        canvas.save()
        canvas.clipRect(origin, 0, origin + viewport, h)
        val shift = offset
        canvas.drawBitmap(bitmap, origin - shift, 0f, null)
        canvas.drawBitmap(bitmap, origin - shift + scrollLoopWidth, 0f, null)
        canvas.restore()
    }

    private fun drawStatusBand(canvas: Canvas, w: Int, h: Int) {
        val (left, right) = currentPads(w)
        val midLeft = left.coerceAtLeast(0)
        val midRight = (w - right).coerceAtLeast(midLeft)
        // Dark paints no band. The launcher overlay stays on that path so wallpaper shows through.
        if (TapeColors.statusBandFills(stripTheme)) {
            fill.color = TapeColors.band(stripTheme)
            canvas.drawRect(midLeft.toFloat(), 0f, midRight.toFloat(), h.toFloat(), fill)
        }
        val mid = midRight - midLeft
        if (mid <= 0) return
        val textPx = TypedValue.applyDimension(
            TypedValue.COMPLEX_UNIT_SP,
            textSp,
            resources.displayMetrics,
        )
        val paint = textPaint(textPx)
        applyStatusShadow(paint)
        if (tape.pinned.symbol.isEmpty()) {
            pinWidthPx = 0
            dividerPx = 0
            gapPx = 0
            scrollLeadPin = false
            ensureScroll(h, textPx, tape.scrolling)
            drawScroll(canvas, midLeft, midRight - midLeft, h)
            paint.clearShadowLayer()
            return
        }
        val pinContent = measureSpans(tape.pinned.spans, paint)
        val density = resources.displayMetrics.density
        val dot = "  ·  "
        val dotWidth = if (orangeDividers) 0f else measureText(dot, paint)
        val divider = if (orangeDividers) density.toInt().coerceAtLeast(1) else 0
        val gap = if (orangeDividers) (4f * density).toInt() else 0
        val pinBlock = ceil(pinContent + dotWidth + gap + divider).toInt().coerceAtLeast(1)
        val fits = StatusBandLayout.pinFits(pinBlock, mid)
        scrollLeadPin = !fits
        pinWidthPx = if (fits) pinBlock else 0
        dividerPx = if (fits) divider else 0
        gapPx = if (fits) gap else 0
        val pinLeft = if (fits && pinOnRight) midRight - pinBlock else midLeft
        if (fits) {
            drawBandPin(canvas, pinLeft, h, paint, pinContent, dot, dotWidth, gap, divider)
        }
        ensureScroll(h, textPx, scrollItems())
        val scrollOrigin = if (fits && pinOnRight) midLeft else midLeft + pinWidthPx
        val scrollWidth = if (fits && pinOnRight) pinLeft - midLeft else midRight - scrollOrigin
        drawScroll(canvas, scrollOrigin, scrollWidth, h)
        paint.clearShadowLayer()
    }

    private fun drawBandPin(
        canvas: Canvas,
        pinLeft: Int,
        h: Int,
        paint: Paint,
        pinContent: Float,
        dot: String,
        dotWidth: Float,
        gap: Int,
        divider: Int,
    ) {
        val alpha = if (tape.pinned.stale) TapeColors.STALE_ALPHA else 255
        val separator = if (orangeDividers) (gap + divider).toFloat() else dotWidth
        val textX = if (pinOnRight) pinLeft + separator else pinLeft.toFloat()
        val markX = if (pinOnRight) pinLeft.toFloat() else pinLeft + pinContent
        drawSpans(canvas, tape.pinned.spans, textX, h, paint, alpha)
        if (orangeDividers) {
            fill.color = TapeColors.forStrip(TapeColors.SYMBOL, stripTheme)
            val line = if (pinOnRight) pinLeft + gap / 2f else markX + gap / 2f
            canvas.drawRect(line, h * 0.22f, line + divider, h * 0.78f, fill)
        } else {
            paint.color = TapeColors.forStrip(TapeColors.DOT, stripTheme)
            paint.alpha = 255
            canvas.drawText(dot, markX, baseline(h, paint), paint)
        }
    }

    private fun drawScroll(canvas: Canvas, origin: Int, viewport: Int, h: Int) {
        val bitmap = scrollBitmap
        if (bitmap == null || viewport <= 0 || scrollLoopWidth <= 1f) return
        canvas.save()
        canvas.clipRect(origin, 0, origin + viewport, h)
        canvas.drawBitmap(bitmap, origin - offset, 0f, null)
        canvas.drawBitmap(bitmap, origin - offset + scrollLoopWidth, 0f, null)
        canvas.restore()
    }

    private fun currentPads(widthPx: Int): Pair<Int, Int> {
        if (bandLeftDp >= 0f) {
            return StatusBandLayout.pads(widthPx, bandLeftDp, bandRightDp, resources.displayMetrics.density)
        }
        return bandLeftPx to bandRightPx
    }

    private fun scrollItems(): List<TapeItem> {
        if (statusBand && scrollLeadPin) return listOf(tape.pinned) + tape.scrolling
        return tape.scrolling
    }

    private fun rebuildPin(h: Int) {
        if (h <= 0) return
        val density = resources.displayMetrics.density
        dividerPx = if (orangeDividers) density.toInt().coerceAtLeast(1) else 0
        gapPx = (8f * density).toInt()
        val paint = textPaint(baseTextPx(h) * PIN_SCALE)
        val content = measureSpans(tape.pinned.spans, paint)
        val pad = 8f * density
        val cap = 2f * density
        pinWidthPx = (cap + pad + content + pad).toInt().coerceAtLeast((48f * density).toInt())
    }

    private fun drawPin(canvas: Canvas, h: Int) {
        val density = resources.displayMetrics.density
        val cap = 2f * density
        fill.color = TapeColors.forStrip(TapeColors.PIN_WELL, stripTheme)
        canvas.drawRect(0f, 0f, (pinWidthPx + dividerPx).toFloat(), h.toFloat(), fill)
        fill.color = TapeColors.forStrip(TapeColors.SYMBOL_PIN, stripTheme)
        canvas.drawRect(0f, 0f, cap, h.toFloat(), fill)
        if (orangeDividers && dividerPx > 0) {
            fill.color = TapeColors.forStrip(TapeColors.DIVIDER, stripTheme)
            canvas.drawRect(pinWidthPx.toFloat(), 0f, (pinWidthPx + dividerPx).toFloat(), h.toFloat(), fill)
        }
        val paint = textPaint(baseTextPx(h) * PIN_SCALE)
        val alpha = if (tape.pinned.stale) TapeColors.STALE_ALPHA else 255
        drawSpans(canvas, tape.pinned.spans, cap + 8f * density, h, paint, alpha)
    }

    private fun ensureScroll(h: Int, textPx: Float = baseTextPx(h), items: List<TapeItem> = tape.scrolling) {
        val key = items.hashCode() xor textPx.toBits() xor (if (statusBand) 1 else 0) xor (stripTheme.ordinal shl 2)
        if (scrollBitmap != null && bakedKey == key && bakedHeight == h) return
        val old = scrollBitmap
        scrollBitmap = null
        old?.recycle()
        bakedKey = key
        bakedHeight = h
        if (items.isEmpty() || h <= 0) {
            scrollLoopWidth = 1f
            return
        }
        val paint = textPaint(textPx)
        applyStatusShadow(paint)
        val sep = "  ·  "
        val sepWidth = measureText(sep, paint)
        var contentWidth = 0f
        val itemWidths = items.map { item ->
            val width = measureSpans(item.spans, paint)
            contentWidth += width + sepWidth
            width
        }
        val bitmapWidth = contentWidth.toInt().coerceIn(1, 80_000) + 2
        val bitmap = Bitmap.createBitmap(bitmapWidth, h, Bitmap.Config.ARGB_8888)
        val buffer = Canvas(bitmap)
        if (!statusBand) buffer.drawColor(TapeColors.band(stripTheme))
        var x = 0f
        items.forEachIndexed { index, item ->
            val alpha = if (item.stale) TapeColors.STALE_ALPHA else 255
            x = drawSpans(buffer, item.spans, x, h, paint, alpha)
            paint.color = TapeColors.forStrip(TapeColors.DOT, stripTheme)
            paint.alpha = 255
            val baseline = baseline(h, paint)
            buffer.drawText(sep, x, baseline, paint)
            x += sepWidth
            if (x > bitmapWidth) return@forEachIndexed
            itemWidths[index]
        }
        scrollBitmap = bitmap
        scrollLoopWidth = contentWidth.coerceAtLeast(1f)
    }

    private fun drawSpans(
        canvas: Canvas,
        spans: List<TapeSpan>,
        startX: Float,
        h: Int,
        paint: Paint,
        alpha: Int,
    ): Float {
        var x = startX
        val baseline = baseline(h, paint)
        spans.forEach { span ->
            paint.color = TapeColors.forStrip(span.color, stripTheme)
            paint.alpha = alpha
            canvas.drawText(span.text, x, baseline, paint)
            x += measureText(span.text, paint)
        }
        paint.alpha = 255
        return x
    }

    private fun measureSpans(spans: List<TapeSpan>, paint: Paint): Float =
        spans.sumOf { measureText(it.text, paint).toDouble() }.toFloat()

    private fun measureText(value: String, paint: Paint): Float {
        val measured = paint.measureText(value)
        if (measured > 0f) return measured
        return value.length * paint.textSize * 0.55f
    }

    /** Light prices on a clear strip need a dark halo. Paper and light strips do not. */
    private fun applyStatusShadow(paint: Paint) {
        if (statusBand && stripTheme == StripTheme.DARK) {
            paint.setShadowLayer(resources.displayMetrics.density, 0f, 0f, 0x99000000.toInt())
        } else {
            paint.clearShadowLayer()
        }
    }

    private fun textPaint(textSizePx: Float): Paint {
        text.typeface = typeface
        text.textSize = textSizePx
        text.alpha = 255
        return text
    }

    private fun baseTextPx(h: Int): Float = (h * 0.62f).coerceAtLeast(8f)

    private fun baseline(h: Int, paint: Paint): Float {
        val metrics = paint.fontMetrics
        return h / 2f - (metrics.ascent + metrics.descent) / 2f
    }

    private fun loadFont(): Typeface {
        return try {
            ResourcesCompat.getFont(context, R.font.jetbrains_mono_bold) ?: Typeface.MONOSPACE
        } catch (t: Throwable) {
            Typeface.MONOSPACE
        }
    }

    private companion object {
        const val PIN_SCALE = 1.12f
    }
}
