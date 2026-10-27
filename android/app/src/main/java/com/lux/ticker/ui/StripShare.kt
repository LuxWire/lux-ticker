package com.lux.ticker.ui

import android.content.ClipData
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Typeface
import androidx.core.content.FileProvider
import com.lux.ticker.data.TapeColors
import java.io.File

object StripShare {
    fun share(context: Context, strip: TickerStripView) {
        val width = strip.width.coerceAtLeast(1)
        val height = strip.height.coerceAtLeast(1)
        val shot = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        strip.draw(Canvas(shot))
        val image = withFooter(shot)
        val dir = File(context.cacheDir, "share")
        dir.mkdirs()
        val file = File(dir, "lux-ticker.png")
        file.outputStream().use { image.compress(Bitmap.CompressFormat.PNG, 100, it) }
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.files", file)
        val send = Intent(Intent.ACTION_SEND).apply {
            type = "image/png"
            putExtra(Intent.EXTRA_STREAM, uri)
            putExtra(Intent.EXTRA_TEXT, "@LuxWire")
            clipData = ClipData.newRawUri("LuxWire Ticker", uri)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
        context.startActivity(Intent.createChooser(send, "Share LuxWire Ticker"))
    }

    private fun withFooter(strip: Bitmap): Bitmap {
        val footer = (strip.width * 0.16f).toInt().coerceIn(36, 96)
        val out = Bitmap.createBitmap(strip.width, strip.height + footer, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(out)
        canvas.drawColor(TapeColors.PAPER)
        canvas.drawBitmap(strip, 0f, 0f, null)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = TapeColors.SYMBOL
            textSize = footer * 0.42f
            typeface = Typeface.create(Typeface.MONOSPACE, Typeface.BOLD)
        }
        canvas.drawText("@LuxWire", footer * 0.35f, strip.height + footer * 0.68f, paint)
        return out
    }
}
