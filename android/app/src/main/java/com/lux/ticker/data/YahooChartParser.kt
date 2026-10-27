package com.lux.ticker.data

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.longOrNull

object YahooChartParser {
    private val json = Json { isLenient = true }

    fun parse(body: String, symbol: String, fetchedAtMillis: Long): ParsedQuote {
        val root = try {
            json.parseToJsonElement(body).jsonObject
        } catch (e: Exception) {
            throw QuoteParseException("Yahoo returned an unreadable quote for $symbol")
        }
        val chart = root["chart"]?.jsonObject
            ?: throw QuoteParseException("Yahoo returned no chart for $symbol")
        val resultElement = chart["result"]
        if (resultElement == null || resultElement is JsonNull) {
            val error = chart["error"]?.asObject()
            val description = error?.string("description") ?: error?.string("code") ?: "No data found"
            throw QuoteParseException(description)
        }
        val result = resultElement.asArray()?.firstOrNull()?.asObject()
            ?: throw QuoteParseException("No data found for $symbol")
        val meta = result["meta"]?.asObject() ?: JsonObject(emptyMap())
        val currency = meta.string("currency") ?: "USD"
        val previous = meta.dbl("chartPreviousClose") ?: meta.dbl("previousClose")

        var price: Double? = null
        var asOf = 0L
        val timestamps = result["timestamp"]?.asArray()
        val closes = result["indicators"]?.asObject()
            ?.get("quote")?.asArray()
            ?.firstOrNull()?.asObject()
            ?.get("close")?.asArray()
        if (timestamps != null && closes != null) {
            val count = minOf(timestamps.size, closes.size)
            for (index in count - 1 downTo 0) {
                val close = closes[index].asDoubleOrNull() ?: continue
                price = close
                asOf = timestamps[index].asLongOrNull()?.times(1_000L) ?: 0L
                break
            }
        }
        if (price == null) {
            // No extended bars: fall back to the regular session print.
            price = meta.dbl("regularMarketPrice") ?: throw QuoteParseException("No price for $symbol")
            asOf = meta.dbl("regularMarketTime")?.times(1_000.0)?.toLong() ?: 0L
        }
        val change = if (previous != null && previous != 0.0) (price - previous) / previous * 100.0 else null
        return ParsedQuote(
            symbol = symbol,
            price = price,
            previousClose = previous,
            changePct = change,
            currency = currency,
            asOfMillis = asOf,
            fetchedAtMillis = fetchedAtMillis,
        )
    }
}

object CoinbaseTickerParser {
    private val json = Json { isLenient = true }

    fun parse(body: String, fetchedAtMillis: Long): ParsedQuote? {
        val obj = try {
            json.parseToJsonElement(body).jsonObject
        } catch (e: Exception) {
            return null
        }
        if (obj.string("type") != "ticker") return null
        if (obj.string("product_id") != "BTC-USD") return null
        val price = obj.string("price")?.toDoubleOrNull() ?: return null
        val open = obj.string("open_24h")?.toDoubleOrNull()
        val change = if (open != null && open != 0.0) (price - open) / open * 100.0 else null
        val asOf = obj.string("time")?.let { raw ->
            runCatching { java.time.Instant.parse(raw).toEpochMilli() }.getOrNull()
        } ?: fetchedAtMillis
        return ParsedQuote(
            symbol = "BTC",
            price = price,
            previousClose = open,
            changePct = change,
            currency = "USD",
            asOfMillis = asOf,
            fetchedAtMillis = fetchedAtMillis,
        )
    }
}

private fun JsonElement.asObject(): JsonObject? = this as? JsonObject

private fun JsonElement.asArray(): JsonArray? = this as? JsonArray

private fun JsonObject.string(key: String): String? {
    val element = this[key] ?: return null
    if (element is JsonNull) return null
    return runCatching { element.jsonPrimitive.content }.getOrNull()
}

private fun JsonObject.dbl(key: String): Double? = this[key].asDoubleOrNull()

private fun JsonElement?.asDoubleOrNull(): Double? {
    if (this == null || this is JsonNull) return null
    val primitive = runCatching { jsonPrimitive }.getOrNull() ?: return null
    return primitive.doubleOrNull ?: primitive.content.toDoubleOrNull()
}

private fun JsonElement?.asLongOrNull(): Long? {
    if (this == null || this is JsonNull) return null
    val primitive = runCatching { jsonPrimitive }.getOrNull() ?: return null
    return primitive.longOrNull ?: primitive.doubleOrNull?.toLong()
}
