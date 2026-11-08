$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version = '7.1.1'
$expected = '8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0'
$toolRoot = Join-Path $projectRoot ('.tools\inno-' + $version)
$download = Join-Path $projectRoot ('.tools\innosetup-' + $version + '-x64.exe')
New-Item -Path (Split-Path -Parent $download) -ItemType Directory -Force | Out-Null
if (-not (Test-Path -LiteralPath $download)) {
    & curl.exe --fail --location --retry 2 --retry-all-errors --retry-delay 2 --connect-timeout 20 --max-time 180 --silent --show-error --output ($download + '.partial') ('https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-' + $version + '-x64.exe')
    if ($LASTEXITCODE -ne 0) {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Inno Setup download failed; no compiler was executed. Retry later or use the official GitHub release download.' }
        & gh release download is-7_1_1 --repo jrsoftware/issrc --pattern innosetup-7.1.1-x64.exe --dir (Split-Path -Parent $download) --clobber
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup API download failed; no compiler was executed.' }
    } else {
        if ((Get-FileHash -LiteralPath ($download + '.partial') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Inno Setup download SHA-256 mismatch.' }
        Move-Item -LiteralPath ($download + '.partial') -Destination $download
    }
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Inno Setup installer SHA-256 mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') { throw ('Inno Setup publisher verification failed: ' + $signature.Status) }
$compiler = Join-Path $toolRoot 'ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $start = [Diagnostics.ProcessStartInfo]::new($download)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($argument in @('/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/SP-', '/TASKS=', ('/DIR=' + $toolRoot))) { $start.ArgumentList.Add($argument) }
    $install = [Diagnostics.Process]::Start($start)
    $install.WaitForExit()
    if ($install.ExitCode -ne 0) { throw ('Inno Setup compiler installation failed: ' + $install.ExitCode) }
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler missing after installation.' }
$compilerSignature = Get-AuthenticodeSignature -LiteralPath $compiler
if ($compilerSignature.Status -ne 'Valid' -or $compilerSignature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') { throw 'Inno Setup compiler publisher verification failed.' }
Write-Output ('Pinned compiler ready: ' + $compiler)
