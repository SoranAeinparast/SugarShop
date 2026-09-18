# ═══════════════════════════════════════════════════════════════
#  SugarShop TWA — build the signed Android APK 100% offline
#  Uses: Android Studio JBR (JDK 21) + local Android SDK (build-tools 35)
#  Output: twa\app-release-signed.apk
# ═══════════════════════════════════════════════════════════════
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$Jbr      = 'C:\Program Files\Android\Android Studio\jbr'
$Sdk      = 'C:\Program Files (x86)\Android\android-sdk'
$BT       = Join-Path $Sdk 'build-tools\35.0.0'
$Platform = Join-Path $Sdk 'platforms\android-35\android.jar'

$Src   = Join-Path $here 'android\src'
$Res   = Join-Path $here 'android\res'
$Assets= Join-Path $here 'android\assets'
$Man   = Join-Path $here 'android\AndroidManifest.xml'
$Out   = Join-Path $here 'build'
$Apk   = Join-Path $here 'app-release-signed.apk'
$Ks    = Join-Path $here 'keystore.jks'

foreach ($t in @($Jbr, $BT, $Platform)) {
    if (-not (Test-Path $t)) { Write-Host "MISSING: $t" -ForegroundColor Red; exit 1 }
}

$env:JAVA_HOME = $Jbr
$env:PATH      = "$Jbr\bin;$BT;$env:PATH"
$Java   = Join-Path $Jbr 'bin\java.exe'
$Javac  = Join-Path $Jbr 'bin\javac.exe'
$Keytool= Join-Path $Jbr 'bin\keytool.exe'
$Aapt2  = Join-Path $BT  'aapt2.exe'
$D8     = Join-Path $BT  'd8.bat'
$Zipal  = Join-Path $BT  'zipalign.exe'
$Signer = Join-Path $BT  'apksigner.bat'

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Path $Out | Out-Null

Write-Host '▶ 1/7  Compiling resources (aapt2 compile)...'
& $Aapt2 compile --dir $Res -o (Join-Path $Out 'res.zip')
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '▶ 2/7  Linking resources + manifest + assets (aapt2 link)...'
$GenDir = Join-Path $Out 'gen'
New-Item -ItemType Directory -Path $GenDir -Force | Out-Null
& $Aapt2 link `
    -I $Platform `
    --manifest $Man `
    -A $Assets `
    --java $GenDir `
    (Join-Path $Out 'res.zip') `
    --auto-add-overlay `
    --min-sdk-version 23 --target-sdk-version 35 `
    -o (Join-Path $Out 'base.apk')
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '▶ 3/7  Compiling Java (javac)...'
$JavaSources = @((Get-ChildItem "$Src\*.java").FullName) + @((Get-ChildItem $GenDir -Recurse -Filter *.java).FullName)
& $Javac --release 8 -nowarn -cp $Platform -d (Join-Path $Out 'classes') $JavaSources
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '▶ 4/7  Dexing (d8)...'
& $D8 --release --lib $Platform --min-api 23 `
      --output $Out `
      (Get-ChildItem (Join-Path $Out 'classes') -Recurse -Filter *.class).FullName
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '▶ 5/7  Adding classes.dex to APK...'
$DexPath = Join-Path $Out 'classes.dex'
if (-not (Test-Path $DexPath)) {
    Write-Host 'classes.dex missing after dex step' -ForegroundColor Red; exit 1
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$apkZip = [System.IO.Compression.ZipFile]::Open((Join-Path $Out 'base.apk'), 'Update')
try {
    $existing = $apkZip.GetEntry('classes.dex')
    if ($existing) { $existing.Delete() }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($apkZip, $DexPath, 'classes.dex') | Out-Null
}
finally { $apkZip.Dispose() }

Write-Host '▶ 6/7  zipalign...'
& $Zipal -f -p 4 (Join-Path $Out 'base.apk') (Join-Path $Out 'aligned.apk')
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '▶ 7/7  Keystore + signing...'
if (-not (Test-Path $Ks)) {
    Write-Host '   creating keystore.jks (first run)...'
    & $Keytool -genkeypair -v `
        -keystore $Ks -alias pastry -keyalg RSA -keysize 2048 -validity 10950 `
        -storepass SugarShopPastry2026 -keypass SugarShopPastry2026 `
        -dname 'CN=Pastry Shop, OU=SoranSoft, O=SoranSoft, L=Tehran, C=IR'
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
& $Signer sign --ks $Ks --ks-pass pass:SugarShopPastry2026 `
      --ks-key-alias pastry --key-pass pass:SugarShopPastry2026 `
      --out $Apk (Join-Path $Out 'aligned.apk')
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host ''
Write-Host '════════════════════════════════════════════════════'
Write-Host "  APK built: $Apk"  -ForegroundColor Green

# Deploy into the web project's tracked-download location
$WebRes = Join-Path (Split-Path -Parent $here) 'SugarShop.Web\Resources'
if (Test-Path $WebRes) {
    Copy-Item $Apk (Join-Path $WebRes 'pastry-app.apk') -Force
    Write-Host '  Copied to SugarShop.Web\Resources\pastry-app.apk (tracked download route)' -ForegroundColor DarkGray
}
Write-Host '  SHA-256 fingerprint (copy into appsettings "Twa:Sha256Fingerprints"):'
Write-Host '────────────────────────────────────────────────────'
& $Keytool -list -v -keystore $Ks -alias pastry -storepass SugarShopPastry2026 |
    Select-String 'SHA256:'
Write-Host '════════════════════════════════════════════════════'
