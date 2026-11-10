<#
.SYNOPSIS
    Generates every app icon and logo in src/TunnelTimer.App/Assets from one drawing routine.

.DESCRIPTION
    The mark is a rainbow tunnel on a rounded ink tile. Run this script again after
    changing the drawing below; all sizes are produced from the same code, so they cannot
    drift apart. Uses System.Drawing (GDI+), which ships with Windows.

.EXAMPLE
    pwsh scripts/generate-icons.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assets = Join-Path $PSScriptRoot '..\src\TunnelTimer.App\Assets'

function New-Canvas([int]$Width, [int]$Height) {
    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return $bitmap, $graphics
}

# The mark is designed on a 256 unit grid; docs/logo.svg is the same drawing as vector.
$ink = [System.Drawing.ColorTranslator]::FromHtml('#1B1A2E')
$light = [System.Drawing.ColorTranslator]::FromHtml('#FFF6DC')

# Six arches from outside to inside. Below 48 px they blur together, so small sizes use
# the three-arch cut with wider bands.
$fullArches = @(
    @{ Radius = 100; Color = '#FF5E5B' }, @{ Radius = 84; Color = '#FF9F1C' }, @{ Radius = 68; Color = '#FFD23F' },
    @{ Radius = 52; Color = '#3DDC97' }, @{ Radius = 36; Color = '#2EC4F1' }, @{ Radius = 20; Color = '#7B61FF' })
$smallArches = @(
    @{ Radius = 92; Color = '#FF5E5B' }, @{ Radius = 62; Color = '#FFD23F' }, @{ Radius = 32; Color = '#2EC4F1' })

# An arch: up the left leg, over the top, down the right leg, in 256 unit coordinates.
function New-ArchPath([single]$X, [single]$Y, [single]$Unit, [single]$Radius) {
    $centerX = 128; $centerY = 140
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddLine($X + ($centerX - $Radius) * $Unit, $Y + 256 * $Unit, $X + ($centerX - $Radius) * $Unit, $Y + $centerY * $Unit)
    $path.AddArc($X + ($centerX - $Radius) * $Unit, $Y + ($centerY - $Radius) * $Unit, 2 * $Radius * $Unit, 2 * $Radius * $Unit, 180, 180)
    $path.AddLine($X + ($centerX + $Radius) * $Unit, $Y + $centerY * $Unit, $X + ($centerX + $Radius) * $Unit, $Y + 256 * $Unit)
    return $path
}

# Draws the mark into a square of the given size at the given offset.
function Add-Mark([System.Drawing.Graphics]$Graphics, [single]$X, [single]$Y, [single]$Size) {
    $unit = $Size / 256
    $isSmall = $Size -lt 48

    $radius = $Size * 0.2266
    $diameter = $radius * 2
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tile.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $tile.AddArc($X + $Size - $diameter, $Y, $diameter, $diameter, 270, 90)
    $tile.AddArc($X + $Size - $diameter, $Y + $Size - $diameter, $diameter, $diameter, 0, 90)
    $tile.AddArc($X, $Y + $Size - $diameter, $diameter, $diameter, 90, 90)
    $tile.CloseFigure()

    $inkBrush = New-Object System.Drawing.SolidBrush $ink
    $Graphics.FillPath($inkBrush, $tile)
    $inkBrush.Dispose()

    # The arches run off the bottom edge, so everything is clipped to the tile.
    $state = $Graphics.Save()
    $Graphics.SetClip($tile, [System.Drawing.Drawing2D.CombineMode]::Intersect)

    $arches = if ($isSmall) { $smallArches } else { $fullArches }
    $stroke = if ($isSmall) { 24 } else { 14 }
    foreach ($arch in $arches) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml($arch.Color)), ($stroke * $unit)
        $path = New-ArchPath $X $Y $unit $arch.Radius
        $Graphics.DrawPath($pen, $path)
        $path.Dispose()
        $pen.Dispose()
    }

    # The light at the end of the tunnel.
    $lightRadius = if ($isSmall) { 14 } else { 13 }
    $lightPath = New-ArchPath $X $Y $unit $lightRadius
    $lightPath.CloseFigure()
    $lightBrush = New-Object System.Drawing.SolidBrush $light
    $Graphics.FillPath($lightBrush, $lightPath)
    $lightBrush.Dispose()
    $lightPath.Dispose()

    $Graphics.Restore($state)
    $tile.Dispose()
}

function New-SquarePng([int]$Size) {
    $bitmap, $graphics = New-Canvas $Size $Size
    Add-Mark $graphics 0 0 $Size
    $graphics.Dispose()
    return $bitmap
}

# A wide canvas with the mark centered at the given share of the height.
function New-WidePng([int]$Width, [int]$Height, [double]$MarkShare) {
    $bitmap, $graphics = New-Canvas $Width $Height
    $size = [single]($Height * $MarkShare)
    Add-Mark $graphics (($Width - $size) / 2) (($Height - $size) / 2) $size
    $graphics.Dispose()
    return $bitmap
}

function Save-Png([System.Drawing.Bitmap]$Bitmap, [string]$Name) {
    $Bitmap.Save((Join-Path $assets $Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $Bitmap.Dispose()
}

# Writes an .ico whose entries are PNG-compressed images, one per size.
function Save-Ico([int[]]$Sizes, [string]$Name) {
    $images = foreach ($size in $Sizes) {
        $bitmap = New-SquarePng $size
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        , $stream.ToArray()
    }

    $file = [System.IO.File]::Create((Join-Path $assets $Name))
    $writer = New-Object System.IO.BinaryWriter $file
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$Sizes.Count)

    $offset = 6 + 16 * $Sizes.Count
    for ($index = 0; $index -lt $Sizes.Count; $index++) {
        $size = $Sizes[$index]
        $writer.Write([byte]($size % 256))
        $writer.Write([byte]($size % 256))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }

    $writer.Dispose()
    $file.Dispose()
}

Save-Ico @(16, 20, 24, 32, 40, 48, 64, 256) 'AppIcon.ico'
Save-Png (New-SquarePng 50) 'StoreLogo.png'
Save-Png (New-SquarePng 88) 'Square44x44Logo.scale-200.png'
Save-Png (New-SquarePng 24) 'Square44x44Logo.targetsize-24_altform-unplated.png'
Save-Png (New-SquarePng 48) 'Square44x44Logo.targetsize-48_altform-lightunplated.png'
Save-Png (New-SquarePng 300) 'Square150x150Logo.scale-200.png'
Save-Png (New-WidePng 620 300 0.6) 'Wide310x150Logo.scale-200.png'
Save-Png (New-WidePng 1240 600 0.4) 'SplashScreen.scale-200.png'
Save-Png (New-SquarePng 72) 'Logo.png'

Get-ChildItem $assets | Select-Object Name, Length
