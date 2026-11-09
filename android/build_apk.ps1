# Build app Android "YT Status" KHONG can Android Studio / Gradle.
# Lan dau tai JDK 17 + Android build-tools (~600 MB) vao thu muc .tools ben canh. Lan sau ~20 giay.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root    = Split-Path -Parent $Here
$Tools   = Join-Path $Root ".tools"
$Jdk     = Join-Path $Tools "jdk17"
$Sdk     = Join-Path $Tools "android-sdk"
$BT_VER  = "34.0.0"
$API     = "34"
$MIN_SDK = "24"
$VersionName = "1.0.0"
$VersionCode = "10000"

function Step($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Yellow }
function Fail($t) { Write-Host "[LOI] $t" -ForegroundColor Red; exit 1 }
function Run($exe, [string[]]$argv) {
    & $exe @argv
    if ($LASTEXITCODE -ne 0) { Fail "$([IO.Path]::GetFileName($exe)) bao loi (ma $LASTEXITCODE)" }
}

New-Item -ItemType Directory -Force -Path $Tools | Out-Null

# ---------------------------------------------------------------- JDK 17
if (-not (Test-Path "$Jdk\bin\javac.exe")) {
    Step "Tai JDK 17 (Temurin, ~190 MB)"
    $zip = Join-Path $Tools "jdk.zip"
    Invoke-WebRequest "https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jdk/hotspot/normal/eclipse" -OutFile $zip
    $tmp = Join-Path $Tools "jdk_tmp"
    if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
    Expand-Archive $zip $tmp
    $inner = Get-ChildItem $tmp -Directory | Select-Object -First 1
    if (Test-Path $Jdk) { Remove-Item -Recurse -Force $Jdk }
    Move-Item $inner.FullName $Jdk
    Remove-Item -Recurse -Force $tmp, $zip
}
$env:JAVA_HOME = $Jdk
$env:Path = "$Jdk\bin;$env:Path"

# ---------------------------------------------------------------- Android SDK
$BT  = Join-Path $Sdk "build-tools\$BT_VER"
$JAR = Join-Path $Sdk "platforms\android-$API\android.jar"
if (-not ((Test-Path "$BT\aapt2.exe") -and (Test-Path $JAR))) {
    $cmd = Join-Path $Sdk "cmdline-tools\latest\bin\sdkmanager.bat"
    if (-not (Test-Path $cmd)) {
        Step "Tai Android command-line tools (~150 MB)"
        $zip = Join-Path $Tools "cmdtools.zip"
        Invoke-WebRequest "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip" -OutFile $zip
        $tmp = Join-Path $Tools "cmd_tmp"
        if (Test-Path $tmp) { Remove-Item -Recurse -Force $tmp }
        Expand-Archive $zip $tmp
        New-Item -ItemType Directory -Force -Path "$Sdk\cmdline-tools" | Out-Null
        if (Test-Path "$Sdk\cmdline-tools\latest") { Remove-Item -Recurse -Force "$Sdk\cmdline-tools\latest" }
        Move-Item "$tmp\cmdline-tools" "$Sdk\cmdline-tools\latest"
        Remove-Item -Recurse -Force $tmp, $zip
    }
    Step "Cai build-tools $BT_VER + platform android-$API (dong y giay phep tu dong)"
    # Ghi san file "da dong y giay phep" (ma bam chuan cua Google, cach cac may build tu dong van dung).
    $lic = Join-Path $Sdk "licenses"
    New-Item -ItemType Directory -Force -Path $lic | Out-Null
    $hashes = "`n24333f8a63b6825ea9c5514f83c2829b004d1fee`n8933bad161af4178b1185d1a37fbf41ea5269c55`nd56f5187479451eabf01fb78af6dfcb131a6481e"
    [IO.File]::WriteAllText((Join-Path $lic "android-sdk-license"), $hashes)
    [IO.File]::WriteAllText((Join-Path $lic "android-sdk-preview-license"), "`n84831b9409646a918e30573bab4c9c91346d8abd`n504667f4c0de7af1a06de9f4b1727b84351f2910")
    # Du phong: tra loi "y" qua file. PowerShell khong truyen chu vao .bat duoc -> chay qua mot file .cmd tam.
    $yesFile = Join-Path $Tools "yes.txt"
    [IO.File]::WriteAllText($yesFile, ("y`r`n" * 40))
    $run = Join-Path $Tools "cai_sdk.cmd"
    $lines = @(
        "@echo off",
        "call `"$cmd`" --sdk_root=`"$Sdk`" --licenses < `"$yesFile`" > nul",
        "call `"$cmd`" --sdk_root=`"$Sdk`" `"build-tools;$BT_VER`" `"platforms;android-$API`" < `"$yesFile`""
    )
    [IO.File]::WriteAllLines($run, $lines)
    & cmd.exe /c $run
    if (-not ((Test-Path "$BT\aapt2.exe") -and (Test-Path $JAR))) { Fail "Khong cai duoc build-tools. Kiem tra mang roi chay lai." }
}

