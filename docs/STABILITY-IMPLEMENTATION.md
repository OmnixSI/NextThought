# Stability implementation — 2026-10-23

Baseline: main `e8ec23a`; local application 1.8.0 / source `c5543aa`.
The implementation includes PR #16 (`aa980aa`) without merging or publishing it.
Checkout stays in the interviewhack project. No production application/data replacement.

## Delivery sequence

- [x] B: coherent answer state; recoverable SSE/polling; event backups off the UI thread.
- [x] C: durable pre-meeting document jobs; real progress; resumable parsing/indexing; formats and capacity.
- [x] D: selected screen target, fresh question context, local OCR enhancement, evidence/search routing.
- [x] E: encrypted relay and browser fallback, container deployment.
- [x] F: native iOS/Android companion and BLE transport.

Checked boxes indicate implementation delivered on stacked draft PRs; release acceptance remains pending.

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

## E — 加密互联网备用连接（draft）

交付 TypeScript/Node WSS 中转、Docker 部署文件和同一份手机静态页面。Mac 原生 CryptoKit 与网页 noble AES-GCM/HKDF 共用测试向量，密钥放二维码 URL fragment，服务器仅持有房间凭据和当前密文。局域网页失联后转中转，HTTPS 备用页面只走 WSS。Mac 统一命令去重并返回加密回执。

验证：162 Swift 测试、3 Node 真 WebSocket 测试、网页离线恢复回归通过；隔离 Mac 应用真实 URLSession→Node→订阅端状态、操作确认和重连快照通过。真实内嵌浏览器成功解密文字、点击“下一条”并显示加密回执后的状态，控制台无错误。npm audit 零已知漏洞（固定 ws 8.22.0）。截图和日志保留本地，不含用户资料。

部署步骤、协议、限制与回滚见 CompanionRelay/README.md。没有部署公网服务或产生云费用；公开 HTTPS/WSS 域名、双平台真实切网、60 分钟测试和独立安全审查仍待验收。中转重启/8 小时到期须重配对。

## F — 原生手机与蓝牙备用（draft）

交付 Kotlin/Compose Android 项目与 SwiftUI iOS Xcode 项目；扫描、粘贴和深链配对，局域网 SSE、静默快照恢复、加密中转、Core Bluetooth/GATT 备用、当前回答/来源/核查更新及操作入口均已实现。Mac 提供可选 peripheral 服务；分片协议、完整消息认证确认、最多三次重传、最新状态合并、过期帧回收及三通道命令去重共用。

验证：166 桌面 Swift 测试通过，含真实跨语言 AES-GCM 向量、乱序/重复/超时片、丢失确认重传、错误确认不能前进以及只发送最新等待状态。独立 Swift 手机共用包编译并通过 3 项测试，其中真实 TCP 验证 SSE 切题、空的新答案、命令一次执行、SSE 静默后的快照补齐。Android 真实构建产生约 10 MB 调试 APK，3 项 JVM 协议测试通过；Android 15 ARM64 模拟器的 Compose/真实 TCP 整链路仪器测试通过，验证同一组切题/操作/静默恢复场景。

iOS UI 未在本机编译：本机只有 Command Line Tools，缺少完整 Xcode/iOS SDK。已提供可确定性重建的 Xcode 项目和手动构建工作流；未提供 IPA，不宣称 iPhone 真机已通过。模拟器不会证明 BLE 射频行为；Mac/iPhone/Android 三方真实 BLE、实体两平台各 60 分钟、锁屏和切网仍是发布门槛。操作和构建说明、协议与回滚见 Mobile/README.md。


## G — 复核加固与隔离评审包

屏幕增强改为在稳定帧中定位实际变化的像素区域，复用区域外文字；记录增强区域与截取区域，避免每次整屏重做重 OCR。按问题保存依据、检索策略、屏幕来源、联网决策和降级原因到会话归档，旧会话缺字段仍可读。取消一个请求不会销毁其他请求正在共用的预热任务。

备份解压前检查路径、链接、重复项、条目数量、扩展容量及磁盘空间；在合并前预检 JSON、配置和原文件哈希。实际归档往返测试恢复原文件；人为破坏原文件后导入会在任何场景合并前失败。会话大文件复制移出主线程，恢复点空间超预算时保留可用备份并提示。

模型联网复核改为精简提示并复用预热会话，区分超时、无效 JSON、API 配置和 HTTP 错误。最终同一 100 题回归仍为 48 条规则、52 条三秒超时回退，召回 96%、稳定题误触发 0%；没有把未解决的模型延迟写成已通过。

`./scripts/build-stability-review.sh` 在隔离 APFS 卷中生成 ARM64 macOS 评审应用并输出 ZIP，使用独立 bundle ID 与应用旁 `.interviewhacker-review` 数据目录，保留生产安装；本地 ad-hoc 签名并验证，不属于公证发行。`dist/review-build.json` 记录构建提交与 SHA-256。离线测试、真实本地协议测试与设备验收边界见 `STABILITY-REVIEW.md`。


## Astra 视觉与交互打磨

应用户要求，由 Astra 审查桌面、网页及两种原生手机界面，结果见 `DESIGN-REVIEW.md`。修正准备队列异常被“0 项未完成”掩盖、依据限制全折叠、三种二维码并列、停止按钮藏在横向栏等问题。配对采用网页/App 两种入口，备用连接收进高级选项；历史位置、触控目标和主操作统一，禁用态有明确视觉反馈。已有原生图标表达各项任务，另补原创对话框/波形 SVG 标识，实际用于 Android adaptive launcher、网页 favicon 与 iOS AppIcon；未新增装饰插画。

手机三端增加独立的操作状态：立即显示发送中；相同动作等待时抑制重复；普通快照不覆盖未确认提示；HTTP 成功或经认证 ACK 才标 Mac 已确认。15 秒未确认后可在原始发送起四分钟内重试，沿用原 UUID。网页覆盖超时、在线状态不清除失败、重试 ID 不变和回执；共享 Swift 真实 TCP 验证重复点击抑制与确认。

最终自动化：171 桌面 Swift 测试、3 个手机共享 Swift 测试、3 个 Android JVM 测试、Android 15 ARM64 模拟器精确指定的真实 TCP/Compose 仪器测试、3 个 Node WebSocket 测试、网页恢复与操作反馈回归、7 个 Python 文档测试及真实 MCP 只读/路径隔离探针通过。真实打包应用的 Vision/冻结帧/旧答案隔离测试通过。桌面 demo 自渲染、真实 IAB 中转网页、Android 模拟器截图已复核；均不冒充真实会议或实体手机证据。
