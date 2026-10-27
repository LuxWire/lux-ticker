package com.lux.ticker.ui

import android.graphics.Bitmap
import android.os.Looper
import android.view.View.MeasureSpec
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.lux.ticker.LuxApp
import com.lux.ticker.data.BtcChart
import com.lux.ticker.data.BtcPoint
import com.lux.ticker.data.BtcPreset
import com.lux.ticker.data.ChartStyle
import com.lux.ticker.data.PaperPalette
import com.lux.ticker.data.ParsedQuote
import com.lux.ticker.data.YahooBar
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import org.robolectric.Shadows
import java.io.File
import java.io.FileOutputStream

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34], application = LuxApp::class, qualifiers = "w360dp-h640dp-xxhdpi")
class PlayListingScreenshots {
    @Test fun home() = shot("01-home") {
        val viewModel: HomeViewModel = viewModel()
        LuxApp.container(this).quotes.let { repo ->
            sampleQuotes().forEach { repo.upsert(it) }
        }
        HomeScreen(
            viewModel = viewModel,
            overlayGranted = true,
            notificationsGranted = true,
            onRequestOverlay = {},
            onRequestNotifications = {},
            onEdit = {},
            onSettings = {},
            onChart = {},
        )
    }

    @Test fun lineChart() = shot("02-chart-line") { ChartFrame(ChartStyle.LINE) }

    @Test fun candleChart() = shot("03-chart-candles") { ChartFrame(ChartStyle.CANDLES) }

    @Test fun tickers() = shot("04-tickers") { EditorRoute(onBack = {}, onChart = {}) }

    @Test fun settings() = shot("05-settings") { SettingsMenuRoute(onBack = {}, onOpen = {}) }

    @Test fun display() = shot("06-display") { SettingsDisplayRoute(onBack = {}) }

    @Test fun whatsNew() = shot("07-whats-new") { SettingsWhatsNewRoute(onBack = {}) }

    @Test fun about() = shot("08-about") { SettingsAboutRoute(onBack = {}) }

    private fun shot(name: String, content: @Composable ScreenshotActivity.() -> Unit) {
        val controller = Robolectric.buildActivity(ScreenshotActivity::class.java).setup()
        val activity = controller.get()
        activity.setContent { LuxTheme { activity.content() } }
        pump()
        val view = activity.window.decorView
        val width = 1080
        val height = 1920
        view.measure(
            MeasureSpec.makeMeasureSpec(width, MeasureSpec.EXACTLY),
            MeasureSpec.makeMeasureSpec(height, MeasureSpec.EXACTLY),
        )
        view.layout(0, 0, width, height)
        view.findViewById<android.view.ViewGroup>(android.R.id.content)?.let { content ->
            findStrip(content)?.setSpeedDpPerSec(0f)
        }
        pump()
        val raw = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        val canvas = android.graphics.Canvas(raw)
        canvas.drawColor(PaperPalette.BG)
        view.draw(canvas)
        val flat = Bitmap.createBitmap(raw.width, raw.height, Bitmap.Config.RGB_565).let { rgb ->
            android.graphics.Canvas(rgb).drawBitmap(raw, 0f, 0f, null)
            rgb.copy(Bitmap.Config.ARGB_8888, false)
        }
        assertTrue("$name is too small: ${flat.width}x${flat.height}", flat.width >= 320 && flat.height >= 320)
        val pixels = IntArray(flat.width * flat.height)
        flat.getPixels(pixels, 0, flat.width, 0, 0, flat.width, flat.height)
        assertTrue("$name looks blank", pixels.toSet().size > 8)
        val dir = File(System.getProperty("lux.screenshot.dir") ?: "build/reports/screenshots")
        dir.mkdirs()
        FileOutputStream(File(dir, "$name.png")).use { out ->
            assertTrue(flat.compress(Bitmap.CompressFormat.PNG, 100, out))
        }
    }

