@echo off
echo ============================================
echo Cine Native Windows App - Single File Publish
echo ============================================
echo.
echo This will create a single portable EXE file
echo with all dependencies bundled inside
echo.

REM Change to Windows-Native directory
cd /d %~dp0Windows-Native

echo Publishing Cine.WinUI as single-file EXE...
echo.

dotnet publish Cine.WinUI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Publish failed!
    echo.
    echo This may be due to XAML compiler issues in .NET 10.0.300
    echo Please try using Visual Studio 2026 to build first.
    echo.
    echo 1. Build in Visual Studio 2026:
    echo    - Open x:\Development\Cine-main\Windows-Native\Cine.sln
    echo    - Press F5 to build
    echo.
    echo 2. Then run this script again
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================
echo SUCCESS! Single-file EXE created
echo ============================================
echo.
echo Your portable EXE is at:
echo   x:\Development\Cine-main\Windows-Native\Cine.WinUI\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\Cine.exe
echo.
echo You can now:
echo   - Copy this single EXE to any Windows 10/11 computer
echo   - Run it without installing .NET 10
echo   - Share it with others (no dependencies needed)
echo.
echo To run:
echo   x:\Development\Cine-main\Windows-Native\Cine.WinUI\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\Cine.exe
echo.
pause
