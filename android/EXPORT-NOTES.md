# Export notes

Android source for LuxWire Ticker 1.4.11 (versionCode 34).

Source commit: `f2787597053c44ae604e4af07684107633f57093`

This directory is a stripped copy of that commit. It has no git history.

## Removed

- Signing configs in `app/build.gradle.kts` (release keystore and Play upload key). Release builds use the debug signing config.
- `keystore.properties.example` and any keystore files. None were in the commit. `local.properties` was not in the commit.
- `google-services.json` was not in the commit.
- The Supabase project URL and publishable anon key. They are read from gitignored `supabase.properties`. `supabase.properties.example` is checked in with empty values.
- X bearer and client id stay out of source. A local build can set them in gitignored `local.properties`. They are empty when that file is absent.
- Personal names and a retired GitHub pages host that appeared only in negative test assertions.
- Machine paths that pointed at this agent's store and artifact directory.

## Left in

- The app source, tests, resources, and the in-app What's New text for 1.4.11.
- Public Privacy and Terms URLs on luxwire.github.io.
- `RELEASE-NOTES.md` as it stood in the source commit, including published APK checksums. It does not contain keystores or the Supabase key.
