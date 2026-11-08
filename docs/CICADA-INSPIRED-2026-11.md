# cicada 来源与适配说明（2026-11）

## 固定来源与许可

- 上游实现：[`wquguru/cicada`，固定提交 `f11cc1354e7f1c04d831a783750b1c62c9ba3ef8`](https://github.com/wquguru/cicada/tree/f11cc1354e7f1c04d831a783750b1c62c9ba3ef8)。该提交的 [`LICENSE`](https://github.com/wquguru/cicada/blob/f11cc1354e7f1c04d831a783750b1c62c9ba3ef8/LICENSE) 是 Apache License 2.0。
- NextThought 只适配了明确标注的声纹分组规则与滤波器组/模型转换逻辑；没有把 cicada 的整套应用或其他无关代码搬入本仓库。改动边界与来源见 [`Resources/ThirdPartyNotices.txt`](../Resources/ThirdPartyNotices.txt)。
- `SpeakerEmbedding.mlpackage` 的权重来自 [WeSpeaker ResNet34-LM / VoxCeleb](https://github.com/wenet-e2e/wespeaker)，权重许可为 [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)。模型权重与 cicada 的 Apache-2.0 代码许可是两项不同的来源。

此前版本把 cicada 标为 MIT，并把声纹模型说成取自 cicada；这两项说法不准确，已按固定提交和模型来源更正。旧版中“未经编译、未运行测试”的备注也是历史状态，不代表当前仓库的验证状态；每次发布的实际验证结果以 `docs/releases/` 和 CI 记录为准。

## 在 NextThought 中的适配范围

相关产品路径包括会后纪要、术语纠错、声纹分组、字幕窗口和会前配置。借鉴的是具体机制，交互与数据仍按 NextThought 的原生 SwiftUI、会话归档和 Paper & Ink 设计系统实现。当前主要落点包括：

| 能力 | NextThought 落点 |
| --- | --- |
| 会议静默结束判断 | `Sources/NextThoughtCore/MeetingEndWatch.swift`、`Sources/NextThought/AppState+AutoEnd.swift` |
| 字幕条与实时窗口形态 | `Sources/NextThoughtCore/SubtitleBar.swift`、`Sources/NextThought/LiveWindowController.swift`、`Sources/NextThought/SubtitleBarController.swift` |
| 术语表、纠错和别名检索 | `Sources/NextThoughtCore/Glossary.swift`、`Sources/NextThought/GlossaryStore.swift` |
| 结构化纪要 | `Sources/NextThoughtCore/StructuredNotes.swift` 与 `Sources/NextThought/` 中对应视图 |
| 可选声纹区分 | `Sources/NextThoughtCore/SpeakerClustering.swift`、`Resources/models/` |
| “我的声音”本机配置 | `Sources/NextThought/VoiceProfile.swift`、`Sources/NextThought/MyVoice.swift` |

该表描述当前代码路径，不代表音频识别质量、误分率或真机权限流程已经完成实测。具体产品验收与设备边界见 [`docs/VALIDATION-2026-10-14.md`](VALIDATION-2026-10-14.md) 及各版本验证记录。
