package com.lux.ticker.net

import com.lux.ticker.data.ParsedQuote
import com.lux.ticker.data.QuoteParseException
import com.lux.ticker.data.YahooChartParser
import com.lux.ticker.data.YahooHttpException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.net.URLEncoder

object YahooUrls {
    fun chart(symbol: String): String {
        val encoded = URLEncoder.encode(symbol, Charsets.UTF_8.name()).replace("+", "%20")
        return "https://query1.finance.yahoo.com/v8/finance/chart/$encoded?interval=1m&range=1d&includePrePost=true"
    }
}

object Net {
    const val USER_AGENT =
        "Mozilla/5.0 (Linux; Android 16; Pixel 11 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36"
}

class YahooClient(private val http: OkHttpClient) {
    suspend fun fetch(symbol: String): ParsedQuote = withContext(Dispatchers.IO) {
        val request = Request.Builder().url(YahooUrls.chart(symbol)).build()
        http.newCall(request).execute().use { response ->
            val body = response.body?.string().orEmpty()
            if (response.code == 429 || response.code >= 500) {
                throw YahooHttpException(response.code, symbol)
            }
            if (!response.isSuccessful && !body.contains("\"chart\"")) {
                throw QuoteParseException("Yahoo HTTP ${response.code} for $symbol")
            }
            YahooChartParser.parse(body, symbol, System.currentTimeMillis())
        }
    }
}
