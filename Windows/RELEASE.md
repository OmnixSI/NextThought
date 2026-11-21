# Releasing NextThought for Windows

This is the human checklist for publishing a Windows build. Everything is done from a clean
`windows/parity` checkout in the repository root; the only command you have to remember is
`Windows/release.ps1 -Version 0.9.0`. The script never publishes anything itself: it prints the
`gh release create` command and stops. Windows releases are tagged `windows-v<version>` and are
never marked "latest", so the repository's download link keeps pointing at the Mac release.

The current release is **0.9.0** for **Windows 10 2004 (build 19041) and later, x64**.

## 1. Prerequisites (one time per machine)

- Windows 10/11 x64 with PowerShell 5.1 or PowerShell 7.
- Git, Python 3.12 (`py -3.12 --version`) and the GitHub CLI (`gh --version`, logged in with
  access to `OmnixSI/NextThought`).
- Inno Setup 7.1.1 is downloaded and verified by the repository; do not install it by hand.
- Nothing here needs the Windows SDK, a code-signing certificate or administrator rights: the
  installer is per-user and unsigned in this release.

Prepare the tools (they are installed into `Windows/.tools`, which is not in git):

```powershell
cd Windows
py -3.12 scripts/bootstrap_sdk.py        # pinned .NET SDK into Windows/.tools/dotnet
./scripts/bootstrap_installer.ps1        # Inno Setup 7.1.1 (SHA-256 and publisher checked)
```

## 2. Build the release

Start from a clean checkout of `windows/parity` (no modified files). The script enforces the
branch, the clean tree, the version match with `src/NextThought.Desktop/NextThought.Desktop.csproj`
and returns a non-zero exit code on the first failure.

```powershell
cd Windows
./release.ps1 -Version 0.9.0
```

What it does, in order:

1. Checks the branch, the clean working tree and that `-Version` equals the version in
   `NextThought.Desktop.csproj` (the only place the version is defined).
2. Runs `verify.ps1` (both test suites, the UTC-timezone Core pass, the Mac fixture codec/revision
   tests and the screenshot batch).
3. Publishes `NextThought.exe` self-contained for x64 with ReadyToRun.
4. Compiles the **unsigned** per-user installer `NextThought-0.9.0-windows-x64-setup.exe`
   (`AppId=NextThought.Desktop`, installs to `%LOCALAPPDATA%\Programs\NextThought`).
5. Writes `SHA256SUMS.txt` and `update.json`.
6. Runs the installer lifecycle test against the build (silent install, `--self-test=startup`,
   overwrite install, uninstall that keeps user data, reinstall; the in-app update path is covered
   by the update unit tests and the real-machine K10 run).
7. Copies everything into `Windows/artifacts/release/0.9.0/` and prints each file's size and
   SHA-256.
8. Prints the publish command (section 3) but does not run it.

Expected time on a warm machine: 5–10 minutes (the first run downloads the SDK, Inno Setup and the
NuGet packages). `artifacts/` is ignored by git; do not commit it.

The release directory must contain exactly these four files:

| File | Content |
| --- | --- |
| `NextThought-0.9.0-windows-x64-setup.exe` | the unsigned per-user installer |
| `SHA256SUMS.txt` | installer SHA-256 |
| `update.json` | tag `windows-v0.9.0`, version, build number, installer file name, SHA-256, commit, publish time |
| `RELEASE_NOTES.md` | the release notes (Chinese first, English after) |

The build number is `git rev-list --count HEAD` on `windows/parity` at build time. It never
decreases on the branch, so the updater can tell two builds of the same version apart.

For CI (or if you already ran `verify.ps1 -SkipScreenshots` yourself) use
`./release.ps1 -Version 0.9.0 -Ci`: it skips the clean-tree/branch checks, the screenshot step and
the installer lifecycle tests, which are all already covered by the CI job.

## 3. Publish

Only after the script printed `RELEASE OK` and the four files exist:

```powershell
gh release create windows-v0.9.0 --target windows/parity --title "NextThought for Windows 0.9.0" --notes-file Windows/artifacts/release/0.9.0/RELEASE_NOTES.md --latest=false Windows/artifacts/release/0.9.0/NextThought-0.9.0-windows-x64-setup.exe Windows/artifacts/release/0.9.0/SHA256SUMS.txt Windows/artifacts/release/0.9.0/update.json
```

