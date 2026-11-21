# E10 验证夹具

`portrait.jpg` 只用于 `--self-test=coach-gaze` 的图片路径（看镜头 / 移开视线两态），不随应用分发。

- 来源：MediaPipe 官方示例资产 <https://storage.googleapis.com/mediapipe-assets/portrait.jpg>
- 大小/SHA-256：176,561 B / `a6f11efaa834706db23f275b6115058fa87fc7f14362681e6abe14e82749de3e`
- 用途：喂给与生产相同的 `FaceLandmarker`（ONNX，Apache-2.0 转换产物）→ `GazeEstimator` 路径，
  记录 `Windows/validation/e10/coach-gaze.md` 中的三态判定。
