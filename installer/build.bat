@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: Cine Installer Build Script
:: P11: Produces CineBootstrapper.exe — a themed MSI + .NET runtime check
::
:: Prerequisites:
::   1. WiX Toolset v4 — install via: dotnet tool install --global wix
::   2. .NET SDK 10.0
:: ============================================================================

set PROJ_DIR=%~dp0
set ROOT_DIR=%PROJ_DIR%..
set PUBLISH_DIR=%ROOT_DIR%\src\App\bin\Release\net10.0-windows\win-x64\publish
set MSI_OUTPUT=%PROJ_DIR%CineMsi\bin\Release
set BOOT_OUTPUT=%PROJ_DIR%CineBootstrapper\bin\Release

echo ════════════════════════════════════════════
echo  Cine Installer Builder
echo ════════════════════════════════════════════

:: Step 1: Publish the app (framework-dependent)
echo.
echo [1/5] Publishing app (framework-dependent)...
dotnet publish %ROOT_DIR%\src\App\App.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:DebugType=embedded ^
    -p:PublishReadyToRun=true ^
    -o %PUBLISH_DIR%
if %ERRORLEVEL% neq 0 (
    echo FAILED: Publish step. Check build errors.
    exit /b 1
)
echo OK

:: Step 2: Remove unnecessary debug files
echo [2/5] Cleaning publish output...
if exist "%PUBLISH_DIR%\*.pdb" del "%PUBLISH_DIR%\*.pdb"
echo OK

:: Step 3: Build MSI (heat + candle + light)
echo [3/5] Building MSI package...
cd /d "%PROJ_DIR%CineMsi"

:: Harvest files from publish directory
wix harvest dir "%PUBLISH_DIR%" ^
    -publishDir "%PUBLISH_DIR%" ^
    -componentGroups CineFiles ^
    -out "%PROJ_DIR%CineMsi\Package.wxs" ^
    -gg -srd -wx all
if %ERRORLEVEL% neq 0 (
    echo WARNING: Heat harvest had issues, continuing...
)

:: Compile MSI
wix build "%PROJ_DIR%CineMsi\CineMsi.wixproj" -out "%MSI_OUTPUT%\Cine.msi"
if %ERRORLEVEL% neq 0 (
    echo FAILED: MSI build.
    exit /b 1
)
echo OK — Cine.msi created

:: Step 4: Build Bootstrapper (Burn)
echo [4/5] Building bootstrapper...
cd /d "%PROJ_DIR%CineBootstrapper"
wix build "%PROJ_DIR%CineBootstrapper\CineBootstrapper.wixproj" ^
    -out "%BOOT_OUTPUT%\CineSetup.exe"
if %ERRORLEVEL% neq 0 (
    echo FAILED: Bootstrapper build.
    exit /b 1
)
echo OK — CineSetup.exe created

:: Step 5: Sign (if certificate available)
echo [5/5] Signing (optional)...
if exist "%PROJ_DIR%\signing.pfx" (
    signtool sign /fd SHA256 /f "%PROJ_DIR%\signing.pfx" /p "PASSWORD" /t http://timestamp.digicert.com "%BOOT_OUTPUT%\CineSetup.exe"
    echo Signed
) else (
    echo WARNING: No signing certificate found (signing.pfx). Skipping.
)

echo.
echo ════════════════════════════════════════════
echo  ✅ BUILD COMPLETE
echo  Output: %BOOT_OUTPUT%\CineSetup.exe
echo ════════════════════════════════════════════
endlocal
