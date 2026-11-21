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
6. Runs the installer lifecycle and in-app update tests against the build.
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
