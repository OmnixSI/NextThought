# #79 Native AOT 实测报告

## 环境

- Windows 11 专业版 10.0.26200，Intel Core i9-14900HX（24C/32T）
- .NET SDK 10.0.403
- Visual Studio Build Tools 2026（18.12.1）
- MSVC 14.52.37025，链接器：`D:\APPs\VisualStudioBuildTools\VC\Tools\MSVC\14.52.37025\bin\Hostx64\x64\link.exe`
- 两个版本均 x64；普通版为 0.5.145 自包含 ReadyToRun，AOT 由 `PublishAot=true`、`PublishSingleFile=true` 发布，ILC 使用上述 MSVC 链接器。
- AOT 探针源码提交：`2e2011c`（UI 第一批之前；UI 改动不影响启动路径和 JSON 使用方式）。

## 表 A：普通自包含版（非 AOT）

| 运行 | 版本 | 配置 | 是否 AOT | 启动秒数 | 私有内存 MiB |
|---|---|---|---:|---:|---:|
| 1 | 0.5.145 | win-x64 self-contained ReadyToRun | 否 | 1.199 | 63.6 |
| 2 | 0.5.145 | win-x64 self-contained ReadyToRun | 否 | 1.197 | 63.7 |
| 3 | 0.5.145 | win-x64 self-contained ReadyToRun | 否 | 1.204 | 63.5 |
| 4 | 0.5.145 | win-x64 self-contained ReadyToRun | 否 | 1.187 | 63.7 |
| 5 | 0.5.145 | win-x64 self-contained ReadyToRun | 否 | 1.189 | 63.5 |
| **中位数** | 0.5.145 | self-contained ReadyToRun | 否 | **1.197** | **63.6** |

## 表 B：Native AOT 版

| 运行 | 版本 | 配置 | 是否 AOT | 启动秒数 | 私有内存 MiB |
|---|---|---|---:|---:|---:|
| 1 | 0.5.145 | win-x64 Native AOT single-file，`NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` | 是 | 0.635 | 57.3 |
| 2 | 0.5.145 | win-x64 Native AOT single-file，`NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` | 是 | 0.514 | 58.3 |
| 3 | 0.5.145 | win-x64 Native AOT single-file，`NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` | 是 | 0.517 | 57.5 |
| 4 | 0.5.145 | win-x64 Native AOT single-file，`NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` | 是 | 0.517 | 57.3 |
| 5 | 0.5.145 | win-x64 Native AOT single-file，`NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` | 是 | 0.516 | 57.5 |
| **中位数** | 0.5.145 | Native AOT single-file，禁用音频枚举 | 是 | **0.517** | **57.5** |

测法：同机、新进程、全新数据目录，每次启动自检 `--startup-check` 记录内部秒数与私有内存；进程间杀进程并间隔 700ms；没有清 OS 文件缓存。AOT 二进制 45.8 MB；启动自检 5/5 通过。

## 裁剪/AOT 分析

AOT 发布日志：`qa\aot-79\publish.log`。统计：

- IL2026（trim）**156** 条，IL3050（AOT）**161** 条，IL2050（COM）**1** 条，IL2075（反射字段）**1** 条。
- 去重到不同文件/行 = **163 个位点**。
- 与 `System.Text.Json`/JsonSerializer 相关的不同位点 **160** 个（多于“约 60 处”的早期估计）；完整清单见 `Windows/AOT_JSON_WARNINGS.csv`。

### 1. NAudio / COM（会崩）

- `ProcessLoopbackCapture.cs(82)`：IL2050，`ActivateAudioInterfaceAsync` 的 COM 封送在裁剪后不保证正确。
- ILC 直接报告：`[NAudio.Wasapi]NAudio.CoreAudioApi.Interfaces.MMDeviceEnumeratorComObject..ctor()` **will always throw**（Invalid IL/CLR metadata）。
- 结论：**NAudio 设备枚举在 Native AOT 下不可用**。AOT 只能用 `NEXTTHOUGHT_DISABLE_AUDIO_DEVICES=1` 启动自检；不能作为正式音频发布包。

### 2. Utf8SherpaInterop 反射（未证明安全）

- `Utf8SherpaInterop.cs(19)`：IL3050，`Marshal.SizeOf(Type)` 需要动态代码。
- `Utf8SherpaInterop.cs(26)`：IL2075，`Type.GetFields(BindingFlags)` 要求 `PublicFields` 元数据保留。
- 结论：**AOT 下未通过安全检查**。当前 AOT 探针禁用了音频，没有真正创建 Sherpa 识别器；一旦走模型加载，存在 `NotSupportedException`/字段元数据被裁剪后布局错误的风险。**没有实测通过，不能声称可用。**

### 3. System.Text.Json 反射（已实测一处失败）

- 160 个不同位点（`JsonSerializer.Serialize/Deserialize<T>`、`JsonArray`、`JsonDocument` 等）；完整清单见 CSV。
- 启动自检使用源生成的 `StartupJsonContext`，5/5 通过；这不代表其它 JSON 路径安全。
- **实测 AOT `--backup-check`：失败（0/1）**。`IncrementalBackupStore` 在写/读恢复点时调用反射式 `JsonSerializer`，AOT 下失败；备份检查目录只留下 Objects，没有写出报告。
- 结论：**未改用源生成 JSON 的路径在 AOT 下不能认为可用**；至少增量备份链 JSON 路径会失败。

## 本轮通过数

| 项目 | 结果 |
|---|---|
| 自包含版冷启动自检 | **5/5 通过**，中位数 1.197 秒 / 63.6 MiB |
| AOT 版冷启动自检 | **5/5 通过**，中位数 0.517 秒 / 57.5 MiB |
| AOT 备份 JSON 检查 | **0/1 通过（失败）** |
| AOT 正式音频功能 | **不可用**（NAudio 设备枚举 will always throw） |

