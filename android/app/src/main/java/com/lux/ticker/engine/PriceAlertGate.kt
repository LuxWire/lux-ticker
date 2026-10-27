package com.lux.ticker.engine

import com.lux.ticker.data.AlertKind
import com.lux.ticker.data.ParsedQuote
import com.lux.ticker.data.PriceAlerts
import com.lux.ticker.data.SavedAlert
import com.lux.ticker.data.SymbolList
import com.lux.ticker.data.TickerSettings

/** Remembers the last price so a target fires once, on the cross. */
class PriceAlertGate {
    private val lastPrice = HashMap<String, Double>()
    private var btcArmed = true
    private val armedById = HashMap<String, Boolean>()

    fun take(settings: TickerSettings, quotes: Map<String, ParsedQuote>): List<PriceAlerts.Hit> {
        val hits = ArrayList<PriceAlerts.Hit>()
        val saved = settings.savedAlerts
        val standingBtc = saved.none { it.kind == AlertKind.BTC_PCT }
        if (standingBtc) {
            val btc = quotes[SymbolList.PINNED]
            val (fire, armed) = PriceAlerts.btcStep(btc?.changePct, settings.btcAlertPct.toDouble(), btcArmed)
            btcArmed = armed
            if (settings.priceAlerts && fire && btc?.changePct != null) {
                hits += PriceAlerts.btcHit(btc.changePct, settings.btcAlertPct.toDouble())
            }
        }
        if (saved.isEmpty()) {
            crossSymbol(
                settings.priceAlerts,
                settings.priceAlertSymbol,
                settings.priceAlertTarget.toDouble(),
                alertId = "",
                quotes,
                hits,
            )
        } else {
            saved.forEach { alert -> fireSaved(settings.priceAlerts, alert, quotes, hits) }
        }
        quotes.forEach { (symbolKey, quote) -> lastPrice[symbolKey] = quote.price }
        return hits
    }

    private fun fireSaved(
        enabled: Boolean,
        alert: SavedAlert,
        quotes: Map<String, ParsedQuote>,
        hits: MutableList<PriceAlerts.Hit>,
    ) {
        when (alert.kind) {
            AlertKind.BTC_PCT -> {
                val btc = quotes[SymbolList.PINNED]
                val armed = armedById[alert.id] ?: true
                val (fire, next) = PriceAlerts.btcStep(btc?.changePct, alert.threshold, armed)
                armedById[alert.id] = next
                if (enabled && fire && btc?.changePct != null) {
                    hits += PriceAlerts.btcHit(btc.changePct, alert.threshold).copy(alertId = alert.id)
                }
            }
            AlertKind.PRICE -> crossSymbol(enabled, alert.symbol, alert.threshold, alert.id, quotes, hits)
        }
    }

    private fun crossSymbol(
        enabled: Boolean,
        symbol: String,
        target: Double,
        alertId: String,
        quotes: Map<String, ParsedQuote>,
        hits: MutableList<PriceAlerts.Hit>,
    ) {
        val name = symbol.trim().uppercase()
        if (name.isEmpty() || target <= 0.0) return
        val quote = quotes[name] ?: return
        val previous = lastPrice[name]
        if (enabled && PriceAlerts.crossed(previous, quote.price, target)) {
            hits += PriceAlerts.priceHit(name, quote.price, target).copy(alertId = alertId)
        }
    }
}
