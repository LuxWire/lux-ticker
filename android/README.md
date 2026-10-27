# LuxWire Ticker

Android companion to the Lux Windows ticker bar. A thin strip across the screen shows a pinned Bitcoin price and a scrolling tape of the rest of the list. It is for personal sideload on a Pixel. A Play App Bundle is prepared and has not been uploaded. There is no developer account yet.

On the phone, prices scroll in a short row under the status bar. Display → Vertical position sets that gap in dp. A new install, and a reset, use 0, so the strip sits on the real status-bar height. Negative moves the strip up onto it. Positive moves it down. A saved gap is kept. Bitcoin stays fixed on that strip when it fits, unless **Pin Bitcoin** is off, in which case it scrolls with the other names. Home-screen widgets are the wide tape, a 1×2, and a 2×2.

## What it does

- Overlay (`TYPE_APPLICATION_OVERLAY`) drawn by a foreground service. Layout y=0 is the top of the screen, so the real status-bar inset is added once, plus the vertical offset from Display (0 until you set a gap or reset). The strip is only as tall as the price row. It does not cover the clock or the status icons unless you pull it up with a negative offset. `FLAG_LAYOUT_IN_SCREEN` and `FLAG_LAYOUT_NO_LIMITS` keep that offset measured from the top of the screen. The strip is `FLAG_NOT_FOCUSABLE` and `FLAG_NOT_TOUCHABLE`, and its window alpha stays at 0.8 so Android will deliver taps and scrolls to whatever is behind it. The edge handle is off unless you turn it on in Settings. The notification's **Hide tape** / **Show tape** action turns the overlay off and on.
- The strip background is transparent, so the wallpaper shows through. It does not paint paper, white, or black. Symbols start at `#F7931A` and prices start white, each with its own setting, and the percent is green or red, in JetBrains Mono Bold at about 13 sp. Text uses the width of the screen, with a small side inset. Scroll defaults to right to left at 50 dp/s. **Left to right** is in Tickers. **Bitcoin side** is Left or Right. The scrolling names run on the other side. The shade and the lock screen share one notification. Its paper strip follows the app theme.
- **Orange dividers** is off unless you turn it on. Off, Bitcoin is separated from the next name by a dot. On, that gap is a thin orange line. An upgrade does not turn the line on.
- **Show on lock screen** is on for a fresh install. Turning it on sets `FLAG_SHOW_WHEN_LOCKED` on the overlay and makes the one ticker notification public. Bitcoin stays fixed. The other names step forward one character at a time while the screen is on, in the shade and on the lock screen. A tap opens the app. Turning it off hides that notification on the keyguard. Android still requires the notification for the foreground service, so the shade keeps the same tape. The app does not set `FLAG_TURN_SCREEN_ON` and does not wake the phone. A saved choice is kept.
- **@LuxWire posts** is on unless you turn it off. The ticker service polls the X API v2 user timeline for `LuxWire` every 3 minutes (2 or 5 in Settings). The first successful poll records the current posts and does not alert. A later post notifies, and a gap while offline catches up to the last 10. A tap opens `https://x.com/LuxWire/status/{id}`. The shade is paper (`#F6F2E8`) with the orange LW tile. **Quiet digest** is off unless you turn it on. Each alert has a Share action. There is no file handoff and no charge.
- Speed defaults to 50 dp/s when that setting has never been saved. **Pre and AH** is on. US names show `PRE` from 4:00–9:30 a.m. America/New_York and `AH` after the close, overnight, on weekends, and on NYSE holidays (including early 1:00 p.m. closes). Turn the setting off to hide both labels. Bitcoin and non-US names (`.T`, `.L`, and other exchange suffixes) have no tag.
- Symbols: the same list as the Windows bar (about 46). Edit them in the app: add ticker, remove ticker, drag to reorder, reset. Stored in DataStore. Bitcoin stays on the list. The left pin is optional.
- **Charts** open from the home screen and from any ticker row. The page is paper (`#F6F2E8`) with an orange (`#F7931A`) line. Presets are 1H, 1D, 1W, 1M, 6M, YTD, 1Y, and ALL, plus a custom from and to. 1H is the last 60 minutes at 1-minute bars. 1D uses 5-minute bars. Longer ranges use hourly or daily bars. **Line** and **Candles** are on every chart. Up candles are `#F7931A`. Down candles are ink `#282622`. Dragging ticks once per point or candle and shows the time in ET. A candle also shows open, high, low, and close. **Chart haptics** is on, and the tick does not depend on the phone's touch-feedback setting. The selected range shows its high and low, and the page shows the all-time high and low, each with the New York time. The search field at the top loads a symbol when you press Enter or the search button. Typed text and the cursor are ink `#161412`. Suggestions follow what you type. An unknown symbol stays on the page with an error. Short ranges refresh about every minute while the screen is open. Longer ranges refresh about every five minutes. Bitcoin from September 17, 2014 onward is Yahoo `BTC-USD`. Earlier Bitcoin history is the Blockchain.com market price. Other symbols are Yahoo. No API key.
- Bitcoin on the tape: Coinbase public websocket `wss://ws-feed.exchange.coinbase.com` (`BTC-USD` ticker). Change is versus `open_24h`.
- Everything else: Yahoo public chart `https://query1.finance.yahoo.com/v8/finance/chart/SYM?interval=1m&range=1d&includePrePost=true` with a browser User-Agent, about every 13 seconds, four at a time. Polling pauses while the screen is off. Failures back off (15 s up to 5 min). The last price stays and dims when it is stale.
- Starts on boot when that setting is on (it is on by default).
- Home-screen widgets: the wide tape, **LuxWire Ticker 1×2**, and **LuxWire Ticker 2×2**. A Quick Settings tile shows or hides the overlay. **Always overlay**, **Lock screen only**, and **Manual refresh** are in Settings. **Open battery settings** opens the system battery list. The app does not declare `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`. Start on boot still restores the strip after a restart.
- **Price alerts** are off until you turn them on. Bitcoin uses a ± percent band. **Add alert** saves another symbol and dollar target. The saved list sits under that field. A hit writes the date and time on that row and posts a popup in addition to the quiet or loud channel.
- **Share** on the home screen sends a strip screenshot with `@LuxWire`. Bitcoin is labeled Coinbase USD spot. Other names are Yahoo, updated or delayed. A stale price is dim and marked `°`. US names can show `PRE` or `AH` when that setting is on.

