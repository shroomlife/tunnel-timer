<#
.SYNOPSIS
    Renders every Microsoft Store listing image into docs/store.

.DESCRIPTION
    Screenshots are composed by docs/store/src/scene.html from the real window captures in
    docs/. Logo art is drawn by docs/store/src/art.html. Both are rendered to PNG with the
    Playwright CLI, which has to be on the PATH.

.EXAMPLE
    pwsh scripts/render-store-assets.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$store = (Resolve-Path (Join-Path $PSScriptRoot '..\docs\store')).Path
$source = 'file:///' + ($store -replace '\\', '/') + '/src'

function Save-Page([string]$Page, [int]$Width, [int]$Height, [string]$Name) {
    $target = Join-Path $store $Name
    playwright screenshot --viewport-size "$Width,$Height" --wait-for-timeout 800 "$source/$Page" $target | Out-Null
    if (-not (Test-Path $target)) { throw "Rendering $Name failed." }
    Get-Item $target | Select-Object Name, Length
}

# Screenshots: 1440x810, above the Store minimum of 1366x768.
Save-Page 'scene.html?s=upcoming' 1440 810 '1-upcoming.png'
Save-Page 'scene.html?s=imminent' 1440 810 '2-imminent-join.png'
Save-Page 'scene.html?s=overdue' 1440 810 '3-overdue.png'
Save-Page 'scene.html?s=panel' 1440 810 '4-panel.png'

# Banner at the top of the README.
Save-Page 'scene.html?s=trio' 1200 400 '..\readme-banner.png'

# Store logos, in the sizes Partner Center asks for.
Save-Page 'art.html' 2160 2160 'box-art-2160.png'
Save-Page 'art.html' 1440 2160 'poster-art-1440x2160.png'
Save-Page 'art.html?title=0' 300 300 'app-tile-300.png'
Save-Page 'art.html?title=0' 3840 2160 'hero-art-3840x2160.png'
