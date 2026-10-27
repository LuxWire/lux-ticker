package com.lux.ticker.data

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.longOrNull
import java.net.URLEncoder
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit
import java.util.Locale

data class BtcPoint(val epochMillis: Long, val price: Double)

data class ChartMark(val price: Double, val epochMillis: Long)

data class ChartRangeMarks(val high: ChartMark?, val low: ChartMark?)

data class YahooBar(
    val epochMillis: Long,
    val close: Double,
    val high: Double,
    val low: Double,
    val open: Double = close,
)

object ChartSymbols {
    const val BTC = "BTC"

    fun isBtc(symbol: String): Boolean = symbol.equals(BTC, ignoreCase = true)

    fun normalize(raw: String): String = raw.trim().uppercase(Locale.US).ifBlank { BTC }

    fun yahooSymbol(symbol: String): String = if (isBtc(symbol)) "BTC-USD" else normalize(symbol)

    fun title(symbol: String): String = if (isBtc(symbol)) "Bitcoin" else normalize(symbol)

    fun pair(symbol: String): String = if (isBtc(symbol)) "BTC/USD" else normalize(symbol)

    fun sourceYahoo(symbol: String): String =
        if (isBtc(symbol)) BtcChart.SOURCE_YAHOO else "Yahoo ${normalize(symbol)}"
}

enum class BtcPreset(val label: String) {
    HOUR("1H"),
    DAY("1D"),
    WEEK("1W"),
    MONTH("1M"),
    SIX_MONTH("6M"),
    YTD("YTD"),
    YEAR("1Y"),
    ALL("ALL"),
}

data class BtcWindow(
    val startMillis: Long,
    val endMillis: Long,
    val label: String,
    val fullHistory: Boolean,
)

sealed class BtcFetch {
    data class Yahoo(
        val startSec: Long,
        val endSec: Long,
        val interval: String,
        val symbol: String = "BTC-USD",
    ) : BtcFetch()
    data object BlockchainAll : BtcFetch()
    data class BlockchainSpan(val startDate: String, val days: Int) : BtcFetch()
}

data class BtcSeries(
    val points: List<BtcPoint>,
    val source: String,
    val high: ChartMark? = null,
    val low: ChartMark? = null,
    val bars: List<YahooBar> = emptyList(),
)

enum class ChartStyle { LINE, CANDLES }

object BtcChart {
    val EARLIEST: LocalDate = LocalDate.of(2009, 1, 3)
    val YAHOO_FIRST: Instant = Instant.parse("2014-09-17T00:00:00Z")
    const val PAPER = PaperPalette.BG
    const val INK = PaperPalette.INK
    const val SERIES = PaperPalette.ACCENT
    /** Up candles use the article orange. Down candles use paper ink. */
    const val CANDLE_UP = PaperPalette.ACCENT
    const val CANDLE_DOWN = PaperPalette.INK
    const val SOURCE_YAHOO = "Yahoo BTC-USD"
    const val SOURCE_CHAIN = "Blockchain.com market price"
    const val SOURCE_BOTH = "Blockchain.com through 2014, then Yahoo BTC-USD"
    const val LOAD_ERROR = "Couldn't load Bitcoin prices. Try again."

    fun loadError(symbol: String): String =
        if (ChartSymbols.isBtc(symbol)) LOAD_ERROR else "Couldn't load ${ChartSymbols.normalize(symbol)} prices. Try again."
    const val RANGE_ERROR = "End date is before the start date."

    private val dayLabel = DateTimeFormatter.ofPattern("MMM d, yyyy", Locale.US)

