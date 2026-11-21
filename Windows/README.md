# NextThought for Windows

NextThought is a local-first meeting assistant: it listens to a meeting on your PC, keeps a live
transcript, answers questions about what was just said, and writes the notes afterwards. This
directory contains the Windows port of NextThought for Mac 3.4.0 (Avalonia/.NET 10). The Mac app is
the reference; this port follows its wording, settings and meeting formats.

**Status:** 0.9.0, unsigned preview build for Windows 10/11. Everything stays on your machine except
the calls you explicitly configure (answer service, web search, cloud recognition).

## System requirements

- Windows 10 version 2004 (build 19041) or later, Windows 11, **x64 only**.
- No administrator rights: the app is per-user.
- A microphone (or system audio capture) for recording; speakers are optional.
- About 1 GB for the app and its first-run models; more if you import many documents.

## Install

1. Download `NextThought-0.9.0-windows-x64-setup.exe` from the `windows-v0.9.0` release.
2. Run it. The build is **not code-signed**, so Windows SmartScreen shows a warning: choose
   **更多信息 / More info** and then **仍要运行 / Run anyway**.
3. The installer copies the app to `%LOCALAPPDATA%\Programs\NextThought` and creates Start-menu and
   (optionally) desktop shortcuts. It installs for the current user only; it never asks for
   administrator rights.
4. Launch **NextThought** and follow the first-run guide. Step 1 offers to download the speech
   recognition model.

If an old "NextThought Windows Preview" build (`NextThought.Windows.Preview`) is installed, the
installer stops and asks you to uninstall it first. Your data is kept — uninstalling the old build
only removes that build's program files.

## First run: what gets downloaded

Models are downloaded separately and verified against pinned SHA-256 values
(`Windows/validation/models.md`); nothing is bundled in the installer.

- **Speech recognition (required for recording)**: one X-ASR streaming model for Chinese/English,
  about 130 MB download (about 170 MB on disk). The setup checklist ("设置清单") shows the state and a
  "下载" button.
- **Semantic search (needed on the first document import)**: Qwen3-Embedding 0.6B (ONNX, quantised),
  about 625 MB, downloaded when you first add documents; the 资料组件 page shows progress.
- **Voiceprint (optional, 区分说话人)**: WeSpeaker ResNet34-LM, about 27 MB, downloaded when you
  turn the feature on.
- **Gaze reminder (optional, off by default)**: the MediaPipe face-landmarker models, about 4 MB.
- **Document components (optional)**: Office/PDF enhanced reading needs an optional component
  package (200 MB or more), installed from the 资料组件 page only if you need Word/Excel/PowerPoint
  import.

Everything is downloaded to `%LOCALAPPDATA%\NextThought`; a failed or interrupted download is
retried from the settings page and is checked before use.

## Where your data lives

- **Data directory:** `%LOCALAPPDATA%\NextThought` (`Sessions`, `models`, `settings.json`,
  `Credentials\answer-api.dpapi`). The settings page can open it directly.
- **Meetings, notes, series, templates, settings and voiceprints** stay there. The app never uploads
  them; the settings page states exactly which services receive text when you configure one (answer
  service, web search, cloud recognition).
- **Backup / move to the Mac:** Settings → 通用 → 备份与恢复 → "导出全部数据…" writes a
  `.ntbackup` file that the Mac app can import (and the Windows app can import a Mac backup). The
  file contains meetings, series, settings, skills, templates and voiceprints, but **not** the
  downloaded models (they are re-downloaded per machine). Answer-service keys are never written to
  the backup.
- **Import:** "导入…" restores a `.ntbackup` into this machine; the review sheet shows new, same and
  conflicting records before anything is written, and a local recovery point is created first.
- **Uninstall:** Windows Settings → Apps (or the Start-menu uninstaller) removes
  `%LOCALAPPDATA%\Programs\NextThought` and the shortcuts. It deliberately **keeps**
  `%LOCALAPPDATA%\NextThought`, so reinstalling restores your meetings and settings. Delete that
  folder by hand if you want to remove everything.

## What is different from the Mac app

NextThought for Windows intentionally differs in these areas only; the rest follows the Mac 3.4.0
behaviour and wording, in both Chinese and English:

- The app window is a normal Windows window (title bar, taskbar, system close/maximize buttons); the
  Mac's "刘海" (notch) presentation uses its no-notch layout.
- Speech recognition runs on-device with X-ASR instead of Apple's speech frameworks; there is no
  system speech-recognition option.
- Translation goes through your configured answer service and the UI says the text leaves the
  machine; there is no on-device offline translator.
- The Mac's screen-recording permission guide does not exist (Windows has no equivalent permission);
  the Windows permissions (microphone, camera) are shown in Settings → 通用 → 权限.
- Legacy Office formats (`.doc`, `.odt`, `.rtf`) are not read directly; save as `.docx`/`.pdf` first
  (Legacy PowerPoint support through LibreOffice is an optional component).
- The Bluetooth fallback channel is not implemented (LAN/relay companion only).
- The update channel selector is hidden: this release only has the formal `windows-v<version>`
  channel.
- Application name in files, "close the tab of the system window" and "click-through first click"
  are Mac-only concepts and are adapted to Windows conventions.
- Answer-service keys are protected with the Windows user protection API (DPAPI); the Mac uses the
  Keychain.

## Settings, shortcuts and help

- **Settings:** title-bar gear (quick settings) or the main window's 设置; six pages mirror the Mac
  app: 场景 / 回答与资料 / 提词与教练 / 纪要 / 通用 / 高级.
- **Setup checklist:** Settings → 通用 → 设置清单 shows permissions, models, answer service and
  document components with a direct action per row.
- **Shortcuts:** Settings → 提词与教练 → 全局快捷键; conflicts are reported there and the buttons
  stay usable.
- **About/version:** Settings → 通用 → 数据与关于; the title bar does not show a version badge.
- **Logs:** `%LOCALAPPDATA%\NextThought\logs` (secrets, tokens and pairing codes are filtered).

## Development and release

The Windows port lives entirely under `Windows/`; do not edit `Sources/` (the Mac app) for Windows
changes.

- Build and run: `Windows/scripts/build_desktop.ps1` (or `dotnet build
  Windows/src/NextThought.Desktop`); the repository pins its SDK under `Windows/.tools`.
- Tests: `Windows/verify.ps1` runs both suites, the UTC pass, the Mac fixture checks and the
  screenshot batch; `Windows/release.ps1 -Version 0.9.0` produces the release artifacts.
- Release process (for maintainers): `Windows/RELEASE.md`.
- Third-party licences and the upstream snapshot: `Windows/THIRD_PARTY_NOTICES.txt`,
  `Windows/licenses/`.

The upstream Mac application has no top-level open-source licence; this Windows port does not change
its licensing. Third-party components and models keep their own licences as listed in the notices.
