# K7 两小时长会实机测试（任务书 §9 K7）— 同时产出 K6 的 60 秒采音证据
# 通过默认扬声器循环播放已知中文语音，程序用系统声音（loopback）录制 2 小时；每分钟记录内存、
# 转写行数、会话目录大小；第 1 分钟快照做 K6 的“60 秒内出现预期句子”断言；停止录音后检查
# “最后一句仍在转写里”（K6 第二部分）。报告写到 validation/realmachine/K7/ 与 K6/。
# 用法：powershell.exe -ExecutionPolicy Bypass -File k7-long-meeting.ps1 -Exe <NextThought.exe> [-Minutes 120]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [int]$Minutes = 120,
    [string]$RepoRoot = ''
)
$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class K7Native
{
    public delegate bool EnumProc(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();

    public static bool HasVisibleWindow(uint processId)
    {
        var found = false;
        EnumWindows((handle, _) =>
        {
            uint pid; GetWindowThreadProcessId(handle, out pid);
            if (pid != processId || !IsWindowVisible(handle)) return true;
            var text = new StringBuilder(256); GetWindowText(handle, text, 256);
            if (text.Length > 0) { found = true; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class K7Input
{
    [StructLayout(LayoutKind.Sequential)]
    public struct KBD { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSE { public int X; public int Y; public uint Data; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct INPUT { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public KBD Key; [FieldOffset(8)] public MOUSE Mouse; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    public static void Chord(ushort[] modifiers, ushort key)
    {
        var list = new List<INPUT>();
        foreach (var modifier in modifiers)
            list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = modifier } });
        list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = key } });
        list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = key, Flags = 0x0002 } });
        for (var index = modifiers.Length - 1; index >= 0; index--)
            list.Add(new INPUT { Type = 1, Key = new KBD { VirtualKey = modifiers[index], Flags = 0x0002 } });
        var inputs = list.ToArray();
        SendInput((uint)inputs.Length, inputs, 40);
    }
}
"@

$k7Dir = Join-Path $RepoRoot 'Windows\validation\realmachine\K7'
$k6Dir = Join-Path $RepoRoot 'Windows\validation\realmachine\K6'
New-Item -ItemType Directory -Path $k7Dir -Force | Out-Null
New-Item -ItemType Directory -Path $k6Dir -Force | Out-Null
$logPath = Join-Path $k7Dir 'runner.log'
function Log([string]$message) {
    $line = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  ' + $message
    Add-Content -Path $logPath -Value $line -Encoding UTF8
    Write-Host $line
}
function Save-Json($path, $value) {
    $value | ConvertTo-Json -Depth 8 | Set-Content -Path $path -Encoding UTF8
}
function Send-RecordHotkey { [K7Input]::Chord(@(0x11, 0x12), 0x4D) }
$player = $null
function Get-SessionDir([string]$dataDir) {
    $sessions = Join-Path $dataDir 'Sessions'
    if (-not (Test-Path $sessions)) { return $null }
    $dir = Get-ChildItem $sessions -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $dir) { return $null }
    return $dir.FullName
}
function Get-TranscriptLines([string]$sessionDir) {
    if (-not $sessionDir) { return 0 }
    $file = Join-Path $sessionDir 'transcript.jsonl'
    if (-not (Test-Path $file)) { return 0 }
    try { return [System.IO.File]::ReadAllLines($file).Length } catch { return 0 }
}
function Get-TranscriptText([string]$sessionDir) {
    if (-not $sessionDir) { return '' }
    $file = Join-Path $sessionDir 'transcript.jsonl'
    if (-not (Test-Path $file)) { return '' }
    $parts = @()
    foreach ($line in [System.IO.File]::ReadAllLines($file)) {
        try { $record = $line | ConvertFrom-Json; if ($record.text) { $parts += [string]$record.text } } catch { }
    }
    return ($parts -join "`n")
}