`--latest=false` is mandatory: if the Windows release were marked latest, the repository's
"download the latest version" link and the Mac in-app updater would point at a Windows-only
release.

## 4. Check after publishing

1. Open the release page and download the installer again; compare its SHA-256 with
   `SHA256SUMS.txt` (`Get-FileHash <file> -Algorithm SHA256`).
2. Install it as a normal user (no administrator prompt), launch the app and check
   Settings → 通用 → 更新 shows 0.9.0.
3. Open the Mac app's "check for updates": it must not see `windows-v0.9.0` (the Mac only accepts
   releases with a `-macOS-arm64.zip` asset and a `v*` tag).
4. Open <https://github.com/OmnixSI/NextThought/releases/latest> and confirm it still resolves to
   the Mac release.
5. Run the human checks in section 6 (K13–K16) and record the results.

If anything is wrong, delete the release (`gh release delete windows-v0.9.0 --yes`), fix, bump the
build number by adding a commit and publish again under the same tag only if the assets were never
downloaded; otherwise publish `windows-v0.9.0` with a new build number only after the release notes
explain it.

## 5. The next version

1. Change `<Version>` in `src/NextThought.Desktop/NextThought.Desktop.csproj` (for example
   `0.9.1`). Do not edit the version anywhere else: the installer, `update.json`, the about page and
   the asset names all read it.
2. `update.json`'s `build` is the commit count on the branch, so any commit after the last release
   makes the same version newer. Do not reuse a published version for a different build.
3. Commit on `windows/parity`, run `./release.ps1 -Version 0.9.1`, then publish with the tag
   `windows-v0.9.1` and `--latest=false`.
4. Update `Windows/RELEASE_NOTES.md`, the "not yet verified" list below and
   `Windows/validation/models.md` when the downloaded model set changes.

## 6. Checks that need a person (K13–K16)

These are not release blockers but must be reported in the release notes as "not yet verified on a
real setup" until they have been run.

### K13 — can meeting participants see the app's windows?

1. Join a Zoom/Teams/腾讯会议 call from this PC and from a second device.
2. Share the whole screen from this PC.
3. Start a meeting in NextThought and open the cue island and the subtitle bar.
4. On the second device, confirm whether the cue island / subtitle bar are visible (they should be
   hidden when "尽力从屏幕共享中排除本应用窗口" is on and the OS allows exclusion) and that the
   answer window is visible only when you open it.
5. Record the result per window; if the meeting app cannot exclude our windows, note it as a
   limitation (the in-app 共享可见性测试 window shows what other participants can see).

### K14 — real phone over LAN and relay

1. Make sure the phone and the PC are on the same Wi-Fi, open Settings → 高级 → 手机伴侣 and start
   the companion; note the address.
2. Scan the QR code with the phone and open the pairing link.
3. Ask a question from the live page, confirm it appears on the phone, answer it, and confirm the
   answer and the item number match the PC.
4. Send the same action id twice and confirm the PC only acts once.
5. Repeat through the relay URL (if configured) with the phone on mobile data.
6. If no second network is available, record the result as "LAN only / relay not verified".

### K15 — multiple screens and different scale factors

1. Connect a second display; set it to 100%, 125% or 150% scaling.
2. In Settings → 提词与教练 choose the cue island display; open the island, the subtitle bar and
   the answer window on the second screen.
3. Confirm nothing is clipped, the island stays above other windows and the window positions are
   remembered after restarting the app.
4. Repeat with the second display unplugged/re-plugged during a meeting.

### K16 — real cloud services

1. Configure a real OpenAI-compatible answer service (API key + address + model) and run one
   question end to end.
2. Enable web search with a real Tavily key and ask a question that needs current information;
   confirm the 联网 answer replaces the fast answer.
3. Configure each of the three cloud speech-recognition providers that have a key and confirm the
   permission notice and the audio upload wording are shown before recording.
4. Record which provider/account was used, and any provider-specific differences.

## 7. Suggested paragraph for the repository README

