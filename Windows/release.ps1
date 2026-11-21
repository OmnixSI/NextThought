param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$Ci,
    [switch]$SkipVerify,
    [switch]$SkipInstaller,
    [switch]$SkipLifecycle
)
# One-command Windows release pipeline (task R2/R3).
#   Windows\release.ps1 -Version 0.9.0          # human release: clean check, full verify, publish, installer
#   Windows\release.ps1 -Version 0.9.0 -Ci      # CI: skip clean/branch checks and the separate verify call
# Output: Windows\artifacts\release\<version>\ with the unsigned setup, SHA256SUMS.txt, update.json,
# RELEASE_NOTES.md; the human gh release command is printed but never executed.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$csproj = Join-Path $projectRoot 'src\NextThought.Desktop\NextThought.Desktop.csproj'
$installerScript = Join-Path $projectRoot 'scripts\build_installer.ps1'
$releaseRoot = Join-Path $projectRoot 'artifacts\release'
$stageRoot = Join-Path $releaseRoot '.stage'
$outRoot = Join-Path $releaseRoot $Version
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Step([string]$name, [scriptblock]$body) {
    Write-Host ('==> ' + $name)
    & $body
    if ($LASTEXITCODE -ne 0) { throw ('Step failed: ' + $name + ' (exit ' + $LASTEXITCODE + ')') }
}
function Assert([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid -Version (expected x.y.z).' }
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'Pinned SDK missing: run py -3.12 scripts/bootstrap_sdk.py.' }

[xml]$project = Get-Content -LiteralPath $csproj
$projectVersion = [string]($project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
Assert ($projectVersion -eq $Version) ('-Version ' + $Version + ' does not match NextThought.Desktop.csproj Version ' + $projectVersion + '.')

Push-Location -LiteralPath $projectRoot
try {
    if (-not $Ci) {
        $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
        Assert ($branch -eq 'windows/parity') ('Release must run on windows/parity, current branch: ' + $branch)
        $dirty = (& git status --porcelain --untracked-files=no)
        Assert (($dirty | Measure-Object).Count -eq 0) 'Working tree is not clean; commit or stash first.'
    }
    if (-not $Ci -and -not $SkipVerify) {
        Step 'verify.ps1 (full; includes the UTC Core pass, Mac fixtures and screenshots)' {
            & (Join-Path $projectRoot 'verify.ps1')
        }
    }
    if ($SkipVerify -and -not $Ci) { Write-Host 'SKIP  verify.ps1 (-SkipVerify)' }

    # Version, build number and commit: the build number is the number of commits on the branch,
    # which is monotonically increasing for the same version (rule documented in RELEASE.md).
    $commit = (& git rev-parse HEAD).Trim()
    $buildNumber = [int]((& git rev-list --count HEAD).Trim())

    if (Test-Path -LiteralPath $outRoot) { Remove-Item -LiteralPath $outRoot -Recurse -Force }
    if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
    New-Item -Path $outRoot -ItemType Directory -Force | Out-Null
    $payload = Join-Path $stageRoot 'payload'
    New-Item -Path $payload -ItemType Directory -Force | Out-Null

    Step 'publish (x64, self-contained, ReadyToRun)' {
        & $dotnet publish (Join-Path $projectRoot 'src\NextThought.Desktop\NextThought.Desktop.csproj') -c Release -r win-x64 `
            --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -o $payload --verbosity minimal
    }
    Assert (Test-Path -LiteralPath (Join-Path $payload 'NextThought.exe')) 'Published payload has no NextThought.exe.'
    $manifest = [ordered]@{
        product  = 'NextThought.Desktop'
        version  = $Version
        revision = $commit
        signing  = 'unsigned-preview'
        build    = $buildNumber
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $payload 'release-manifest.json'), $manifest, [Text.UTF8Encoding]::new($false))

    $installerName = 'NextThought-' + $Version + '-windows-x64-setup'
    $setup = $null
    if ($SkipInstaller) {
        Write-Host 'SKIP  installer (-SkipInstaller)'
    } else {
        Step 'unsigned per-user installer (Inno Setup, NextThought.Desktop.iss)' {
            & $installerScript -Payload $payload -Version $Version -Unsigned `
                -InstallId 'NextThought.Desktop' -ShortcutGroup 'NextThought' `
                -OutputDirectory $outRoot -OutputName $installerName
        }
        $setup = Join-Path $outRoot ($installerName + '.exe')
        Assert (Test-Path -LiteralPath $setup) ('Installer missing: ' + $setup)
    }

    # SHA256SUMS.txt and update.json (fields follow the Mac update manifest, with the installer name).
    if ($setup) {
        $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText((Join-Path $outRoot 'SHA256SUMS.txt'), ($hash + '  ' + (Split-Path -Leaf $setup) + "`n"), [Text.UTF8Encoding]::new($false))
        $update = [ordered]@{
            tag             = 'windows-v' + $Version
            version         = $Version
            build           = $buildNumber
            asset           = (Split-Path -Leaf $setup)
            sha256          = $hash
            commit          = $commit
            publishedAt     = (Get-Date).ToUniversalTime().ToString('o')
            platform        = 'windows-x64'
            unsigned        = $true
        } | ConvertTo-Json
        [IO.File]::WriteAllText((Join-Path $outRoot 'update.json'), $update, [Text.UTF8Encoding]::new($false))
    }

    $notes = Join-Path $projectRoot 'RELEASE_NOTES.md'
    if (Test-Path -LiteralPath $notes) {
        Copy-Item -LiteralPath $notes -Destination (Join-Path $outRoot 'RELEASE_NOTES.md')
    } else {
        $fallback = "# NextThought for Windows $Version`n`n- Unsigned per-user build.`n"
        [IO.File]::WriteAllText((Join-Path $outRoot 'RELEASE_NOTES.md'), $fallback, [Text.UTF8Encoding]::new($false))
    }

    if (-not $SkipLifecycle -and -not $Ci) {
        Step 'installer lifecycle and in-app update tests (scripts/test_installer_lifecycle.ps1)' {
            & (Join-Path $projectRoot 'scripts\test_installer_lifecycle.ps1')
        }
    } elseif ($Ci) {
        Write-Host 'SKIP  installer lifecycle/update tests in CI (-Ci); run release.ps1 locally for them.'
    }

    Write-Host ''
    Write-Host ('Release artifacts: ' + $outRoot)
    Get-ChildItem -LiteralPath $outRoot -File | Sort-Object Name | ForEach-Object {
        $size = '{0:N1} KB' -f ($_.Length / 1KB)
        $sha = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Host ('  ' + $_.Name + '  ' + $size + '  SHA256 ' + $sha)
    }
    Write-Host ''
    Write-Host 'Next steps for a human (NOT executed by this script):'
    Write-Host ('  gh release create windows-v' + $Version + ' --target windows/parity --title "NextThought for Windows ' + $Version + '" --notes-file "' + (Join-Path $outRoot 'RELEASE_NOTES.md') + '" --latest=false "' + $setup + '" "' + (Join-Path $outRoot 'SHA256SUMS.txt') + '" "' + (Join-Path $outRoot 'update.json') + '"')
    Write-Host ''
    Write-Host 'RELEASE OK'
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
