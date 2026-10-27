# LuxWire Ticker 1.4.8

Installs over 1.4.7. Package `com.lux.ticker`. versionCode 31. The ticker stays directly under the status bar. A restart, rotation, theme change, lock screen, or service restart does not add that bar a second time. A gap under the bar is added only when one was already saved. Privacy is `https://luxwire.github.io/privacy.html` and Terms are `https://luxwire.github.io/terms.html`.

Sideload APK SHA-256: `9a3f48654056c27056fd165b1fa95c3e85a5a8b9cbc8298c1cff7b1239efc50c`

Same sideload certificate as 1.4.1 through 1.4.7, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `9e84f5ea3f90e9b9cf03db19f2911d8b519898395cc160f13780dd047b4535c1`

The Play bundle is signed with the same upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded.

APK: `LuxTicker-Android-1.4.8.apk` (also copied as `LuxTicker.apk`). AAB: `play/LuxTicker-Android-1.4.8-vc31.aab`.

On a Pixel 10 Pro XL the window is sometimes already placed below the status bar. The app still added that bar again, and it also added the old 30 dp factory gap when no gap had been saved. Those two extras land near 115 dp, on top of the home-screen widgets. The real status-bar height is now applied once. A saved gap is kept. A missing gap stays 0, which is flush with the real bar, and that 0 is not written over an existing setting.

## 1.4.7

Installs over 1.4.6. Package `com.lux.ticker`. versionCode 30. An update keeps settings that were already saved. What's New is one roll-up per minor version: 1.4, 1.3, 1.2, 1.1, then 1.0. Privacy is `https://luxwire.github.io/privacy.html` and Terms are `https://luxwire.github.io/terms.html`.

Sideload APK SHA-256: `feffd1cc2fe826935eb018e53689cc619adad89b583b8851e4bf7a44f8b59bd0`

Same sideload certificate as 1.4.1 through 1.4.6, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `bdc2f38b8137621533f6957ba4367da1ad05ca06cfcc3a6fc9eca51a68d9ebbb`

The Play bundle is signed with the same upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded.

APK: `LuxTicker-Android-1.4.7.apk` (also copied as `LuxTicker.apk`). AAB: `play/LuxTicker-Android-1.4.7-vc30.aab`.

Settings were being replaced on update for two reasons that both run on the first launch after install. Cloud sync treated a missing `sync_updated_at` as time 0, pulled a newer server row, and wrote every field. A server document that omitted a key was filled with factory defaults before that write. `ensureFactoryDefaults()` also wrote the 30 dp offset whenever that one key was missing, including on an upgrade that already had other settings. Defaults now apply only to an empty file. A phone that already has settings and no clock pushes those settings. A pull changes only keys present in the server JSON. The datastore is excluded from backup, and if Android still restores after the rule change, an existing datastore file is kept.

## 1.4.6

Installs over 1.4.5. Package `com.lux.ticker`. versionCode 29. Continue with Google and Continue with X use Supabase PKCE in a Custom Tab and return on `luxwire://auth-callback`. Privacy and Terms are linked on sign-in and About.

Sideload APK SHA-256: `2d25cd86599a279410bae2ee5b5b213614a7525e6a135ed493018ab3d40aff46`

Same sideload certificate as 1.4.1 through 1.4.5, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `d1b7744d23100b644604b335f72829f5697ade63eca2d580263cffcca192077a`

The Play bundle is signed with the same upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded.

APK: `LuxTicker-Android-1.4.6.apk` (also copied as `LuxTicker.apk`). AAB: `play/LuxTicker-Android-1.4.6-vc29.aab`.

## 1.4.5

Installs over 1.4.4. Package `com.lux.ticker`. versionCode 28. The pull-down tape follows system night mode: navy `#0E1520`, brighter gold `#FFD15C`, light Pre/AH `#F4EFE4`, lighter green `#7DFFA3`, lighter red `#FF8A80`. Each of those is at least 4.5:1 on the navy. Daytime paper colors are unchanged.