> **Windows (preview, 0.9.0):** download `NextThought-0.9.0-windows-x64-setup.exe` from the
> `windows-v0.9.0` release. It is an unsigned, per-user build for Windows 10 2004+ (x64): Windows
> SmartScreen will show a warning — choose "More info" → "Run anyway". The installer puts the app
> in `%LOCALAPPDATA%\Programs\NextThought`; on first run it downloads the speech-recognition models
> (about 0.3–0.7 GB). Meetings, settings and models live in `%LOCALAPPDATA%\NextThought` and are not
> removed by uninstalling. See `Windows/README.md` for details and `Windows/RELEASE.md` for the
> release process.

## 8. Troubleshooting

- **Locked restore / NU1004**: the lock files are per project; run
  `dotnet restore Windows/NextThought.Windows.sln --locked-mode` from `Windows` and commit the
  regenerated `packages.lock.json` if a package reference changed.
- **A running app blocks the build**: close every `NextThought` process (including the cue island
  and subtitle windows) before running the script.
- **Inno Setup download fails**: the script verifies the SHA-256 and publisher; retry, or download
  the official `innosetup-7.1.1-x64.exe` and place it under `Windows/.tools` as
  `innosetup-7.1.1-x64.exe`.
- **Old preview installed**: the installer detects the old `NextThought.Windows.Preview` identity
  and asks you to uninstall it first; the user data directory is kept.

## 9. 本机实测（阶段 7，K1–K16）

K1 已由 Agent 在本机实测通过，证据在 `Windows/validation/realmachine/K1/`（`report.json` +
开/关两轮的 GDI 与 Windows.Graphics.Capture 截图）。复现：

```powershell
Windows/.tools/dotnet/dotnet.exe build Windows/src/NextThought.Desktop -c Release --artifacts-path %TEMP%\nt-home-artifacts
Windows/.tools/dotnet/dotnet.exe build Windows/tools/realmachine/WgcCapture -c Release --artifacts-path %TEMP%\nt-wgc-artifacts
Set-ExecutionPolicy -Scope Process Bypass -Force
Windows/scripts/realmachine/k1-capture-hide.ps1 -Exe %TEMP%\nt-home-artifacts\bin\NextThought.Desktop\release_win-x64\NextThought.exe
```

正式包的启动参数只有 `--demo`、`--screenshots`、`--self-test`（任务书 §2.5）。演示/走查用的两个
Mac 同名开关是**环境变量**，不占用命令行：`NEXTTHOUGHT_DEMO_VOICES=1`（线下多人演示会议）、
`NEXTTHOUGHT_DEMO_CHURN=<file>`（`--demo` 走查时把会中页渲染计数写进文件，配合
`--self-test=live-churn` 的自检版）。

其余各项的实机操作步骤（结果记到 `Windows/validation/realmachine/<编号>/`；标「人」的项不阻塞发布，
但发布说明的「尚未实机验证」要列出）：

### K2 不抢焦点（人）
1. 打开记事本，点一下正文让它在前台。
2. 打开 NextThought（演示模式或真实开会都行），让提词岛出现；依次用鼠标点击提词岛上的每个按钮
   （开始、暂停、下一条、⋯ 菜单里的各项）。
3. 每次点击后立刻看任务栏：**前台窗口应仍是记事本**（提词岛不激活、不抢输入焦点）。
4. 不点记事本正文，直接在这台电脑上打字：文字应进入记事本（说明焦点没被提词岛拿走）。
5. 记下设备、点击的按钮、前台窗口截图。

### K3 鼠标穿透（人）
1. 打开记事本最大化（或放在提词岛下方的任意可编辑窗口）。
2. 让提词岛处于**收起**状态，把鼠标放在岛的区域内点击并拖选：下面的记事本应收到点击/选中（穿透）。
3. 点一下岛展开；再点岛上的按钮：按钮动作应触发（不穿透）。
4. 记录两种状态下点击落点与结果。

### K4 关闭与隐藏（人）
1. 开会中让提词岛显示，按 Alt+F4 关掉岛：程序**不应退出**。
2. 然后提一个问题（或点「回答」）：岛应重新出现。
3. 切到字幕模式，点「隐藏所有窗口」后继续播放/说话：字幕条应保持隐藏。
4. 在穿透状态下点「隐藏所有窗口」：屏幕上不应留任何 NextThought 窗口（含岛/字幕/提词器）。
5. 记录每步前后窗口列表（用任务管理器/Alt+Tab 截图）。

