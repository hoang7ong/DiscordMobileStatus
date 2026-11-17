@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title Build DiscordMobileStatus.exe
echo ==============================================================
echo   BUILD DiscordMobileStatus.exe  (dung trinh bien dich co san trong Windows,
echo   khong can cai gi them, mat vai giay)
echo ==============================================================
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FW%\csc.exe"
if not exist "%CSC%" (
  echo [LOI] Khong thay csc.exe cua .NET Framework 4. Windows 10/11 deu co san -
  echo       neu thieu, bat ".NET Framework 4.8 Advanced Services" trong "Turn Windows features on or off".
  pause & exit /b 1
)

rem --- app dang chay thi tu tat (file bi khoa), build xong bat lai. Tat ca ban cu YTStatus.exe.
taskkill /im YTStatus.exe /f >nul 2>nul
set "WASRUN="
tasklist /fi "imagename eq DiscordMobileStatus.exe" 2>nul | find /i "DiscordMobileStatus.exe" >nul && set "WASRUN=1"
if defined WASRUN (
  echo Dang tat DiscordMobileStatus.exe dang chay ...
  taskkill /im DiscordMobileStatus.exe /f >nul 2>nul
  timeout /t 2 /nobreak >nul
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
  /win32icon:"pc\DiscordMobileStatus.ico" /out:"DiscordMobileStatus.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  "pc\DiscordMobileStatus.cs"
if errorlevel 1 (
  echo.
  echo [LOI] Bien dich that bai. Chup man hinh / copy chu phia tren gui lai de sua.
  if defined WASRUN start "" "%~dp0DiscordMobileStatus.exe" --an
  pause & exit /b 1
)
echo.
echo XONG: DiscordMobileStatus.exe
if defined WASRUN (
  start "" "%~dp0DiscordMobileStatus.exe" --an
  echo Da bat lai DiscordMobileStatus.exe ^(chay an duoi khay^).
)
pause
