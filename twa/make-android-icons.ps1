# Generates Android launcher mipmaps (ic_launcher.png) from the STORE LOGO - fully offline.
param(
    [string]$LogoPath = "SugarShop.Web\wwwroot\media\2026\08\3d9505786e7c4386bd3bec187ad77e56.png"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path $LogoPath)) { $LogoPath = Join-Path $root $LogoPath }
if (-not (Test-Path $LogoPath)) { Write-Error "Logo not found: $LogoPath"; exit 1 }
Write-Host "Using logo: $LogoPath"

$src = [System.Drawing.Image]::FromFile($LogoPath)
try {
    $dpiMap = @{ 36 = 'ldpi'; 48 = 'mdpi'; 72 = 'hdpi'; 96 = 'xhdpi'; 144 = 'xxhdpi'; 192 = 'xxxhdpi' }
    foreach ($size in @(36, 48, 72, 96, 144, 192)) {
        $dir = Join-Path $PSScriptRoot "android\res\mipmap-$($dpiMap[$size])"
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $bmp = New-Object System.Drawing.Bitmap($size, $size)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode     = 'AntiAlias'
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.Clear([System.Drawing.Color]::FromArgb(255, 255, 248, 240))  # cream #FFF8F0
        $box = [int]($size * 0.78)
        $scale = [Math]::Min($box / $src.Width, $box / $src.Height)
        $w = [int]($src.Width * $scale); $h = [int]($src.Height * $scale)
        $g.DrawImage($src, [int](($size-$w)/2), [int](($size-$h)/2), $w, $h)
        $g.Dispose()
        $bmp.Save((Join-Path $dir 'ic_launcher.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "  mipmap-$($dpiMap[$size])/ic_launcher.png ($size px)"
    }
}
finally { $src.Dispose() }
Write-Host 'Launcher icons done.'
