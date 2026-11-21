# K11 干净系统首次引导（本机无法执行，留给人）

- 日期：2026-11-21 · 版本：0.9.0（`windows/parity`）
- 尝试与事实：
  1. `C:\Windows\System32\WindowsSandbox.exe` 不存在（`Test-Path` = False）。
  2. `dism /online /Get-FeatureInfo /FeatureName:Containers-DisposableClientVM` 返回 **Error: 740 — Elevated permissions are required to run DISM**，当前会话无法查询/启用 Windows 沙盒功能。
  3. 因此本机不能创建干净 Windows 环境做首次引导检查。
- 按任务书 §9 K11「没有沙盒则写明并留给人」处理：步骤已写进 `Windows/RELEASE.md §9 K11`（干净系统安装 → 走完首次引导 → 检查衬线标题与中文字体 → 记录 `validation/realmachine/K11/`）。
- 结论：**留给人**，K11 不标已验证。