    fun preset(preset: BtcPreset, now: Instant, zone: ZoneId): BtcWindow {
        val end = now.toEpochMilli()
        return when (preset) {
            BtcPreset.HOUR -> window(now.minus(60, ChronoUnit.MINUTES).toEpochMilli(), end, "Last 60 minutes")
            BtcPreset.DAY -> window(now.minus(24, ChronoUnit.HOURS).toEpochMilli(), end, "Last 24 hours")
            BtcPreset.WEEK -> window(now.minus(7, ChronoUnit.DAYS).toEpochMilli(), end, "Last 7 days")
            BtcPreset.MONTH -> window(now.minus(30, ChronoUnit.DAYS).toEpochMilli(), end, "Last 30 days")
            BtcPreset.SIX_MONTH -> window(now.atZone(zone).toLocalDate().minusMonths(6).atStartOfDay(zone).toInstant().toEpochMilli(), end, "Last 6 months")
            BtcPreset.YTD -> {
                val startDate = LocalDate.of(now.atZone(zone).year, 1, 1)
                window(
                    startDate.atStartOfDay(zone).toInstant().toEpochMilli(),
                    end,
                    "YTD · from ${dayLabel.format(startDate)} · device time",
                )
            }
            BtcPreset.YEAR -> window(now.minus(365, ChronoUnit.DAYS).toEpochMilli(), end, "Last 365 days")
            BtcPreset.ALL -> window(
                EARLIEST.atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli(),
                end,
                "All · from ${dayLabel.format(EARLIEST)}",
                fullHistory = true,
            )
        }
    }

    /** Null when the range is backwards or empty. */
    fun custom(from: LocalDate, to: LocalDate, now: Instant, zone: ZoneId): BtcWindow? {
        if (to.isBefore(from)) return null
        if (from.isBefore(EARLIEST) || to.isBefore(EARLIEST)) return null
        val start = from.atStartOfDay(zone).toInstant()
        val endOfDay = to.plusDays(1).atStartOfDay(zone).toInstant().minusMillis(1)
        val end = if (endOfDay.isAfter(now)) now else endOfDay
        if (!end.isAfter(start)) return null
        val label = "${dayLabel.format(from)} – ${dayLabel.format(to)}"
        return window(start.toEpochMilli(), end.toEpochMilli(), label)
    }

    fun refreshMillis(window: BtcWindow): Long {
        val days = (window.endMillis - window.startMillis) / 86_400_000.0
        return if (days <= 31.0) 60_000L else 5 * 60_000L
    }

    fun intervalFor(startMillis: Long, endMillis: Long): String {
        val hours = (endMillis - startMillis) / 3_600_000.0
        return when {
            hours <= 1.5 -> "1m"
            hours <= 36.0 -> "5m"
            hours <= 24.0 * 35.0 -> "60m"
            else -> "1d"
        }
    }

    fun intervalCaption(interval: String): String = when (interval) {
        "1m" -> "1-minute candles"
        "5m" -> "5-minute candles"
        "15m" -> "15-minute candles"
        "60m" -> "hourly candles"
        "1d" -> "daily candles"
        else -> "$interval candles"
    }

    fun plan(window: BtcWindow, symbol: String = ChartSymbols.BTC): List<BtcFetch> {
        val yahooSymbol = ChartSymbols.yahooSymbol(symbol)
        if (!ChartSymbols.isBtc(symbol)) {
            val start = window.startMillis / 1000L
            val end = (window.endMillis / 1000L).coerceAtLeast(start + 60L)
            return listOf(
                BtcFetch.Yahoo(
                    startSec = start,
                    endSec = end,
                    interval = intervalFor(window.startMillis, window.endMillis),
                    symbol = yahooSymbol,
                ),
            )
        }
        val yahooFrom = maxOf(window.startMillis, YAHOO_FIRST.toEpochMilli())
        val yahoo = if (yahooFrom < window.endMillis) {
            BtcFetch.Yahoo(
                startSec = yahooFrom / 1000L,
                endSec = window.endMillis / 1000L,
                interval = intervalFor(yahooFrom, window.endMillis),
                symbol = yahooSymbol,
            )
        } else {
            null
        }
        if (window.fullHistory || window.startMillis < YAHOO_FIRST.toEpochMilli()) {
            val chain: BtcFetch = if (window.fullHistory || spanDays(window) > 800) {
                BtcFetch.BlockchainAll
            } else {
                val startDate = Instant.ofEpochMilli(window.startMillis).atZone(ZoneOffset.UTC).toLocalDate()
                BtcFetch.BlockchainSpan(startDate.toString(), spanDays(window).coerceAtLeast(1))
            }
            return listOfNotNull(chain, yahoo)
        }
        return listOfNotNull(yahoo)
    }

    fun yahooUrl(fetch: BtcFetch.Yahoo): String {
        val encoded = URLEncoder.encode(fetch.symbol, Charsets.UTF_8.name()).replace("+", "%20")
        return "https://query1.finance.yahoo.com/v8/finance/chart/$encoded" +
            "?period1=${fetch.startSec}&period2=${fetch.endSec}&interval=${fetch.interval}&includePrePost=true"
    }

