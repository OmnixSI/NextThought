# Stability implementation — 2026-10-23

Baseline: main `e8ec23a`; local application 1.8.0 / source `c5543aa`.
The implementation includes PR #16 (`aa980aa`) without merging or publishing it.
Checkout stays in the interviewhack project. No production application/data replacement.

## Delivery sequence

- [ ] B: coherent answer state; recoverable SSE/polling; event backups off the UI thread.
- [ ] C: durable pre-meeting document jobs; real progress; resumable parsing/indexing; formats and capacity.
- [ ] D: selected screen target, fresh question context, local OCR enhancement, evidence/search routing.
- [ ] E: encrypted relay and browser fallback, container deployment.
- [ ] F: native iOS/Android companion and BLE transport.

## Validation rules

Automated and isolated application checks are separate from physical-device and real-meeting acceptance.
No real-device latency, OCR accuracy, or 60-minute meeting pass is claimed without measurement.
Full Xcode/iOS SDK is not currently selected/installed; macOS Swift CLT is available.

## Acceptance pending real devices/data

- iPhone Safari and Android Chrome: 60 minutes, background/foreground, network loss and recovery.
- Native phone BLE: packet loss, Wi-Fi loss, foreground/background and switching.
- Independent document/retrieval/search evaluation corpus and real conference replay.

## Stage B verification

- 152 Swift tests passed (includes command deduplication, ordered snapshot framing,
  content-addressed backup deduplication, recovery and corruption rejection).
- Offline browser regression passed: initial snapshot, SSE heartbeat, stale frames,
  new-question empty state, stalled-stream polling, SSE errors and command IDs.
- Existing offline Codex transport regression and four Python normalization tests passed.
- Isolated `.ci.stability` app data export/import round trip passed; pre-import
  recovery points are generated asynchronously. Production data was not used.
- Physical phone and 60-minute timing acceptance remains pending.

## Stage C implementation and measured validation

- SQLite/WAL preparation jobs are durable and bound to their original workspace.
  The UI accepts consecutive uploads; two light jobs and one heavy job may run.
  Original files are stored once by SHA-256. Cancellation/restart preserves page
  checkpoints and batch vector checkpoints. Meetings pause preparation and retain
  the prepared retrieval index; active display changes do not retarget uploads.
- Limits are now 512 MiB and 2000 PDF pages; former character caps were removed.
  Legacy `.ppt` uses an isolated LibreOffice profile; PPTX groups, merged tables,
  notes and chart values are extracted. Worker progress is delivered to the UI.
- Codex PDFs are page-scoped, Office is assigned bounded groups, long UTF-8 text
  and multipage TIFF are scoped. Successful units are reused after interruption.
- Local OCR selector supports pinned Q8 and Q4 models, with actual selected model
  passed through preparation. Q4 revision `c8987b2` was downloaded and all pinned
  file sizes and hashes verified. The six authored real-route samples completed
  in 25.3 seconds with 27/27 numeric anchors; merged cells, independent columns
  and the authored formula samples were reviewed. This is not full-character
  accuracy or a claim about arbitrary documents.
- 155 Swift tests and seven Python tests passed. A packaged `.ci` application
  exercised three immediate batches totaling 20 text files (including >200k
  characters), a corrupt PDF, workspace switching and SQLite persistence.
- Authored PPTX extraction retained grouped text, merged header, speaker note and
  chart values. A real LibreOffice binary `.ppt` round trip completed in 4.6 s.
- 401, 1000 and 2000 native PDF pages were fully retained; 2001 pages rejected.
  Valid 25/100/500 MiB PDFs passed the actual DocumentImporter with text, numbers
  and page locator intact. These padding-stream PDFs test capacity, not OCR quality.
- User-provided SCAN-BEST paper: 15,395,279 bytes (14.68 MiB), 16 physical pages.
  Real preparation completed in 30.5 s including intentional cancel/retry, with
  16/16 pages, 73,806 output characters and unchanged original SHA-256. Local
  evidence contains the progress trace and warnings; paper contents are not
  committed or uploaded to GitHub. Page coverage is not an accuracy guarantee.
- Real Codex route completed 6/6 authored samples (93.3 s). No user paper was
  submitted to Codex in this test.

Remaining validation: independent retrieval calibration and physical-device/
real-meeting acceptance. Large Excel/Office atomic blocks still need their own
quality corpus; exceeding a processing resource must fail explicitly, not clip.


## D — 屏幕、检索与联网证据（draft）

新增选定窗口/显示器/区域的屏幕服务；每题冻结画面、物理页来源和依据，失效时清空旧匹配。快速 Vision OCR 保存坐标与置信度，复杂页面可独立调用本机增强并按模型能力附图。联网核查以同一回答 ID 保存，用户手动应用修订，手机显示更新提示。

验证：160 Swift 测试；隔离应用真实 Vision OCR 验证选定来源、坐标、冻结图片、旧帧失效、问题 ID 隔离及联网修订。该测试注入采集图像，尚未覆盖真实 ScreenCaptureKit 权限、外接屏及会议延迟。

Qwen3 实际向量评估：24 份自编事实文档、48 条同义问题、12 条无关问题。首次按验证集最高可行阈值 0.57 选择，验证 Recall@8 95.8%，已查看测试集 83.3%，未达标。改用验证可行区间中点 0.43，验证及原测试回归均为 100%，无关资料返回率均为 0%。原测试集因已查看，仅作回归；独立真实资料验收仍待完成。阈值与冻结样本在版本库中，报告保留首次失败。

100 条中英搜索问题的实际路由：48 条规则触发，52 条模型判断超过三秒后正确记录降级；总体时效召回 96%，稳定题误触发 0%。模型复核成功率在此环境为 0%，不可宣称模型链路性能达标。截图题、追问共用入口，纯改写复用依据；搜索失败不标为已核查。独立模型延迟及端到端事实人工评分仍待验收。

回滚：撤销本阶段提交；新增 ScreenContext/EvidenceBundle 与回答修订字段均为可选，旧会话可读，原始资料和旧备份不重写。该阶段依赖 C，不单独投产。
