#!/usr/bin/env pwsh
# Build-MSIX.ps1 — Creates MSIX from published output without Packager tool
param(
    [string]$SourceDir = "publish\win-x64",
    [string]$OutputDir = "dist",
    [string]$Version = "1.0.0",
    [string]$Arch = "x64"
)

$ErrorActionPreference = "Stop"
Push-Location (Split-Path -Parent $MyInvocation.MyCommand.Path)

# Clean
Remove-Item -Force "$OutputDir\*.msix", "$OutputDir\mapping.txt" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $OutputDir | Out-Null

# Find MakeAppx
$sdkDirs = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
    Where-Object { $_.Name -match '^10\.' } |
    Sort-Object Name -Descending
$makeAppx = $null
foreach ($sdk in $sdkDirs) {
    $candidate = Join-Path $sdk.FullName "$Arch\MakeAppx.exe"
    if (Test-Path $candidate) { $makeAppx = $candidate; break }
}
if (-not $makeAppx) { Write-Error "MakeAppx.exe not found. Install Windows SDK."; exit 1 }
Write-Host "MakeAppx: $makeAppx"

# Build file mapping (exclude PDBs + on-demand runtime DLLs)
$excludeDlls = @('libmpv-2.dll', 'libEGL.dll', 'libGLESv2.dll', 'av_libglesv2.dll')
$lines = @("[Files]")
$files = Get-ChildItem $SourceDir -File | Where-Object {
    $_.Extension -ne '.pdb' -and $excludeDlls -notcontains $_.Name
}
$excluded = 0
foreach ($f in $files) {
    $src = $f.FullName.Replace('\', '/')
    $lines += '"{0}" "{1}"' -f $src, $f.Name
}
foreach ($dll in $excludeDlls) {
    if (Test-Path "$SourceDir\$dll") { $excluded++; Write-Host "  Excluded: $dll" }
}
Write-Host "$($files.Count) files included, $excluded excluded"

$mappingPath = "$OutputDir\mapping.txt"
$lines -join "`r`n" | Out-File -FilePath $mappingPath -Encoding ascii

# Build MSIX
$manifestPath = "src\App\Package.appxmanifest"
$msixPath = "$OutputDir\Cine_$($Version)_$Arch.msix"

Write-Host "Packaging: $msixPath"
$result = & $makeAppx pack /m $manifestPath /f $mappingPath /p $msixPath /o 2>&1
$exitCode = $LASTEXITCODE
if ($result) { Write-Host ($result -join "`n") }

if ($exitCode -eq 0 -and (Test-Path $msixPath)) {
    $size = (Get-Item $msixPath).Length / 1MB
    Write-Host "✓ MSIX created: $msixPath ({0:N0} MB)" -f $size
} else {
    Write-Error "MakeAppx failed with exit code $exitCode"
    exit 1
}

Pop-Location
