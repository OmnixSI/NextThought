# 已知缺口（KNOWN_GAPS，第二批）

本文件按《收尾决定》（`WINDOWS_FINAL_DECISIONS.md`）第 1 节建立：本版接受、但要在第二批补的差别。每条格式：**页面 — Mac 的做法 — Windows 现状 — 打算怎么做**。同样的内容写进发布说明的「与 Mac 版的已知差别」。

1. **shell（麦克风电平条回声着色）** — Mac 有音频层面的回声监测，电平条按回声着色 — Windows 目前只有文字层面的兜底（`Core.EchoSuppression` 已移植，未接入采音管线） — 第二批把 `EchoSuppression` 接入 `AudioCaptureService` 管线并为电平表加 echo 标志。
2. **insights（屏幕类型判断）** — Mac 用模型判断屏幕上是幻灯片/网页/代码等 — Windows 用启发式（文本特征） — 第二批接入本机模型判断并保留启发式兜底。
3. **presenter（讲稿要点生成质量）** — Mac 用模型判断讲稿要点与备注 — Windows 的启发式生成只是近似 — 第二批按 Mac 的模型路径替换启发式。
4. **assistant（拖动改列/拖动调权重）** — Mac 支持拖动调整对话列宽和权重 — Windows 未做拖拽 — 第二批实现拖拽并持久化布局。
5. **screenshot-question（复杂画面增强读取）** — Mac 可用本机模型增强复杂画面的读取 — Windows 只有 OCR/文本上下文 — 第二批接入本机模型增强路径。
6. **export（对话框分页预览）** — Mac 在导出对话框里分页预览 PDF/Word — Windows 只显示投影源文本 — 第二批用系统 `Windows.Data.Pdf` 把生成的 PDF 渲染成页面图片再预览。
7. **document-settings（回答服务直接读取原文件和图片）** — Mac 的回答服务可直接读原文件/图片 — Windows 目前先转文本/OCR — 第二批把原文件内容作为模型输入（与 §3.3 截图图像输入共用客户端能力）。