No API key and no paid data. A LuxWire account is optional. The ticker works signed out. Signed in, tickers and settings sync. Tokens stay in encrypted storage on the phone.

## Build

JDK 17 or newer, Android SDK platform 37.2, and build-tools 37.0.0. The app targets API 37.

```bash
echo "sdk.dir=$HOME/Android/Sdk" > local.properties
./gradlew :app:testDebugUnitTest
./gradlew :app:assembleRelease
```

The release APK is `app/build/outputs/apk/release/app-release.apk`.

### Account backend

Sign-in, cloud sync, and Pro entitlements use a Supabase project you configure yourself. Copy the example and fill in the project URL and the publishable anon key:

```bash
cp supabase.properties.example supabase.properties
```

`supabase.properties` is gitignored. Leave it out and the app still builds; account calls have no host until you add it. Do not put a service-role key in that file. This tree does not include SQL migrations.

### Signing

The delivered APK is signed. The keystore is not in git. To build an update that installs over this one, create `keystore/keystore.properties` (see `keystore.properties.example`) pointing at a keystore you keep yourself:

```bash
mkdir -p keystore
keytool -genkeypair -v \
  -keystore keystore/lux-ticker-release.jks \
  -alias luxticker \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -storepass YOUR_PASSWORD \
  -dname "CN=Lux Ticker, OU=Personal, O=Lux, C=US"
```

`keystore.properties`:

```
storeFile=keystore/lux-ticker-release.jks
storePassword=YOUR_PASSWORD
keyAlias=luxticker
keyPassword=YOUR_PASSWORD
```

PKCS12 uses one password for the store and the key. If `keystore/keystore.properties` is missing, the release build is signed with the debug keystore instead, and Android will not upgrade an install that was signed with a different key. Uninstall first in that case. Do not commit the `.jks` or the password file.

The Play upload key is a different keystore, kept outside this repo. Sideload builds stay on the key above so they still install over the existing APK. `LUX_UPLOAD_PROPS=/path/to/upload-keystore.properties ./gradlew :app:bundleUpload` signs an Android App Bundle with that upload key. The upload variant forces `X_BEARER` and `X_CLIENT_ID` to empty strings.

JetBrains Mono is bundled under the SIL Open Font License. See `third_party/jetbrains-mono/OFL.txt`.

## Install on a Pixel

