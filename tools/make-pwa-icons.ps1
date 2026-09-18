# Generates PWA app icons from the STORE LOGO - fully offline, no external services.
# Run from repo root:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-pwa-icons.ps1
# Optional: -LogoPath "path\to\logo.png"  (default: current store logo in wwwroot\media)
param(
    [string]$LogoPath = ""
)

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot

# اگر مسیر لوگو داده نشده، لوگوی پیش‌فرض پروژه استفاده می‌شود.
# برای استفاده از لوگوی فروشگاه: مسیر فایل فیزیکی LogoPath را از تنظیمات بگیرید و پاس دهید:
#   powershell -File tools\make-pwa-icons.ps1 -LogoPath "SugarShop.Web\wwwroot\media\...\logo.png"
if ([string]::IsNullOrWhiteSpace($LogoPath)) {
    $LogoPath = Join-Path $root 'SugarShop.Web\wwwroot\images\SORANSOFT_LOGO.png'
}

if (-not (Test-Path $LogoPath)) {
    Write-Error "Logo not found: $LogoPath"
    exit 1
}
Write-Host "Using logo: $LogoPath"

$outDir = Join-Path $root 'SugarShop.Web\wwwroot\icons'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$src = [System.Drawing.Image]::FromFile($LogoPath)
try {
    # لوگو روی کاشی مربعی کِرِم کشیده می‌شود؛ برای maskable محتوا داخل ۸۰٪ مرکزی امن است.
    function New-Icon([int]$size, [bool]$maskable) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode     = 'AntiAlias'
        $g.InterpolationMode = 'HighQualityBicubic'

        $bg = [System.Drawing.Color]::FromArgb(255, 255, 248, 240)  # cream #FFF8F0
        $g.Clear($bg)

        $contentBox = if ($maskable) { 0.62 } else { 0.78 }
        $maxW = [int]($size * $contentBox)
        $maxH = [int]($size * $contentBox)
        $scale = [Math]::Min($maxW / $src.Width, $maxH / $src.Height)
        $w = [int]($src.Width  * $scale)
        $h = [int]($src.Height * $scale)
        $x = [int](($size - $w) / 2)
        $y = [int](($size - $h) / 2)
        $g.DrawImage($src, $x, $y, $w, $h)

        $g.Dispose()
        return $bmp
    }

    foreach ($spec in @(
        @{ Size = 192; Name = 'pwa-icon-192.png';  Maskable = $false },
        @{ Size = 512; Name = 'pwa-icon-512.png';  Maskable = $false },
        @{ Size = 512; Name = 'pwa-icon-maskable-512.png'; Maskable = $true }
    )) {
        $bmp = New-Icon $spec.Size $spec.Maskable
        $bmp.Save((Join-Path $outDir $spec.Name), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Created $($spec.Name)"
    }

    # 180x180 apple-touch-icon (iOS home screen)
    $apple = New-Icon 180 $false
    $apple.Save((Join-Path $outDir 'apple-touch-icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $apple.Dispose()
    Write-Host 'Created apple-touch-icon.png'
}
finally {
    $src.Dispose()
}
