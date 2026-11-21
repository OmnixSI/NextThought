<#
.SYNOPSIS
  H7: one local verification command. Restores locked dependencies, builds, runs both test
  projects, runs the Core tests a second time with the process treated as UTC (B24), runs the
  A15 Mac fixture comparison tests and (unless skipped) the D1 screenshot batch.
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
    # B24: Windows cannot change TimeZoneInfo.Local for a process without administrator rights
    # (TZ is only honoured on Unix; tzutil needs elevation). The Core test assembly reads this
    # variable in its ModuleInitializer and points the production-local-time seam at UTC, so the
    # second Core run exercises the suite exactly as if the machine's zone were UTC. The app never
    # reads the variable, so production behaviour is unchanged. See validation/timezone.md.
    Invoke-Step 'tests: NextThought.Core.Tests (UTC)' {
        $previousTimeZone = $env:NEXTTHOUGHT_TEST_TIMEZONE
        $env:NEXTTHOUGHT_TEST_TIMEZONE = 'UTC'
        try {
            & $dotnet test tests/NextThought.Core.Tests -c Release --no-build --no-restore `
                --logger 'trx;LogFileName=core-utc.trx' --results-directory artifacts/verify --verbosity minimal
        }
        finally {
            if ($null -eq $previousTimeZone) { Remove-Item Env:NEXTTHOUGHT_TEST_TIMEZONE -ErrorAction SilentlyContinue }
            else { $env:NEXTTHOUGHT_TEST_TIMEZONE = $previousTimeZone }
        }
    }

    # H7: the old key-name-only check tool could pass while the revisions were wrong
    # wrong, so it was removed. The A15 comparison now runs as the localization fixture suites in the
    # Core test project: MacFixtureCodecTests reads every family with the production types and writes
    # it back for a key-by-key comparison, MacFixtureRevisionTests recomputes revisions.json. Both
    # skip themselves (with a printed reason) when the committed Mac fixtures are absent.
    if ($SkipMacCheck) {
        $results.Add('SKIP  A15 Mac fixture comparison tests (-SkipMacCheck)')
    } else {
        Invoke-Step 'tests: Mac fixture codec + revision parity (A15)' {
            & $dotnet test tests/NextThought.Core.Tests -c Release --no-build --no-restore `
                --filter 'FullyQualifiedName~MacFixtureRoundTripTests|FullyQualifiedName~MacFixtureCodecTests|FullyQualifiedName~MacRevisionParityTests|FullyQualifiedName~MacFixtureRevisionTests' `
                --logger 'trx;LogFileName=mac-fixtures.trx' --results-directory artifacts/verify --verbosity minimal
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
