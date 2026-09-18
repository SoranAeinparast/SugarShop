@echo off
REM ============================================================
REM  SugarShop TWA icon generator — requires ImageMagick (magick)
REM  Usage: generate-icons.bat <path-to-logo.png>
REM ============================================================
setlocal
if "%~1"=="" (
    echo Usage: generate-icons.bat ^<logo.png^>
    exit /b 1
)
set LOGO=%~1
set OUT=%~dp0android\res
for %%S in (36 48 72 96 144 192) do (
    magick "%LOGO%" -resize %%Sx%%S -background "#FFF8F0" -gravity center -extent %%Sx%%S "%OUT%\mipmap-%%S\ic_launcher.png"
)
echo Done. Icons written to %OUT%\mipmap-*
endlocal
