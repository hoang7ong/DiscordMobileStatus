@echo off
chcp 65001 >nul
cd /d "%~dp0"
title Build app Android Discord Mobile Status
echo ==============================================================
echo   BUILD APP ANDROID "DISCORD MOBILE STATUS"
echo   Lan dau tai cong cu (~600 MB, 5-15 phut). Lan sau vai chuc giay.
echo ==============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0android\build_apk.ps1"
echo.
pause
