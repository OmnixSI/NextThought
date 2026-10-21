# v1.8.0 发布验证

2026-10-21，设置与引导重构已通过 [PR #15](https://github.com/OmnixSI/InterviewHacker/pull/15) 合并，发布标签与资产中的提交均为 `1146f149645bf2ffcfae93f340df6eb322970797`。版本 `1.8.0`，build `1800`。

## 检查记录

- [PR 最终提交验证](https://github.com/OmnixSI/InterviewHacker/actions/runs/39524266058)：143 项 Swift 测试、4 项 Python 文档回归、CodexTransport 回归、release build，以及 App 数据、文档设置/引导、签名、更新与 X-ASR 自检。
- [主分支验证](https://github.com/OmnixSI/InterviewHacker/actions/runs/39525118598)：相同验证集。
- [Release 工作流](https://github.com/OmnixSI/InterviewHacker/actions/runs/39525125758)：测试、签名 App、ZIP、DMG、随包数据与引导自检，草稿资产下载校验后公开发布。
- 发布后重新下载全部六个资产，逐个校验大小与 SHA-256；确认正式、稳定、latest 标签为 v1.8.0。

## 已验证的分发资产

[正式 Release](https://github.com/OmnixSI/InterviewHacker/releases/tag/v1.8.0) 包含：

| 资产 | 验证 |
|---|---|
| `InterviewHacker-1.8.0-macOS-arm64.dmg` | SHA-256、磁盘映像校验、只读挂载，App 与 ZIP 的二进制一致 |
| `InterviewHacker-1.8.0-macOS-arm64.zip` | SHA-256、解压、完整 codesign 校验、arm64、版本 1.8.0/build 1800 |
| `InterviewHacker-document-runtime-1.7.0-macOS-arm64.tar.gz` | 与固定运行包 SHA-256/大小一致，复用既有版本 |
| `runtime-manifest.json` | 与源代码中的固定清单一致 |
| `SHA256SUMS.txt` | 覆盖其余五个资产，实际下载全部通过 |
| `update.json` | tag、版本、build、ZIP 文件名/哈希与发布提交一致 |

ZIP 和 DMG 内的 App 都包含实际随包第三方声明；签名 designated requirement 与原 v1.7.0 分发 App 相同，不含临时 cdhash 约束。沿用既有本机签名，无私钥上传，未新增 Developer ID 公证。

## 实际更新演练

使用隔离的旧版 v1.7.0 App，通过真实更新代码查询稳定 Release、下载 v1.8.0、验证校验值与签名，再在测试目录执行应用替换。最终输出 `staged v1.8.0 (version 1.8.0)`，自检通过。没有替换 `/Applications` 中的应用。

演练副本使用独立 `.ci.releaseprobe` 身份和 ad-hoc 测试签名，因此其日志中的“测试副本签名不同”提示属于该隔离环境；正式 v1.7.0/v1.8.0 分发包之间的签名一致性另行验证通过。原件资料和用户语音模型未参与替换。

## 环境与清理

实际机器为 M2 Pro、16 GB、macOS 26.5.2；Info.plist 与 Mach-O 的部署目标为 macOS 14.0。没有 macOS 14 真机验证。界面检查与真实模型质量范围见 [设置验证](VALIDATION-SETTINGS.md) 和 [v1.7.0 文档模型验证](VALIDATION-v1.7.0.md)；本次没有重复在线 OCR 对照调用。

CI 使用项目目录中的前台临时 runner，不安装常驻服务。运行后注销并删除 runner、重建缓存和本轮已确认的临时测试目录。原始模型保留一份（32 个文件，共 1,879,769,106 字节），发布包、校验文件与压缩验证记录保留。
