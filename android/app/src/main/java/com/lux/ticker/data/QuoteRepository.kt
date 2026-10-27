package com.lux.ticker.data

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class QuoteRepository(private val cache: QuoteCache) {
    private val quotesState = MutableStateFlow(cache.load())
    val quotes: StateFlow<Map<String, ParsedQuote>> = quotesState.asStateFlow()
    private val gate = Any()
    private var lastSaveMillis = 0L

    fun upsert(quote: ParsedQuote) {
        val next = synchronized(gate) {
            val updated = quotesState.value + (quote.symbol to quote)
            quotesState.value = updated
            updated
        }
        val now = System.currentTimeMillis()
        val stockUpdate = quote.symbol != SymbolList.PINNED
        if (stockUpdate || now - lastSaveMillis > 5_000L) {
            lastSaveMillis = now
            runCatching { cache.save(next) }
        }
    }

    fun snapshot(): Map<String, ParsedQuote> = quotesState.value
}
