# E10 看镜头提醒：本机与录屏自检证据

- 日期：2026-11-21；分支 `windows/parity`；产物：Debug 桌面构建（版本 0.9.0）
- 硬件：`BisonCam, NB Pro`（设备状态 OK），Windows 11；摄像头隐私存储 `NonPackaged = Allow`
- 命令（隔离数据目录，跑完自动退出）：

```powershell
$env:NEXTTHOUGHT_DATA_DIR = (New-Item -ItemType Directory "$env:TEMP\nt-e10-$(Get-Random)")
Windows\src\NextThought.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\NextThought.exe --demo --self-test=coach-gaze
```

结果：`coach-gaze-check: passed`（完整 JSON 见 `coach-gaze-check.json`）。

## 三段检查

| 步骤 | 结果 | 说明 |
|---|---|---|
| 看到镜头模型资产 | 通过 | `assets/face-landmarker/`（转换后的两张 ONNX，SHA 见 `SHA256SUMS`） |
| 摄像头打开并在内存里分析帧 | 通过 | `started=True`、权限允许；2.5 秒内 7 帧、**全部 NoFace**（机器前无人），帧只在内存、不落盘 |
| 录好的正面像检测到人脸 | 通过 | 478 点、presence 1.000、右眼轮廓宽 54.9 px |
| 看镜头：判定 OnCamera | 通过 | 用正面像 12 帧校准（中位数 baseline）后 `state=OnCamera, deviation=0.00` |
| 移开视线：判定 Away | 通过 | 把检测到的双眼虹膜点外移 0.35 倍眼宽（模拟视线离开镜头，`GazeEstimator` 的真实输入）后 `state=Away, deviation=2.92` |
| 没有人脸：判定 NoFace | 通过 | 纯灰图 → 检测不到人脸 → `state=NoFace` |

## 三态判定实情与限制（重要）

- **看镜头 / 移开视线**：本机摄像头前没有人，无法用真人完成这两态；按任务书 §8.2 第 6 条的替代路径，用录好的正面像喂**同一条 `FaceLandmarker` + `GazeEstimator` 处理路径**，其中“移开”是把模型检测到的虹膜点按眼宽外移 0.35 倍（等价于真人把视线移开时 `GazeEstimator` 收到的输入），阈值与生产代码完全一致（没有为自检放宽）。
- **没有人脸**：真实摄像头路径实测 7 帧全 NoFace，同时灰图的图片路径也判 NoFace。
- 因此发布验收仍应注明：**尚未用真人在真实摄像头前验证“看镜头/移开”两态**；相机硬件本身已实测可打开、可采集、帧只在内存。

## 设置页证据

- `screens/settings-coach-light.png`、`screens/settings-coach-dark.png`：提词与教练页的「临场教练」卡（留意眼神开关、灵敏度、校准按钮、摄像头权限提示、本机处理说明），默认关闭。

## 测试夹具

- `images/portrait.jpg`：MediaPipe 官方示例资产 <https://storage.googleapis.com/mediapipe-assets/portrait.jpg>，
  176,561 B，SHA-256 `a6f11efaa834706db23f275b6115058fa87fc7f14362681e6abe14e82749de3e`；仅作验证夹具，不随应用分发。
- 复跑：保持该文件在 `Windows/validation/e10/images/`，执行上面的命令即可。
