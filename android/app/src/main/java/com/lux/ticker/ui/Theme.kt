package com.lux.ticker.ui

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.core.view.WindowCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.lux.ticker.LuxApp
import com.lux.ticker.R
import com.lux.ticker.data.PaperPalette
import com.lux.ticker.data.TickerSettings

val TapeFont = FontFamily(Font(R.font.jetbrains_mono_bold, FontWeight.Bold))

private val Paper = Color(PaperPalette.BG)
private val PaperPanel = Color(PaperPalette.PANEL)
private val PaperInk = Color(PaperPalette.INK)
private val PaperBody = Color(PaperPalette.BODY)
private val PaperSub = Color(PaperPalette.SUB)
private val PaperRule = Color(PaperPalette.RULE)
private val Ink = Color(0xFF1A140C)
private val Orange = Color(PaperPalette.ACCENT)
private val Night = Color(0xFF12100E)
private val NightSurface = Color(0xFF1C1814)
private val NightRaised = Color(0xFF2A241C)
private val NightMuted = Color(0xFFC4B8A8)
private val Cool = Color(0xFFF4F6F3)

private val LuxLight = lightColorScheme(
    primary = Orange,
    onPrimary = Color.White,
    secondary = Orange,
    onSecondary = Color.White,
    background = Cool,
    surface = Color.White,
    surfaceVariant = Color(0xFFE4E8E2),
    onBackground = Ink,
    onSurface = Ink,
    onSurfaceVariant = Color(0xFF5C564C),
    error = Color(0xFFB42318),
    onError = Color.White,
    outline = Color(0xFFD5DAD3),
)

private val LuxPaper = lightColorScheme(
    primary = Orange,
    onPrimary = Color.White,
    primaryContainer = PaperPanel,
    onPrimaryContainer = PaperBody,
    inversePrimary = Orange,
    secondary = Orange,
    onSecondary = Color.White,
    secondaryContainer = Orange,
    onSecondaryContainer = Color.White,
    tertiary = Orange,
    onTertiary = Color.White,
    tertiaryContainer = PaperPanel,
    onTertiaryContainer = PaperBody,
    background = Paper,
    onBackground = PaperBody,
    surface = Paper,
    onSurface = PaperBody,
    surfaceVariant = PaperPanel,
    onSurfaceVariant = PaperSub,
    surfaceTint = Color.Transparent,
    inverseSurface = PaperBody,
    inverseOnSurface = Paper,
    error = Color(0xFFB42318),
    onError = Color.White,
    errorContainer = Color(0xFFF4D6D2),
    onErrorContainer = Color(0xFF5C1612),
    outline = PaperRule,
    outlineVariant = Color(PaperPalette.RULE_SOFT),
    scrim = PaperBody,
    surfaceBright = Paper,
    surfaceDim = PaperPanel,
    surfaceContainerLowest = Paper,
    surfaceContainerLow = Paper,
    surfaceContainer = PaperPanel,
    surfaceContainerHigh = PaperPanel,
    surfaceContainerHighest = PaperPanel,
)

private val LuxDark = darkColorScheme(
    primary = Orange,
    onPrimary = Ink,
    secondary = Orange,
    onSecondary = Ink,
    background = Night,
    surface = NightSurface,
    surfaceVariant = NightRaised,
    onBackground = Paper,
    onSurface = Paper,
    onSurfaceVariant = NightMuted,
    error = Color(0xFFFF5A5A),
    onError = Ink,
    outline = Color(0xFF3F382F),
)

@Composable
fun LuxTheme(content: @Composable () -> Unit) {
    val context = LocalContext.current
    val settings by LuxApp.container(context).settings.settings.collectAsStateWithLifecycle(
        initialValue = TickerSettings(),
    )
    val systemDark = isSystemInDarkTheme()
    val dark = settings.appTheme.resolveDark(systemDark)
    val scheme = when {
        dark -> LuxDark
        settings.appTheme.paperSurface(systemDark) -> LuxPaper
        else -> LuxLight
    }
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val activity = view.context.findActivity() ?: return@SideEffect
            val window = activity.window
            val argb = scheme.background.toArgb()
            window.statusBarColor = argb
            window.navigationBarColor = argb
            window.decorView.setBackgroundColor(argb)
            if (Build.VERSION.SDK_INT >= 29) {
                window.isStatusBarContrastEnforced = false
                window.isNavigationBarContrastEnforced = false
            }
            val bars = WindowCompat.getInsetsController(window, view)
            bars.isAppearanceLightStatusBars = !dark
            bars.isAppearanceLightNavigationBars = !dark
        }
    }
    MaterialTheme(colorScheme = scheme, content = content)
}

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}
