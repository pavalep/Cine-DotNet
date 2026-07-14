#  Generate MSIX App Icons from Source ICO
#  Requires: .NET (System.Drawing is cross-platform)
#  Usage:    .\generate-app-icons.ps1

param(
    [string]$SourceIco = "UI\Resources\AppIcon.ico",
    [string]$OutputDir = "Assets"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $scriptDir $SourceIco
$outputPath = Join-Path $scriptDir $OutputDir

if (-not (Test-Path $sourcePath)) {
    Write-Error "Source icon not found: $sourcePath"
    exit 1
}

if (-not (Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

# Use C# to load/resize via System.Drawing (Windows only, but works in PS on win)
Add-Type -AssemblyName System.Drawing

$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($sourcePath)
if (-not $icon) {
    Write-Error "Failed to extract icon from: $sourcePath"
    exit 1
}

$sizes = @{
    "SimbaLogo44x44.png"    = 44
    "SimbaLogo71x71.png"    = 71
    "SimbaLogo75x75.png"    = 75
    "SimbaLogo124x124.png"  = 124
    "SimbaLogo150x150.png"  = 150
    "SimbaLogo310x150.png"  = @{ Width = 310; Height = 150 }
    "SimbaLogo310x310.png"  = 310
    "SimbaSplash.png"       = @{ Width = 620; Height = 300 }
    "SimbaBadge24x24.png"   = 24
}

foreach ($entry in $sizes.GetEnumerator()) {
    $name = $entry.Key
    $dim = $entry.Value

    if ($dim -is [hashtable]) {
        $w = $dim.Width
        $h = $dim.Height
    } else {
        $w = $dim
        $h = $dim
    }

    $bitmap = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.Clear([System.Drawing.Color]::FromArgb(0x1A, 0x1A, 0x2E))  # Simba dark bg
    $g.DrawIcon($icon, [System.Drawing.Rectangle]::new(0, 0, $w, $h))
    $g.Dispose()

    $outFile = Join-Path $outputPath $name
    $bitmap.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Host "  Created: $name ($($w)x$($h))"
}

$icon.Dispose()
Write-Host ""
Write-Host "All assets generated in: $outputPath"