1. Copy `LuxTicker.apk` onto the phone (USB, Drive, or Nearby Share). You do not need a Play account. USB debugging is optional.
2. Allow the app that will open the APK to install unknown apps. On current Pixels: **Settings → Apps → Special app access → Install unknown apps**, then pick Files or Chrome and allow it. Opening the APK also links straight to that switch if it is still off.
3. Tap the APK and install. No device-admin or device-owner access is required. Version 1.4.8 installs over 1.4.7 when it is signed with the same key. An update keeps settings that were already saved. The ticker stays directly under the status bar. Settings → Account can continue with Google or X. That sign-in opens in a browser tab and returns through luxwire://auth-callback. Privacy (https://luxwire.github.io/privacy.html) and Terms (https://luxwire.github.io/terms.html) are linked on that screen and on Contact / About. At night the shade tape uses a navy band, brighter gold, a light Pre and AH label, and lighter green and red. Daytime paper colors stay the same. Display → Vertical position accepts a typed dp value next to the slider. A fresh install draws the strip at that default. Settings → Account is optional. A new install uses 50 dp/s and sits directly under the status bar until you set a gap. The strip stays transparent. Pre and AH are on. Chart haptics are on. Symbol color and price color are separate. Each setting has a reset, and Reset everything asks first. Charts include 1H and a Line / Candles toggle. Search on a chart loads the symbol you type. One notification carries the tape in the shade and on the lock screen. Paper matches the LuxWire article card (`#F6F2E8`, accent `#F7931A`). Typed text on paper is ink `#161412`. @LuxWire alerts still come from the X feed. This build is a sideload until a Play install exists. Billing is not in the app.
4. Open **LuxWire Ticker**.
5. Tap **Allow display over other apps**. On the system screen, turn **LuxWire Ticker** on, then return. The strip sits flush under the status bar.
6. Allow notifications when asked. The ongoing notification is the tape. It is silent. **Hide tape** and **Show tape** live there. There is not a second "Status bar" row. **@LuxWire posts** uses its own channel.
7. Optional: long-press the home screen, open Widgets, and place **LuxWire Ticker**, **LuxWire Ticker 1×2**, or **LuxWire Ticker 2×2**. Optional: add the **LuxWire Ticker** Quick Settings tile.
8. Optional: Settings → **Orange dividers** if you want the orange line after Bitcoin. Leave **Edge handle** off and the strip takes no taps.

From a computer, with USB debugging already on:

```bash
adb install -r LuxTicker.apk
```

If the strip does not come back after a reboot, set LuxWire Ticker's battery use to Unrestricted (**Settings → Apps → LuxWire Ticker → App battery usage**). The service does not hold a wake lock and does not poll while the screen is off.

## Tests

```bash
./gradlew :app:testDebugUnitTest
```

Covers the NYSE calendar (holidays, observed days, early closes, DST), Yahoo and Coinbase quote parsing against saved fixtures (the Yahoo file is a real chart response, the Coinbase file is a real ticker frame), symbol persistence and reorder, and the rule that BTC is pinned and absent from the scrolling tape and the widget text. A Robolectric test lays out the strip, checks that the pin reserves the left edge, and writes `ticker-strip.png`. Another checks that the overlay sits one status-bar height below the top of the screen until a gap is saved, that a second status-bar inset is not added on top of that, that the strip is shorter than the status bar, and that `FLAG_SHOW_WHEN_LOCKED` is set only when **Show on lock screen** is on. `PlayListingScreenshots` renders the store phone screenshots from the real screens into `app/build/reports/screenshots`.

## Limits

- Stock quotes are polled, not streamed. Yahoo's public chart endpoint is unofficial. It can rate-limit, change shape, or return nothing. The app keeps the last good price and dims it.
- Session tags follow the NYSE cash clock in `America/New_York`, not Tokyo or London hours, and not the nearly 24-hour clock of `CL=F`. Non-US suffixes and crypto pairs simply omit the tag.
- The widget marquee depends on the launcher. The overlay is the smooth scroll.
- A new build signed with a different key cannot upgrade this install.
- Lock screen and shade: **Show on lock screen** is on for a new install, and a saved on/off choice stays. The overlay flag is still applied, but stock Pixel System UI force-hides non-system overlays while the keyguard is up. The tape the user sees there is the foreground-service notification. There is no second "Status bar" row. Bitcoin stays fixed. While the screen is on, including the shade and the lock screen, the other names advance one character at a time. The step is about 160 ms at 50 dp/s. Android will not run a 60 fps animation inside a notification, and `setSelected` is not a remotable method, so the layout marquee does not move on its own. Turning **Show on lock screen** off hides the tape on the keyguard. The shade still shows it. The app does not dismiss the keyguard and does not wake the screen.
- LuxWire alerts poll X. There is no Play billing, no Wade file, and no Windows notify. Public posts need an app-only bearer. In the app, open Settings → Alerts, paste the bearer, and tap **Save bearer**. The token stays in the app's private storage and is not in source. A build machine can also set `x.bearer` in `local.properties` (gitignored) so `BuildConfig.X_BEARER` is filled at compile time. Leave it blank to keep the secret out of the APK.

  Subscriber posts are not in the app-only timeline. Create an X app with OAuth 2.0 and a public client, set the callback URL to `luxwireticker://oauth`, and enable scopes `tweet.read`, `users.read`, and `offline.access`. Paste that client id in Settings, tap **Save client id**, then **Sign in to X**. The signed-in account must be able to read the subscriber posts. The app then polls with that user token and refreshes it. **Sign out of X** drops the user token and leaves the bearer in place. **Check @LuxWire now** polls once. Turning **@LuxWire posts** off stops alerts. The poll runs while the ticker service is alive.
- The strip's top edge depends on the phone reporting a status-bar inset. Until that inset arrives, the strip uses the system status-bar height. A side cutout still insets the text.
- Overlay placement, tap pass-through, the notification action, boot start, lock-screen visibility, and 60/120 Hz scroll cannot be confirmed without a phone. This environment has no Pixel and the checks above are unit tests plus a Robolectric render.
