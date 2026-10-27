package com.lux.ticker.data

object SymbolList {
    const val PINNED = "BTC"

    fun ensurePinned(list: List<SymbolSpec>): List<SymbolSpec> {
        val pinned = list.firstOrNull { it.symbol == PINNED } ?: DefaultSymbols.btc
        val rest = LinkedHashMap<String, SymbolSpec>()
        list.forEach { spec ->
            if (spec.symbol != PINNED && spec.symbol !in rest) rest[spec.symbol] = spec
        }
        val label = pinned.label.ifBlank { "BTC" }
        return listOf(SymbolSpec(PINNED, label)) + rest.values
    }

    fun pinned(list: List<SymbolSpec>): SymbolSpec = ensurePinned(list).first()

    fun scrolling(list: List<SymbolSpec>): List<SymbolSpec> = ensurePinned(list).drop(1)

    fun remove(list: List<SymbolSpec>, symbol: String): List<SymbolSpec> {
        val full = ensurePinned(list)
        if (symbol == PINNED) return full
        return listOf(full.first()) + full.drop(1).filter { it.symbol != symbol }
    }

    fun add(list: List<SymbolSpec>, spec: SymbolSpec): List<SymbolSpec> {
        val full = ensurePinned(list)
        if (spec.symbol == PINNED || full.any { it.symbol == spec.symbol }) return full
        return full + spec
    }

    /** [from] and [to] are indexes in the scrolling list, never the pinned slot. */
    fun moveScrolling(list: List<SymbolSpec>, from: Int, to: Int): List<SymbolSpec> {
        val full = ensurePinned(list)
        val scroll = full.drop(1).toMutableList()
        if (from !in scroll.indices || to !in scroll.indices || from == to) return full
        val item = scroll.removeAt(from)
        scroll.add(to, item)
        return listOf(full.first()) + scroll
    }
}

object SymbolJson {
    fun encode(list: List<SymbolSpec>): String = buildString {
        append('[')
        list.forEachIndexed { index, spec ->
            if (index > 0) append(',')
            append("{\"symbol\":\"")
            append(escape(spec.symbol))
            append("\",\"label\":\"")
            append(escape(spec.label))
            append("\"}")
        }
        append(']')
    }

    fun decodeOrNull(raw: String): List<SymbolSpec>? {
        val text = raw.trim()
        if (!text.startsWith('[') || !text.endsWith(']')) return null
        if (text == "[]") return emptyList()
        val out = mutableListOf<SymbolSpec>()
        var cursor = 0
        while (cursor < text.length) {
            val start = text.indexOf('{', cursor)
            if (start < 0) break
            val end = text.indexOf('}', start)
            if (end < 0) return null
            val body = text.substring(start + 1, end)
            val symbol = field(body, "symbol") ?: return null
            val label = field(body, "label") ?: symbol
            if (!SymbolRules.isValid(symbol)) return null
            out += SymbolSpec(symbol, label)
            cursor = end + 1
        }
        return out
    }

    fun decodeStored(raw: String?): List<SymbolSpec> {
        if (raw.isNullOrBlank()) return DefaultSymbols.all
        val decoded = decodeOrNull(raw) ?: return DefaultSymbols.all
        return SymbolList.ensurePinned(decoded)
    }

    private fun escape(value: String): String = buildString {
        value.forEach { char ->
            when (char) {
                '\\' -> append("\\\\")
                '"' -> append("\\\"")
                else -> append(char)
            }
        }
    }

    private fun field(body: String, name: String): String? {
        val key = "\"$name\""
        val keyAt = body.indexOf(key)
        if (keyAt < 0) return null
        val colon = body.indexOf(':', keyAt + key.length)
        if (colon < 0) return null
        val open = body.indexOf('"', colon + 1)
        if (open < 0) return null
        val out = StringBuilder()
        var index = open + 1
        while (index < body.length) {
            val char = body[index]
            if (char == '\\' && index + 1 < body.length) {
                out.append(body[index + 1])
                index += 2
                continue
            }
            if (char == '"') return out.toString()
            out.append(char)
            index++
        }
        return null
    }
}