    fun marks(points: List<BtcPoint>): ChartRangeMarks {
        if (points.isEmpty()) return ChartRangeMarks(null, null)
        val high = points.maxBy { it.price }
        val low = points.minBy { it.price }
        return ChartRangeMarks(ChartMark(high.price, high.epochMillis), ChartMark(low.price, low.epochMillis))
    }

    fun mergeMarks(parts: List<ChartRangeMarks>): ChartRangeMarks {
        val highs = parts.mapNotNull { it.high }
        val lows = parts.mapNotNull { it.low }
        return ChartRangeMarks(highs.maxByOrNull { it.price }, lows.minByOrNull { it.price })
    }

    private val etLabel = DateTimeFormatter.ofPattern("MMM d, yyyy h:mm a 'ET'", Locale.US)

    fun formatEt(epochMillis: Long): String =
        etLabel.format(Instant.ofEpochMilli(epochMillis).atZone(MarketSession.NEW_YORK))

    /** True when the finger moved onto a different print. Lift does not tick. */
    fun scrubTick(previous: Int, next: Int?): Boolean = next != null && next != previous

    fun blockchainUrl(fetch: BtcFetch): String = when (fetch) {
        BtcFetch.BlockchainAll ->
            "https://api.blockchain.info/charts/market-price?timespan=all&format=json&sampled=false"
        is BtcFetch.BlockchainSpan ->
            "https://api.blockchain.info/charts/market-price?start=${fetch.startDate}&timespan=${fetch.days}days&format=json&sampled=false"
        is BtcFetch.Yahoo -> yahooUrl(fetch)
    }

    fun changePct(points: List<BtcPoint>): Double? {
        if (points.size < 2) return null
        val first = points.first().price
        val last = points.last().price
        if (first == 0.0) return null
        return (last - first) / first * 100.0
    }

    /**
     * Merges extra bars into [max] candles. Each bucket keeps the first open,
     * the last close, and the highest high and lowest low.
     */
    fun drawBars(bars: List<YahooBar>, max: Int = 180): List<YahooBar> {
        if (bars.size <= max || max < 2) return bars
        val bucket = bars.size.toDouble() / max
        return (0 until max).map { index ->
            val start = (index * bucket).toInt().coerceIn(0, bars.lastIndex)
            val end = (((index + 1) * bucket).toInt()).coerceIn(start + 1, bars.size)
            val slice = bars.subList(start, end)
            val first = slice.first()
            val last = slice.last()
            YahooBar(
                epochMillis = last.epochMillis,
                close = last.close,
                high = slice.maxOf { it.high },
                low = slice.minOf { it.low },
                open = first.open,
            )
        }
    }

    fun closesAsBars(points: List<BtcPoint>): List<YahooBar> =
        points.map { YahooBar(it.epochMillis, it.price, it.price, it.price, it.price) }

    fun stitchBars(early: List<YahooBar>, later: List<YahooBar>): List<YahooBar> {
        val split = YAHOO_FIRST.toEpochMilli()
        val head = early.filter { it.close > 0.0 && it.epochMillis < split }
        val tail = later.filter { it.close > 0.0 && it.epochMillis >= split }
        return (head + tail).sortedBy { it.epochMillis }
    }

    /** Keeps the first and last prints so the line still shows the range. */
    fun drawPoints(points: List<BtcPoint>, max: Int = 500): List<BtcPoint> {
        if (points.size <= max || max < 2) return points
        val step = (points.size - 1).toDouble() / (max - 1)
        val sampled = (0 until max).map { index ->
            points[(index * step).toInt().coerceAtMost(points.lastIndex)]
        }
        return if (sampled.last().epochMillis == points.last().epochMillis) sampled else sampled.dropLast(1) + points.last()
    }

    /** [fraction] is 0 at the first print and 1 at the last. */
    fun nearestIndex(points: List<BtcPoint>, fraction: Float): Int {
        if (points.isEmpty()) return -1
        if (points.size == 1) return 0
        val clamped = fraction.coerceIn(0f, 1f)
        val start = points.first().epochMillis
        val span = (points.last().epochMillis - start).coerceAtLeast(1L)
        val target = start + (clamped * span).toLong()
        var best = 0
        var bestDistance = Long.MAX_VALUE
        points.forEachIndexed { index, point ->
            val distance = kotlin.math.abs(point.epochMillis - target)
            if (distance < bestDistance) {
                best = index
                bestDistance = distance
            }
        }
        return best
    }

