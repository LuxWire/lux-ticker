package com.lux.ticker.engine

import android.util.Log
import com.lux.ticker.data.CoinbaseTickerParser
import com.lux.ticker.data.DisplayMode
import com.lux.ticker.data.FeedClock
import com.lux.ticker.data.QuoteRepository
import com.lux.ticker.data.SymbolList
import com.lux.ticker.data.SymbolStore
import com.lux.ticker.data.YahooHttpException
import com.lux.ticker.net.YahooClient
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.channels.ClosedReceiveChannelException
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import kotlinx.coroutines.withTimeoutOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference

class QuoteEngine(
    private val yahoo: YahooClient,
    private val webSocketHttp: OkHttpClient,
    private val quotes: QuoteRepository,
    private val symbols: SymbolStore,
    private val screenOn: () -> Boolean,
    private val feed: FeedClock = FeedClock(),
) {
    private var job: Job? = null
    private val displayMode = AtomicReference(DisplayMode.ALWAYS)
    private val generation = AtomicInteger(0)
    private val wake = Channel<Unit>(Channel.CONFLATED)

    fun setDisplayMode(mode: DisplayMode) {
        displayMode.set(mode)
        wake.trySend(Unit)
    }

    fun requestRefresh() {
        generation.incrementAndGet()
        wake.trySend(Unit)
    }

    fun start(scope: CoroutineScope) {
        if (job?.isActive == true) return
        job = scope.launch(Dispatchers.IO) {
            launch { stockLoop() }
            launch { bitcoinLoop() }
        }
    }

    fun stop() {
        job?.cancel()
        job = null
    }

    private suspend fun stockLoop() {
        var backoffMs = 0L
        var seen = 0
        while (true) {
            val next = awaitTurn(seen)
            val refreshed = next != seen
            seen = next
            val mode = displayMode.get()
            if (mode == DisplayMode.MANUAL && !refreshed) continue
            if (mode != DisplayMode.MANUAL && !screenOn() && !refreshed) continue
            val started = System.currentTimeMillis()
            val names = symbols.read().map { it.symbol }.filter { it != SymbolList.PINNED }.distinct()
            val result = fetchAll(names)
            val shouldBackoff = result.rateLimited > 0 ||
                (names.size >= 4 && result.failed * 2 > names.size)
            feed.noteStockBatch(result.ok, result.failed)
            if (result.failed > 0) {
                Log.i(TAG, "yahoo ok=${result.ok} fail=${result.failed} limited=${result.rateLimited}")
            }
            if (shouldBackoff) {
                backoffMs = if (backoffMs == 0L) 15_000L else (backoffMs * 2).coerceAtMost(300_000L)
                delay(backoffMs)
            } else {
                backoffMs = 0L
                val elapsed = System.currentTimeMillis() - started
                delay((POLL_MS - elapsed).coerceAtLeast(250L))
            }
        }
    }

    private suspend fun fetchAll(names: List<String>): Batch = coroutineScope {
        if (names.isEmpty()) return@coroutineScope Batch(0, 0, 0)
        val gate = Semaphore(4)
        val ok = AtomicInteger()
        val failed = AtomicInteger()
        val limited = AtomicInteger()
        names.map { symbol ->
            launch {
                gate.withPermit {
                    try {
                        quotes.upsert(yahoo.fetch(symbol))
                        ok.incrementAndGet()
                    } catch (e: YahooHttpException) {
                        failed.incrementAndGet()
                        if (e.code == 429 || e.code >= 500) limited.incrementAndGet()
                    } catch (e: CancellationException) {
                        throw e
                    } catch (e: Exception) {
                        failed.incrementAndGet()
                    }
                }
            }
        }.forEach { it.join() }
        Batch(ok.get(), failed.get(), limited.get())
    }

    private suspend fun awaitTurn(seen: Int): Int {
        if (displayMode.get() == DisplayMode.MANUAL) {
            while (generation.get() == seen) {
                withTimeoutOrNull(60_000L) { wake.receive() }
            }
            return generation.get()
        }
        if (generation.get() != seen) return generation.get()
        if (!screenOn()) {
            withTimeoutOrNull(SCREEN_OFF_PAUSE_MS) { wake.receive() }
        }
        return generation.get()
    }

    private suspend fun bitcoinLoop() {
        var backoffMs = 1_000L
        var seen = 0
        val request = Request.Builder().url(COINBASE_WS).build()
        while (true) {
            val mode = displayMode.get()
            if (mode == DisplayMode.MANUAL) {
                val next = awaitTurn(seen)
                seen = next
            } else if (!screenOn()) {
                awaitTurn(seen)
                continue
            }
            val events = Channel<String>(Channel.BUFFERED)
            val socket = webSocketHttp.newWebSocket(request, object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    webSocket.send(SUBSCRIBE)
                }

                override fun onMessage(webSocket: WebSocket, text: String) {
                    events.trySend(text)
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    events.close()
                }

                override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                    events.close(t)
                }
            })
            var received = false
            val giveUpAt = System.currentTimeMillis() + 12_000L
            try {
                while (if (mode == DisplayMode.MANUAL) !received else screenOn() && displayMode.get() != DisplayMode.MANUAL) {
                    val text = withTimeoutOrNull(1_000L) { events.receive() }
                    if (text == null) {
                        if (!received && System.currentTimeMillis() > giveUpAt) break
                        continue
                    }
                    val quote = CoinbaseTickerParser.parse(text, System.currentTimeMillis()) ?: continue
                    received = true
                    backoffMs = 1_000L
                    quotes.upsert(quote)
                }
            } catch (e: ClosedReceiveChannelException) {
                // The socket closed. The loop reconnects unless the screen is off.
            } catch (e: CancellationException) {
                socket.cancel()
                throw e
            } catch (e: Exception) {
                Log.i(TAG, "coinbase reconnect: ${e.javaClass.simpleName}")
            } finally {
                socket.cancel()
                events.close()
            }
            if (mode == DisplayMode.MANUAL || !screenOn()) continue
            delay(backoffMs)
            backoffMs = (backoffMs * 2).coerceAtMost(30_000L)
        }
    }

    private data class Batch(val ok: Int, val failed: Int, val rateLimited: Int)

    private companion object {
        const val TAG = "LuxTicker"
        const val POLL_MS = 13_000L
        const val SCREEN_OFF_PAUSE_MS = 2_000L
        const val COINBASE_WS = "wss://ws-feed.exchange.coinbase.com"
        const val SUBSCRIBE =
            """{"type":"subscribe","product_ids":["BTC-USD"],"channels":["ticker"]}"""
    }
}