Sideload APK SHA-256: `38fd906336d9d82f3f00ceb1b3daf1eceae4d73e9bdbe118a661813adae62452`

Same sideload certificate as 1.4.1 through 1.4.4, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `69e8333601dee0ea1b34affc72ae5738e125699a47fe6715e5f7d119a1c48021`

The Play bundle is signed with the same upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded.

APK: `LuxTicker-Android-1.4.5.apk` (also copied as `LuxTicker.apk`). AAB: `play/LuxTicker-Android-1.4.5-vc28.aab`.

## 1.4.4

Installs over 1.4.3. Package `com.lux.ticker`. versionCode 27. Profile reads and writes use `profiles.user_id`.

Sideload APK SHA-256: `a0b1e3fa9ae2f88858854aa95d361f1da24e78a7525d9676fe99d60f585e2fd2`

Same sideload certificate as 1.4.1 through 1.4.3, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `edf9c9cbcdf46e9949d6ce9d7ff189c0b76acf6f9c72c0fac3cb5dd5e0496f51`

The Play bundle is signed with the same upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded.

APK: `LuxTicker-Android-1.4.4.apk` (also copied as `LuxTicker.apk`). AAB: `play/LuxTicker-Android-1.4.4-vc27.aab`.

## 1.4.3

Installs over 1.4.2. Package `com.lux.ticker`. versionCode 26. Display has a typed vertical position, and that position is what the strip draws. A LuxWire account is optional.

Sideload APK SHA-256: `74a9f5e16bf0300721a50907166cd49c37c0854d874f166a893c5caceb6a8b38`

Same sideload certificate as 1.4.1 and 1.4.2, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `83e4fa5e3acdbbb0bdbc05bb3875416d471888574a3e2a42f942e6c9bb560afb`

The Play bundle is signed with the upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded. SQL migrations were not applied to the Supabase project.

APK: `LuxTicker-Android-1.4.3.apk` (also copied as `LuxTicker.apk`). AAB: `play/LuxTicker-Android-1.4.3-vc26.aab`.

## 1.4.2

Installs over 1.4.1. Package `com.lux.ticker`. versionCode 25. What's New is a bulleted list: one short line per change, grouped by version, newest first.

The 1.4.1 sideload APK remains versionCode 23 on the sideload certificate, SHA-256 `2a4f069cca867c9e362f75d6ced5bc576f5f2a5c931886e51b1389e03f60885b`. The 1.4.1 Play bundle was versionCode 24. This build is versionCode 25 on both the sideload APK and the Play bundle.

Sideload APK SHA-256: `b9077f06a9176f4653ff722dcdbc4bf05a0221933e6e6e9848de903a550e8809`

Same sideload certificate as 1.4.1, SHA-256 `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`.

Play AAB SHA-256: `ed70d7f6b0ddfb04f9dcba05ca9dc5904b0f5f39e1c9178ca6c7307c73991a8d`

The Play bundle is signed with the upload key, certificate SHA-256 `0D:18:32:AF:9A:76:09:A5:0E:24:F8:9F:AD:80:B6:8C:1A:CB:FA:2A:DD:34:93:D9:8B:4E:70:79:0A:60:91:30`. It has not been uploaded.

SHA-256: `2a4f069cca867c9e362f75d6ced5bc576f5f2a5c931886e51b1389e03f60885b`

Certificate SHA-256: `d39d70febde76e274bdcf216a57761d7f27d2de015dba4a2b65646f665139597`

APK: `LuxTicker-Android-1.4.1.apk` (also copied as `LuxTicker.apk`).

## Fixes