    fun inWindow(points: List<BtcPoint>, window: BtcWindow): List<BtcPoint> =
        points.filter { it.price > 0.0 && it.epochMillis in window.startMillis..window.endMillis }
            .sortedBy { it.epochMillis }

    fun stitch(early: List<BtcPoint>, later: List<BtcPoint>): List<BtcPoint> {
        val split = YAHOO_FIRST.toEpochMilli()
        val head = early.filter { it.price > 0.0 && it.epochMillis < split }
        val tail = later.filter { it.price > 0.0 && it.epochMillis >= split }
        return (head + tail).sortedBy { it.epochMillis }
    }

    private fun window(start: Long, end: Long, label: String, fullHistory: Boolean = false) =
        BtcWindow(start, end, label, fullHistory)

    private fun spanDays(window: BtcWindow): Int =
        ((window.endMillis - window.startMillis) / 86_400_000L).toInt() + 1
}

object BtcChartParse {
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    fun yahoo(body: String): List<BtcPoint> = bars(body).map { BtcPoint(it.epochMillis, it.close) }

    fun bars(body: String): List<YahooBar> {
        val root = json.parseToJsonElement(body).jsonObject
        val chart = root["chart"]?.jsonObject ?: return emptyList()
        val result = chart["result"] as? JsonArray ?: return emptyList()
        val row = result.firstOrNull() as? JsonObject ?: return emptyList()
        val timestamps = row["timestamp"] as? JsonArray ?: return emptyList()
        val quote = row["indicators"]?.jsonObject
            ?.get("quote")?.jsonArray
            ?.firstOrNull()?.jsonObject
            ?: return emptyList()
        val closes = quote["close"] as? JsonArray ?: return emptyList()
        val opens = quote["open"] as? JsonArray
        val highs = quote["high"] as? JsonArray
        val lows = quote["low"] as? JsonArray
        val count = minOf(timestamps.size, closes.size)
        val out = ArrayList<YahooBar>(count)
        for (index in 0 until count) {
            val close = closes[index].asDouble() ?: continue
            val seconds = timestamps[index].asLong() ?: continue
            if (close <= 0.0) continue
            val high = highs?.getOrNull(index)?.asDouble()?.takeIf { it > 0.0 } ?: close
            val low = lows?.getOrNull(index)?.asDouble()?.takeIf { it > 0.0 } ?: close
            val open = opens?.getOrNull(index)?.asDouble()?.takeIf { it > 0.0 } ?: close
            out += YahooBar(seconds * 1000L, close, high, low, open)
        }
        return out
    }

    fun marksFromBars(bars: List<YahooBar>): ChartRangeMarks {
        if (bars.isEmpty()) return ChartRangeMarks(null, null)
        val high = bars.maxBy { it.high }
        val low = bars.minBy { it.low }
        return ChartRangeMarks(ChartMark(high.high, high.epochMillis), ChartMark(low.low, low.epochMillis))
    }

    fun blockchain(body: String): List<BtcPoint> {
        val root = json.parseToJsonElement(body).jsonObject
        val values = root["values"] as? JsonArray ?: return emptyList()
        val out = ArrayList<BtcPoint>(values.size)
        values.forEach { element ->
            val row = element as? JsonObject ?: return@forEach
            val seconds = row["x"].asLong() ?: return@forEach
            val price = row["y"].asDouble() ?: return@forEach
            if (price <= 0.0) return@forEach
            out += BtcPoint(seconds * 1000L, price)
        }
        return out
    }

    private fun JsonElement?.asDouble(): Double? {
        if (this == null || this is JsonNull) return null
        val primitive = runCatching { jsonPrimitive }.getOrNull() ?: return null
        return primitive.doubleOrNull ?: primitive.contentOrNull?.toDoubleOrNull()
    }

    private fun JsonElement?.asLong(): Long? {
        if (this == null || this is JsonNull) return null
        val primitive = runCatching { jsonPrimitive }.getOrNull() ?: return null
        return primitive.longOrNull ?: primitive.doubleOrNull?.toLong()
    }
}
