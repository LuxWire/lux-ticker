@echo off
powershell -NoProfile -Command "Unblock-File -LiteralPath '%~dp0LuxTicker.exe'; Start-Process -FilePath '%~dp0LuxTicker.exe'"