# ---------------------------------------------------------------- build
$Src   = Join-Path $Here "app\src\main"
$Out   = Join-Path $Here "build"
if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory -Force -Path "$Out\classes", "$Out\dex" | Out-Null

Step "1/6 Dong goi tai nguyen (aapt2)"
Run "$BT\aapt2.exe" @("compile", "--dir", "$Src\res", "-o", "$Out\res.zip")
Run "$BT\aapt2.exe" @("link", "-o", "$Out\unsigned.apk", "-I", $JAR, "--manifest", "$Src\AndroidManifest.xml",
    "--min-sdk-version", $MIN_SDK, "--target-sdk-version", $API,
    "--version-code", $VersionCode, "--version-name", $VersionName, "$Out\res.zip")

Step "2/6 Bien dich Java"
$javaFiles = Get-ChildItem "$Src\java" -Recurse -Filter *.java | ForEach-Object { $_.FullName }
# --release 8: lop java.* lay tu JDK (android.jar thieu LambdaMetafactory.metafactory -> loi lambda),
# android.* lay tu android.jar qua classpath. d8 o buoc sau tu chuyen lambda cho Android.
Run "$Jdk\bin\javac.exe" (@("-encoding", "UTF-8", "--release", "8", "-nowarn",
    "-classpath", $JAR, "-d", "$Out\classes") + $javaFiles)

Step "3/6 Chuyen sang DEX (d8)"
Run "$Jdk\bin\jar.exe" @("cf", "$Out\classes.jar", "-C", "$Out\classes", ".")
Run "$BT\d8.bat" @("--release", "--min-api", $MIN_SDK, "--lib", $JAR, "--output", "$Out\dex", "$Out\classes.jar")

Step "4/6 Ghep classes.dex vao APK"
Push-Location "$Out\dex"
try { Run "$BT\aapt.exe" @("add", "$Out\unsigned.apk", "classes.dex") | Out-Null } finally { Pop-Location }

Step "5/6 Can chinh (zipalign)"
Run "$BT\zipalign.exe" @("-p", "-f", "4", "$Out\unsigned.apk", "$Out\aligned.apk")

Step "6/6 Ky APK"
# Giu nguyen 1 khoa de lan sau cai de len ban cu duoc. MAT FILE NAY = phai go app cu truoc khi cai ban moi.
$Ks = Join-Path $Here "khoa_ky\ytstatus.jks"
if (-not (Test-Path $Ks)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $Ks) | Out-Null
    Run "$Jdk\bin\keytool.exe" @("-genkeypair", "-keystore", $Ks, "-alias", "ytstatus", "-keyalg", "RSA", "-keysize", "2048",
        "-validity", "10000", "-storepass", "ytstatus", "-keypass", "ytstatus", "-dname", "CN=YT Status, C=VN")
}
$Apk = Join-Path $Root "YTStatus.apk"
Run "$BT\apksigner.bat" @("sign", "--ks", $Ks, "--ks-pass", "pass:ytstatus", "--key-pass", "pass:ytstatus",
    "--ks-key-alias", "ytstatus", "--out", $Apk, "$Out\aligned.apk")
Run "$BT\apksigner.bat" @("verify", $Apk)
# don dep: file chu ky phu (.idsig) va thu muc build tam
Remove-Item "$Apk.idsig" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force $Out -ErrorAction SilentlyContinue

$kb = [math]::Round((Get-Item $Apk).Length / 1KB)
Write-Host ""
Write-Host "XONG: YTStatus.apk ($kb KB)" -ForegroundColor Green
Write-Host "  Gui file nay vao dien thoai (Zalo, Drive, cap USB...) roi bam de cai."
