<#
.SYNOPSIS
    Composes the Microsoft Store screenshots (1366x768 PNG) from the window captures in docs/.

.DESCRIPTION
    The Store requires screenshots of at least 1366x768. The app's windows are much smaller,
    so each screenshot places the real, unscaled captures on a calm desktop-like background
    at the position they take on screen: the widget top right, the panel bottom right.
    No logos or marketing text are added, as the Store listing guidelines ask.

.EXAMPLE
    pwsh scripts/compose-store-screenshots.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$docs = Join-Path $PSScriptRoot '..\docs'
$output = Join-Path $docs 'store'
New-Item -ItemType Directory -Force $output | Out-Null

$width = 1366
$height = 768
$margin = 28

function New-RoundedPath([single]$X, [single]$Y, [single]$W, [single]$H, [single]$Radius) {
    $d = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# A soft colour field, like the ones behind the panel header.
function Add-Glow([System.Drawing.Graphics]$Graphics, [single]$CenterX, [single]$CenterY, [single]$Radius, [string]$Color, [int]$Alpha) {
    $ellipse = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ellipse.AddEllipse($CenterX - $Radius, $CenterY - $Radius, 2 * $Radius, 2 * $Radius)
    $brush = New-Object System.Drawing.Drawing2D.PathGradientBrush $ellipse
    $base = [System.Drawing.ColorTranslator]::FromHtml($Color)
    $brush.CenterColor = [System.Drawing.Color]::FromArgb($Alpha, $base)
    $brush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $base))
    $Graphics.FillPath($brush, $ellipse)
    $brush.Dispose()
    $ellipse.Dispose()
}

# Draws a window capture with the rounded corners and shadow Windows gives it on screen.
function Add-Window([System.Drawing.Graphics]$Graphics, [string]$File, [int]$X, [int]$Y, [int]$TrimSides, [int]$TrimBottom) {
    $source = [System.Drawing.Image]::FromFile((Join-Path $docs $File))
    $w = $source.Width - 2 * $TrimSides
    $h = $source.Height - $TrimBottom

    foreach ($step in 12..1) {
        $shadow = New-RoundedPath ($X - $step) ($Y - $step + 6) ($w + 2 * $step) ($h + 2 * $step) (8 + $step)
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(5, 27, 26, 46))
        $Graphics.FillPath($brush, $shadow)
        $brush.Dispose()
        $shadow.Dispose()
    }

    $clip = New-RoundedPath $X $Y $w $h 8
    $state = $Graphics.Save()
    $Graphics.SetClip($clip)
    $Graphics.DrawImage($source, (New-Object System.Drawing.Rectangle $X, $Y, $w, $h), $TrimSides, 0, $w, $h, [System.Drawing.GraphicsUnit]::Pixel)
    $Graphics.Restore($state)

    $border = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 27, 26, 46)), 1
    $Graphics.DrawPath($border, $clip)
    $border.Dispose()
    $clip.Dispose()
    $source.Dispose()
    return @{ Width = $w; Height = $h }
}

function New-Screenshot([string]$Name, [string]$Widget, [bool]$WithPanel) {
    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#F1EEE8'))
    Add-Glow $graphics 180 120 620 '#3DDC97' 70
    Add-Glow $graphics 640 760 640 '#2EC4F1' 60
    Add-Glow $graphics 1250 380 620 '#7B61FF' 55

    if ($Widget) {
        # The widget capture carries the invisible resize border of the window: 7 px left,
        # right and bottom.
        $source = [System.Drawing.Image]::FromFile((Join-Path $docs $Widget))
        $widgetWidth = $source.Width - 14
        $source.Dispose()
        [void](Add-Window $graphics $Widget ($width - $widgetWidth - $margin) $margin 7 7)
    }

    if ($WithPanel) {
        $source = [System.Drawing.Image]::FromFile((Join-Path $docs 'panel.png'))
        $panelWidth = $source.Width
        $panelHeight = $source.Height
        $source.Dispose()
        $x = if ($Widget) { $margin + 120 } else { [int](($width - $panelWidth) / 2) }
        [void](Add-Window $graphics 'panel.png' $x ([int](($height - $panelHeight) / 2)) 0 0)
    }

    $graphics.Dispose()
    $path = Join-Path $output $Name
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Get-Item $path | Select-Object Name, Length
}

New-Screenshot '1-upcoming.png' 'widget-upcoming.png' $false
New-Screenshot '2-imminent-join.png' 'widget-imminent.png' $false
New-Screenshot '3-overdue.png' 'widget-overdue.png' $false
New-Screenshot '4-panel.png' 'widget-upcoming.png' $true
