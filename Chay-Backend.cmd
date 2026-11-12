@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-LocalBackend.ps1" -Foreground
if errorlevel 1 pause
