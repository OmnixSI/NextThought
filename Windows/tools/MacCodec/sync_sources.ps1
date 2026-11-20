# Create the junction SwiftPM needs for the read-only Mac Core sources.
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$target = Join-Path $root "Sources\NextThoughtCore"
$link = Join-Path $PSScriptRoot "Sources\NextThoughtCore"
if (Test-Path $link) { cmd /c rmdir "$link" }
New-Item -ItemType Junction -Path $link -Target $target | Out-Null
Write-Host "Linked $link -> $target"
