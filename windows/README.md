# LuxWire Ticker

A thin stock and crypto ticker for Windows 10 and Windows 11 (x64). It sits on the top or bottom edge of the primary monitor, scrolls right to left, and registers as a Windows app bar so maximized windows leave room for it.

No administrator password. Prices come from public feeds and need no API key. A LuxWire account is optional; the ticker works signed out.

Version 1.4.14. Install with `LuxTicker-Setup-1.4.14.exe`, or run the portable `LuxTicker.exe`. Running the Setup again upgrades the copy already installed and keeps settings. The ticker docks directly above the Windows taskbar (not on top of Start) and loops. A new install uses the Wall Street tape: a dark board with amber and green. Appearance can switch to NYSE, Nasdaq, Cboe, NYSE American, NYSE Arca, IEX, MIAX, MEMX, or Lux Wire paper, and the background color accepts any hex or RGB. Opacity still applies. Colors you already saved stay. Speed, font, spacing, and separate symbol, price, and change colors are in Settings. Live updates while scrolling rewrites prices in place and flashes green on an uptick and red on a downtick. Vertical position moves the bar from the dock, from −240 to +640. Always on top is on, so Show Desktop and a full-screen app leave the bar where it is. Paper matches the Lux Wire article colors: cream `#F6F2E8` and orange `#F7931A`. **Chart** on the bar opens a chart. Search any symbol. 1H is the last 60 minutes. Line or Candles switches the series. Click or drag to read the price, or open, high, low, and close, with the Eastern time. @LuxWire post alerts poll X while the app is running. Open Settings from the Start Menu, the label on the bar, or the tray icon. Click-through lets clicks pass through the scrolling prices. Chart and Settings stay clickable. Check for updates is in Settings. A startup failure writes `%APPDATA%\LuxTicker\startup.log` and shows the error. The same log records the bar position, size, opacity, and monitor.

## Install

Run `LuxTicker-Setup.exe`. It does not ask for an administrator password. The wizard installs LuxWire Ticker for the current user under `%LOCALAPPDATA%\LuxTicker` and shows the files being copied.

It always adds:

- A **Start Menu** shortcut, **LuxWire Ticker**, so the app shows up with other apps
- **LuxWire Ticker Settings** in the Start Menu
- An **Apps & features** entry with the orange Lux Wire icon, and an uninstall command

The wizard asks:

- **Desktop shortcut** (on the first time you install)
- **Pin to Start** — Windows 11 may ignore an installer pin; the Start Menu entry is still there
- **Pin to the taskbar** — Windows 11 usually blocks this. If it does not pin, open Start, right-click LuxWire Ticker, and choose Pin to taskbar
- **Start LuxWire Ticker when Windows starts** — the same per-user Run key the app uses (`LuxTicker`)

The last page can launch LuxWire Ticker, which opens Settings so you can see the app. After that, the Start Menu or desktop shortcut opens the ticker above the taskbar. The tray icon is still there.

Settings, the symbol list, and logs stay in `%APPDATA%\LuxTicker`. Uninstall does not delete them.

## Run the portable exe

Double-click `LuxTicker.exe` if you do not want to install. The first launch writes its files under `%APPDATA%\LuxTicker` and docks to the bottom of the primary monitor, directly above the taskbar. After a browser or Drive download, see `RUN.txt` (Unblock / SmartScreen).

The bar is half the taskbar’s height in physical pixels (a 45 px taskbar produces a 23 px bar). It follows DPI changes and taskbar moves. If the taskbar auto-hides, the ticker sits flush with that screen edge. Always on top is the default, so Show Desktop, Win+D, Win+M, and a full-screen app leave the bar in place. Turn that off under Position & Dock and a full-screen app can cover it.

**Chart** on the right end of the bar opens the chart. Right-click the bar, or click **Settings** beside it:

