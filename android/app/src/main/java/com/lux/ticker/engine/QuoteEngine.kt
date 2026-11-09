package com.lux.ticker.engine

import android.util.Log
import com.lux.ticker.data.BatteryGate
import com.lux.ticker.data.BatterySession
import com.lux.ticker.data.CoinbaseTickerParser
import com.lux.ticker.data.DisplayMode
import com.lux.ticker.data.FeedClock
import com.lux.ticker.data.FetchKind
import com.lux.ticker.data.FetchPlan
import com.lux.ticker.data.ParsedQuote
import com.lux.ticker.data.PriceRefresh
import com.lux.ticker.data.PriceRefreshPace
import com.lux.ticker.data.QuoteRepository
import com.lux.ticker.data.WakeLedger
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
    private val powerSave: () -> Boolean = { false },
    private val deviceIdle: () -> Boolean = { false },
    private val feed: FeedClock = FeedClock(),
    val wakeLedger: WakeLedger = WakeLedger(),
) {
    private var job: Job? = null
    private val displayMode = AtomicReference(DisplayMode.ALWAYS)
    private val priceRefresh = AtomicReference(PriceRefresh.LIVE)
    private val generation = AtomicInteger(0)
    private val wake = Channel<Unit>(Channel.CONFLATED)

    fun setDisplayMode(mode: DisplayMode) {
        val previous = displayMode.getAndSet(mode)
        if (previous != mode) wake.trySend(Unit)
    }

    /** Screen, battery saver, or Doze changed. The wait looks again. A redraw does not call this. */
    fun noteConditionsChanged() {
        wake.trySend(Unit)
    }

    fun setPriceRefresh(refresh: PriceRefresh) {
        val previous = priceRefresh.getAndSet(refresh)
        if (previous != refresh) wake.trySend(Unit)
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
        releaseIdle()
    }

    private suspend fun stockLoop() {
        var backoffMs = 0L
        var seen = 0
        while (true) {
            val refreshed = generation.get() != seen
            seen = generation.get()
            val plan = currentPlan(refreshed)
            if (!plan.fetches) {
                releaseIdle()
                waitOut(plan.waitMs)
                continue
            }
            val started = System.currentTimeMillis()
            val names = symbols.read().map { it.symbol }.filter { it != SymbolList.PINNED }.distinct()
            var result = Batch(0, 0, 0)
            BatterySession.aroundFetch(wakeLedger) {
                result = fetchAll(names)
            }
            val shouldBackoff = result.rateLimited > 0 ||
                (names.size >= 4 && result.failed * 2 > names.size)
            feed.noteStockBatch(result.ok, result.failed)
            if (result.failed > 0) {
                Log.i(TAG, "yahoo ok=${result.ok} fail=${result.failed} limited=${result.rateLimited}")
            }
            if (plan.kind == FetchKind.ONCE) {
                val steady = currentPlan(userRefresh = false)
                if (steady.fetches) waitOut(steady.stockPeriodMs)
                continue
            }
            val refresh = priceRefresh.get()
            if (shouldBackoff) {
                backoffMs = PriceRefreshPace.stockBackoffMs(refresh, backoffMs, powerSave())
                waitOut(backoffMs)
            } else {
                backoffMs = 0L
                val elapsed = System.currentTimeMillis() - started
                waitOut((plan.stockPeriodMs - elapsed).coerceAtLeast(250L))
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

    private fun currentPlan(userRefresh: Boolean): FetchPlan = BatteryGate.plan(
        refresh = priceRefresh.get(),
        screenOn = screenOn(),
        powerSave = powerSave(),
        deviceIdle = deviceIdle(),
        manual = displayMode.get() == DisplayMode.MANUAL,
        userRefresh = userRefresh,
    )

    private fun releaseIdle() {
        BatterySession.idle(wakeLedger)
        runCatching { webSocketHttp.connectionPool.evictAll() }
    }

    private suspend fun bitcoinLoop() {
        var backoffMs = 1_000L
        var seen = 0
        while (true) {
            val refreshed = generation.get() != seen
            if (refreshed) seen = generation.get()
            val plan = currentPlan(refreshed)
            if (!plan.fetches) {
                releaseIdle()
                waitOut(plan.waitMs)
                continue
            }
            if (plan.holdSocket) {
                backoffMs = runLiveSocket(backoffMs)
            } else {
                var quote: ParsedQuote? = null
                BatterySession.aroundFetch(wakeLedger) {
                    quote = oneBitcoinQuote()
                }
                quote?.let { quotes.upsert(it) }
                val steady = if (plan.kind == FetchKind.ONCE) currentPlan(userRefresh = false) else plan
                if (steady.fetches && !steady.holdSocket) waitOut(steady.waitMs)
            }
        }
    }

    /** Live Coinbase socket. Returns the reconnect backoff for the next attempt. */
    private suspend fun runLiveSocket(backoffMs: Long): Long {
        var next = backoffMs
        val session = openBitcoinSocket()
        var received = false
        val giveUpAt = System.currentTimeMillis() + 12_000L
        try {
            while (currentPlan(userRefresh = false).holdSocket) {
                val text = withTimeoutOrNull(1_000L) { session.events.receive() }
                if (text == null) {
                    if (!received && System.currentTimeMillis() > giveUpAt) break
                    continue
                }
                val quote = CoinbaseTickerParser.parse(text, System.currentTimeMillis()) ?: continue
                received = true
                next = 1_000L
                quotes.upsert(quote)
            }
        } catch (e: ClosedReceiveChannelException) {
            // The socket closed. The loop reconnects unless the screen is off.
        } catch (e: CancellationException) {
            session.socket.cancel()
            throw e
        } catch (e: Exception) {
            Log.i(TAG, "coinbase reconnect: ${e.javaClass.simpleName}")
        } finally {
            session.close()
        }
        if (!currentPlan(userRefresh = false).holdSocket) {
            releaseIdle()
            return 1_000L
        }
        waitOut(next)
        return (next * 2).coerceAtMost(30_000L)
    }

    /** One ticker message, then the socket closes. Used when Price refresh is not Live. */
    private suspend fun oneBitcoinQuote(): ParsedQuote? {
        val session = openBitcoinSocket()
        val giveUpAt = System.currentTimeMillis() + 12_000L
        try {
            while (System.currentTimeMillis() < giveUpAt) {
                val text = withTimeoutOrNull(1_000L) { session.events.receive() } ?: continue
                return CoinbaseTickerParser.parse(text, System.currentTimeMillis()) ?: continue
            }
            return null
        } catch (e: ClosedReceiveChannelException) {
            return null
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            Log.i(TAG, "coinbase reconnect: ${e.javaClass.simpleName}")
            return null
        } finally {
            session.close()
        }
    }

    private fun openBitcoinSocket(): BitcoinSocket {
        val events = Channel<String>(Channel.BUFFERED)
        val request = Request.Builder().url(COINBASE_WS).build()
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
        return BitcoinSocket(socket, events)
    }

    /**
     * Wait [ms]. A tape redraw that sends a wake without changing the screen, saver,
     * Doze, mode, or Price refresh does not end the wait. That used to collapse a
     * 30 minute choice back into a fetch on every quote.
     */
    private suspend fun waitOut(ms: Long) {
        if (ms <= 0L) return
        var left = ms
        while (left > 0L) {
            val before = stamp()
            val started = System.currentTimeMillis()
            val signaled = withTimeoutOrNull(left) { wake.receive() }
            val spent = System.currentTimeMillis() - started
            if (signaled == null) return
            if (stamp() != before) return
            left -= spent
        }
    }

    private fun stamp(): String = listOf(
        priceRefresh.get().name,
        displayMode.get().name,
        screenOn(),
        powerSave(),
        deviceIdle(),
        generation.get(),
    ).joinToString("|")

    private class BitcoinSocket(val socket: WebSocket, val events: Channel<String>) {
        fun close() {
            socket.cancel()
            events.close()
        }
    }

    private data class Batch(val ok: Int, val failed: Int, val rateLimited: Int)

    private companion object {
        const val TAG = "LuxTicker"
        const val COINBASE_WS = "wss://ws-feed.exchange.coinbase.com"
        const val SUBSCRIBE =
            """{"type":"subscribe","product_ids":["BTC-USD"],"channels":["ticker"]}"""
    }
}
