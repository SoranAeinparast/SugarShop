# Generates drawable-nodpi/splash_logo.png (480px, logo on cream) - fully offline.
param(
    [string]$LogoPath = "SugarShop.Web\wwwroot\media\2026\09\1568f3bc3b22415fb5d0fdca86fe9889.png"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path $LogoPath)) { $LogoPath = Join-Path $root $LogoPath }
if (-not (Test-Path $LogoPath)) { Write-Error "Logo not found: $LogoPath"; exit 1 }
Write-Host "Using logo: $LogoPath"

$src = [System.Drawing.Image]::FromFile($LogoPath)
try {
    $size = 480
    $dir = Join-Path $PSScriptRoot 'android\res\drawable-nodpi'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 255, 248, 240))  # cream #FFF8F0
    $box = [int]($size * 0.72)
    $scale = [Math]::Min($box / $src.Width, $box / $src.Height)
    $w = [int]($src.Width * $scale); $h = [int]($src.Height * $scale)
    $g.DrawImage($src, [int](($size-$w)/2), [int](($size-$h)/2), $w, $h)
    $g.Dispose()
    $bmp.Save((Join-Path $dir 'splash_logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  drawable-nodpi/splash_logo.png ($size px)"
}
finally { $src.Dispose() }
Write-Host 'Splash logo done.'
