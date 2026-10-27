package com.lux.ticker.ui

object ReleaseNotes {
    const val VERSION = "1.4.8"
    const val DATED = "October 27, 2026 ET"
    const val PRIVACY_URL = "https://luxwire.github.io/privacy.html"
    const val TERMS_URL = "https://luxwire.github.io/terms.html"

    const val WHATS_NEW =
        "1.4\n" +
            "The ticker stays directly under the status bar\n" +
            "A restart, rotation, or theme change does not drop it onto the widgets\n" +
            "An update keeps settings that were already saved\n" +
            "Continue with Google and Continue with X are on\n" +
            "Sign-in opens in a browser tab and returns to the app\n" +
            "Privacy and Terms are linked on sign-in and About\n" +
            "The pull-down tape follows system night mode\n" +
            "Night is navy, with brighter gold and a light Pre and AH label\n" +
            "Green and red are lighter on that navy\n" +
            "Daytime paper colors stay the same\n" +
            "The account profile uses the shared user id\n" +
            "Vertical position has a box for the dp value, next to the slider\n" +
            "A fresh install draws the strip at the default position\n" +
            "Account is optional, and the ticker works signed out\n" +
            "Chart search loads the symbol when you press Enter or the search button\n" +
            "Typed text on paper is ink #161412, including the cursor\n" +
            "Chart scrubbing ticks on each point and each candle\n" +
            "Symbol color and price color are separate\n" +
            "Pre and AH are on\n" +
            "Ranges include 1H, 1D, 1W, 1M, 6M, YTD, 1Y, and ALL\n" +
            "Line or Candles\n" +
            "One notification carries the tape\n\n" +
            "1.3\n" +
            "Vertical position is in Display\n" +
            "The gap is dp from the bottom of the status bar\n" +
            "The gap under the status bar is 0.3 dp\n" +
            "0.3 sits just under the status bar\n" +
            "The home strip is flush under the status bar\n" +
            "The status-bar tape background is transparent again\n" +
            "The status-bar tape follows the app theme\n" +
            "Paper matches LuxWire articles\n" +
            "Background is #F6F2E8\n" +
            "Accent is #F7931A\n" +
            "Bitcoin chart on the home screen\n\n" +
            "1.2\n" +
            "Built-in @LuxWire X feed alerts\n" +
            "Public posts use a bearer token\n" +
            "Sign in to X for subscriber posts\n" +
            "Alerts are on unless you turn them off\n\n" +
            "1.1\n" +
            "The name on the phone is LuxWire Ticker\n" +
            "The lock-screen ticker is paper, not black\n" +
            "Paper is a display color next to System, Light, and Dark\n" +
            "Tickers opens on the full list\n" +
            "You can save more than one price alert\n\n" +
            "1.0\n" +
            "Settings is a menu\n" +
            "Tickers, Display, Alerts, Battery, What's new, and Contact / About each open on their own screen\n" +
            "The app follows the phone theme unless you pick Dark or Light under Display\n" +
            "This install is a sideload until Play\n" +
            "There is no billing in this version"

    const val ABOUT =
        "Sideload until Play. This build is installed from an APK. A Play Store version is planned. Billing is not in this app.\n\n" +
            "Privacy. Bitcoin prices are the Coinbase USD spot. Other prices are Yahoo chart quotes, updated on a delay, not an exchange feed. " +
            "The Bitcoin chart uses Yahoo BTC-USD, and years before September 2014 use the Blockchain.com market price. " +
            "LuxWire posts are read from the @LuxWire timeline on X. " +
            "LuxWire Ticker does not ask for location or contacts, and it does not sell data. " +
            "Publisher: Hermes Lux. The privacy policy is the page linked from the store listing. " +
            "An X sign-in stays on this phone and is left out of Android backup. " +
            "An optional LuxWire account syncs tickers and settings. The ticker works without one."

    const val CONTACT = "@LuxWire on X"

    const val WHY_OVERLAY =
        "LuxWire Ticker draws a thin price strip over other apps, just under the status bar. It does not read the screen. Taps pass through to the app behind it."
    const val WHY_LOCK = "So prices can show on the lock screen. Stock Pixel still hides the overlay itself. The shade and the lock screen share one notification."
    const val WHY_NOTIFICATIONS =
        "Notifications are the silent ticker tape, LuxWire posts, and price alerts. Nothing else is posted."

    const val EMPTY_TICKERS = "No tickers yet. Add one above."
    const val REFRESH_ERROR = "Couldn't refresh prices. Try again."
    const val INVALID_TICKER = "Invalid ticker"
}
