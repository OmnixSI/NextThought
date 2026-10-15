# 开发与发布

## 项目结构

```text
Sources/
  InterviewHackerCore/   问题意图与候选、VAD、流式协议解析、云端识别消息、资料检索、Prompt、手机伴侣
  InterviewHacker/       SwiftUI 应用、音频采集、Zipformer / Apple / 云端识别、回答引擎、截图与快捷键
Tests/InterviewHackerCoreTests/   Swift Testing 单元测试与可选的本机 Zipformer 集成测试
Resources/               Info.plist、entitlements、第三方声明
scripts/                 build-app.sh（构建+签名+打包）、sign-app.sh、make-icon.swift
Tools/Validation/        用于识别验证的音频样本、合成音频播放器和题图工具
docs/                    开发文档、验证记录、发布说明与截图
```

## 常用命令

Zipformer 权重来源、具体校验值与本机实际识别结果见[迁移和验证记录](ZIPFORMER-MIGRATION.md)。

```sh
swift test --disable-sandbox
swift build --disable-sandbox
./scripts/build-app.sh
INTERVIEWHACKER_MAKE_DMG=1 ./scripts/build-app.sh
```

构建脚本会将 SwiftPM 所选的静态 Sherpa-ONNX 和 ONNX Runtime 框架链接进应用，不要求运行时安装 Python、Homebrew 或外部服务。第一次使用本机识别时，应用从官方模型仓库下载完整的 Zipformer 文件组到 `~/Library/Application Support/InterviewHacker/models/`。

若要运行使用真实模型文件的集成测试：

```sh
IH_ZIPFORMER_MODEL_DIR="$HOME/Library/Application Support/InterviewHacker/models/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20" \
  swift test --disable-sandbox --filter ZipformerStreamingIntegrationTests
```

没有设置该变量时，这项本机模型集成测试会直接返回；其他测试不要求下载模型或联网。

## 截图与演示模式

`INTERVIEWHACKER_DEMO=1` 启动时使用示例会话，不采集音频、不调用识别或回答模型。可选设置 `INTERVIEWHACKER_DEMO_SECTION=assistant|materials|settings|history`、`INTERVIEWHACKER_DEMO_APPEARANCE=light|dark`。`.github/workflows/screenshots.yml` 在 macOS runner 上生成 README 截图。

## 发布

`.github/workflows/release.yml` 在 Apple Silicon runner 上运行测试并构建 DMG、ZIP 与 `SHA256SUMS.txt`：

| 操作 | 结果 |
| --- | --- |
| 推送到 `main` | 更新滚动预发布 `main-latest`；提交信息含 `[skip release]` 时跳过 |
| 提交信息含 `[release v0.3.0]` | 在该提交创建正式版本 |
| 推送版本标签 | 创建对应版本 |
| 发布 GitHub Release | 自动构建并附加 DMG / ZIP |

> 私有仓库的 macOS runner 按 10 倍计费分钟数。

### 签名与隐私权限

- CI 默认使用临时 ad-hoc 签名，没有 Apple 账号要求。该签名每个版本都不同，因此 macOS 可能要求重新授予隐私权限。
- 本机开发构建默认运行 `scripts/setup-local-signing.sh`，它会复用仓库 `.local-signing/` 中的既有自签名证书。不要删除或重建此身份：麦克风、录屏与系统音频授权依赖固定的 Bundle ID 和签名身份。该流程不需要 Apple Developer 账号。
- 需要把固定签名用于 CI 时，可运行 `scripts/setup-release-signing.sh` 按脚本说明写入 Actions Secrets。配置 Developer ID 和公证凭据后，release workflow 会使用 hardened runtime 与 `Resources/InterviewHacker.entitlements` 公证并装订。
- Bundle ID 保持为 `com.interviewhacker.app`。构建只签应用主程序；Sherpa-ONNX 和 ONNX Runtime 是静态链接，不生成需要单独签名的独立助手进程或动态运行库。

### 本地构建

目标平台为 Apple Silicon arm64、macOS 14 或更新版本。源码构建需要 Swift 6 和 Apple Command Line Tools，不需要 CMake。构建会生成：

- `dist/InterviewHacker.app`
- `dist/InterviewHacker-macOS.zip`
- 可选的 `dist/InterviewHacker.dmg`

应用内首次模型下载与校验之后，识别离线运行。第三方 runtime 和模型的版本、来源、许可及模型文件校验值见 `Resources/ThirdPartyNotices.txt`。

## 语音识别实现

默认引擎是 Sherpa-ONNX 1.13.8 C API，固定 SwiftPM 依赖到 `v1.13.8`；该包把 ONNX Runtime 1.28.2 的静态 macOS 框架一起链接。Zipformer 权重选用官方文档列出的 `sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20`。Sherpa 文档将它列为中文和英文双语在线 transducer，并展示实时麦克风识别用法。权重来自 Hugging Face 仓库固定修订 `98590b7ed6443e77b714204da2757d75e1a642f4`，采用 Apache-2.0 许可证。第三方声明中记录了全部模型文件 SHA-256。

应用将音频采集服务提供的 16 kHz 单声道 Int16 PCM 转成 float 输入，按每块音频增量喂给在线 recognizer。所有 Sherpa C API 调用串行排入后台队列。系统声音和麦克风各建独立 online stream，共享已加载的只读 recognizer 权重，但不共享解码状态。现有 `VoiceActivitySegmenter` 是本地 Zipformer 唯一的句尾提交机制，使用 700 ms 句尾静音门限；模型端点检测保持关闭，避免两套机制重复提交。停止或暂停前先排空采集回调，再结束对应流，把尾句提交一次。

识别回调返回当前完整假设，临时字幕按原有来源分别替换；VAD 句尾、暂停或停止时的最终结果只提交一次。识别器提供的时间信息按输入 PCM 累计样本换算；当前 streaming 模型不提供逐词时间戳。系统音频继续标记为“对方”，麦克风标记为“我”；问题检测只读取系统音频结果。专业词通过 Sherpa transducer 的 `modified_beam_search` 与 BPE hotwords 文件作为上下文提示，不承诺固定准确率增益。

原来保存的 `whisper` 本地引擎选择会迁移到 Zipformer；Apple Speech、云端引擎和答案模型配置不变。OpenAI 转写服务的旧 Whisper 模型 ID 会迁移为空值并回到服务默认模型；Soniox 与 Deepgram 的独立自定义模型 ID 保留。旧模型目录和系统级缓存不会扫描、加载或删除。

## 音频角色与回答模型

- 系统声音与麦克风分别采集、转换和流式解码，分别标记“对方”和“我”。麦克风首次使用请求系统授权；暂停、恢复、停止同时作用于两路。
- 转录、导出和发给 Codex 的上下文保留角色；自动问题检测只看系统声音，不做声纹识别，也不区分系统声音中的多位说话者。建议戴耳机，避免麦克风重复收到扬声器声音。
- 回答引擎与模型设置独立于识别模型；Codex 服务失败时显示错误，不静默切换其他回答模型。
