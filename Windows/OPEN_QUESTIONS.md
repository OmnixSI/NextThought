# 未决问题（OPEN_QUESTIONS）

按《收尾决定》（`C:\Users\Thunderobot\Downloads\WINDOWS_FINAL_DECISIONS.md`）§4，此前清单里的 12 条已全部裁决：

- 「接受」的差别 → `Windows/ACCEPTED_DIFFERENCES.md`（长期成立的平台差异）
- 「第二批」的差别 → `Windows/KNOWN_GAPS.md`（同时写进发布说明「与 Mac 版的已知差别」）
- 其余「修」的条目 → 已按页分配到修复任务并逐页更新 `compare.md` 结论

## 需要人或其他设备的事项（不阻塞 Windows 侧代码）

| 事项 | 说明 |
|---|---|
| 发布 | 由人执行 `Windows/RELEASE.md` 里的 `gh release create windows-v0.9.0 … --latest=false`；代理不创建标签/发布 |
| Mac 上导入 Windows 导出的备份 | 在 Mac 上用 NextThought 导入 `Windows/tests/Fixtures/win-export/all-scenarios-from-windows.ntbackup`，核对数量并逐场打开 |
| K11 干净系统首次引导 | 需要干净的 Windows 沙盒/新系统，本机无 Windows 沙盒且启用需要管理员权限 |
| K13 真实会议软件共享 | 需要第二台设备（Zoom/Teams/腾讯会议）确认对方看不到窗口 |
| K14 真实手机配对 | 需要真实手机（局域网与中转两种方式） |
| K15 多屏与缩放 | 需要多显示器/不同缩放比例环境 |
| K16 真实云端账号 | 需要真实识别/联网/回答服务账号的端到端验证 |
| E10 摄像头前的真人复测 | 真人复测看镜头/移开两态 |
| Mac `Verify` 工作流 `archive-ux` self-test 失败 | 发生在自托管 Mac runner 的 Mac 自检路径（`NSCocoaErrorDomain Code=4`），不在 Windows 代码内，需 Mac 维护者排查；不是 Windows 的阻塞项 |

以上事项已写进发布说明的「尚未实机验证」一节。