### K5 设置的保存（人）
1. 设置里改一个快捷键、把提词浮窗透明度拖到 0.5、收起上下文侧栏、用 Ctrl+= 放大文字。
2. 开始并结束一场会议（确认会议正常结束）。
3. 退出程序后重新打开：四项都应保留（快捷键生效、透明度 0.5、侧栏仍收起、文字仍是放大档）。
4. 记录改前/改后/重启后的截图与 `%LOCALAPPDATA%\NextThought\settings.json` 的相关键。

### K6 采音和识别（人）
1. 准备一段 60 秒、内容已知的中文语音文件（例如 10 句明确的话），用默认扬声器播放。
2. NextThought 新建会议、开始录音，让语音完整播完（识别走系统声音/默认扬声器回路）。
3. 停止录音后检查转写：应出现语音里的预期句子（至少能对上 8/10 句）。
4. 再说最后一句「这是最后一句测试」并立刻停止录音：这句应仍在转写里。
5. 记录语音文件、识别模型、转写截图。

### K7 长会议（人）
1. 循环播放 K6 的语音文件，连续录 2 小时；每 10 分钟记录一次任务管理器里 NextThought 的内存
   （工作集）和转写条数。
2. 断言：转写持续增长不中断；内存不呈持续上涨趋势（末尾与中段相比无单调上升）；结束后会议列表能
   正常打开该会议、纪要页能打开。
3. 配置了回答服务时，同时确认会中回答与会后纪要有生成；没有配置就写「纪要生成未验证」。
4. 记录每分钟内存表与最终转写条数。

### K8 手机页面（人）
1. 让手机与电脑在同一局域网，打开设置里的手机配对，得到配对链接/二维码。
2. 用手机（或电脑上的无头浏览器）打开配对链接。
3. 对照电脑与手机：当前问题、当前回答、会议状态、第几条（序号）应一致。
4. 在手机上对同一个「回答/动作」按钮快速点两次（同一动作编号发两次）：电脑只应执行一次。
5. 记录两端截图与动作日志。

### K11 干净系统（人，尽量）
1. 若本机有 Windows 沙盒：启用「Windows 沙盒」（需专业版/企业版），在沙盒里运行安装包。
2. 走完首次引导：标题应为衬线体（Source Serif 4），中文应为思源黑体，不能出现方框/回退字体。
3. 记录沙盒版本、安装包 SHA-256、引导截图；没有沙盒就写「本机无 Windows 沙盒，未验证」。

### K12 音频设备切换/拔出/睡眠（人，尽量）
1. 本机有两个以上音频输出设备时：会议中在设置里切换默认输出设备，转写应继续（或按提示处理）。
2. 拔出耳机/麦克风：录音应停止或提示，不应崩溃；重新插回后应能继续。
3. 合盖睡眠 1 分钟后唤醒：会议不应卡死；没有音频输入时应给出提示。
4. 记录设备列表、切换前后转写截图。

### K13 会议软件里对方是否看得到窗口（人）
1. 第二台设备加入 Zoom / Teams / 腾讯会议。
2. 共享屏幕并打开 NextThought：勾选/不勾选「共享时隐藏窗口」两种情况各看一次，对方画面里
   NextThought 窗口是否出现（勾选时应看不到，未勾选时应看到）。
3. 记录两端截图与会议软件版本。

### K14 真实手机：局域网和中转（人）
1. 手机与电脑同一局域网，扫码配对，确认页面能加载、问题/回答同步。
2. 手机关掉 Wi-Fi 用蜂窝网络（中转）：确认中转配对可连、延迟可接受、断线后能重连。
3. 记录网络拓扑、截图、配对码。

### K15 多屏和不同缩放比例（人）
1. 接第二块屏幕，分别在 100%/125%/150% 缩放下打开 NextThought。
2. 检查：主窗口、提词岛、字幕条、提词浮窗都出现在正确屏幕、大小正确；防截屏在两个屏幕都生效。
3. 把主窗口拖到另一个屏幕，确认提词岛跟随/不跟随的行为与 Mac 版一致。
4. 记录每个屏幕分辨率/缩放与截图。

### K16 真实云服务与账号（人）
1. 在设置里配置真实的识别服务（云）、联网搜索与回答服务账号。
2. 各开一场短会验证：云端转写、联网搜索引用、真实回答都能工作，界面显示正确。
3. 记录服务商、账号类型、界面截图、请求出错时的提示。

