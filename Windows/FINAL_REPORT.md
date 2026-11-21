# NextThought Windows 修复任务 — 阶段报告

- 分支：`windows/parity`
- 提交：`93329d8`（本报告随后单独提交）
- 版本：0.5.145
- 构建：Release，**0 错误**（最后验证）
- 测试：Core **872/872**，NextThought.Tests **255/255**
- 截图：`--demo --screenshots` 全量 **96 张 PNG**（中/英 × 浅/深、会议 8 标签、设置 6 页、引导、提词岛）

## 已完成

H1、D2、D3、D4、D5、D7、D10、D11、F9、F17。

## 已实现、未验证

D1、D6、D8、E2、E3、E4、E5、E6、E7、E8、E11、E13、E14、E15、E16、E18、E20、E21、E22、E24、E25、E26、F1–F8、F10–F12、F14–F20。
限制均在 `Windows/PORTING_STATUS.md` 各条目中写明：主要是未与 Mac 逐像素对照、未实机验证硬件/云端链路。

## 阻塞（未做）

| 任务 | 阻塞原因 |
|---|---|
| H2 | 需要逐方法重命名/映射 179 个 Mac 扩展方法；当前 C# 方法名 0 个完全匹配，重命名会牵动大量 API 与测试 |
| A15 | MacCodec Swift 编译依赖 CryptoKit/sqlite3/CoreGraphics/Sherpa；官方 swift-crypto 需 Clang 22，本机 Clang 21 |
| E9 | 持续 ScreenWatch 缺少「截屏→系统 OCR」只读平台接口 |
| E10 | 看镜头缺少摄像头帧来源（MediaCapture 未实现） |
| E12 | 我的声音录入缺少 speaker embedding 模型/导出接口 |
| F13 | Windows BLE GATT 外设端未实现，手机端不在本仓库 |

详见 `Windows/OPEN_QUESTIONS.md`。

## 证据

- `Windows/PORTING_STATUS.md`：逐任务改动、测试与限制
- `Windows/FIX_TASKS.md`：状态列
- `Windows/validation/screens/`：页面/设置/会议标签/引导截图
- `Windows/validation/companion/COMPARISON.md`：伴侣对照表
- 每次提交只做一项或一组紧密相关任务，均有测试通过记录

## 结论

PDF 中可在此 Windows 环境完成并验证的 UI/行为项已实现并持续通过构建与测试；H2/A15/E9/E10/E12/F13 依赖 Mac 工具链、摄像头、屏幕 OCR、说话人 embedding 或 BLE 外设，属于本环境无法独立完成的阻塞项，未标记为完成。