$startedAt = Get-Date
$app = $null
Log "K7 runner start: minutes=$Minutes exe=$Exe"
$dataDir = Join-Path $env:TEMP ('nt-k7-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$report = [ordered]@{
    test = 'K7 两小时长会议'
    startedAt = $startedAt.ToString('s')
    minutesRequested = $Minutes
    exe = $Exe
    dataDir = $dataDir
    passed = $false
    failures = @()
    memory = @()
    transcriptCounts = @()
}
try {
    # ---- 已知语音 WAV（SAPI 中文语音，默认扬声器循环播放）----
    $wav = Join-Path $k7Dir 'known-speech.wav'
    $knownSentences = @(
        '这是长会议测试的第一句，今天讨论系统设计。',
        '第二句：缓存命中率目标是百分之九十二。',
        '第三句：灰度期间比较 P95 与错误率。',
        '第四句：会后补一张读写时序图。',
        '最后一句：这是测试的最后一句。'
    )
    if (-not (Test-Path $wav)) {
        Log 'rendering known-speech.wav with SAPI (Huihui)'
        $voice = New-Object -ComObject SAPI.SpVoice
        $stream = New-Object -ComObject SAPI.SpFileStream
        $stream.Open($wav, 3, $false)
        $voice.AudioOutputStream = $stream
        $voice.Rate = -1
        foreach ($sentence in $knownSentences) { $voice.Speak($sentence) | Out-Null; $voice.Speak('  ') | Out-Null }
        $stream.Close()
    }
    Log ("wav=" + $wav + " size=" + (Get-Item $wav).Length)

    # ---- 隔离数据目录：junction 复用本机模型，麦克风关闭，只录系统声音 ----
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
    $modelsSource = Join-Path $env:LOCALAPPDATA 'NextThought\models'
    if (Test-Path $modelsSource) { New-Item -ItemType Junction -Path (Join-Path $dataDir 'models') -Target $modelsSource | Out-Null }
    $runtimesSource = Join-Path $env:LOCALAPPDATA 'NextThought\runtimes'
    if (Test-Path $runtimesSource) { New-Item -ItemType Junction -Path (Join-Path $dataDir 'runtimes') -Target $runtimesSource | Out-Null }
    $settings = @{
        microphoneEnabled = $false
        automaticAnswers = $false
        onboardingGuideCompletedVersion = '3.4.0'
        capturePrivacy = $true
    }
    Save-Json (Join-Path $dataDir 'settings.json') $settings

    # ---- 启动应用 ----
    $env:NEXTTHOUGHT_DATA_DIR = $dataDir
    $app = Start-Process -FilePath $Exe -PassThru
    $report.appPid = $app.Id
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline -and -not $app.HasExited) {
        if ([K7Native]::HasVisibleWindow([uint32]$app.Id)) { break }
        Start-Sleep -Milliseconds 250
    }
    if ($app.HasExited) { throw '应用启动后退出' }
    Log 'main window visible'
    Start-Sleep -Seconds 2

    # ---- 播放已知语音并开始记录（全局快捷键 Ctrl+Alt+M），等会话目录出现 ----
    $player = New-Object System.Media.SoundPlayer $wav
    $player.PlayLooping()
    Log 'audio looping on default speaker; sending record hotkey'
    $sessionDir = $null
    $recordDeadline = (Get-Date).AddSeconds(180)
    while ((Get-Date) -lt $recordDeadline -and -not $app.HasExited) {
        Send-RecordHotkey
        Start-Sleep -Seconds 6
        $sessionDir = Get-SessionDir $dataDir
        if ($sessionDir) { break }
    }
    if (-not $sessionDir) { throw '开始记录失败：180 秒内没有出现会话目录（模型/音频设备问题）' }
    $recordingStarted = Get-Date
    $report.recordingStartedAt = $recordingStarted.ToString('s')
    $report.sessionDir = $sessionDir
    Log ("recording started, session=" + $sessionDir)

    # ---- 每分钟采样 ----
    $lastCount = 0
    $stalls = 0
    $samples = 0
    for ($minute = 1; $minute -le $Minutes; $minute++) {
        $target = $recordingStarted.AddMinutes($minute)
        $wait = ($target - (Get-Date)).TotalMilliseconds
        if ($wait -gt 0) { Start-Sleep -Milliseconds ([int]$wait) }
        if ($app.HasExited) { $report.failures += ("应用在第 " + $minute + " 分钟退出"); break }
        $app.Refresh()
        $sessionDir = Get-SessionDir $dataDir
        $count = Get-TranscriptLines $sessionDir
        $grew = ($count -gt $lastCount)
        if (-not $grew -and $minute -gt 2) { $stalls++ } else { $stalls = 0 }
        $sample = [ordered]@{
            minute = $minute
            at = (Get-Date).ToString('s')
            elapsedSeconds = [int]((Get-Date) - $recordingStarted).TotalSeconds
            workingSetMB = [Math]::Round($app.WorkingSet64 / 1MB, 1)
            privateMB = [Math]::Round($app.PrivateMemorySize64 / 1MB, 1)
            transcriptLines = $count
            transcriptGrew = $grew
            sessionBytes = if ($sessionDir) { (Get-ChildItem $sessionDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum } else { 0 }
            audioPlaying = $true
        }
        $report.memory += $sample
        $report.transcriptCounts += $count
        $samples++
        Save-Json (Join-Path $k7Dir 'report-partial.json') $report
        Add-Content -Path (Join-Path $k7Dir 'memory.csv') -Value ("{0},{1},{2},{3},{4}" -f $minute, $sample.workingSetMB, $sample.privateMB, $count, $sample.sessionBytes) -Encoding UTF8
        Log ("minute " + $minute + " ws=" + $sample.workingSetMB + "MB lines=" + $count + " grew=" + $grew)
        $lastCount = $count
        if ($minute -eq 1) {
            # K6 第一部分：60 秒转写里应出现已知语音的句子
            $text = Get-TranscriptText $sessionDir
            Set-Content -Path (Join-Path $k6Dir 'transcript-first-60s.txt') -Value $text -Encoding UTF8
            $keywords = @('缓存', '命中率', '灰度', '时序图')
            $found = @($keywords | Where-Object { $text -like ('*' + $_ + '*') })
            Save-Json (Join-Path $k6Dir 'report-part1.json') ([ordered]@{
                test = 'K6 采音识别（60 秒）'
                at = (Get-Date).ToString('s')
                transcriptLines = $count
                matchedKeywords = $found
                transcriptExcerpt = $text.Substring(0, [Math]::Min(600, $text.Length))
                passed = ($found.Count -ge 2)
            })
            Log ("K6 first-60s keywords=" + ($found -join ','))
        }
    }
    $report.samples = $samples

    # ---- 停止录音，检查最后一句仍在转写里（K6 第二部分）----
    Send-RecordHotkey
    Start-Sleep -Seconds 15
    $player.Stop()
    $sessionDir = Get-SessionDir $dataDir
    $finalText = Get-TranscriptText $sessionDir
    $finalCount = Get-TranscriptLines $sessionDir
    Set-Content -Path (Join-Path $k6Dir 'transcript-final.txt') -Value $finalText -Encoding UTF8
    $lastSentenceFound = ($finalText -like '*最后*') -and ($finalText -like '*测试*')
    $part1Path = Join-Path $k6Dir 'report-part1.json'
    $part1 = @{ passed = $false; matchedKeywords = @() }
    if (Test-Path $part1Path) { try { $part1 = Get-Content $part1Path -Raw | ConvertFrom-Json } catch { } }
    Save-Json (Join-Path $k6Dir 'report.json') ([ordered]@{
        test = 'K6 采音识别（默认扬声器 + 系统声音录制；证据来自 K7 同一次运行的 60 秒快照与停止检查）'
        date = (Get-Date).ToString('s')
        exe = $Exe
        knownSpeechWav = $wav
        minute60Keywords = $part1.matchedKeywords
        minute60Passed = $part1.passed
        finalTranscriptLines = $finalCount
        lastSentenceFound = $lastSentenceFound
        passed = ($part1.passed -and $lastSentenceFound)
        transcriptExcerpt = $finalText.Substring(0, [Math]::Min(600, $finalText.Length))
    })
    Log ("K6 part2 lastSentenceFound=" + $lastSentenceFound + " finalLines=" + $finalCount)

    # ---- 收尾：结束应用，分析连续性/内存 ----
    if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    $counts = @($report.transcriptCounts)
    $nonZero = @($counts | Where-Object { $_ -gt 0 })
    $stallRuns = 0; $runLength = 0
    for ($index = 1; $index -lt $counts.Count; $index++) {
        if ($counts[$index] -le $counts[$index - 1]) { $runLength++; if ($runLength -gt $stallRuns) { $stallRuns = $runLength } } else { $runLength = 0 }
    }
    $memory = @($report.memory)
    $third = [Math]::Max(1, [int]($memory.Count / 3))
    $first = @($memory | Select-Object -First $third)
    $last = @($memory | Select-Object -Last $third)
    $firstValues = @($first | ForEach-Object { [double]$_.workingSetMB })
    $lastValues = @($last | ForEach-Object { [double]$_.workingSetMB })
    $firstAvg = if ($firstValues.Count -gt 0) { ($firstValues | Measure-Object -Average).Average } else { 0 }
    $lastAvg = if ($lastValues.Count -gt 0) { ($lastValues | Measure-Object -Average).Average } else { 0 }
    $ratio = if ($firstAvg -gt 0) { [Math]::Round($lastAvg / $firstAvg, 2) } else { 0 }
    $sessionOk = $false
    if ($sessionDir) {
        $sessionOk = (Test-Path (Join-Path $sessionDir 'meta.json')) -and ((Get-TranscriptLines $sessionDir) -gt 0)
    }
    $report.memoryFirstThirdAvgMB = [Math]::Round($firstAvg, 1)
    $report.memoryLastThirdAvgMB = [Math]::Round($lastAvg, 1)
    $report.memoryRatio = $ratio
    $report.maxStallSamples = $stallRuns
    $report.finalTranscriptLines = $finalCount
    $report.sessionReopens = $sessionOk
    $report.finishedAt = (Get-Date).ToString('s')
    if ($samples -lt ($Minutes - 1)) { $report.failures += ("采样不足：" + $samples + "/" + $Minutes) }
    if ($finalCount -le 0) { $report.failures += '最终转写为空' }
    if ($stallRuns -gt 3) { $report.failures += ("转写停顿超过 3 分钟：最大停顿样本 " + $stallRuns) }
    if (-not $sessionOk) { $report.failures += '结束后会议文件不可读' }
    if ($ratio -ge 1.5) { $report.failures += ("内存持续增长：末三分之一/首三分之一 = " + $ratio) }
    $report.passed = (@($report.failures).Count -eq 0)
    Save-Json (Join-Path $k7Dir 'report.json') $report
    Save-Json (Join-Path $k7Dir 'report-partial.json') $report
    Log ("K7 done passed=" + $report.passed + " failures=" + (@($report.failures) -join '; '))
} catch {
    $report.finishedAt = (Get-Date).ToString('s')
    $report.failures += ($_.Exception.Message)
    $report.passed = $false
    Save-Json (Join-Path $k7Dir 'report.json') $report
    Save-Json (Join-Path $k7Dir 'report-partial.json') $report
    Log ('K7 FAILED: ' + $_.Exception.Message)
} finally {
    try { if ($player) { $player.Stop() } } catch { }
    try { if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue } } catch { }
}
