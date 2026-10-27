package com.lux.ticker.account

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.browser.customtabs.CustomTabColorSchemeParams
import androidx.browser.customtabs.CustomTabsIntent
import com.lux.ticker.data.PaperPalette

/** Opens https pages, including Supabase OAuth, in a Custom Tab. */
object AccountBrowser {
    fun open(context: Context, url: String) {
        val tabs = tabs()
        runCatching { tabs.launchUrl(context, Uri.parse(url)) }
            .onFailure {
                context.startActivity(
                    Intent(Intent.ACTION_VIEW, Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
                )
            }
    }

    fun tabIntent(): Intent = tabs().intent

    private fun tabs(): CustomTabsIntent {
        val paper = CustomTabColorSchemeParams.Builder()
            .setToolbarColor(PaperPalette.BG)
            .setNavigationBarColor(PaperPalette.BG)
            .build()
        val tabs = CustomTabsIntent.Builder()
            .setShowTitle(true)
            .setDefaultColorSchemeParams(paper)
            .setShareState(CustomTabsIntent.SHARE_STATE_OFF)
            .build()
        tabs.intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        return tabs
    }
}
