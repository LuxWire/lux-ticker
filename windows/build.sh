#!/usr/bin/env bash
# Cross-compile the self-contained Windows exe and run the unit tests.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet test tests/LuxTicker.Tests/LuxTicker.Tests.csproj -c Release --logger "console;verbosity=minimal"
dotnet publish src/LuxTicker.App/LuxTicker.App.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:EnableWindowsTargeting=true \
  -p:DebugType=embedded

echo "exe: src/LuxTicker.App/bin/Release/net8.0-windows/win-x64/publish/LuxTicker.exe"
