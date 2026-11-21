# K18 文档组件真实下载 / 取消 / 网络中断 · 本机实测记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面（Session 1）
- 被测版本：`NextThought 0.9.0+…`（Release，`--self-test=document-download` 同一次构建）
- 组件：`DocumentAssets.Layout`（PP-DocLayoutV3，132,004,944 字节；文档管线最小的真实组件），走
  `DocumentAssets.InstallAsync`（HuggingFace 固定 revision 下载 + SHA-256 按文件校验）。
- 数据目录：所有下载/中断测试都在 `%TEMP%\nt-k18-*` 的一次性 `AppPaths` 里，不碰应用自己的数据和模型。

## 方法

应用内自检 `RunDocumentDownloadCheckAsync`（`--self-test=document-download`）：

1. **真实下载成功**：`InstallAsync(layout)` → `IsInstalled` → `VerifyAsync`（逐文件 SHA-256）。
2. **取消**：新目录，`CancellationTokenSource(900ms)` 中途取消，断言收到 `OperationCanceledException`
   且组件没有被当成已安装（部分文件不构成完整安装）。
3. **网络中断 + 重试**：新目录先在 900 ms 取消（模拟传输中断），确认未安装；随后**重新** `InstallAsync`
   成功，证明中断留下的状态不会阻止下一次完整下载/校验。

## 结果：通过

`report.json`：

- `modelBytes: 132004944`，`downloadSeconds: 46.1`，`installed: true`，`sha256Verified: true`；
- `cancelRequested: true`、`cancelObserved: true`、`cancelInstalled: false`；
- `interruptObserved: true`、`afterInterruptInstalled: false`、`retryInstalled: true`、`retrySeconds: 41.3`；
- `passed: true`，`failures: []`。

证据：`report.json`、`runner.out.txt`（`document-download-check: passed`）、`runner.err.txt`。

## 说明

- 「网络中断」用**传输中途取消**模拟（本机无法对固定 HuggingFace 地址做断网/不可达注入，且正式包没有
  改地址入口）；断言的是中断后的恢复语义：中断状态不显示已安装、重新下载能成功并通过 SHA-256。真实
  断网（拔网线/防火墙拒绝）留给人，可在同一自检上重跑（把取消改成断网即可）。
- 下载两次各 ~40–46 s（132 MB），成功与重试都做了完整 SHA-256 校验。
