param(
    [switch]$Force
)

$AssetDir = Join-Path $PSScriptRoot "Assets"
if (!(Test-Path $AssetDir)) { New-Item -ItemType Directory -Path $AssetDir -Force | Out-Null }

# ──────────────────────────────────────────────
# 1. Copy AppIcon.ico from project as Logo.png
# ──────────────────────────────────────────────
$IconSource = Join-Path $PSScriptRoot "..\src\App\UI\Resources\AppIcon.ico"
$LogoDest = Join-Path $AssetDir "Logo.png"
if (Test-Path $IconSource) {
    Write-Host "Copying AppIcon to Assets/Logo.png..."
    Copy-Item $IconSource $LogoDest -Force
} else {
    Write-Host "WARNING: AppIcon.ico not found at $IconSource"
    Write-Host "Creating placeholder Logo.png..."
    # Create a minimal 1x1 PNG as placeholder
    $pngHeader = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQIHWNgAAIABQABNjN9GQAAAAlwSFlzAAAWJQAAFiUBSVIk8AAAAA0lEQVQI12NgYPgPAAEDAQAR3X3ZAAAASUVORK5CYII=")
    [System.IO.File]::WriteAllBytes($LogoDest, $pngHeader)
}

# ──────────────────────────────────────────────
# 2. Generate Background.bmp (520x440 dark gradient)
# ──────────────────────────────────────────────
$BgPath = Join-Path $AssetDir "Background.bmp"
if ($Force -or !(Test-Path $BgPath)) {
    Write-Host "Generating Background.bmp (520x440 dark gradient)..."

    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 520, 440
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Dark background gradient: #0D0D1A → #1A1A2E → #16213E → #0F3460
    for ($y = 0; $y -lt 440; $y++) {
        $t = $y / 439.0
        $r = [int](13 + (26 - 13) * $t)
        $gC = [int](13 + (26 - 13) * $t)
        $b = [int](26 + (46 - 26) * $t)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($r, $gC, $b))
        $g.FillRectangle($brush, 0, $y, 520, 1)
        $brush.Dispose()
    }

    # Add a subtle accent line at the top
    $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(108, 180, 255))
    $g.FillRectangle($accentBrush, 0, 0, 520, 3)
    $accentBrush.Dispose()

    $bmp.Save($BgPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "  → Background.bmp created (520x440)"
} else {
    Write-Host "  → Background.bmp already exists, use -Force to regenerate"
}

# ──────────────────────────────────────────────
# 3. Generate BackgroundSmall.bmp (300x200)
# ──────────────────────────────────────────────
$BgSmallPath = Join-Path $AssetDir "BackgroundSmall.bmp"
if ($Force -or !(Test-Path $BgSmallPath)) {
    Write-Host "Generating BackgroundSmall.bmp (300x200)..."

    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 300, 200
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

    for ($y = 0; $y -lt 200; $y++) {
        $t = $y / 199.0
        $r = [int](13 + (26 - 13) * $t)
        $gC = [int](13 + (26 - 13) * $t)
        $b = [int](26 + (46 - 26) * $t)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($r, $gC, $b))
        $g.FillRectangle($brush, 0, $y, 300, 1)
        $brush.Dispose()
    }

    $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(108, 180, 255))
    $g.FillRectangle($accentBrush, 0, 0, 300, 2)
    $accentBrush.Dispose()

    $bmp.Save($BgSmallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "  → BackgroundSmall.bmp created (300x200)"
}

# ──────────────────────────────────────────────
# 4. Generate Banner.bmp (used by MSI UI)
# ──────────────────────────────────────────────
$BannerPath = Join-Path $AssetDir "Banner.bmp"
if ($Force -or !(Test-Path $BannerPath)) {
    Write-Host "Generating Banner.bmp (493x58)..."

    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 493, 58
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

    # Dark blue gradient
    for ($y = 0; $y -lt 58; $y++) {
        $t = $y / 57.0
        $r = [int](22 + (30 - 22) * $t)
        $gC = [int](22 + (40 - 22) * $t)
        $b = [int](46 + (80 - 46) * $t)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($r, $gC, $b))
        $g.FillRectangle($brush, 0, $y, 493, 1)
        $brush.Dispose()
    }

    # Accent bottom line
    $lineBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(108, 180, 255))
    $g.FillRectangle($lineBrush, 0, 56, 493, 2)
    $lineBrush.Dispose()

    $bmp.Save($BannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "  → Banner.bmp created (493x58)"
}

Write-Host ""
Write-Host "Assets generated in: $AssetDir"
Write-Host "  - Logo.png"
Write-Host "  - Background.bmp"
Write-Host "  - BackgroundSmall.bmp"
Write-Host "  - Banner.bmp"
