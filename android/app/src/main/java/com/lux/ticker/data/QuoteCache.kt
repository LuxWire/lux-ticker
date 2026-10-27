package com.lux.ticker.data

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.put
import java.io.File

class QuoteCache(private val file: File) {
    fun load(): Map<String, ParsedQuote> {
        if (!file.exists()) return emptyMap()
        return try {
            val root = Json.parseToJsonElement(file.readText()) as? JsonArray ?: return emptyMap()
            buildMap {
                root.forEach { element ->
                    val obj = element as? JsonObject ?: return@forEach
                    val symbol = obj.str("symbol") ?: return@forEach
                    val price = obj.num("price") ?: return@forEach
                    put(
                        symbol,
                        ParsedQuote(
                            symbol = symbol,
                            price = price,
                            previousClose = obj.num("previousClose"),
                            changePct = obj.num("changePct"),
                            currency = obj.str("currency") ?: "USD",
                            asOfMillis = obj.num("asOfMillis")?.toLong() ?: 0L,
                            fetchedAtMillis = obj.num("fetchedAtMillis")?.toLong() ?: 0L,
                        ),
                    )
                }
            }
        } catch (e: Exception) {
            emptyMap()
        }
    }

    fun save(quotes: Map<String, ParsedQuote>) {
        val array = buildJsonArray {
            quotes.values.forEach { quote ->
                add(
                    buildJsonObject {
                        put("symbol", quote.symbol)
                        put("price", quote.price)
                        if (quote.previousClose != null) put("previousClose", quote.previousClose)
                        if (quote.changePct != null) put("changePct", quote.changePct)
                        put("currency", quote.currency)
                        put("asOfMillis", quote.asOfMillis)
                        put("fetchedAtMillis", quote.fetchedAtMillis)
                    },
                )
            }
        }
        file.parentFile?.mkdirs()
        val tmp = File(file.parentFile, file.name + ".tmp")
        tmp.writeText(array.toString())
        if (!tmp.renameTo(file)) {
            file.writeText(tmp.readText())
            tmp.delete()
        }
    }

    private fun JsonObject.str(key: String): String? {
        val element = this[key] ?: return null
        if (element is JsonNull) return null
        val primitive = element as? JsonPrimitive ?: return null
        return primitive.content
    }

    private fun JsonObject.num(key: String): Double? {
        val element = this[key] ?: return null
        if (element is JsonNull) return null
        val primitive = element as? JsonPrimitive ?: return null
        return primitive.doubleOrNull ?: primitive.content.toDoubleOrNull()
    }

    companion object {
        const val FILE_NAME = "quotes.json"
    }
}
