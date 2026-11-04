# 受 cicada 启发的一批改进（2026-11）

来源：[wquguru/cicada](https://github.com/wquguru/cicada)（MIT）。借鉴的是做法，不是代码；声纹模型文件取自 cicada，来源与许可见 `Resources/ThirdPartyNotices.txt`。

## 做了什么

| 功能 | 入口 | 关键文件 |
| --- | --- | --- |
| 会议自动收尾 | 设置 → 会议声音与麦克风；引导最后一步 | `MeetingEndWatch.swift`、`AppState+AutoEnd.swift`、`AutoEndViews.swift` |
| 字幕条 / 实时窗口形态 | 会中顶栏菜单、⌃⌥S、设置 → 回答浮窗 | `SubtitleBar.swift`、`LiveWindowController.swift`、`SubtitleBarController.swift` |
| 术语与人名表 | 设置 → 资料；转写里右键“加入术语表…” | `Glossary.swift`、`GlossaryStore.swift`、`GlossaryView.swift` |
| 纪要格式（提纲 / 鱼骨图 / 观点对照表） | 纪要页格式菜单；设置 → 纪要 | `StructuredNotes.swift`、`FishboneView.swift` 等 |
| 区分说话人（声纹） | 设置里的开关，默认关闭 | `SpeakerID.swift`、`SpeakerClustering.swift`、`Resources/models/` |
| 我的声音（自动认出“我”） | 设置 → 区分说话人 → 录入我的声音 | `VoiceProfile.swift`、`MyVoice.swift`；声纹存于 `Voiceprint/me.json`（0600），不进备份 |
| 引导改版 + API 接入 | 首次启动；设置 → 回答 | `Onboarding.swift`、`AnswerServiceViews.swift`、`AnswerServiceSupport.swift` |

## 没有做（建议另开）

- 主窗口改成不抢焦点的面板；删除 `DetachedModules.swift` 死代码；把三套“隐藏窗口”逻辑合并成一套。
- 术语表目前不传给云端识别，也不改写转写显示；“搜索转写行”只差 `runContentSearch` 里一行。
- 联网搜索默认服务商仍是 Codex：只用 API 的用户需要在 设置 → 回答 → 联网搜索 选 Tavily，否则自动联网不会触发。
- ⌃⌥S 与 cicada 的默认快捷键相同，两个应用同时运行会冲突。

## 未经编译 / 未经真机验证

这批改动是在没有 Swift 工具链的环境里写成的：**没有任何一行 Swift 编译或运行过**。纯逻辑部分用 Python 做了等价复现，并写了 Swift Testing 用例，但用例也没跑过。合并前请在 Mac 上依次执行：

```bash
swift test
./scripts/build-app.sh
```

重点留意（可能编译不过或表现不对）：

1. `Glossary.swift`（Swift 6 模式下的嵌套泛型 / 并发检查）及其一个全角字符用例。
2. 字幕条：`NSApp.deactivate()`、`withObservationTracking`、`NSHostingView.fittingSize`；不抢焦点与屏幕共享隐藏需要真机确认。
3. 声纹：`Fbank` 里原地 `vDSP_vmul`；Apple Speech / 云端识别的时间戳对齐；Core ML 模型首次编译耗时；`build-stability-review.sh` 与 CI 手工拼装的 app 没有带上模型文件。
4. 纪要格式：`ViewpointMatrixView` 的 Grid、鱼骨图排版与阈值都没看过实际效果。
5. 引导：`@Bindable` 在计算属性里的用法、`Duration / Duration`、`Text(... specifier:)` 与本地化键 `%.1f` / `%lld%%` 是否对得上；各页版式（560 pt 栏宽、280 pt 转写高度）是猜的。
6. 旧安装保存的 `quickStartStep` 因步骤从 5 变 6 可能落在不同页（已限制范围，未迁移）。
7. 截图工作流 `screenshots.yml`、`capture.py` 的步骤序号已改，未运行。
8. 新增英文文案是机器写的，需要有人通读一遍。
9. 我的声音：判定阈值 0.55 是按 cicada 的分组阈值（0.5）略提高的经验值，没有用真实录音调过；误认别人为“我”比漏认更糟，所以偏保守。录入 4 句各 7 秒用 `MicrophoneCaptureService` 单独采集，会议进行中不允许录入。`MyVoice.recordTake` 里 `Task.detached` 返回元组、`try loaded ?? Voiceprints.load()` 的写法也没编译过。
