# 共享可见性测试 + QRCodeView（E13 · §6.7）Mac 3.4.0 源码 ↔ Windows 逐元素对照

- Mac 基准：Mac 3.4.0 `f015e8c6`，`ContentView.swift`（ShareTestSheet / QRCodeView）、`AppState+Live.swift`（runShareVisibilityTest）
- Windows 代码：`Views/ShareTestWindow.axaml(.cs)`、`Views/Controls/QrCodeView.cs`、`AppState.ShareTest.cs`
- Windows 截图（本目录）：`share-test-light.png` / `share-test-dark.png`（640 宽 sheet）、`qr-code-light.png`（QRCodeView 白底圆角卡片）
- 证据生成：`--self-test share-test`（隔离数据目录）

| # | Mac 元素 | 值 | Windows 现状 | 结论 |
|---|---|---|---|---|
| 1 | sheet | 640 宽、padding 20、spacing 12 | 同（Width 640、Margin 20、Spacing 12、SizeToContent 高度） | 一致 |
| 2 | 标题 | 「共享可见性测试」zoomed headline | 同（label 15） | 一致 |
| 3 | 说明 | 长文案「下面是 ScreenCaptureKit 截取的整个屏幕…实际范围取决于会议软件和共享方式。」secondary | 同（Mac 原句 loc key、body secondary、换行） | 一致 |
| 4 | 截图 | shareTestImage = captureDisplay(hidingAppWindows:false)（含本应用浮窗），等比适配、maxHeight 420、圆角 8 | 同（ScreenCapture.VirtualScreen() 整屏含自身浮窗；Uniform、MaxHeight 420、圆角 8） | 一致 |
| 5 | 关闭 | 右下「关闭」primary、默认动作、清空 shareTestImage | 同（primary IsDefault、ClearShareTestImage） | 一致 |
| 6 | 入口 | 设置「共享可见性测试」按钮 → runShareVisibilityTest | Windows 入口待接（settings R 组由 settings-parity 收尾） | 部分 |
| 7 | QRCodeView | QRCoder 内容 + padding 6、白底圆角 8、等比；Mac 用于配对地址（SettingsView.swift:547） | `Views/Controls/QrCodeView.cs` 同（自检截图 qr-code-light.png） | 实现一致 |

## 差异 / 未解决

1. **入口按钮**：窗口与状态就绪；需 settings-parity 在设置页加一行「共享可见性测试」绑定 `RunShareVisibilityTestAsync` 并打开 `ShareTestWindow`（Lead 协调，避免我动 settings R 组）。
2. **QRCodeView 落位**：Mac 配对卡片用 168×168 QRCodeView，Windows 现有配对卡 `CompanionQr` 图片是同一内容；待 settings-parity 换用 QRCodeView。
3. 断连显示器会话下整屏抓取可能纯白，自检只校验图像尺寸/存在性。
