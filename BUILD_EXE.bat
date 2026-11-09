@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title Build YTStatus.exe
echo ==============================================================
echo   BUILD YTStatus.exe  (dung trinh bien dich co san trong Windows,
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

rem --- YTStatus.exe dang chay thi tu tat (file bi khoa), build xong bat lai
set "WASRUN="
tasklist /fi "imagename eq YTStatus.exe" 2>nul | find /i "YTStatus.exe" >nul && set "WASRUN=1"
if defined WASRUN (
  echo Dang tat YTStatus.exe dang chay ...
  taskkill /im YTStatus.exe /f >nul 2>nul
  timeout /t 2 /nobreak >nul
)

"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
  /win32icon:"pc\YTStatus.ico" /out:"YTStatus.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  "pc\YTStatus.cs"
if errorlevel 1 (
  echo.
  echo [LOI] Bien dich that bai. Chup man hinh / copy chu phia tren gui lai de sua.
  if defined WASRUN start "" "%~dp0YTStatus.exe" --an
  pause & exit /b 1
)
echo.
echo XONG: YTStatus.exe
if defined WASRUN (
  start "" "%~dp0YTStatus.exe" --an
  echo Da bat lai YTStatus.exe ^(chay an duoi khay^).
)
pause
