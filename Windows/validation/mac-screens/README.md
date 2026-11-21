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

<!-- mac-win-index:start -->
<!-- 由 Windows/scripts/build_mac_screens_index.py 生成，勿手改此区间。 -->

## Windows 对照索引

Mac 参考图共 **93** 张（26 组视图 × 浅/深）；Windows 最新截图批次 `validation/screens/current/` 共 **95** 张（本文件不包含 PNG，Windows 截图目录不入库）。下表把每张 Mac 图对到 Windows 截图与逐元素对照文档。

| Mac 参考图（浅 / 深） | Mac 视图 | Windows 截图（`validation/screens/current/`） | 对照文档 | 说明 |
|---|---|---|---|---|
| [home-light.png](../mac-screens/home/home-light.png)<br>[home-dark.png](../mac-screens/home/home-dark.png) | HomeView 首页 | [home-light.png](../screens/current/home-light.png)<br>[home-dark.png](../screens/current/home-dark.png) | [home/compare.md](../screens/home/compare.md) |  |
| [prepare-light.png](../mac-screens/prepare/prepare-light.png)<br>[prepare-dark.png](../mac-screens/prepare/prepare-dark.png) | PrepareView 准备页 | [prepare-light.png](../screens/current/prepare-light.png)<br>[prepare-dark.png](../screens/current/prepare-dark.png) | [prepare/compare.md](../screens/prepare/compare.md) |  |
| [live-light.png](../mac-screens/live/live-light.png)<br>[live-dark.png](../mac-screens/live/live-dark.png) | LiveSessionView 会中（问答标签） | [live-light.png](../screens/current/live-light.png)<br>[live-dark.png](../screens/current/live-dark.png) | [live/compare.md](../screens/live/compare.md) |  |
| [live-notes-light.png](../mac-screens/live/live-notes-light.png)<br>[live-notes-dark.png](../mac-screens/live/live-notes-dark.png) | 会中右栏实时纪要 | — | [live/compare.md](../screens/live/compare.md) | Windows 会中页没有「实时纪要」右栏（E11 之后的独立视图未落地） |
| [review-light.png](../mac-screens/notes/review-light.png)<br>[review-dark.png](../mac-screens/notes/review-dark.png) | NotesPageView 回顾纪要 | — | [notes/compare.md](../screens/notes/compare.md) | Windows 刷新批次只截了纪要/转写/回答/个人笔记四个标签，回顾页未截 |
| [work-light.png](../mac-screens/notes/work-light.png)<br>[work-dark.png](../mac-screens/notes/work-dark.png) | 工作会议纪要 | — | [notes/compare.md](../screens/notes/compare.md) | 同上：工作会议纪要模板页未截 |
| [transcript-light.png](../mac-screens/notes/transcript-light.png)<br>[transcript-dark.png](../mac-screens/notes/transcript-dark.png) | 会后转写标签 | [transcript-light.png](../screens/current/transcript-light.png)<br>[transcript-dark.png](../screens/current/transcript-dark.png) | [notes/compare.md](../screens/notes/compare.md) |  |
| [series-light.png](../mac-screens/series/series-light.png)<br>[series-dark.png](../mac-screens/series/series-dark.png) | WorkspacesView 系列页 | [series-light.png](../screens/current/series-light.png)<br>[series-dark.png](../screens/current/series-dark.png)<br>[library-light.png](../screens/current/library-light.png)<br>[library-dark.png](../screens/current/library-dark.png) | [series/compare.md](../screens/series/compare.md) | Windows 还多一个仅 Windows 的「资料库」页（导入/解析资料，Mac 无直接对应）；Mac 系列页的对应主体是 series-* |
| [window-light.png](../mac-screens/settings/window-light.png)<br>[window-dark.png](../mac-screens/settings/window-dark.png) | 主窗口里的设置（通用页） | [settings-light.png](../screens/current/settings-light.png)<br>[settings-dark.png](../screens/current/settings-dark.png) | [settings-structure/compare.md](../screens/settings-structure/compare.md) |  |
| [quick-settings-light.png](../mac-screens/settings/quick-settings-light.png)<br>[quick-settings-dark.png](../mac-screens/settings/quick-settings-dark.png) | 齿轮弹出的快速设置 | [quick-settings-light.png](../screens/current/quick-settings-light.png)<br>[quick-settings-dark.png](../screens/current/quick-settings-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [scenes-light.png](../mac-screens/settings-pages/scenes-light.png)<br>[scenes-dark.png](../mac-screens/settings-pages/scenes-dark.png) | 设置 · 场景 | [settings-scene-light.png](../screens/current/settings-scene-light.png)<br>[settings-scene-dark.png](../screens/current/settings-scene-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [answers-light.png](../mac-screens/settings-pages/answers-light.png)<br>[answers-dark.png](../mac-screens/settings-pages/answers-dark.png) | 设置 · 回答与资料 | [settings-answer-light.png](../screens/current/settings-answer-light.png)<br>[settings-answer-dark.png](../screens/current/settings-answer-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [presenter-light.png](../mac-screens/settings-pages/presenter-light.png)<br>[presenter-dark.png](../mac-screens/settings-pages/presenter-dark.png) | 设置 · 提词与教练 | [settings-coach-light.png](../screens/current/settings-coach-light.png)<br>[settings-coach-dark.png](../screens/current/settings-coach-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [notes-light.png](../mac-screens/settings-pages/notes-light.png)<br>[notes-dark.png](../mac-screens/settings-pages/notes-dark.png) | 设置 · 纪要 | [settings-notes-light.png](../screens/current/settings-notes-light.png)<br>[settings-notes-dark.png](../screens/current/settings-notes-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [general-light.png](../mac-screens/settings-pages/general-light.png)<br>[general-dark.png](../mac-screens/settings-pages/general-dark.png) | 设置 · 通用 | [settings-general-light.png](../screens/current/settings-general-light.png)<br>[settings-general-dark.png](../screens/current/settings-general-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [advanced-light.png](../mac-screens/settings-pages/advanced-light.png)<br>[advanced-dark.png](../mac-screens/settings-pages/advanced-dark.png) | 设置 · 高级 | [settings-advanced-light.png](../screens/current/settings-advanced-light.png)<br>[settings-advanced-dark.png](../screens/current/settings-advanced-dark.png) | [settings-pages/compare.md](../screens/settings-pages/compare.md) |  |
| [step-1-light.png](../mac-screens/onboarding/step-1-light.png)<br>[step-1-dark.png](../mac-screens/onboarding/step-1-dark.png) | 引导 · 第一步 场合 | [onboarding-step0-light.png](../screens/current/onboarding-step0-light.png)<br>[onboarding-step0-dark.png](../screens/current/onboarding-step0-dark.png) | [onboarding/compare.md](../screens/onboarding/compare.md) |  |
| [step-2-light.png](../mac-screens/onboarding/step-2-light.png)<br>[step-2-dark.png](../mac-screens/onboarding/step-2-dark.png) | 引导 · 第二步 声音 | [onboarding-step1-light.png](../screens/current/onboarding-step1-light.png)<br>[onboarding-step1-dark.png](../screens/current/onboarding-step1-dark.png) | [onboarding/compare.md](../screens/onboarding/compare.md) |  |
| [step-3-light.png](../mac-screens/onboarding/step-3-light.png)<br>[step-3-dark.png](../mac-screens/onboarding/step-3-dark.png) | 引导 · 第三步 回答服务 | [onboarding-step2-light.png](../screens/current/onboarding-step2-light.png)<br>[onboarding-step2-dark.png](../screens/current/onboarding-step2-dark.png) | [onboarding/compare.md](../screens/onboarding/compare.md) |  |
| [step-4-light.png](../mac-screens/onboarding/step-4-light.png)<br>[step-4-dark.png](../mac-screens/onboarding/step-4-dark.png) | 引导 · 第四步 提词与开始 | [onboarding-step3-light.png](../screens/current/onboarding-step3-light.png)<br>[onboarding-step3-dark.png](../screens/current/onboarding-step3-dark.png) | [onboarding/compare.md](../screens/onboarding/compare.md) |  |
| [cue-light.png](../mac-screens/island/cue-light.png)<br>[cue-dark.png](../mac-screens/island/cue-dark.png) | 提词岛 · 参考回答 | [island-cue.png](../screens/current/island-cue.png) | [island/compare.md](../screens/island/compare.md) |  |
| [hearing-light.png](../mac-screens/island/hearing-light.png)<br>[hearing-dark.png](../mac-screens/island/hearing-dark.png) | 提词岛 · 对方在说 | [island-hearing.png](../screens/current/island-hearing.png) | [island/compare.md](../screens/island/compare.md) |  |
| [deck-light.png](../mac-screens/island/deck-light.png)<br>[deck-dark.png](../mac-screens/island/deck-dark.png) | 提词岛 · 会中面板 | [island-deck.png](../screens/current/island-deck.png) | [island/compare.md](../screens/island/compare.md) |  |
| [launcher-light.png](../mac-screens/island/launcher-light.png)<br>[launcher-dark.png](../mac-screens/island/launcher-dark.png) | 提词岛 · 无会议启动器 | [island-launcher.png](../screens/current/island-launcher.png) | [island/compare.md](../screens/island/compare.md) |  |
| [pinned-light.png](../mac-screens/island/pinned-light.png)<br>[pinned-dark.png](../mac-screens/island/pinned-dark.png) | 提词岛 · 钉住展开（双语） | — | [island/compare.md](../screens/island/compare.md) | Windows 提词岛没有「钉住展开（双语）」态 |
| [autoend-light.png](../mac-screens/island/autoend-light.png)<br>[autoend-dark.png](../mac-screens/island/autoend-dark.png) | 提词岛 · 自动结束提醒 | — | [island/compare.md](../screens/island/compare.md) | Windows 提词岛没有「自动结束提醒」态 |

**会中状态矩阵（live-states，Mac 20 种状态 × 浅/深）：**

| [busy-dark.png](../mac-screens/live-states/busy-dark.png) | 会中状态 · busy | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [busy-light.png](../mac-screens/live-states/busy-light.png) | 会中状态 · busy | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [cancel-late-callback-dark.png](../mac-screens/live-states/cancel-late-callback-dark.png) | 会中状态 · cancel-late-callback | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [cancel-late-callback-light.png](../mac-screens/live-states/cancel-late-callback-light.png) | 会中状态 · cancel-late-callback | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [component-samples-light.png](../mac-screens/live-states/component-samples-light.png) | 会中状态 · component-samples | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [empty-dark.png](../mac-screens/live-states/empty-dark.png) | 会中状态 · empty | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [empty-light.png](../mac-screens/live-states/empty-light.png) | 会中状态 · empty | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [focus-busy-dark.png](../mac-screens/live-states/focus-busy-dark.png) | 会中状态 · focus-busy | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [focus-busy-light.png](../mac-screens/live-states/focus-busy-light.png) | 会中状态 · focus-busy | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [focus-dark.png](../mac-screens/live-states/focus-dark.png) | 会中状态 · focus | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [focus-light.png](../mac-screens/live-states/focus-light.png) | 会中状态 · focus | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [large-text-dark.png](../mac-screens/live-states/large-text-dark.png) | 会中状态 · large-text | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [large-text-light.png](../mac-screens/live-states/large-text-light.png) | 会中状态 · large-text | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [normal-dark.png](../mac-screens/live-states/normal-dark.png) | 会中状态 · normal | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [normal-light.png](../mac-screens/live-states/normal-light.png) | 会中状态 · normal | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [overlay-dark.png](../mac-screens/live-states/overlay-dark.png) | 会中状态 · overlay | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [overlay-light.png](../mac-screens/live-states/overlay-light.png) | 会中状态 · overlay | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [overlay-popover-dark.png](../mac-screens/live-states/overlay-popover-dark.png) | 会中状态 · overlay-popover | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [overlay-popover-light.png](../mac-screens/live-states/overlay-popover-light.png) | 会中状态 · overlay-popover | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [reading-history-dark.png](../mac-screens/live-states/reading-history-dark.png) | 会中状态 · reading-history | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [reading-history-light.png](../mac-screens/live-states/reading-history-light.png) | 会中状态 · reading-history | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [save-recovery-dark.png](../mac-screens/live-states/save-recovery-dark.png) | 会中状态 · save-recovery | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [save-recovery-light.png](../mac-screens/live-states/save-recovery-light.png) | 会中状态 · save-recovery | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [save-warning-dark.png](../mac-screens/live-states/save-warning-dark.png) | 会中状态 · save-warning | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [save-warning-light.png](../mac-screens/live-states/save-warning-light.png) | 会中状态 · save-warning | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [selected-focus-dark.png](../mac-screens/live-states/selected-focus-dark.png) | 会中状态 · selected-focus | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [selected-focus-light.png](../mac-screens/live-states/selected-focus-light.png) | 会中状态 · selected-focus | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [selection-dark.png](../mac-screens/live-states/selection-dark.png) | 会中状态 · selection | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [selection-light.png](../mac-screens/live-states/selection-light.png) | 会中状态 · selection | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [stale-dark.png](../mac-screens/live-states/stale-dark.png) | 会中状态 · stale | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [stale-light.png](../mac-screens/live-states/stale-light.png) | 会中状态 · stale | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [stale-manual-edit-dark.png](../mac-screens/live-states/stale-manual-edit-dark.png) | 会中状态 · stale-manual-edit | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [stale-manual-edit-light.png](../mac-screens/live-states/stale-manual-edit-light.png) | 会中状态 · stale-manual-edit | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [streaming-dark.png](../mac-screens/live-states/streaming-dark.png) | 会中状态 · streaming | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [streaming-light.png](../mac-screens/live-states/streaming-light.png) | 会中状态 · streaming | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [streaming-reading-history-dark.png](../mac-screens/live-states/streaming-reading-history-dark.png) | 会中状态 · streaming-reading-history | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [streaming-reading-history-light.png](../mac-screens/live-states/streaming-reading-history-light.png) | 会中状态 · streaming-reading-history | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [success-dark.png](../mac-screens/live-states/success-dark.png) | 会中状态 · success | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [success-light.png](../mac-screens/live-states/success-light.png) | 会中状态 · success | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [unavailable-dark.png](../mac-screens/live-states/unavailable-dark.png) | 会中状态 · unavailable | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |
| [unavailable-light.png](../mac-screens/live-states/unavailable-light.png) | 会中状态 · unavailable | —（由 `--self-test live-session-ui` 生成状态报告，未截图） | [live/compare.md](../screens/live/compare.md) | 状态矩阵未截 |

### 未覆盖

Mac 有、Windows 刷新批次没有对应截图的视图：

- `live/live-notes`
- `notes/review`
- `notes/work`
- `island/pinned`
- `island/autoend`

Windows 截图批次里没有 Mac 参考的视图（英文界面版本同源，不单列）：

- `validation/screens/current/answers-dark.png`
- `validation/screens/current/answers-en-dark.png`
- `validation/screens/current/answers-en-light.png`
- `validation/screens/current/answers-light.png`
- `validation/screens/current/controls-dark.png`
- `validation/screens/current/controls-light.png`
- `validation/screens/current/corrected-transcript-dark.png`
- `validation/screens/current/corrected-transcript-en-dark.png`
- `validation/screens/current/corrected-transcript-en-light.png`
- `validation/screens/current/corrected-transcript-light.png`
- `validation/screens/current/history-dark.png`
- `validation/screens/current/history-en-dark.png`
- `validation/screens/current/history-en-light.png`
- `validation/screens/current/history-light.png`
- `validation/screens/current/home-en-dark.png`
- `validation/screens/current/home-en-light.png`
- `validation/screens/current/island-bilingual.png`
- `validation/screens/current/island-collapsed.png`
- `validation/screens/current/island-listening.png`
- `validation/screens/current/library-en-dark.png`
- `validation/screens/current/library-en-light.png`
- `validation/screens/current/live-en-dark.png`
- `validation/screens/current/live-en-light.png`
- `validation/screens/current/notes-dark.png`
- `validation/screens/current/notes-en-dark.png`
- `validation/screens/current/notes-en-light.png`
- `validation/screens/current/notes-light.png`
- `validation/screens/current/onboarding-first.png`
- `validation/screens/current/onboarding-last.png`
- `validation/screens/current/personal-dark.png`
- `validation/screens/current/personal-en-dark.png`
- `validation/screens/current/personal-en-light.png`
- `validation/screens/current/personal-light.png`
- `validation/screens/current/prepare-en-dark.png`
- `validation/screens/current/prepare-en-light.png`
- `validation/screens/current/quick-settings-en-dark.png`
- `validation/screens/current/quick-settings-en-light.png`
- `validation/screens/current/series-en-dark.png`
- `validation/screens/current/series-en-light.png`
- `validation/screens/current/settings-advanced-en-dark.png`
- `validation/screens/current/settings-advanced-en-light.png`
- `validation/screens/current/settings-answer-en-dark.png`
- `validation/screens/current/settings-answer-en-light.png`
- `validation/screens/current/settings-coach-en-dark.png`
- `validation/screens/current/settings-coach-en-light.png`
- `validation/screens/current/settings-en-dark.png`
- `validation/screens/current/settings-en-light.png`
- `validation/screens/current/settings-general-en-dark.png`
- `validation/screens/current/settings-general-en-light.png`
- `validation/screens/current/settings-notes-en-dark.png`
- `validation/screens/current/settings-notes-en-light.png`
- `validation/screens/current/settings-scene-en-dark.png`
- `validation/screens/current/settings-scene-en-light.png`
- `validation/screens/current/transcript-en-dark.png`
- `validation/screens/current/transcript-en-light.png`

### 使用方法

1. 在 `validation/screens/current/` 跑一次 `NextThought.exe --demo --screenshots=validation/screens/current`，Windows 截图不入库，只用于本地对照。
2. 打开 `validation/screens/<视图>/compare.md`，按「Mac 参考图」一节的链接逐张对照浅/深两版。
3. Mac 图更新后重跑 `python Windows/scripts/build_mac_screens_index.py` 刷新本索引。
<!-- mac-win-index:end -->
