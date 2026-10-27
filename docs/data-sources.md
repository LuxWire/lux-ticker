# Data sources

Lux Ticker has no server of its own. Every market data source below is fetched **directly on the user's device** (the Windows PC or Android phone running the app), straight from the provider's public endpoint. There is no LuxWire proxy, relay, or cache server in between.

**No provider data is redistributed.** This repository contains no copies, snapshots, or cached files of any provider's data. Unit tests use small synthetic (made-up) payloads that only mimic the response format.

**You are responsible for complying with each provider's terms** when you build, run, or modify this app. Check the terms below before use, and do not add server-side collection, caching for others, or republishing of provider data.

| Provider | Used for | Terms |
|---|---|---|
| Yahoo Finance | Stock/ETF quotes, charts, symbol search | https://legal.yahoo.com/us/en/yahoo/terms/otos/index.html |
| Coinbase (Exchange API) | Live BTC-USD price and daily candles | https://www.coinbase.com/legal/user_agreement and https://www.coinbase.com/legal/developer-platform/terms-of-service |
| BitcoinTreasuries | Treasury company references (not fetched by the current code; if added, it must be fetched on-device only) | https://bitcointreasuries.net/terms |
| Blockchain.com (Android BTC long-range chart) | Historical BTC price | https://www.blockchain.com/legal/terms |
| X API (optional @LuxWire feed, user signs in with their own account) | Posts from @LuxWire | https://developer.x.com/en/developer-terms/agreement-and-policy |

Links to TradingView and Yahoo Finance pages are plain links that open in the user's browser; the app does not fetch data from TradingView.

Lux Ticker is not affiliated with or endorsed by any of these providers.