- **Chart** — starts on BTC/USD. The search box at the top takes a stock, an ETF, or crypto such as BTC or ETH. Enter, or a suggestion, loads that symbol. Recent searches stay. An unknown symbol says so. Presets are 1H, 1D, 1W, 1M, 6M, YTD, 1Y, and ALL. 1H is the last 60 minutes. Line or Candles switches the series. 1H candles are 1-minute, 1D candles are 5-minute, and longer ranges are hourly or daily. Custom From and To for Bitcoin go back to 17 Sep 2014, the first print Yahoo publishes. Click or drag to read the price, or open, high, low, and close, with the Eastern time. The chart refreshes while it is open. The header shows the symbol.
- **Settings...** — tickers, appearance, motion, dock, charts, account, updates, and about. What's new is in Updates. Account is optional: email and password, with a username if you want one. Signed in, settings and the watchlist sync. Sign out, delete account, and forgot password are there. Continue with Google and Continue with X open the browser and return to this PC. Privacy and Terms are on that screen and in About.
- **Above the taskbar** / **Top of the screen**. Vertical position is a slider under Dock. 0 is that baseline. Negative moves toward the edge. Positive moves into the desktop.
- **Speed** — slow, normal, or fast
- **Start with Windows** — on or off. This writes a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` and nowhere else.
- **LuxWire post alerts** — one switch, plus how often to check, and only when a token is already saved. There is no key field. Turn it off and polling stops.
- **Edit tickers...** — add, remove, and reorder symbols
- **Quit**

The scroll pauses while the pointer is over the bar. Only one instance runs at a time.

The orange LW tile is the exe icon, the Setup icon, the Start Menu icon, the window icon, the notification icon, and the tray icon. Letters are paper `#F6F2E8` on `#F7931A`. Every size, from 16 through 256 including 96, is the same mark scaled. The tray and title bar use the frame that matches the current DPI. Right-click the tray icon for the same menu. Double-click the tray icon, or the bar, to open Settings. The Start Menu item **LuxWire Ticker Settings** opens Settings even when the ticker is already running.

## Edit the symbols

**Edit tickers...** checks a quote before a symbol is added. `BTC` is the Coinbase feed. Anything else is a Yahoo symbol (`BRK-B`, `3350.T`, `^GSPC`, `CL=F`). An optional label changes what is shown (`^GSPC` is labeled `S&P 500` in the default list). Drag a row, or use Up / Down, to reorder. **Reset to default list** restores the built-in set. Changes hit the scrolling bar immediately.

The live list is `%APPDATA%\LuxTicker\symbols.json`. You can edit that file while the app is closed, or while it is open (it reloads the file). Dock, speed, startup, and Lux Wire post alerts are in `%APPDATA%\LuxTicker\settings.json`. A log is at `%APPDATA%\LuxTicker\lux-ticker.log`.

## @LuxWire posts

Pro features are included. There is no paywall in this build. Alerts are on until you turn them off. While LuxWire Ticker is running it polls `https://api.x.com/2/users/:id/tweets` for @LuxWire every 3 minutes (2, 3, or 5 in Settings). The first poll marks the posts already on the timeline as seen and does not toast. Later polls toast new posts. A gap while the PC is offline is caught up on the next poll, capped at the newest 10. The seen cursor is `%APPDATA%\LuxTicker\lux-wire\seen.json`. Tap a toast and Windows opens `https://x.com/LuxWire/status/{id}`.

No token is built into the app, and Settings never asks for one. If `%APPDATA%\LuxTicker\x-auth.json` has no usable token and the environment does not supply one, the LuxWire section stays hidden.

A token already saved keeps working. `LUXWIRE_X_BEARER` fills an empty bearer and is not written back to disk. `LUXWIRE_X_CLIENT_ID` fills an empty client id the same way. The file stays `%APPDATA%\LuxTicker\x-auth.json`.

A saved user token is used for polls, so subscriber posts this account can see are included. When that token is missing or stale and refresh fails, the app falls back to the bearer and public posts still toast. Turning **LuxWire post alerts** off stops polling and does not move the seen cursor. The callback a saved sign-in uses is `http://127.0.0.1:47321/callback`.

```json
{
  "symbols": [
    { "symbol": "BTC", "label": "BTC", "kind": "crypto", "currency": "USD" },
    { "symbol": "3350.T", "label": "3350.T", "kind": "intl", "currency": "JPY" },
    { "symbol": "SWC.L", "label": "SWC.L", "kind": "intl", "currency": "GBp" },
    { "symbol": "^GSPC", "label": "S&P 500", "kind": "us", "currency": "USD" },
    { "symbol": "CL=F", "label": "OIL", "kind": "continuous", "currency": "USD" }
  ]
}
```

`kind` is `crypto`, `us`, `intl`, or `continuous`. US names show `PRE` before the open. After the close the bar shows the symbol and the price. Crypto, non-US listings, and `continuous` names (crude oil, and symbols such as `ETH-USD`) do not get a session tag. `3350.T` is shown in yen. `SWC.L` is shown in pence (`GBp`, not pounds).

## What the numbers are

