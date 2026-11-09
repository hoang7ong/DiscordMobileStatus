@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title Cai YT Status vao dien thoai qua cap USB
echo ==============================================================
echo   CAI YTStatus.apk VAO DIEN THOAI QUA CAP USB
echo   (cach nay khong bi Google Play Protect chan)
echo.
echo   Truoc khi chay: tren dien thoai bat "Gỡ lỗi USB"
echo     Cai dat ^> Thong tin dien thoai ^> Thong tin phan mem ^> bam 7 lan "So hieu ban tao"
echo     Cai dat ^> Tuy chon nha phat trien ^> bat "Gỡ lỗi USB"
echo   Cam cap, dien thoai hoi "Cho phep go loi USB?" thi bam Cho phep.
echo ==============================================================
echo.
if not exist "YTStatus.apk" (echo [LOI] Chua co YTStatus.apk - chay BUILD_APK.bat truoc. & pause & exit /b 1)
set "SDK=%~dp0.tools\android-sdk"
set "ADB=%SDK%\platform-tools\adb.exe"
if exist "%ADB%" goto co_adb
echo Tai cong cu adb (~10 MB) ...
set "JAVA_HOME=%~dp0.tools\jdk17"
> "%TEMP%\yes_yt.txt" (for /l %%i in (1,1,20) do echo y)
call "%SDK%\cmdline-tools\latest\bin\sdkmanager.bat" --sdk_root="%SDK%" "platform-tools" < "%TEMP%\yes_yt.txt"
if not exist "%ADB%" (echo [LOI] Khong tai duoc adb. & pause & exit /b 1)
:co_adb
"%ADB%" start-server >nul 2>nul
echo Dang cho dien thoai (cam cap + bam Cho phep tren dien thoai) ...
"%ADB%" wait-for-device
echo Dang cai ...
"%ADB%" install -r "YTStatus.apk"
if errorlevel 1 (echo [LOI] Cai that bai, xem thong bao phia tren. & pause & exit /b 1)
echo.
echo XONG. Mo app YT Status tren dien thoai. Co the tat "Gỡ lỗi USB" sau khi cai.
pause
