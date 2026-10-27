# LuxWire Ticker
A free, open-source scrolling market ticker for Bitcoin and Bitcoin-treasury stocks, for **Android** and **Windows**. No ads, no account, no tracking.

> Status: pre-release skeleton. Source code (Android and Windows, Pro features included) will be added after a secrets audit.

## Features
- Scrolling ticker with symbol, price and change, each in its own color, plus background color and opacity settings
- Windows: always-on-top bar above the taskbar that stays put on Win+D, click-through mode with Chart and Settings still clickable, and a tray icon
- Chart page with search for any ticker
- Pre-market and after-hours quotes (Android)
- Settings with sidebar and search, per-section resets and reset-all
- Recommended ticker fonts listed first
- Optional LuxWire post alerts
- All times labeled ET

## Data sources
Prices come from a pluggable provider. Only sources whose terms allow it are shipped by default, and some fetch on the user's device under that provider's own terms. See `docs/data-sources.md` (to be added).

## Build
Android (planned):
```
cd android && ./gradlew assembleRelease   # needs JDK 17+ and Android SDK (target API 36+)
```
Windows (planned):
```
cd windows && dotnet publish -c Release
```
Release signing keys are never stored in this repo. Use your own keystore for local builds.

## Screenshots
_Placeholder: Android ticker, Windows bar, Chart page, Settings._

## Contributing
Issues and pull requests are welcome. Please:
1. Open an issue first for big changes.
2. Keep secrets, keys and personal data out of commits.
3. Run the tests before opening a PR.

## License
MIT. See LICENSE. © 2026 LuxWire
