param(
    [string]$Exe,
    [string]$OutputDirectory
)
# R8 / §10.8: logs must never carry keys, tokens or pairing codes. Run the app's self-test flow with
# fake secrets planted in every secret-looking settings field, then search the whole data directory
# (including Logs) for them. This never touches the real installation or user data.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Exe) { $Exe = Join-Path $root 'src\NextThought.Desktop\bin\Release\net10.0-windows10.0.19041.0\win-x64\NextThought.exe' }
if (-not (Test-Path -LiteralPath $Exe)) {
    $candidates = Get-ChildItem (Join-Path $root 'src\NextThought.Desktop\bin') -Recurse -Filter NextThought.exe -ErrorAction SilentlyContinue
    if ($candidates) { $Exe = $candidates[0].FullName } else { throw 'NextThought.exe not found; build the desktop app first.' }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'validation\release-cleanliness' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$sandbox = Join-Path $env:TEMP ('nt-log-secret-' + [Guid]::NewGuid().ToString('N'))
$dataRoot = Join-Path $sandbox 'data'
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null

$fakeApiKey = 'nt-fake-api-key-' + [Guid]::NewGuid().ToString('N')
$fakeToken = 'nt-fake-token-' + [Guid]::NewGuid().ToString('N')
$fakePairing = 'nt-fake-pairing-' + [Guid]::NewGuid().ToString('N')
$settings = [ordered]@{
    schema = 1
    apiKey = $fakeApiKey
    answerApiKey = $fakeApiKey
    cloudSttKey = $fakeApiKey
    tavilyApiKey = $fakeApiKey
    tavilyKey = $fakeApiKey
    githubToken = $fakeToken
    companionPairingCode = $fakePairing
    companionToken = $fakePairing
} | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $dataRoot 'settings.json'), $settings, [Text.UTF8Encoding]::new($false))

$secrets = @($fakeApiKey, $fakeToken, $fakePairing)
function Run-SelfTest([string]$name) {
    $start = [Diagnostics.ProcessStartInfo]::new($Exe)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.Environment['NEXTTHOUGHT_DATA_DIR'] = $dataRoot
    $start.Arguments = '--self-test=' + $name
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit(180000)) { try { $process.Kill() } catch { } }
    return $process.ExitCode
}

$flows = @(
    @{ name = 'startup'; exit = (Run-SelfTest 'startup') },
    @{ name = 'settings-presets'; exit = (Run-SelfTest 'settings-presets') }
)

$violations = [Collections.Generic.List[object]]::new()
$scanned = 0
foreach ($file in Get-ChildItem -LiteralPath $sandbox -Recurse -File -ErrorAction SilentlyContinue) {
    $scanned++
    $content = [IO.File]::ReadAllText($file.FullName, [Text.Encoding]::UTF8)
    foreach ($secret in $secrets) {
        if ($content.Contains($secret)) { $violations.Add([pscustomobject]@{ file = $file.FullName; secretKind = 'planted' }) }
    }
    foreach ($pattern in @('sk-[A-Za-z0-9]{16,}', 'github_pat_[A-Za-z0-9_]{20,}', 'ghp_[A-Za-z0-9]{20,}', 'tvy[A-Za-z0-9]{20,}')) {
        if ([regex]::IsMatch($content, $pattern)) { $violations.Add([pscustomobject]@{ file = $file.FullName; secretKind = $pattern }) }
    }
}

$report = [ordered]@{
    checkedAt = [DateTimeOffset]::Now.ToString('o')
    exe = $Exe
    passed = ($violations.Count -eq 0)
    scannedFiles = $scanned
    flows = $flows
    plantedSecretKinds = @('api key', 'github token', 'pairing code')
    violations = $violations
    note = 'The fake secrets are planted in every secret-looking settings field; the self-test flows exercise the settings and startup paths, then the whole isolated data directory (Logs included) is searched for the planted values and for common token patterns.'
}
$reportPath = Join-Path $outputRoot 'log-secret-check.json'
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
try { Remove-Item -LiteralPath $sandbox -Recurse -Force } catch { }
Write-Output ('log-secret-check: scanned ' + $scanned + ' files; violations=' + $violations.Count + ' -> ' + $reportPath)
if ($violations.Count -gt 0) { exit 1 }
