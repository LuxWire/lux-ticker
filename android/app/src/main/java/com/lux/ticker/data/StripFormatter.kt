package com.lux.ticker.data

import java.time.Instant

data class TapeSpan(val text: String, val color: Int)

data class TapeItem(
    val symbol: String,
    val spans: List<TapeSpan>,
    val stale: Boolean,
    val emphasized: Boolean,
)

data class TapeModel(
    val pinned: TapeItem,
    val scrolling: List<TapeItem>,
) {
    companion object {
        val EMPTY = TapeModel(
            pinned = TapeItem("BTC", listOf(TapeSpan("BTC", TapeColors.SYMBOL_PIN), TapeSpan(" —", TapeColors.MUTED)), false, true),
            scrolling = emptyList(),
        )
    }
}

data class WidgetContent(val pinned: String, val scrolling: String) {
    companion object {
        fun from(tape: TapeModel) = WidgetContent(
            pinned = StripFormatter.plain(tape.pinned),
            scrolling = StripFormatter.plain(tape.scrolling),
        )
    }
}

object StripFormatter {
    const val BTC_STALE_MS = 20_000L
    const val STOCK_STALE_MS = 45_000L

    fun fromSettings(
        symbols: List<SymbolSpec>,
        quotes: Map<String, ParsedQuote>,
        now: Instant,
        settings: TickerSettings,
    ): TapeModel = build(
        symbols = symbols,
        quotes = quotes,
        now = now,
        pinBtc = settings.pinBtc,
        showSessionTags = settings.showSessionTags,
        symbolColor = settings.symbolColor,
        priceColor = settings.priceColor,
    )

    fun build(
        symbols: List<SymbolSpec>,
        quotes: Map<String, ParsedQuote>,
        now: Instant,
        pinBtc: Boolean = true,
        showSessionTags: Boolean = true,
        symbolColor: Int = TapeColors.SYMBOL,
        priceColor: Int = TapeColors.PRICE,
    ): TapeModel {
        if (!pinBtc) {
            val scrolling = symbols.map { spec ->
                item(
                    spec = spec,
                    quote = quotes[spec.symbol],
                    session = if (spec.symbol == SymbolList.PINNED) SessionTag.NONE else MarketSession.tag(spec.symbol, now),
                    now = now,
                    emphasized = false,
                    showSessionTags = showSessionTags,
                    symbolColor = symbolColor,
                    priceColor = priceColor,
                )
            }
            return TapeModel(unpinned(), scrolling)
        }
        val pinnedSpec = SymbolList.pinned(symbols)
        val pinned = item(
            spec = pinnedSpec,
            quote = quotes[SymbolList.PINNED],
            session = SessionTag.NONE,
            now = now,
            emphasized = true,
            showSessionTags = showSessionTags,
            symbolColor = symbolColor,
            priceColor = priceColor,
        )
        val scrolling = SymbolList.scrolling(symbols).map { spec ->
            item(
                spec = spec,
                quote = quotes[spec.symbol],
                session = MarketSession.tag(spec.symbol, now),
                now = now,
                emphasized = false,
                showSessionTags = showSessionTags,
                symbolColor = symbolColor,
                priceColor = priceColor,
            )
        }
        return TapeModel(pinned, scrolling)
    }

    private fun unpinned() = TapeItem("", emptyList(), stale = false, emphasized = false)

    fun item(
        spec: SymbolSpec,
        quote: ParsedQuote?,
        session: SessionTag,
        now: Instant,
        emphasized: Boolean,
        showSessionTags: Boolean = true,
        symbolColor: Int = TapeColors.SYMBOL,
        priceColor: Int = TapeColors.PRICE,
    ): TapeItem {
        val symbolInk = if (emphasized && symbolColor == TapeColors.SYMBOL) TapeColors.SYMBOL_PIN else symbolColor
        if (quote == null) {
            return TapeItem(
                symbol = spec.symbol,
                spans = listOf(TapeSpan(spec.label, symbolInk), TapeSpan(" —", TapeColors.MUTED)),
                stale = false,
                emphasized = emphasized,
            )
        }
        val spans = mutableListOf(TapeSpan(spec.label, symbolInk))
        if (showSessionTags && session == SessionTag.PRE) {
            spans += TapeSpan(" PRE", TapeColors.TAG)
        }
        if (showSessionTags && session == SessionTag.AFTER) {
            spans += TapeSpan(" AH", TapeColors.TAG)
        }
        spans += TapeSpan(" " + PriceFormat.formatPrice(quote.price, quote.currency), priceColor)
        val change = quote.changePct
        if (change != null) {
            val color = when {
                change > 0.005 -> TapeColors.UP
                change < -0.005 -> TapeColors.DOWN
                else -> TapeColors.FLAT
            }
            spans += TapeSpan(" " + PriceFormat.formatPct(change), color)
        }
        val limit = if (spec.symbol == SymbolList.PINNED) BTC_STALE_MS else STOCK_STALE_MS
        val stale = now.toEpochMilli() - quote.fetchedAtMillis > limit
        if (stale) spans += TapeSpan(" °", TapeColors.MUTED)
        return TapeItem(spec.symbol, spans, stale, emphasized)
    }

    fun plain(item: TapeItem): String = item.spans.joinToString("") { it.text }

    fun plain(items: List<TapeItem>): String = items.joinToString("  ·  ") { plain(it) }
}
