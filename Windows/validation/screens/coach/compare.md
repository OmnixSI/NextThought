# 看镜头提醒设置卡（E10 · §8.2）：Mac 3.4.0 源码 → Windows「临场教练」逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`；`Sources/NextThought/SettingsPresenter.swift`（PresenterSettings 的「临场教练」section）、
  `Sources/NextThought/LiveCoach.swift`（GazeSensitivity / GazeCamera / calibrate）
- Windows 代码：`Windows/src/NextThought.Desktop/Views/SettingsView.axaml(.cs)` 的「临场教练」卡；
  状态与相机在 `AppState.Coach.cs`；采集/推理在 `Windows/src/NextThought.Platform/Gaze/`（GazeCamera、
  FaceLandmarker、GazeImage、GazeBaselineStore）
- Windows 截图（本目录）：`settings-coach-light.png` / `settings-coach-dark.png`（中文），
  `settings-coach-en-light.png` / `settings-coach-en-dark.png`（英文），由 `--screenshots` 批次渲染同一构建
- 三态/相机证据：`Windows/validation/e10/coach-gaze.md`（`--self-test=coach-gaze`）

| # | Mac 元素（PresenterSettings「临场教练」） | Mac 行为 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | 卡片位置/标题 | 设置 → 提词与教练 → 「临场教练」section | 同页同位置（提词卡之后、字幕条之前），标题「临场教练」 | 一致 |
| 2 | 「留意眼神是否离开镜头（使用摄像头）」 | Toggle，默认关（`coachWatchesGaze` = false）；打开立刻 startWatching | CheckBox 同名，绑定 `Settings.CoachWatchesGaze`；切换即写 Mac key 并打开/关闭相机 | 一致（默认关一致） |
| 3 | 摄像头权限 | `cameraAccess` denied/restricted → Note「摄像头还没有允许。」+「去允许」跳权限页 | `IsGazePermissionDenied` → 同样两行；「去允许」打开 `ms-settings:privacy-webcam` | 一致（Windows 权限入口是系统设置） |
| 4 | 「灵敏度」 | MenuPicker 宽松/标准/严格，默认标准，改动即 setTolerance（0.17/0.12/0.09） | ComboBox 三项，默认标准，改动即 `GazeCamera.Tolerance` | 一致 |
| 5 | 「校准：看着摄像头 2 秒」 | 只在 watchesGaze 时显示；按住期间 spinner +「正在校准，请看着摄像头…」；成功「已校准」、失败「没有看清眼睛…」 | Button「校准：看着摄像头 2 秒」，摄像头未运行时禁用；状态行走 `GazeStatusText`（正在看镜头/已校准/失败提示） | 一致（按钮常显但禁用，见差异 2） |
| 6 | 校准持久化 | UserDefaults `coachGazeBaseline` 四个 Double，下次启动仍认为已校准 | Windows sidecar `WindowsState/coach-gaze-baseline.json`（不进 settings.json/备份），启动恢复 baseline | 行为一致（存储位置按 Windows 惯例） |
| 7 | 隐私说明 | Note「画面只在这台 Mac 上分析成几个特征点，不保存、不上传。…」 | Note「画面只在这台电脑上分析成几个特征点，不保存、不上传。」（Windows 独立英文行已进 WORDING.md） | 一致（Mac 后半句提词岛说明按 Windows 文案省略） |
| 8 | 关闭看镜头 | `watchesGaze=false` → camera.stop、gaze 清空、away 复位 | 同（StopGazeWatchingAsync：相机停止、GazeAttention.Reset、岛提示清除） | 一致 |
| 9 | 会议生命周期 | begin 时若 enabled+watchesGaze 则 start；end 时 stop 并把 eyeContact 写入 summary | `BeginAutoEnd` 调 `ApplyCoachGazeSettings`；`StopAutoEnd` 停相机、`coachSummary.ObserveEyeContact` | 一致 |
| 10 | 岛内提示 | `shouldLookUp`：说话中移开视线 ≥4s → 「看镜头」+ eye 符号（LiveSessionView/CueIsland） | cue island `CueIslandCoachHint` 优先显示「看镜头」、符号 `eye`；`GazeAttention` 阈值同 4s/4s | 一致 |

## 差异 / 未解决

1. Mac 的「会中轻提示：语速、说得太久、发言占比」总开关（`coachEnabled`）Windows 设置页还没有 UI；
   该 key 已按 Mac 名持久化、默认 true，属于 J/D 组的教练 UI，不在 E10 范围。
2. Mac 只在打开「留意眼神」后才显示灵敏度/校准；Windows 卡片始终显示这两项（校准按钮在相机未运行时禁用，
   灵敏度改动会在下次打开看镜头时生效）。是一个轻差异，功能结果一致。
3. Mac 用 Vision `VNDetectFaceLandmarksRequest`（有 Vision 的 yaw/pitch）；Windows 按任务书 §8.2 用
   MediaPipe Face Landmarker（ONNX），`GazeFrame` 的 yaw/pitch 为 null，静态姿态由校准吸收。
4. 英文模式下的设置卡已截图核对（en-light/en-dark）；相机三态限制见 `validation/e10/coach-gaze.md`：
   本机摄像头前无真人，「看镜头/移开」用录好的正面像走同一处理路径验证，发布说明需注明未用真人验证。
