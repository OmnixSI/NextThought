param([switch]$Publish, [switch]$Installer, [switch]$Unsigned, [string]$CertificateThumbprint, [string]$SignToolPath)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw 'Run py -3.12 scripts/bootstrap_sdk.py first, or install the pinned SDK into .tools/dotnet.'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$sourceRevision = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($Installer -and -not $Publish) { throw 'Use -Publish -Installer together.' }
if ($Unsigned -and $CertificateThumbprint) { throw 'Choose unsigned preview or a real signing certificate.' }
if ($Installer -and -not $Unsigned -and -not $CertificateThumbprint) { throw 'Installer requires a real certificate or explicit -Unsigned for validation.' }
function Invoke-Dotnet {
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit $LASTEXITCODE)" }
}
Push-Location -LiteralPath $projectRoot
try {
    Invoke-Dotnet restore NextThought.Windows.sln --locked-mode --verbosity minimal
    Invoke-Dotnet build NextThought.Windows.sln -c Release --no-restore --verbosity minimal
    Invoke-Dotnet test tests/NextThought.Tests -c Release --no-build --no-restore --logger 'trx;LogFileName=regression.trx' --results-directory artifacts/tests --verbosity minimal
    if ($Publish) {
        [xml]$appProject = Get-Content -LiteralPath 'src\NextThought.Windows\NextThought.Windows.csproj'
        $version = [string]($appProject.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
        if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid application version.' }
        $artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
        $output = [IO.Path]::GetFullPath((Join-Path $artifactRoot ('NextThought-win-x64-' + $version)))
        if (-not $output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid publish directory.' }
        if (Test-Path -LiteralPath $output) {
            $running = Get-Process -Name NextThought -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($output + '\', [StringComparison]::OrdinalIgnoreCase) }
            if ($running) { throw 'Close the published NextThought application before rebuilding.' }
            Remove-Item -LiteralPath $output -Recurse -Force
        }
        Invoke-Dotnet publish src/NextThought.Windows -c Release --self-contained true '-p:PublishSingleFile=false' '-p:RestoreLockedMode=true' -o $output --verbosity minimal
        Invoke-Dotnet publish src/NextThought.Cli -c Release --self-contained true '-p:PublishSingleFile=false' '-p:RestoreLockedMode=true' -o (Join-Path $output 'tools') --verbosity minimal
        # Task 0-8 archived the P1–P4 validation records under Windows/archive/; copy the
        # historical one that still exists from there so a preview build does not fail on it.
        if (Test-Path -LiteralPath 'archive\P2_VALIDATION.md') { Copy-Item -LiteralPath 'archive\P2_VALIDATION.md' -Destination (Join-Path $output 'P2_VALIDATION.md') }
        if (Test-Path -LiteralPath 'P3_VALIDATION.md') { Copy-Item -LiteralPath P3_VALIDATION.md -Destination (Join-Path $output 'P3_VALIDATION.md') }
        if (Test-Path -LiteralPath 'P4_VALIDATION.md') { Copy-Item -LiteralPath P4_VALIDATION.md -Destination (Join-Path $output 'P4_VALIDATION.md') }
        foreach ($document in @('FEATURE_PARITY.md', 'P4_SECOND_PC.md')) { if (Test-Path -LiteralPath $document) { Copy-Item -LiteralPath $document -Destination $output } }
        $ocrHelpers = Join-Path $output 'tools\ocr'
        New-Item -Path $ocrHelpers -ItemType Directory -Force | Out-Null
        foreach ($helper in @('prepare_enhanced_ocr.py','prepare_native_ocr_model.py','prepare_layout_model.py')) { Copy-Item -LiteralPath (Join-Path 'scripts' $helper) -Destination $ocrHelpers }
        Copy-Item -LiteralPath 'validation\p3\ocr-requirements-lock.txt' -Destination $ocrHelpers
        Copy-Item -LiteralPath README.md -Destination (Join-Path $output 'README.md')
        Copy-Item -LiteralPath PORTING_STATUS.md -Destination (Join-Path $output 'PORTING_STATUS.md')
        if (Test-Path -LiteralPath 'P1_VALIDATION.md') { Copy-Item -LiteralPath 'P1_VALIDATION.md' -Destination (Join-Path $output 'P1_VALIDATION.md') }
        Copy-Item -LiteralPath 'scripts\Launch.cmd' -Destination (Join-Path $output 'Launch.cmd')
        Copy-Item -LiteralPath 'scripts\Run-Validation.cmd' -Destination (Join-Path $output 'Run-Validation.cmd')
        $validationOutput = Join-Path $output 'tools\validation'
        New-Item -Path $validationOutput -ItemType Directory -Force | Out-Null
        Copy-Item -LiteralPath 'validation\p4\public-sample.wav' -Destination $validationOutput
        Copy-Item -LiteralPath 'validation\p4\README.md' -Destination $validationOutput
        Copy-Item -LiteralPath '..\Resources\ThirdPartyNotices.txt' -Destination (Join-Path $output 'UPSTREAM_NOTICES.txt')
        Copy-Item -LiteralPath 'licenses' -Destination (Join-Path $output 'licenses') -Recurse
        $signing = 'unsigned-preview'
        if ($CertificateThumbprint) {
            & (Join-Path $PSScriptRoot 'sign_release.ps1') -Directory $output -CertificateThumbprint $CertificateThumbprint -SignToolPath $SignToolPath
            $signing = 'authenticode:' + $CertificateThumbprint.ToLowerInvariant()
        }
        if ((& git -C $projectRoot rev-parse HEAD).Trim() -ne $sourceRevision) { throw 'Source commit changed during the build; rerun to keep the release traceable.' }
        # Prefer the pinned Python 3.12 through the launcher (write_release_manifest.py needs
        # hashlib.file_digest, i.e. Python 3.11+); fall back to a compatible python on PATH.
        $pyLauncher = Get-Command py -ErrorAction SilentlyContinue
        $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
        if ($pyLauncher) {
            & $pyLauncher.Source -3.12 scripts/write_release_manifest.py --directory $output --version $version --signing $signing
        } elseif ($pythonCommand -and $pythonCommand.Source -notlike '*WindowsApps*') {
            & $pythonCommand.Source scripts/write_release_manifest.py --directory $output --version $version --signing $signing
        } else {
            throw 'Python 3.11+ is required to write the release manifest (use the py launcher).'
        }
        if ($LASTEXITCODE -ne 0) { throw 'Release manifest creation failed.' }
        $zip = Join-Path $artifactRoot ('NextThought-Windows-preview-' + $version + '-win-x64.zip')
        Compress-Archive -LiteralPath $output -DestinationPath $zip -Force
        $digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($zip + '.sha256', "$digest  $([IO.Path]::GetFileName($zip))`n", [Text.UTF8Encoding]::new($false))
        Write-Output "Published: $output"
        Write-Output "Archive: $zip"
        Write-Output "SHA256: $digest"
        if ($Installer) {
            & (Join-Path $PSScriptRoot 'build_installer.ps1') -Payload $output -Version $version -Unsigned:$Unsigned -CertificateThumbprint $CertificateThumbprint -SignToolPath $SignToolPath
        }
    }
} finally { Pop-Location }
