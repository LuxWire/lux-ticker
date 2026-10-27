package com.lux.ticker.data

import java.text.DecimalFormat
import java.text.DecimalFormatSymbols
import java.util.Locale

object PriceFormat {
    private val symbols = DecimalFormatSymbols(Locale.US)

    fun formatPrice(price: Double, currency: String): String {
        val prefix = when (currency.uppercase(Locale.US)) {
            "JPY" -> "¥"
            "GBP" -> "£"
            "EUR" -> "€"
            else -> ""
        }
        val pattern = when {
            currency.equals("JPY", ignoreCase = true) -> "#,##0.##"
            price >= 1.0 -> "#,##0.00"
            else -> "#,##0.0000"
        }
        return prefix + DecimalFormat(pattern, symbols).format(price)
    }

    fun formatPct(pct: Double): String {
        return DecimalFormat("+0.00;-0.00", symbols).format(pct) + "%"
    }
}
