@echo off
echo ============================================
echo Cine Native Windows App - Build Script
echo ============================================
echo.
echo This will build your native Cine Windows app
echo using .NET 10 command line (no Visual Studio needed)
echo.

REM Change to Windows-Native directory
cd /d %~dp0Windows-Native

echo Step 1: Cleaning previous builds...
dotnet clean

echo.
echo Step 2: Restoring NuGet packages...
dotnet restore

echo.
echo Step 3: Building Cine.Core...
dotnet build Cine.Core -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build Cine.Core!
    pause
    exit /b 1
)

echo.
echo Step 4: Building Cine.Media...
dotnet build Cine.Media -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to build Cine.Media!
    pause
    exit /b 1
)

echo.
echo Step 5: Building Cine.WinUI...
dotnet build Cine.WinUI -c Release
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ============================================
    echo WARNING: Build had errors, but you can still try Visual Studio 2026
    echo ============================================
    echo.
    echo Try:
    echo   1. Open Visual Studio 2026
    echo   2. Open x:\Development\Cine-main\Windows-Native\Cine.sln
    echo   3. Press F5 to build
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================
echo SUCCESS! Cine built successfully
echo ============================================
echo.
echo Your EXE is located at:
echo   x:\Development\Cine-main\Windows-Native\Cine.WinUI\bin\Release\net10.0-windows10.0.26100.0\win-x64\Cine.exe
echo.
echo To run as a SINGLE FILE (portable):
echo   dotnet publish Cine.WinUI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
echo.
echo Then run:
echo   x:\Development\Cine-main\Windows-Native\Cine.WinUI\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\Cine.exe
echo.
pause
