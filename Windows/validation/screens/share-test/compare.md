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
| 6 | 入口 | 设置「共享可见性测试」按钮 → runShareVisibilityTest | 提词与教练页「屏幕共享」下的「共享可见性测试」按钮打开本窗口（`RunShareVisibilityTestAsync` + `ShowDialog`） | 一致 |
| 7 | QRCodeView | QRCoder 内容 + padding 6、白底圆角 8、等比；Mac 用于配对地址（SettingsView.swift:547） | 配对卡使用 `Controls/QrCodeView`（168×168、Text=CompanionLink、白底圆角 8） | 一致 |

## 差异 / 未解决

无。入口与 QRCodeView 落位都在 E13/E28 完成；断连显示器下整屏抓取可能纯白属于抓取环境限制，自检只校验图像尺寸与存在性。

## 结论

逐条一致。
