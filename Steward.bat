@echo off
rem Starts Steward: tray app and the schedule container, same as at Windows sign-in.
rem Double-click it, or use the Steward shortcut next to it (it has the app icon).
start "" /min powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0start-steward.ps1"
