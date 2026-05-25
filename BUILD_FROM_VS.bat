@echo off
echo ============================================
echo Building Cine Native from VS 2026
echo ============================================
echo.
cd /d %~dp0

echo Step 1: Building Cine.Core...
dotnet build Cine.Core -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cine.Core build failed!
    exit /b 1
)

echo.
echo Step 2: Building Cine.Media...
dotnet build Cine.Media -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cine.Media build failed!
    exit /b 1
)

echo.
echo Step 3: Building Cine.WinUI...
dotnet build Cine.WinUI -c Release
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Cine.WinUI build failed!
    exit /b 1
)

echo.
echo ============================================
echo SUCCESS! All projects built!
echo ============================================
echo.
echo Output DLL at:
echo   Cine.WinUI\bin\Release\net10.0-windows\Cine.WinUI.dll
echo.
echo Published app at:
echo   Cine.WinUI\publish\
echo.
pause