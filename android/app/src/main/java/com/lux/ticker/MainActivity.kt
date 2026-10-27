package com.lux.ticker

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.lux.ticker.account.AccountEvents
import com.lux.ticker.engine.TickerService
import com.lux.ticker.ui.AppNav
import com.lux.ticker.ui.LuxTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        acceptAccountLink(intent)
        ContextCompat.startForegroundService(this, Intent(this, TickerService::class.java))
        enableEdgeToEdge()
        setContent {
            LuxTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background,
                    tonalElevation = 0.dp,
                ) {
                    AppNav()
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        acceptAccountLink(intent)
    }

    override fun onResume() {
        super.onResume()
        ContextCompat.startForegroundService(
            this,
            Intent(this, TickerService::class.java).setAction(TickerService.ACTION_REFRESH),
        )
    }

    private fun acceptAccountLink(intent: Intent?) {
        val uri = intent?.data ?: return
        if (uri.scheme != "luxwire" || uri.host != "auth-callback") return
        AccountEvents.offer(uri)
        intent.data = null
    }
}
