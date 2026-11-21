# K10 应用内更新（本机假发布源）· 本机实测记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880
- 依据：任务书 §10.5「在本机起一个假的发布源（只在测试工程里注入）」+ 裁决 §6
- 方法：假发布源由测试工程 `Windows/tests/NextThought.Tests/FakeReleaseDirectory.cs` 提供——它生成本地
  目录形状的一份发布（`releases.json` + `update.json` + `NextThought-1.0.0-windows-x64-setup.exe`
  + `.sha256`），更新流程通过正式代码的 `UpdateFeedPolicy` 读取，**不经过网络**，正式程序没有改地址入口。
- 执行：`dotnet test tests/NextThought.Tests --filter "UpdateInstallerTests|WindowsUpdateCatalogTests|UpdateFeedParityTests"`
- 结果：**9/9 通过**（`dotnet-test-update-flow.log`、`k10-update-flow.trx`）

## §10.5 各步骤与覆盖

| §10.5 步骤 | 覆盖测试 | 结果 |
|---|---|---|
| 假发布源里放 `windows-v0.9.1`、只认 Windows 标签+安装包+`update.json` | `WindowsUpdateCatalogTests.OnlyWindowsTaggedReleasesWithInstallerAndManifestAreCandidates`、`TheWindowsFeedOffersTheLatestWindowsReleaseOnly`、`WindowsVersionComesOnlyFromTheWindowsTagPrefix` | ✅ |
| 程序发现更新（后台检查） | `UpdateFeedParityTests.RollingReleaseIsFoundThroughItsManifestBuildNumber`、`ManifestMustDescribeTheRelease` | ✅ |
| 后台下载安装包 + SHA-256 校验通过后按钮出现 | `UpdateInstallerTests.InstallerIsDownloadedAndVerifiedBeforeTheButtonAppears` | ✅ |
| 安装包 SHA-256 错误 → 拒绝并删除，当前版本不受影响 | `UpdateInstallerTests.InstallerWithAWrongChecksumIsRejectedAndDeleted` | ✅ |
| 更新清单只从 Windows 发布读取 `asset` | `WindowsUpdateCatalogTests.TheManifestReadsTheInstallerAssetFieldFromTheWindowsRelease`、`TheAppExposesOnlyTheFormalWindowsChannel` | ✅ |

## 未覆盖（写明）

- 「点击重启以更新 → 真正运行安装包 → 重新打开后版本是 0.9.1、数据都在」：安装包是假的
  （测试工程只生成 payload 与 sha），本机没有第二份真实发布包可装；§10.5 的安装生命周期测试
  （`K9`）属发布脚本 `release.ps1` 的步骤，不在此处重复。上述覆盖到“校验通过→可安装/校验失败→拒绝”的
  边界，真正的 exe 覆盖安装与重启后数据保留需在发布批次用真实安装包做（R5/K9）。
- 应用内更新在 Release 安装包里的 QA 覆盖开关已被 `#if DEBUG` 去掉（R8），所以本项用测试工程注入的
  假发布源完成，符合任务书规定的「仅测试工程注入」。