- Chart search loads the symbol you type. Press Enter, IME Done, or the search button. The symbol is trimmed and uppercased. A name already on the tape opens immediately. An unknown symbol is checked with Yahoo and stays on the page with “No chart for XYZ.” A bad symbol says “Invalid ticker.” Matching names appear as you type.
- Typed text and the cursor are ink `#161412` on the paper chart, including when the app theme is Dark. Other fields use `#161412` on paper and light, and cream `#F6F2E8` on the night surface.
- Chart scrubbing ticks on each point in Line and each candle in Candles. **Chart haptics** is on unless you turn it off. While it is on, the tick does not follow the phone’s touch-feedback setting. The view asks for `CLOCK_TICK`, then `SEGMENT_TICK` on API 30+, with `KEYBOARD_TAP` on older phones, and a short vibrator tick if the view does not perform it. `VIBRATE` is in the manifest.
- The name on the phone is LuxWire Ticker. The launcher label, the notification channel, headers, and What’s New use one word. The package id is still `com.lux.ticker`.

## Defaults

A missing preference, a new install, and **Reset everything** use these. A value you already saved, including a 0.3 dp gap or a 36 dp/s speed from 1.3.x, stays until you reset that setting.

- Scroll speed: 50 dp/s. The slider is still 12–160.
- Vertical position: 30 dp under the status bar. Range is still −120 to 800.
- Pre and AH: on. US names show Pre from 4:00 to 9:30 a.m. New York, and AH after the close. Bitcoin and non-US names stay untagged.
- Chart haptics: on. Display has a switch and a reset.
- Symbol color: orange `#F7931A`. Price color: white. The percent stays green or red.
- Each setting has its own Reset. **Reset everything** asks first. Tickers and the X sign-in stay. Saved price alerts are cleared.

## Charts

- Presets are 1H, 1D, 1W, 1M, 6M, YTD, 1Y, and ALL, plus a custom from and to.
- 1H is the last 60 minutes, requested as 1-minute bars. 1D stays 5-minute bars. A week or month uses hourly bars. Longer ranges use daily bars.
- **Line** and **Candles** are on Bitcoin and on every other ticker. Up candles are `#F7931A`. Down candles are ink `#282622`.
- Dragging ticks once per point in line mode, and once per drawn candle in candle mode, when chart haptics is on. Lifting your finger clears the crosshair.
- A candle under your finger shows open, high, low, close, and the time in ET.
- The selected range shows its high and low. The page also shows the all-time high and all-time low, each with the New York date and time. Bitcoin’s all-time figures merge Yahoo daily high/low with the Blockchain.com history. Other symbols use Yahoo daily high/low.
- A ticker search sits at the top of every chart. Tapping a name in Tickers opens that chart.
- The home strip stays transparent. The paper palette and the @LuxWire feed are unchanged.

## Notifications

1.4.1 keeps one LuxWire Ticker notification, id 1001, on the public channel. Hide tape and Show tape stay on that one row. There is no “Status bar” line. The shade title is the app label, LuxWire Ticker.

While the screen is on, the names beside Bitcoin advance one character at a time. At 50 dp/s that step is about 160 ms, in the shade and on the lock screen. Turning **Show on lock screen** off hides the tape on the keyguard. The shade still shows it, because Android requires the foreground-service notification.

## What Android does not allow

RemoteViews cannot run the home strip’s frame-by-frame scroll. `setSelected`, which a marquee needs, is not a method SystemUI will call on a notification TextView, so the marquee in the layout does not move. Notification updates are also coalesced by the system, so a 60 fps crawl is not available on the shade or the lock screen.

The working alternative is the character step above, on the single public notification, whenever the screen is interactive. The home overlay is still the smooth scroll. The screen-off and always-on display do not get that step, because the process waits until the display is on.

There is no phone in this build environment. Search, field contrast, and the scrub tick were checked with unit tests, not on a device.

## Play bundle

The sideload APK in this note stays versionCode 23 and stays on the sideload certificate. The Play App Bundle is versionCode 24 of the same 1.4.1 code, signed with a new upload key that is not in git. `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` is not in that bundle. Settings opens the system battery list instead. X tokens are excluded from Android backup, and the upload build does not embed an X bearer or client id. Nothing has been uploaded.
