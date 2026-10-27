package com.lux.ticker.ui

import android.net.Uri
import androidx.compose.runtime.Composable
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.lux.ticker.data.ChartSymbols

fun chartRoute(symbol: String): String = "chart/${Uri.encode(ChartSymbols.normalize(symbol))}"

@Composable
fun AppNav() {
    val nav = rememberNavController()
    NavHost(navController = nav, startDestination = "home") {
        composable("home") {
            HomeRoute(
                onEdit = { nav.navigate("edit") },
                onSettings = { nav.navigate("settings") },
                onChart = { nav.navigate(chartRoute("BTC")) },
            )
        }
        composable(
            route = "chart/{symbol}",
            arguments = listOf(navArgument("symbol") { type = NavType.StringType }),
        ) { entry ->
            val symbol = ChartSymbols.normalize(entry.arguments?.getString("symbol") ?: "BTC")
            BtcChartRoute(
                symbol = symbol,
                onBack = { nav.popBackStack() },
                onOpenSymbol = { next ->
                    nav.navigate(chartRoute(next)) {
                        popUpTo("chart/{symbol}") { inclusive = true }
                    }
                },
            )
        }
        composable("edit") {
            EditorRoute(
                onBack = { nav.popBackStack() },
                onChart = { symbol -> nav.navigate(chartRoute(symbol)) },
            )
        }
        composable("settings") {
            SettingsMenuRoute(
                onBack = { nav.popBackStack() },
                onOpen = { route -> nav.navigate(route) },
            )
        }
        composable("settings/tickers") {
            SettingsTickersRoute(
                onBack = { nav.popBackStack() },
                onChart = { symbol -> nav.navigate(chartRoute(symbol)) },
            )
        }
        composable("settings/display") { SettingsDisplayRoute(onBack = { nav.popBackStack() }) }
        composable("settings/alerts") { SettingsAlertsRoute(onBack = { nav.popBackStack() }) }
        composable("settings/account") { AccountRoute(onBack = { nav.popBackStack() }) }
        composable("settings/battery") { SettingsBatteryRoute(onBack = { nav.popBackStack() }) }
        composable("settings/whats-new") { SettingsWhatsNewRoute(onBack = { nav.popBackStack() }) }
        composable("settings/about") { SettingsAboutRoute(onBack = { nav.popBackStack() }) }
    }
}