- **BTC** streams from the Coinbase Exchange public websocket (`wss://ws-feed.exchange.coinbase.com`, ticker channel `BTC-USD`). The percent is versus the previous completed UTC daily close. The socket reconnects on its own.
- **Everything else** is polled from Yahoo’s public chart endpoint about every 12 seconds (`interval=1m&range=1d&includePrePost=true`), a few symbols at a time. A failed symbol keeps its last price and dims. The feed backs off if Yahoo rate-limits.
- The percent is versus the previous regular close. During pre-market the price is the pre-market print. After hours, overnight, and weekends it is the latest extended-hours print.

US session times are Eastern, including daylight saving. The regular session is 9:30–16:00. Pre-market is 4:00–9:30. After-hours is 16:00–20:00 (13:00–17:00 on NYSE early-close days). Holidays and early closes follow the NYSE calendar (weekends observed, Good Friday, Juneteenth, Thanksgiving Friday, Christmas Eve, July 3 when July 4 is a weekday).

## Account and updates

Copy `luxwire.config.example.json` to `luxwire.config.json`. That copy is gitignored. Fill in:

- `supabaseUrl` — your project URL, such as `https://YOUR_PROJECT.supabase.co`
- `supabaseAnonKey` — the anon or publishable key from that project
- `updateFeedUrl` — optional. Leave it empty to skip update checks. When set, it is the URL of a text file: a version on the first line, and a Setup download URL on the second line.

The app looks for the file in this order: the path in `LUXWIRE_CONFIG`, `luxwire.config.json` next to the program (and up to eight parent folders, so a checkout works), then `%APPDATA%\LuxTicker\luxwire.config.json`.

Sign-in, sync, and Google or X account sign-in stay in the app. They do nothing until the URL and key are set. Pro features are included. There is no paywall in this build. The shared account SQL is not in this repository. Create the tables in your own project. The installer does not apply SQL.

## Build

Requires the .NET 8 SDK. From this directory:

```bash
dotnet test tests/LuxTicker.Tests/LuxTicker.Tests.csproj -c Release
dotnet publish src/LuxTicker.App/LuxTicker.App.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:EnableWindowsTargeting=true \
  -p:DebugType=embedded
```

The portable exe is:

`src/LuxTicker.App/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/LuxTicker.exe`

The installer is Inno Setup 6 (`installer/LuxTicker.iss`), per-user, no admin password. Compile it with ISCC after the publish step. Output: `installer/output/LuxTicker-Setup-1.4.14.exe`. The same AppId and folder replace an older install. Settings under `%APPDATA%\LuxTicker` are left in place.

Cross-compiling from Linux is supported (`EnableWindowsTargeting`). GitHub Actions (`.github/workflows/build.yml`) runs the tests on Ubuntu and publishes the exe on `windows-latest`. Copy the exe wherever you like. It does not need to stay next to other files.

## No administrator rights

`app.manifest` sets `requestedExecutionLevel` to `asInvoker` with `uiAccess="false"`. The setup package uses the same rule (`PrivilegesRequired=lowest`). Neither one asks for an admin password. The app does not write to Program Files, does not write HKLM, and does not install a service or a scheduled task. The installer copies `LuxTicker.exe` to `%LOCALAPPDATA%\LuxTicker` and registers uninstall under the current user so Apps & features can remove it. Settings, the symbol list, the log, the font, `x-auth.json`, and the account session (`account.bin`, protected for the current Windows user) live under `%APPDATA%\LuxTicker`. The project URL and anon key come from `luxwire.config.json`. See Account and updates above. The SQL schema is not in this repository. This ticker uses the app key `lux-ticker-windows`. The same tables can serve the Android ticker (`lux-ticker-android`) and Lux 531 (`lux-531`). Toasts use a per-user Start Menu shortcut, `%APPDATA%\Microsoft\Windows\Start Menu\Programs\LuxWire Ticker.lnk`. Start with Windows is only the per-user Run key.

## Limits

Stock quotes are polled, not streamed. A symbol Yahoo does not know cannot be added. The app bar covers the primary monitor. Vertical taskbars are inset so the ticker does not cover them. On Windows 11 the shell’s suggested bottom rect is the screen edge, inside the taskbar; LuxWire Ticker discards that and reserves the band above the taskbar instead. Auto-hide taskbars can slide over the ticker while they are visible, which is how Windows treats that edge. A taskbar that lives on another monitor does not move the primary bar.

## License

MIT. See `LICENSE`.
