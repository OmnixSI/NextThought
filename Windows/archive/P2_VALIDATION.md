# P2 验收记录

日期：2026-11-07。基于 P1 `fee0473`，本轮预览版 0.2.0。原 Mac 源码、签名和 TCC 配置保持原样。

| 步骤 | 实现与验收 | 证据 |
|---|---|---|
| P2-1 | PDF 原生中文/英文文字、扫描 PDF、PNG OCR、DOCX 段落/表格、PPTX 页序、XLSX 工作表/单元格及 TXT/MD。7 个公开合成文件全部通过。PDF 两页及扫描页已渲染查看，桌面选第 2 页显示对应原页和中文摘录。原文件 SHA-256、去重、取消不留半成品、禁止 XML DTD 已检查。 | `artifacts/p2/documents.json`、`test-results/p2-1.trx` |
| P2-2 | 中文双字词及英文词 BM25 检索，1200 字分块/200 字重叠，默认最多 6 段/7000 字。相关资料经用户所选服务回答；保存实际引用编号、原文位置、文件哈希和摘录，可在历史查看来源并导出。DeepSeek 官方 deepseek-flash 真实返回“林工”，正确引用 project.pdf 第 2 页；不匹配查询为空，未提供编号会被拒绝。 | `artifacts/p2/retrieval.json`、`retrieval.md`、`test-results/p2-2.trx` |
| P2-3 | 使用本机 Codex 0.167.0-alpha.3.2 的 app-server stdio 协议：initialize、thread/start、turn/start、增量事件、turn/completed、turn/interrupt。账号由 CLI 管理，应用不读取或复制其凭据。每请求启动临时工作目录、read-only/never 会话，结束清理进程树。真实连接成功，4 次增量通知；取消 1.55 秒，随后恢复成功。设置可切换 API/Codex；回答及纪要共用接口。 | `artifacts/p2/codex.json`、`codex-schema/` |
| P2-4 | 托盘、跨应用快捷键恢复主窗、悬浮窗开关已通过桌面操作验证。Ctrl+Alt+N 恢复主窗，O 切换悬浮窗，R 开始/结束记录。应用选择在开始前检查 PID/启动时间，不保存 PID。Windows process loopback 包含目标子进程，不按设备过滤；麦克风独立控制。双独立进程测试，440 Hz 幅值 0.039999，非目标 880 Hz 约 1.21e-7；目标播放停止后 RMS 约 8.59e-7，66 帧。 | `artifacts/p2/process-audio.json` |
| P2-5 | v1 .ntbackup ZIP 导出导入，文件哈希、格式/版本、路径/符号链接/重复名、文件数/大小校验；导入前备份，重复 ID 保留当前数据，中途失败撤回本次新增项。49 项回归通过，包括 Windows 往返、Mac 包装目录结构样本、路径穿越、未来版本、哈希篡改、提交中途失败回滚。 | `artifacts/p2/test-results/p2-5.trx` |

## 使用与边界

- 资料库保存受管原文件及提取文字；Office 不依赖安装 Office，不执行宏。旧版 `.doc/.ppt/.xls` 请先另存新版。Word 不计算页码；Excel 公式取缓存值、日期保留底层值；PPT 仅文字/表格。图片和扫描 PDF 使用 Windows 已安装的中文/英文 OCR 语言包。复杂版面、手写、多页 TIFF 后续帧不在基础识别范围。
- 勾选“参考资料库”后，在本机全部已导入资料中检索；相关摘录随问题发送到所选远程服务。引用证明对应摘录和来源位置，不能代替人工判断回答是否正确。本轮未加入向量检索或 P3 模型。
- Codex 模型留空时用账号默认模型；API 的 deepseek-flash 与 Codex 模型设置分开。Codex 使用文本问答流程，不接受交互式工具或权限请求；read-only 是 CLI 会话配置，不应理解为应用实现了独立文件系统隔离。
- 应用采音要求 Windows build 20348+。选“整个输出设备混音”时会采该设备其他应用声音；选具体应用时采目标及其子进程，覆盖其使用的输出设备。浏览器/会议软件若共用音频子进程，应结合实际程序结构选择上层进程。本轮频率隔离为两个独立测试进程，未据此声称所有会议软件均已实机验收。
- 沿用用户 HECATE 与麦克风 4 倍增益设置。本轮不新增麦克风采音、不测试 USB 插拔、不保存原始音频。快捷键 R 的实际麦克风录音未重复测试。
- 备份包含会议、Windows 资料以及已保留的迁移档案；不包含 API Key、语音模型、设备设置。导入沿用本机连接设置。Mac 完整/场景 v1 `.ntbackup` 和旧名 `.ihbackup` 可导入；会议文件直接恢复，场景资料的 pages 转为可检索 Markdown，并保留 Mac 页序。其余场景参数、知识、技能、原文件等完整保存在 `Migration/<SHA256>.ntbackup`，尚未映射全部 Mac 界面功能。重复 ID 的传入版本也保存在该原始档案中。增量 `.ntrestore`、含 secrets.plist 的备份暂不接收；Mac 应导出不含凭据的完整备份。本轮使用按源码结构构造的样本，未做真实 Mac 双向实机验收。

## 复现

在 Windows 目录执行 `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Publish`。
公开合成样本生成脚本 `scripts/create_p2_fixtures.py` 仅用于 QA，需要 Python 的 reportlab/Pillow/python-docx/python-pptx/openpyxl；软件运行不依赖 Python。
发布包 tools 中的 CLI 支持 `documents-test`、`retrieval-test --live`、`codex-test`、`process-audio-test`；参数和示例见 README。真实 API 测试只允许匹配合成样本 SHA-256 的资料，使用本机保存的凭据，不把 Key 写入报告。
