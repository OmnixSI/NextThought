# E10 第 1 步：Face Landmarker 许可与 TFLite→ONNX 转换证据

日期：2026-11-21（分支 `windows/parity`）。任务书 §8.2 第 1 条要求先把模型许可证原文、来源链接、
转换脚本、转换后模型 SHA-256 提交。本文件记录这一步的可复算证据；实现（摄像头/推理/校准）在后续
`E10:` 提交。

## 1. 许可（已确认宽松许可，无需 OPEN_QUESTIONS）

| 项目 | 值 |
|---|---|
| 模型 | MediaPipe Face Landmarker（BlazeFace short-range + FaceMesh V2 + blendshape 三网络 bundle） |
| 来源 | https://huggingface.co/litert-community/MediaPipe-FaceLandmarker |
| 修订 | `cfb66f449dc5cb9f8dada3d52174d4cdae1c9ef1` |
| 模型许可 | **Apache-2.0**：HF 模型卡 metadata `license: apache-2.0`，且仓库内逐字附带三份上游 Google model card PDF |
| 许可原文 | `Windows/licenses/MediaPipe-Face-Landmarker-LICENSE.txt`（apache.org 2.0 原文，SHA-256 `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`） |
| 上游 model card | `Windows/licenses/MediaPipe-BlazeFace-Model-Card.pdf`（`cd335c06…f758`）、`MediaPipe-FaceMesh-V2-Model-Card.pdf`（`c6add060…565a`）、`MediaPipe-Blendshape-V2-Model-Card.pdf`（`c8e9cf60…45ae`），与 HF README 公布值逐字节一致 |

## 2. 源文件（可下载、SHA 固定）

- `face_landmarker.task`：3,758,596 B，SHA-256 `64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff`
- 解包后子模型（完整值见 `numeric-alignment.json` 的 `manifest.submodels_sha256`）：
  - `face_detector.tflite` 229,746 B → SHA-256 `b4578f35940bf5a1a655214a1cce5cab13eba73c1297cd78e1a04c2380b0152f`
  - `face_landmarks_detector.tflite` 2,553,590 B → SHA-256 `c7d54204ce0448474c7f3fa9af494787c0965cbdd6f20fc72867e43046bd43d5`

## 3. 转换（一条命令可重生成）

```powershell
py -3.12 -m pip install -r Windows/scripts/face_landmarker_requirements.txt
python Windows/scripts/convert_face_landmarker.py
```

工具链固定在 `Windows/scripts/face_landmarker_requirements.txt`：Python 3.12、tflite2onnx 0.4.1、
onnx 1.23.2、onnxruntime 1.32.0、tensorflow-cpu 2.21.0、numpy 2.5.5。

**为什么需要补丁**：tflite2onnx 0.4.1 对 FaceMesh 图崩溃——该图的逐通道 PRelu `alpha` 是
`[1,1,C]`（无 batch 维），布局传播按 4 维 NHWC→NCHW 处理时 shape/权重转置抛
`IndexError` / `ValueError: axes don't match array`。脚本把这类 channel-last 逐通道参数重排为
ONNX NCHW 输入所需的 `[1,C,1,1]` 广播形状（本次 69 个，全部是 PRelu alpha）；其它秩不匹配的张量
记录在 manifest 的 `left_untouched`（本次为空）。补丁写在脚本里、warnings 明确，不是静默改模型。

**确定性**：脚本以 `PYTHONHASHSEED=0` 自重启（tflite2onnx 遍历 Python set，序列化字节受 hash
randomization 影响）。连续两次从同一 `.task` 重生成，两个 ONNX 的 SHA-256 完全一致；`--check`
只校验已提交文件与 `SHA256SUMS`，可离线复算。

转换产物：

| 文件 | 大小 | SHA-256 |
|---|---:|---|
| `Windows/assets/face-landmarker/face_detector.onnx` | 425,659 B | `fd2a4d2133b48a515bbfe248480cbcd7c9efa95d82dbf0d82bb99a5ae57efeb9` |
| `Windows/assets/face-landmarker/face_landmarks_detector.onnx` | 4,996,125 B | `f03920f259c58ea9695ff53698386f9b1c104bd22cbe8942626be5bda6b01d4c` |

## 4. 数值对齐（TFLite 参考运行时 vs ONNX Runtime）

固定随机图像（`numpy.default_rng(7) * 255`，ONNX 侧转 NCHW），逐输出比较：

| 模型/输出 | 元素数 | max_abs | max_rel | TFLite 输出范围 | 判定 |
|---|---:|---:|---:|---|---|
| face_detector / regressors | 14,336 | 0.390625 | 6.46e-2（近零值时） | -82,820 … 275,705 | 通过（容差 = 范围×1e-4） |
| face_detector / classificators | 896 | 0.75 | 7.91e-6 | -713,134 … -18.8 | 通过 |
| face_landmarks_detector / landmarks（1434=478×3） | 1,434 | 0.004639 | 1.26e-3（近零值时） | -4,140.5 … 1,130.1 | 通过 |
| face_landmarks_detector / face_presence | 1 | 0.000366 | 5.05e-7 | 724.967 | 通过 |
| face_landmarks_detector / tongue_out | 1 | 0 | 0 | 0 | 通过 |

源 bundle 是 **fp16 权重**，所以绝对误差在输出量级上是 1e-6～1e-5 相对量级（regressors 的
`max_rel` 大是因为输出里存在接近 0 的分量，绝对误差 0.39 相对 275k 量级仍是 1.4e-6）。完整机器可读
结果：`Windows/validation/e10/numeric-alignment.json`（含源/子模型/产物 SHA 与补丁统计）。

## 5. 运行时接口（给下一步实现）

- `face_detector.onnx`：输入 `input` NCHW `[1,3,128,128]` float32（0…255 像素尺度）；输出
  `regressors [1,896,16]`、`classificators [1,896,1]`。
- `face_landmarks_detector.onnx`：输入 `input_12` NCHW `[1,3,256,256]`；输出 `Identity [1,1434,1,1]`
  （478 点 × x,y,z，flatten 顺序与 TFLite 一致）、`Identity_1 [1,1,1,1]` presence、
  `Identity_2 [1,1]` tongue-out。

未做/下一步：native ONNX Runtime 包、MediaPipe geometry pipeline 的 C# 重写、640×480 内存帧采集、
GazeEstimator 接线、设置/校准 UI、三态自测证据——将在后续 `E10:` 提交完成。
