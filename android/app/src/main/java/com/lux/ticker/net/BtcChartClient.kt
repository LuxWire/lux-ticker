package com.lux.ticker.net

import com.lux.ticker.data.BtcChart
import com.lux.ticker.data.BtcChartParse
import com.lux.ticker.data.BtcFetch
import com.lux.ticker.data.BtcPoint
import com.lux.ticker.data.BtcSeries
import com.lux.ticker.data.BtcWindow
import com.lux.ticker.data.ChartRangeMarks
import com.lux.ticker.data.ChartSymbols
import com.lux.ticker.data.YahooBar
import okhttp3.OkHttpClient
import okhttp3.Request

/** Public price history. Bitcoin uses Yahoo BTC-USD from 2014 and Blockchain.com before that. Other symbols use Yahoo. */
class BtcChartClient(private val http: OkHttpClient) {
    fun load(window: BtcWindow): BtcSeries = load(ChartSymbols.BTC, window)

    fun load(symbol: String, window: BtcWindow): BtcSeries {
        var earlyBars = emptyList<YahooBar>()
        var laterBars = emptyList<YahooBar>()
        var usedYahoo = false
        var usedChain = false
        var failure: Exception? = null
        BtcChart.plan(window, symbol).forEach { fetch ->
            try {
                when (fetch) {
                    is BtcFetch.Yahoo -> {
                        val bars = yahooBars(fetch).filter { it.epochMillis in window.startMillis..window.endMillis }
                        if (bars.isNotEmpty()) {
                            laterBars = bars
                            usedYahoo = true
                        }
                    }
                    else -> {
                        val points = BtcChartParse.blockchain(get(BtcChart.blockchainUrl(fetch)))
                            .filter { it.epochMillis in window.startMillis..window.endMillis }
                        if (points.isNotEmpty()) {
                            earlyBars = BtcChart.closesAsBars(points)
                            usedChain = true
                        }
                    }
                }
            } catch (e: Exception) {
                failure = e
            }
        }
        val bars = when {
            usedYahoo && usedChain -> BtcChart.stitchBars(earlyBars, laterBars)
            usedChain -> earlyBars
            usedYahoo -> laterBars
            else -> emptyList()
        }.sortedBy { it.epochMillis }
        if (bars.isEmpty()) throw failure ?: IllegalStateException("empty")
        val points = bars.map { BtcPoint(it.epochMillis, it.close) }
        val marks = BtcChartParse.marksFromBars(bars)
        val source = when {
            usedYahoo && usedChain -> BtcChart.SOURCE_BOTH
            usedChain -> BtcChart.SOURCE_CHAIN
            else -> ChartSymbols.sourceYahoo(symbol)
        }
        return BtcSeries(points, source, marks.high, marks.low, bars)
    }

    /**
     * All-time high and low. Bitcoin merges Yahoo daily high/low with the
     * Blockchain.com history so prints before September 2014 still count.
     */
    fun lifetimeMarks(symbol: String, nowMillis: Long): ChartRangeMarks {
        val startSec = if (ChartSymbols.isBtc(symbol)) BtcChart.YAHOO_FIRST.epochSecond else 0L
        val yahoo = BtcFetch.Yahoo(
            startSec = startSec,
            endSec = (nowMillis / 1000L).coerceAtLeast(startSec + 86_400L),
            interval = "1d",
            symbol = ChartSymbols.yahooSymbol(symbol),
        )
        val yahooMarks = runCatching {
            BtcChartParse.marksFromBars(BtcChartParse.bars(get(BtcChart.yahooUrl(yahoo))))
        }.getOrDefault(ChartRangeMarks(null, null))
        if (!ChartSymbols.isBtc(symbol)) return yahooMarks
        val chainMarks = runCatching {
            BtcChart.marks(BtcChartParse.blockchain(get(BtcChart.blockchainUrl(BtcFetch.BlockchainAll))))
        }.getOrDefault(ChartRangeMarks(null, null))
        return BtcChart.mergeMarks(listOf(yahooMarks, chainMarks))
    }

    private fun yahooBars(fetch: BtcFetch.Yahoo): List<YahooBar> {
        val primary = BtcChartParse.bars(get(BtcChart.yahooUrl(fetch)))
        if (primary.size >= 2 || fetch.interval == "1d") return primary
        val coarser = when (fetch.interval) {
            "1m" -> "5m"
            "5m" -> "15m"
            "15m" -> "60m"
            else -> "1d"
        }
        return BtcChartParse.bars(get(BtcChart.yahooUrl(fetch.copy(interval = coarser))))
    }

    private fun get(url: String): String {
        val request = Request.Builder().url(url).header("Accept", "application/json").build()
        http.newCall(request).execute().use { response ->
            val body = response.body?.string().orEmpty()
            if (!response.isSuccessful) error("HTTP ${response.code}")
            return body
        }
    }
}
