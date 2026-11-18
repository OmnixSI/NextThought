param(
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $InstallDir 'NextThought.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "NextThought.exe not found in $InstallDir" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$checks = @(
    @{ Name = 'startup';              Flag = '--startup-check=' },
    @{ Name = 'acceptance';           Flag = '--acceptance-check=' },
    @{ Name = 'page';                 Flag = '--page-check=' },
    @{ Name = 'glossary-search';      Flag = '--glossary-search-check=' },
    @{ Name = 'process';              Flag = '--process-check=' },
    @{ Name = 'session-search';       Flag = '--session-search-check=' },
    @{ Name = 'series-briefing';      Flag = '--series-briefing-check=' },
    @{ Name = 'history-search';       Flag = '--history-search-check=' },
    @{ Name = 'backup-manifest';      Flag = '--backup-manifest-check=' },
    @{ Name = 'meeting-plan';         Flag = '--meeting-plan-check=' },
    @{ Name = 'evidence';             Flag = '--evidence-check=' },
    @{ Name = 'user-content';         Flag = '--user-content-check=' },
    @{ Name = 'workspace-deletion';   Flag = '--workspace-deletion-check=' },
    @{ Name = 'document-job';         Flag = '--document-job-check=' },
    @{ Name = 'archive-persistence';  Flag = '--archive-persistence-check=' },
    @{ Name = 'update-backup';        Flag = '--update-backup-check=' },
    @{ Name = 'update-restore';       Flag = '--update-restore-check=' }
)

$results = @()
foreach ($check in $checks) {
    $report = Join-Path $OutputDir ($check.Name + '.json')
    $stdout = Join-Path $OutputDir ($check.Name + '.out.txt')
    $stderr = Join-Path $OutputDir ($check.Name + '.err.txt')
    $env:NEXTTHOUGHT_DATA_DIR = Join-Path $OutputDir ('data-' + $check.Name)
    $process = Start-Process -FilePath $exe -ArgumentList @($check.Flag + $report) `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { Stop-Process -Id $process.Id -Force } catch { }
        $results += [pscustomobject]@{ name = $check.Name; passed = $false; error = 'timeout' }
        continue
    }
    $passed = $false
    $errorText = ''
    if (Test-Path -LiteralPath $report) {
        try {
            $parsed = [IO.File]::ReadAllText($report, [Text.Encoding]::UTF8) | ConvertFrom-Json
            $passed = [bool]$parsed.passed
        } catch { $errorText = 'report is not valid JSON' }
    } else {
        $errorText = 'report missing'
    }
    if (-not $passed -and -not $errorText) { $errorText = 'check reported failure' }
    $results += [pscustomobject]@{ name = $check.Name; passed = $passed; error = $errorText }
}

$summary = [pscustomobject]@{
    passed = (@($results | Where-Object { -not $_.passed }).Count -eq 0)
    total = $results.Count
    checks = $results
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDir 'summary.json') -Encoding UTF8
foreach ($result in $results) {
    $mark = if ($result.passed) { 'PASS' } else { 'FAIL' }
    Write-Host ("{0} {1} {2}" -f $mark, $result.name, $result.error)
}
if (-not $summary.passed) { exit 1 }
