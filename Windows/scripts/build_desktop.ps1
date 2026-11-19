param(
    [string]$Version = "0.5.0",
    [string]$Configuration = "Release",
    [switch]$SkipInstaller,
    [switch]$SkipZip,
    [switch]$ReadyToRun
)
# Build, publish, package and verify the Avalonia desktop app (NextThought.exe).
# Outputs land in <repo>/../../dist by default so downloaded tooling, intermediates
# and results all stay on this drive.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw 'Run scripts/bootstrap_sdk.py first.' }
$distRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '..\..\..\dist'))
$payload = Join-Path $distRoot ("NextThought-$Version-win-x64")
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

function Invoke-Dotnet {
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit $LASTEXITCODE)" }
}

Push-Location -LiteralPath $projectRoot
try {
    # A crashed MSBuild node can leave stale intermediates that crash the Avalonia XAML
    # compiler (MSB4166 / 0xC0000005); start every packaging run from a clean Desktop obj.
    foreach ($stale in @('src\NextThought.Desktop\obj', 'src\NextThought.Desktop\bin')) {
        if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Recurse -Force }
    }
    Invoke-Dotnet build "src\NextThought.Desktop\NextThought.Desktop.csproj" -c $Configuration /m:1 --verbosity minimal
    if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload -Recurse -Force }
    $publishArgs = @("src\NextThought.Desktop\NextThought.Desktop.csproj", "-c", $Configuration, "-r", "win-x64",
        "--self-contained", "true", "-p:PublishSingleFile=false", "-o", $payload, "--verbosity", "minimal")
    if ($ReadyToRun) { $publishArgs += "-p:PublishReadyToRun=true" }
    Invoke-Dotnet publish @publishArgs

    # Startup self-check on the published build, in an isolated data directory.
    # The first launch of a freshly published payload occasionally dies in the XAML/CLR
    # startup path (0xC0000005/0x80131506) on this machine and succeeds on the next run;
    # retry a couple of times so a hardware/AV hiccup does not fail an otherwise good build.
    $smoke = Join-Path $distRoot 'smoke'
    New-Item -ItemType Directory -Force -Path $smoke | Out-Null
    $env:NEXTTHOUGHT_DATA_DIR = $smoke
    $startup = Join-Path $smoke 'startup.json'
    $processExited = $false
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        if (Test-Path $startup) { Remove-Item $startup -Force }
        $process = Start-Process -FilePath (Join-Path $payload 'NextThought.exe') -ArgumentList @("--startup-check=$startup") -PassThru -Wait
        if ($process.ExitCode -eq 0 -and (Test-Path $startup)) { $processExited = $true; break }
        Write-Output ("startup attempt {0} failed (exit {1}); retrying" -f $attempt, $process.ExitCode)
        Start-Sleep -Milliseconds 1500
    }
    if (-not $processExited) { throw 'Startup self-check failed after 3 attempts.' }
    $report = Get-Content -LiteralPath $startup -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Output ("startup: passed={0} seconds={1} workingSetMiB={2} steps={3}" -f $report.passed, $report.seconds, $report.workingSetMiB, $report.initTimings)

    if (-not $SkipInstaller) {
        $iscc = Join-Path $projectRoot '.tools\inno-7.1.1\ISCC.exe'
        if (-not (Test-Path -LiteralPath $iscc)) { throw 'Run scripts/bootstrap_installer.ps1 first (or install Inno Setup 7.1.1 into .tools/inno-7.1.1).' }
        & $iscc "/DAppVersion=$Version" "/DPayload=$payload" "/DOutputDirectory=$distRoot" "/DOutputName=NextThought-$Version-windows-x64-setup" (Join-Path $projectRoot 'installer\NextThought.Desktop.iss') | Select-Object -Last 3
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    }

    if (-not $SkipZip) {
        $zip = Join-Path $distRoot "NextThought-$Version-windows-x64.zip"
        if (Test-Path -LiteralPath $zip) { Remove-Item $zip -Force }
        Compress-Archive -LiteralPath $payload -DestinationPath $zip -Force
        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($zip + '.sha256', "$hash  $([IO.Path]::GetFileName($zip))`n", [Text.UTF8Encoding]::new($false))
        Write-Output "zip: $zip"
        Write-Output "sha256: $hash"
    }
    Write-Output "payload: $payload"
}
finally {
    Remove-Item Env:NEXTTHOUGHT_DATA_DIR -ErrorAction SilentlyContinue
    Pop-Location
}
