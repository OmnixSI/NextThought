# 设置与引导重构验证（未发布）

本次为 v1.7.0 之后的设置与引导改动，尚未发布新版本。未替换日常安装，未修改正式资料或模型；预览 App 使用独立的 `.ci.settingsreview` 身份与测试数据目录。

## 验证环境

M2 Pro、16 GB、macOS 26.5.2、Command Line Tools 的 Swift 6.1.2。macOS 14+ 是部署兼容目标，本次没有 macOS 14 真机结果。

## 已完成

| 检查 | 结果 |
|---|---|
| `swift test` | 143 项通过；包含组件依赖的全部 64 种组合、核心启动条件的全部 64 种组合、首次/功能升级/补丁版本策略与备份过滤 |
| `swift build -c release` | 通过；仍有原有 `WorkspaceStore.swift` 的 Sendable 警告 |
| `Tools/Validation/CodexTransport/run.sh` | 通过；并发预热、请求取消、晚到确认、取消后恢复、20 次立即退出 |
| Python 文档回归 | 4 项通过 |
| `--self-test document-setup` | 通过；组件复用、取消、晚到选择保护、引导重置/继续/完成、高级开关持久化、不清空配置、API 必填地址与本地/远程密钥判定 |
| 原生 SwiftUI 界面渲染 | 23 个状态生成有效 PNG；包括五步引导、七组默认页、八组高级页、已安装资料组件、基础 API 表单、核心未就绪 |

界面人工检查覆盖默认页的必要操作、API 地址/密钥/模型可见性、下载与已安装状态、核心未就绪时的禁用按钮、就绪时的开始入口。取消、持久化与版本重置通过实际 App 状态自检验证；没有把截图渲染当成完整的点击自动化或真实语音识别测试。

本次截图将权限、登录与部分模型状态设为演示值，不请求音频设备、下载模型或调用在线回答服务。真实文档 OCR/向量性能沿用 [v1.7.0 报告](VALIDATION-v1.7.0.md)，本次没有重新调用三条在线/本机模型对照。

## 复现

先编译 App，将其 bundle identifier 改为独立 `.ci.` 身份，避免读写正式设置与钥匙串。设置显式测试目录后运行：

```sh
INTERVIEWHACKER_DEMO=1 INTERVIEWHACKER_DATA_DIR=/absolute/test-data \
  /absolute/SettingsReview.app/Contents/MacOS/InterviewHacker --self-test document-setup

python3 Tools/Validation/Settings/capture.py \
  --app /absolute/SettingsReview.app --output /absolute/screenshots --data /absolute/test-data
```

渲染工具保存每个状态的原生 PNG、日志及 SHA-256 清单。代表预览：

- [默认设置](screenshots/settings-simple-dark.png)
- [实时对话引导](screenshots/onboarding-realtime-dark.png)
- [资料可选能力](screenshots/document-options-dark.png)

完整设置分层和用户取舍见 [设置设计](SETTINGS-DESIGN.md)。
