package com.lux.ticker

import android.app.Application
import android.content.Context
import android.util.Log
import com.lux.ticker.account.AccountController
import com.lux.ticker.data.FeedClock
import com.lux.ticker.data.QuoteCache
import com.lux.ticker.data.QuoteRepository
import com.lux.ticker.data.SettingsStore
import com.lux.ticker.data.SymbolStore
import com.lux.ticker.data.settingsDataStore
import com.lux.ticker.data.symbolDataStore
import com.lux.ticker.net.Net
import com.lux.ticker.net.YahooClient
import okhttp3.OkHttpClient
import java.io.File
import java.util.concurrent.TimeUnit

class LuxApp : Application() {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
        runCatching { container.account.start() }
            .onFailure { Log.w("LuxTicker", "account", it) }
    }

    companion object {
        fun container(context: Context): AppContainer =
            (context.applicationContext as LuxApp).container
    }
}

class AppContainer(context: Context) {
    private val appContext = context.applicationContext

    val http: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .callTimeout(20, TimeUnit.SECONDS)
        .addInterceptor { chain ->
            chain.proceed(
                chain.request().newBuilder()
                    .header("User-Agent", Net.USER_AGENT)
                    .header("Accept", "application/json")
                    .build(),
            )
        }
        .build()

    val webSocketHttp: OkHttpClient = http.newBuilder()
        .readTimeout(0, TimeUnit.MILLISECONDS)
        .callTimeout(0, TimeUnit.MILLISECONDS)
        .build()

    val feed = FeedClock()
    val quotes = QuoteRepository(QuoteCache(File(appContext.filesDir, QuoteCache.FILE_NAME)))
    val settings = SettingsStore(appContext.settingsDataStore)
    val symbols = SymbolStore(appContext.symbolDataStore) { settings.noteLocalEdit() }
    val yahoo = YahooClient(http)

    /** Account calls do not send the Yahoo browser User-Agent. */
    val accountHttp: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(12, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .callTimeout(25, TimeUnit.SECONDS)
        .build()

    val account = AccountController(appContext, accountHttp, settings, symbols)
}