    private fun pump() {
        val shadow = Shadows.shadowOf(Looper.getMainLooper())
        repeat(40) {
            if (shadow.isIdle) return
            shadow.runToNextTask()
        }
    }

    private fun findStrip(view: android.view.View): TickerStripView? {
        if (view is TickerStripView) return view
        if (view is android.view.ViewGroup) {
            for (index in 0 until view.childCount) {
                findStrip(view.getChildAt(index))?.let { return it }
            }
        }
        return null
    }

    private fun sampleQuotes(): List<ParsedQuote> {
        val now = 1_761_200_000_000L
        fun q(symbol: String, price: Double, change: Double, currency: String) =
            ParsedQuote(symbol, price, price, change, currency, now, now)
        return listOf(
            q("BTC", 108412.50, 1.84, "USD"),
            q("IBIT", 46.59, 0.76, "USD"),
            q("MSTR", 352.18, 5.12, "USD"),
            q("GLD", 242.15, -0.31, "USD"),
            q("NVDA", 195.85, 0.80, "USD"),
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
private fun ChartFrame(style: ChartStyle) {
    val paper = Color(PaperPalette.BG)
    val panel = Color(PaperPalette.PANEL)
    val ink = Color(PaperPalette.INK)
    val body = Color(PaperPalette.BODY)
    val series = Color(PaperPalette.ACCENT)
    val down = Color(BtcChart.CANDLE_DOWN)
    val start = 1_761_100_000_000L
    val points = (0 until 48).map { index ->
        val wave = kotlin.math.sin(index / 6.0) * 1800 + index * 40
        BtcPoint(start + index * 60_000L, 104_000.0 + wave)
    }
    val bars = points.mapIndexed { index, point ->
        val open = point.price - if (index % 3 == 0) 220 else -140
        YahooBar(
            epochMillis = point.epochMillis,
            open = open,
            high = maxOf(open, point.price) + 180,
            low = minOf(open, point.price) - 160,
            close = point.price,
        )
    }
    Scaffold(
        containerColor = paper,
        topBar = {
            TopAppBar(
                title = { Text("Bitcoin") },
                navigationIcon = { TextButton(onClick = {}) { Text("Back", color = body) } },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = paper,
                    titleContentColor = body,
                    navigationIconContentColor = body,
                ),
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 16.dp, vertical = 8.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            LuxOutlinedField(
                value = "",
                onValueChange = {},
                modifier = Modifier.fillMaxWidth(),
                forcePaper = true,
                label = { Text("Ticker") },
            )
            Text("BTC/USD", color = ink)
            Text("$108,412.50", color = body)
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                BtcPreset.entries.forEach { preset ->
                    FilterChip(
                        selected = preset == BtcPreset.DAY,
                        onClick = {},
                        label = { Text(preset.label) },
                        modifier = Modifier.heightIn(min = 48.dp),
                        colors = paperChipColors(panel, ink, series),
                    )
                }
            }
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(
                    selected = style == ChartStyle.LINE,
                    onClick = {},
                    label = { Text("Line") },
                    modifier = Modifier.heightIn(min = 48.dp),
                    colors = paperChipColors(panel, ink, series),
                )
                FilterChip(
                    selected = style == ChartStyle.CANDLES,
                    onClick = {},
                    label = { Text("Candles") },
                    modifier = Modifier.heightIn(min = 48.dp),
                    colors = paperChipColors(panel, ink, series),
                )
            }
            if (style == ChartStyle.CANDLES) {
                CandleChart(
                    bars = bars,
                    paper = paper,
                    ink = ink,
                    up = series,
                    down = down,
                    selected = null,
                    onScrub = {},
                    description = "Bitcoin candles",
                )
            } else {
                BtcLineChart(
                    points = points,
                    paper = paper,
                    ink = ink,
                    series = series,
                    selected = null,
                    onScrub = {},
                    description = "Bitcoin chart",
                )
            }
        }
    }
}
