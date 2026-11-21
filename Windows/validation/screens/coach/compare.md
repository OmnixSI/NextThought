> 截图来源：提交 `940a0280`（2026-11-21 17:24，最终构建；浅/深/英与状态截图均由该提交生成）。



# 看镜头提醒设置卡（E10 · §8.2）：Mac 3.4.0 源码 → Windows「临场教练」逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`；`Sources/NextThought/SettingsPresenter.swift`（PresenterSettings 的「临场教练」section）、
  `Sources/NextThought/LiveCoach.swift`（isEnabled / GazeSensitivity / GazeCamera / calibrate）
- Windows 代码：`Windows/src/NextThought.Desktop/Views/SettingsView.axaml(.cs)` 的「临场教练」卡；
  状态与相机在 `AppState.Coach.cs`；提示生成在 `AppState.StructuredNotes.cs`、岛提示在 `AppState.Cue.cs`；
  采集/推理在 `Windows/src/NextThought.Platform/Gaze/`（GazeCamera、FaceLandmarker、GazeImage、GazeBaselineStore）
- Windows 截图（本目录）：`settings-coach-light.png` / `settings-coach-dark.png`（中文），
  `settings-coach-en-light.png` / `settings-coach-en-dark.png`（英文），由 `--screenshots` 批次渲染同一构建
- 三态/相机证据：`Windows/validation/e10/coach-gaze.md`（`--self-test=coach-gaze`）

| # | Mac 元素（PresenterSettings「临场教练」） | Mac 行为 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 卡片位置/标题 | 设置 → 提词与教练 → 「临场教练」section | 同页同位置（提词卡之后、字幕条之前），标题「临场教练」 | 一致 |
| 2 | 「会中轻提示：语速、说得太久、发言占比」总开关 | Toggle，默认开（`coachEnabled` = true）；关闭时 `camera.stop()`、`nudge` 清空，`ingest/refresh/end` 全部短路（岛上不再出提示，纪要摘要为 nil） | CheckBox 同名，绑定 `Settings.CoachEnabled`（`coachEnabled`，默认 true）；切换立即写 Mac key；关闭时 `ApplyCoachEnabledAsync` 停相机、清 `CoachHint`，岛提示 `CueIslandCoachEnabled` 含 `Settings.CoachEnabled`，每秒教练 tick/逐行 ingest 均短路，`BuildCoachSummary` 不再生成纪要教练摘要 | 一致 |
| 3 | 「留意眼神是否离开镜头（使用摄像头）」 | Toggle，默认关（`coachWatchesGaze` = false）；打开立刻 startWatching | CheckBox 同名，绑定 `Settings.CoachWatchesGaze`；切换即写 Mac key 并打开/关闭相机 | 一致（默认关一致） |
| 4 | 摄像头权限 | `cameraAccess` denied/restricted → Note「摄像头还没有允许。」+「去允许」跳权限页；只在 watchesGaze 时显示 | `IsGazePermissionDenied` → 同样两行，位于 watchesGaze 条件容器内；「去允许」打开 `ms-settings:privacy-webcam` | 一致（Windows 权限入口是系统设置） |
| 5 | 「灵敏度」 | MenuPicker 宽松/标准/严格，默认标准，改动即 setTolerance（0.17/0.12/0.09）；只在 watchesGaze 时显示 | ComboBox 三项，默认标准，改动即 `GazeCamera.Tolerance`，位于 watchesGaze 条件容器内（关闭时隐藏） | 一致 |
| 6 | 「校准：看着摄像头 2 秒」 | 只在 watchesGaze 时显示；期间 spinner +「正在校准，请看着摄像头…」；成功「已校准」、失败「没有看清眼睛…」 | Button 位于 watchesGaze 条件容器内（关闭时隐藏），摄像头未运行时禁用；状态行走 `GazeStatusText`（正在看镜头/已校准/失败提示） | 一致 |
| 7 | 校准持久化 | UserDefaults `coachGazeBaseline` 四个 Double，下次启动仍认为已校准 | Windows sidecar `WindowsState/coach-gaze-baseline.json`（不进 settings.json/备份），启动恢复 baseline | 行为一致（存储位置按 Windows 惯例） |
| 8 | 隐私说明 | Note「画面只在这台 Mac 上分析成几个特征点，不保存、不上传。…」 | Note「画面只在这台电脑上分析成几个特征点，不保存、不上传。」（Windows 独立英文行已进 WORDING.md），始终显示 | 一致（Mac 后半句提词岛说明按 Windows 文案省略） |
| 9 | 关闭看镜头 | `watchesGaze=false` → camera.stop、gaze 清空、away 复位 | 同（StopGazeWatchingAsync：相机停止、GazeAttention.Reset、岛提示清除） | 一致 |
| 10 | 会议生命周期 | begin 时若 enabled+watchesGaze 则 start；end 时 stop 并把 eyeContact 写入 summary（enabled=false 时 summary 为 nil） | `BeginAutoEnd` 调 `ApplyCoachGazeSettings`（先查 `coachEnabled`）；`StopAutoEnd` 停相机、`coachSummary.ObserveEyeContact`；`BuildCoachSummary` 在关闭时不生成摘要 | 一致 |
| 11 | 岛内提示 | `shouldLookUp`：说话中移开视线 ≥4s → 「看镜头」+ eye 符号（LiveSessionView/CueIsland） | cue island `CueIslandCoachHint` 优先显示「看镜头」、符号 `eye`；`GazeAttention` 阈值同 4s/4s；总开关关闭时 `CueIslandCoachEnabled` 为 false | 一致 |

## 差异 / 未解决

1. ~~「会中轻提示」总开关（`coachEnabled`）Windows 设置页没有 UI~~：已修（本轮 E27）——卡片首个 CheckBox「会中轻提示：语速、说得太久、发言占比」绑 `Settings.CoachEnabled`，持久化到 `coachEnabled`（默认 true），并接线到岛提示/每秒 tick/逐行 ingest/纪要摘要与相机。
2. ~~灵敏度/校准始终显示~~：已修（本轮 E27）——权限行、灵敏度下拉、校准按钮与相机状态行都放进 `IsVisible="{Binding Settings.CoachWatchesGaze}"` 容器，关闭「留意眼神」时隐藏（Mac 原句与顺序：留意眼神 → 权限 → 灵敏度 → 校准）。
3. Mac 用 Vision `VNDetectFaceLandmarksRequest`（有 Vision 的 yaw/pitch）；Windows 按任务书 §8.2 用
   MediaPipe Face Landmarker（ONNX），`GazeFrame` 的 yaw/pitch 为 null，静态姿态由校准吸收。
   （已裁决：见 `ACCEPTED_DIFFERENCES.md` coach 3）
4. 英文模式下的设置卡已截图核对（en-light/en-dark）；相机三态限制见 `validation/e10/coach-gaze.md`：
   本机摄像头前无真人，「看镜头/移开」用录好的正面像走同一处理路径验证，发布说明需注明未用真人验证。
5. 截图：本轮新增总开关与条件行后，`settings-coach-*` 待 prepare 收口轮统一重拍。

## 结论

逐条一致；另有 1 条已裁决的差别（coach 3：Vision vs MediaPipe 的 yaw/pitch 语义数值差异，见 `ACCEPTED_DIFFERENCES.md`）。
