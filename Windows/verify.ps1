<#
.SYNOPSIS
  H7: one local verification command. Restores locked dependencies, builds, runs both test
  projects, runs the A15 MacCodec check and (unless skipped) the D1 screenshot batch.
  Stops at the first failing step and prints a short summary for PORTING_STATUS.md.
#>
param(
    [switch]$SkipScreenshots,
    [switch]$SkipMacCheck
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'Pinned SDK missing: run py -3.12 scripts/bootstrap_sdk.py' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$results = New-Object System.Collections.Generic.List[string]
function Invoke-Step([string]$name, [scriptblock]$body) {
    $started = Get-Date
    Write-Host "==> $name"
    & $body
    if ($LASTEXITCODE -ne 0) { throw "Step failed: $name (exit $LASTEXITCODE)" }
    $seconds = [int]((Get-Date) - $started).TotalSeconds
    $script:results.Add("PASS  $name  (${seconds}s)")
}
Push-Location -LiteralPath $projectRoot
try {
    Invoke-Step 'restore (locked, runtime projects)' {
        foreach ($project in @('src/NextThought.Core', 'src/NextThought.Platform', 'src/NextThought.Desktop')) {
            & $dotnet restore $project --locked-mode --verbosity minimal
            if ($LASTEXITCODE -ne 0) { return }
        }
    }
    Invoke-Step 'restore (locked, test projects)' {
        foreach ($project in @('tests/NextThought.Core.Tests', 'tests/NextThought.Tests')) {
            & $dotnet restore $project --locked-mode --no-dependencies --verbosity minimal
            if ($LASTEXITCODE -ne 0) { return }
        }
    }
    Invoke-Step 'build (Release)' {
        & $dotnet build src/NextThought.Desktop -c Release --no-restore --verbosity minimal
    }
    # The test projects reference Core/Platform sources; build them here so the later
    # `dotnet test --no-build` runs fresh assemblies instead of a previous batch's binaries.
    Invoke-Step 'build (tests, Release)' {
        & $dotnet build tests/NextThought.Core.Tests -c Release --no-restore --verbosity minimal
        if ($LASTEXITCODE -ne 0) { return }
        & $dotnet build tests/NextThought.Tests -c Release --no-restore --verbosity minimal
    }
    Invoke-Step 'tests: NextThought.Core.Tests' {
        & $dotnet test tests/NextThought.Core.Tests -c Release --no-build --no-restore `
            --logger 'trx;LogFileName=core.trx' --results-directory artifacts/verify --verbosity minimal
    }
    Invoke-Step 'tests: NextThought.Tests' {
        & $dotnet test tests/NextThought.Tests -c Release --no-build --no-restore `
            --logger 'trx;LogFileName=app.trx' --results-directory artifacts/verify --verbosity minimal
    }
    $fixtures = Join-Path $projectRoot 'tests\Fixtures\mac'
    if ($SkipMacCheck) {
        $results.Add('SKIP  A15 MacCodec check (-SkipMacCheck)')
    } elseif (-not (Test-Path -LiteralPath $fixtures)) {
        $results.Add('SKIP  A15 MacCodec check (fixtures missing: human must run emit on macOS)')
        Write-Warning "A15 fixtures missing: $fixtures"
    } else {
        Invoke-Step 'build (A15 check tool)' {
            & $dotnet build tools/MacCodecCheck -c Release --no-restore --verbosity minimal
        }
        Invoke-Step 'A15 MacCodec check' {
            & $dotnet run --project tools/MacCodecCheck -c Release --no-build -- --fixtures $fixtures
        }
    }
    if ($SkipScreenshots) {
        $results.Add('SKIP  D1 screenshots (-SkipScreenshots)')
    } else {
        Invoke-Step 'D1 screenshots (demo, light/dark)' {
            $shots = Join-Path $projectRoot 'validation\screens\current'
            & $dotnet run --project src/NextThought.Desktop -c Release --no-build -- --demo "--screenshots=$shots"
        }
    }
    $results.Add('VERIFY OK')
}
catch {
    $results.Add('FAIL  ' + $_.Exception.Message)
    throw
}
finally {
    Pop-Location
    Write-Host ''
    Write-Host '--- verify summary ---'
    $results | ForEach-Object { Write-Host $_ }
}
