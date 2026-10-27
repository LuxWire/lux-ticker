package com.lux.ticker.data

import java.util.Locale

object ChartSearch {
    const val LIMIT = 6

    fun normalize(raw: String): String = SymbolRules.normalize(raw)

    fun formatError(symbol: String): String? = when {
        symbol.isEmpty() -> "Enter a ticker."
        !SymbolRules.isValid(symbol) -> "Invalid ticker"
        else -> null
    }

    fun unknownMessage(symbol: String): String = "No chart for $symbol."

    fun offlineMessage(symbol: String): String = "Couldn't check $symbol. Try again."

    fun suggestions(query: String, known: List<SymbolSpec>, limit: Int = LIMIT): List<SymbolSpec> {
        val needle = normalize(query)
        if (needle.isEmpty()) return emptyList()
        return known
            .filter { spec ->
                spec.symbol.contains(needle, ignoreCase = true) ||
                    spec.label.contains(needle, ignoreCase = true)
            }
            .distinctBy { it.symbol.uppercase(Locale.US) }
            .take(limit)
    }
}
