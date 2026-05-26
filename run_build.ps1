cd "X:\Development\Cine-main\Windows-Native"
dotnet restore Cine.Avalonia/Cine.Avalonia.csproj 2>&1
echo ===
dotnet build Cine.Avalonia/Cine.Avalonia.csproj --no-restore 2>&1