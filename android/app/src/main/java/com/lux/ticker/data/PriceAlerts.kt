package com.lux.ticker.data

import kotlin.math.abs

object PriceAlerts {
    data class Hit(
        val title: String,
        val body: String,
        val key: String,
        val alertId: String = "",
    )

    /**
     * [armed] is true after the move has come back inside the band.
     * A fire happens on the step from inside to outside, then the band stays quiet
     * until the move comes back in.
     */
    fun btcStep(changePct: Double?, threshold: Double, armed: Boolean): Pair<Boolean, Boolean> {
        if (changePct == null || threshold <= 0.0) return false to armed
        val outside = abs(changePct) >= threshold
        if (!outside) return false to true
        if (armed) return true to false
        return false to false
    }

    /** First sight of a price does not count. Later ticks fire when they cross [target]. */
    fun crossed(previous: Double?, price: Double, target: Double): Boolean {
        if (previous == null || target <= 0.0) return false
        return (previous < target && price >= target) || (previous > target && price <= target)
    }

    fun btcHit(changePct: Double, threshold: Double): Hit {
        val band = formatBand(threshold)
        return Hit(
            title = "BTC ±$band%",
            body = "BTC ${PriceFormat.formatPct(changePct)} · Coinbase USD spot · alert ±$band%",
            key = "btc:$band",
        )
    }

    fun priceHit(symbol: String, price: Double, target: Double): Hit {
        val name = symbol.trim().uppercase()
        return Hit(
            title = "$name hit ${PriceFormat.formatPrice(target, "USD")}",
            body = "$name ${PriceFormat.formatPrice(price, "USD")} hit ${PriceFormat.formatPrice(target, "USD")}",
            key = "px:$name:$target",
        )
    }

    fun badSymbol(error: Throwable): Boolean = when (error) {
        is QuoteParseException -> true
        is YahooHttpException -> error.code in 400..499 && error.code != 429
        else -> false
    }

    fun formatHit(millis: Long, zone: java.time.ZoneId = java.time.ZoneId.systemDefault()): String {
        if (millis <= 0L) return ""
        val formatter = java.time.format.DateTimeFormatter.ofPattern("MMM d, yyyy, h:mm a", java.util.Locale.US)
        return java.time.Instant.ofEpochMilli(millis).atZone(zone).format(formatter)
    }

    fun formatBand(threshold: Double): String {
        val scaled = kotlin.math.round(threshold * 100.0) / 100.0
        val whole = scaled.toInt()
        return if (scaled == whole.toDouble()) whole.toString() else scaled.toString()
    }
}

object AlertJson {
    private val row = Regex(
        """\{"id":"([^"]+)","kind":"(BTC_PCT|PRICE)","symbol":"([^"]+)","threshold":(-?[0-9.]+),"hit":(-?[0-9]+)\}""",
    )

    fun encode(list: List<SavedAlert>): String = buildString {
        append('[')
        list.forEachIndexed { index, alert ->
            if (index > 0) append(',')
            append("{\"id\":\"")
            append(alert.id)
            append("\",\"kind\":\"")
            append(alert.kind.name)
            append("\",\"symbol\":\"")
            append(alert.symbol)
            append("\",\"threshold\":")
            append(number(alert.threshold))
            append(",\"hit\":")
            append(alert.hitAtMillis)
            append('}')
        }
        append(']')
    }

    fun decode(raw: String?): List<SavedAlert> {
        if (raw.isNullOrBlank()) return emptyList()
        return row.findAll(raw).map { match ->
            SavedAlert(
                id = match.groupValues[1],
                kind = if (match.groupValues[2] == AlertKind.BTC_PCT.name) AlertKind.BTC_PCT else AlertKind.PRICE,
                symbol = match.groupValues[3],
                threshold = match.groupValues[4].toDoubleOrNull() ?: return@map null,
                hitAtMillis = match.groupValues[5].toLongOrNull() ?: 0L,
            )
        }.filterNotNull().toList()
    }

    private fun number(value: Double): String {
        val scaled = kotlin.math.round(value * 10_000.0) / 10_000.0
        val whole = scaled.toLong()
        return if (scaled == whole.toDouble()) whole.toString() else scaled.toString()
    }
}
