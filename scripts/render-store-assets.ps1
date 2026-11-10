<#
.SYNOPSIS
    Renders every Microsoft Store listing image into docs/store.

.DESCRIPTION
    First the debug build of the app draws its own widget and panel in fixed states into
    docs/, at three times their on-screen size (ScreenshotExport.cs). Then
    docs/store/src/scene.html composes the screenshots from those images, and
    docs/store/src/art.html draws the logo art. Both pages are rendered to PNG with the
    Playwright CLI, which has to be on the PATH.

    Needs the debug build, registered once with Add-AppxPackage -Register, and Tunnel Timer
    must not be running: the app is single instance and the export is a run of its own.

.EXAMPLE
    pwsh scripts/render-store-assets.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$docs = Join-Path $repository 'docs'
$store = Join-Path $docs 'store'
$source = 'file:///' + ($store -replace '\\', '/') + '/src'

# Step 1: the app draws its windows. It needs its package identity, so it is started inside
# the package context, not as a plain executable.
if (Get-Process TunnelTimer -ErrorAction SilentlyContinue) { throw 'Quit Tunnel Timer first: the screenshot export is a run of its own.' }
$executable = Join-Path $repository 'src\TunnelTimer.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TunnelTimer.exe'
if (-not (Test-Path $executable)) { throw 'Build the debug app first, see CLAUDE.md.' }
$windowImages = 'widget-upcoming.png', 'widget-imminent.png', 'widget-overdue.png', 'panel.png'
$startedAt = Get-Date
Invoke-CommandInDesktopPackage -PackageFamilyName 'shroomlife.TunnelTimerMeetingCountdown_szn2xxnwmd44j' -AppId 'App' -Command $executable -Args "--export-screenshots `"$docs`""
$deadline = $startedAt.AddSeconds(60)
do { Start-Sleep -Milliseconds 500 } while ((Get-Process TunnelTimer -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline)
foreach ($image in $windowImages) {
    $file = Get-Item (Join-Path $docs $image) -ErrorAction SilentlyContinue
    if (-not $file -or $file.LastWriteTime -lt $startedAt) { throw "The app did not write $image. See app.log in the package's LocalState folder." }
}

function Save-Page([string]$Page, [int]$Width, [int]$Height, [string]$Name) {
    $target = Join-Path $store $Name
    playwright screenshot --viewport-size "$Width,$Height" --wait-for-timeout 800 "$source/$Page" $target | Out-Null
    if (-not (Test-Path $target)) { throw "Rendering $Name failed." }
    Get-Item $target | Select-Object Name, Length
}

# Screenshots: 3840x2160, the largest size the Store takes. The scene is laid out for
# 1440x810 and enlarged by 3840 / 1440.
Save-Page 'scene.html?s=upcoming&z=2.6667' 3840 2160 '1-upcoming.png'
Save-Page 'scene.html?s=imminent&z=2.6667' 3840 2160 '2-imminent-join.png'
Save-Page 'scene.html?s=overdue&z=2.6667' 3840 2160 '3-overdue.png'
Save-Page 'scene.html?s=panel&z=2.6667' 3840 2160 '4-panel.png'

# Banner at the top of the README, twice the size it is shown at.
Save-Page 'scene.html?s=trio&z=2' 2400 800 '..\readme-banner.png'

# Store logos, in the sizes Partner Center asks for.
Save-Page 'art.html' 2160 2160 'box-art-2160.png'
Save-Page 'art.html' 1440 2160 'poster-art-1440x2160.png'
Save-Page 'art.html?title=0' 300 300 'app-tile-300.png'
Save-Page 'art.html?title=0' 3840 2160 'hero-art-3840x2160.png'
