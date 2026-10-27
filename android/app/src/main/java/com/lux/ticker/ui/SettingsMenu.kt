package com.lux.ticker.ui

data class SettingsDestination(
    val route: String,
    val title: String,
    val detail: String,
)

object SettingsMenu {
    val entries = listOf(
        SettingsDestination(
            route = "settings/tickers",
            title = "Tickers",
            detail = "The list, add ticker, scroll direction, and Bitcoin side.",
        ),
        SettingsDestination(
            route = "settings/display",
            title = "Display",
            detail = "Colors, Pre and AH, speed, position, chart haptics, and the strip.",
        ),
        SettingsDestination(
            route = "settings/alerts",
            title = "Alerts",
            detail = "LuxWire posts and saved price alerts.",
        ),
        SettingsDestination(
            route = "settings/account",
            title = "Account",
            detail = "Optional. The ticker works signed out. Sign in to sync this phone.",
        ),
        SettingsDestination(
            route = "settings/battery",
            title = "Battery",
            detail = "Always overlay, lock screen only, or manual refresh.",
        ),
        SettingsDestination(
            route = "settings/whats-new",
            title = "What's new",
            detail = "What changed in this build.",
        ),
        SettingsDestination(
            route = "settings/about",
            title = "Contact / About",
            detail = "@LuxWire. Sideload until Play.",
        ),
    )
}
