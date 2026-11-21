# Mac 截图（浅色 / 深色）

逐元素对照用的 Mac 标准图。2026-11-21 在 Mac14,10（M2 Pro）、macOS 26.5.2 上，从 NextThought 3.4.0 build 3400（main f015e8c6）的演示模式渲染：示例数据，不录音、不联网、不调模型。界面语言简体中文，窗口 1120×820 pt，最宽 1600 px。

渲染方式：开发预览副本（独立 bundle id）隐藏启动、从不激活，应用把自己的窗口画进 PNG（`NEXTTHOUGHT_DEMO_SNAPSHOT`、`NEXTTHOUGHT_DEMO_SETTINGS_SNAPSHOT`、`NEXTTHOUGHT_DEMO_QUICK_SETTINGS`、`NEXTTHOUGHT_DEMO_ISLAND`），不需要录屏权限，也不在任何显示器上出现。

每张图都有 `-light` 和 `-dark` 两版。

| 目录 | 图 | Mac 视图 | 演示参数 |
|---|---|---|---|
| `home/` | home | HomeView 首页 | `SECTION=home` |
| `prepare/` | prepare | PrepareView 准备页，含 3.4.0「这场用什么」 | `SECTION=prepare` |
| `live/` | live | LiveSessionView 会中（问答标签） | `SECTION=live` |
| `live/` | live-notes | 会中右栏「实时纪要」（3.4.0） | `SECTION=live-notes`，`LIVE_NOTES=1` |
| `notes/` | review | NotesPageView 回顾纪要 | `SECTION=meetings` |
| `notes/` | work | 工作会议纪要（决定、待办·谁·哪天交付） | `DEFAULTS=demoNotes=work` |
| `notes/` | transcript | 会后转写标签 | `MEETING_TAB=transcript` |
| `series/` | series | WorkspacesView 系列页 | `SECTION=library` |
| `settings/` | window | 主窗口里的设置（通用页） | `SECTION=settings` |
| `settings/` | quick-settings | 齿轮弹出的快速设置 | `QUICK_SETTINGS=<png>` |
| `settings-pages/` | scenes / answers / presenter / notes / general / advanced | SettingsPageView 六页全高：场景、回答与资料、提词与教练、纪要、通用、高级 | `SETTINGS_PAGE=<页名>`，980 pt 宽 |
| `onboarding/` | step-1 … step-4 | 四步引导：场合、声音、回答服务、提词与开始 | `ONBOARDING=1`，`quickStartStep=0…3` |
| `island/` | cue / hearing / deck / launcher / pinned / autoend | CueIslandView：参考回答、对方在说、会中面板、无会议时的启动器、钉住展开（双语）、自动结束提醒 | `ISLAND_PHASE=<phase>` |
| `live-states/` | 20 种状态 | 会中页状态矩阵（1200 pt）：normal、streaming、busy、focus、success、save-warning、save-recovery、unavailable、stale、empty、reading-history、selection、large-text、overlay 和 overlay-popover 等，以及 component-samples（仅浅色） | `--self-test live-session-ui` |

术语表、技能、声纹、同声传译、联网搜索、资料处理、备份等设置分区都在 `settings-pages/` 的全高页里：术语和人名、声纹、备份在「通用」，技能、资料、联网搜索在「回答与资料」，同声传译和字幕在「提词与教练」。

没有收录：提词岛的 compact、open、slide 三个状态。演示钩子在会议进行中渲染时会被「对方在说」盖住，会议空闲时画不出内容，留到修好钩子再补；slide 的现有参考是 `docs/screenshots/v3/island-slide.png`。英文界面这次没有拍。
