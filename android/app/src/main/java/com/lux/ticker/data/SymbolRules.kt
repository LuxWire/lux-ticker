package com.lux.ticker.data

import java.util.Locale

object SymbolRules {
    private val valid = Regex("^[A-Z0-9.^=-]{1,16}$")
    private val cryptoPair = Regex("^[A-Z][A-Z0-9]{1,12}-(USD|USDT)$")
    private val exchangeSuffixes = setOf(
        "T", "L", "HK", "TO", "V", "PA", "DE", "AX", "SS", "SZ", "KS", "KQ",
        "TW", "TWO", "SI", "NS", "BO", "SW", "MI", "AS", "BR", "MX", "SA",
        "JO", "IR", "TA", "ST", "HE", "OL", "CO", "BK", "JK",
    )

    fun normalize(raw: String): String = raw.trim().uppercase(Locale.US)

    fun isValid(symbol: String): Boolean = valid.matches(symbol)

    fun isCrypto(symbol: String): Boolean = symbol == "BTC" || cryptoPair.matches(symbol)

    fun isNonUs(symbol: String): Boolean {
        val dot = symbol.lastIndexOf('.')
        if (dot <= 0 || dot == symbol.lastIndex) return false
        return symbol.substring(dot + 1) in exchangeSuffixes
    }
}
