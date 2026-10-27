package com.lux.ticker.data

/**
 * Names the source and the age. Stock lines say updated or delayed.
 * They never say live. Bitcoin is the Coinbase USD spot.
 */
object FeedHonesty {
    fun btc(ageMillis: Long?, online: Boolean): String {
        val fresh = online && ageMillis != null && ageMillis in 0..20_000
        val state = if (fresh) "" else " · cached"
        return "Coinbase USD spot$state · ${agePhrase(ageMillis)}"
    }

    fun stocks(ageMillis: Long?, online: Boolean): String {
        val delayed = !online || ageMillis == null || ageMillis > 15 * 60_000
        val state = if (delayed) "delayed" else "updated"
        return "Yahoo · $state · ${agePhrase(ageMillis)}"
    }

    fun agePhrase(ageMillis: Long?): String {
        if (ageMillis == null || ageMillis < 0L) return "no update yet"
        val seconds = ageMillis / 1000L
        return when {
            seconds < 60L -> "${seconds}s ago"
            seconds < 3600L -> "${seconds / 60L}m ago"
            else -> "${seconds / 3600L}h ago"
        }
    }

    fun btcAgeMillis(quotes: Map<String, ParsedQuote>, nowMillis: Long): Long? =
        quotes[SymbolList.PINNED]?.fetchedAtMillis?.let { (nowMillis - it).coerceAtLeast(0L) }

    fun stockAgeMillis(quotes: Map<String, ParsedQuote>, nowMillis: Long): Long? {
        val newest = quotes
            .filterKeys { it != SymbolList.PINNED }
            .values
            .maxOfOrNull { it.fetchedAtMillis }
            ?: return null
        return (nowMillis - newest).coerceAtLeast(0L)
    }
}
