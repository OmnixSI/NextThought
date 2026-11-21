# K5 设置保存（本机实测）

- 日期：2026-11-21 · 版本：0.9.0（`windows/parity`）· 机器：Windows 11 10.0.26200
- 方法：`NextThought.exe --self-test=k5-settings` 在同一隔离数据目录（`NEXTTHOUGHT_DATA_DIR`）连续运行两次：
  - 第一次（`k5-set-run.json`，`phase=set`，passed）：按设置页同样的入口写入四项——
    快捷键 `record=Ctrl+Alt+F9`（走 `SaveShortcutsCommand`，settings.json 的 `shortcuts`）、
    上下文侧栏隐藏（`ContextSidebarHidden=true`，走 `Settings.SaveAsync()`）、
    提词透明度 `0.5`（Mac `teleprompterOpacity`，`WindowLayout.Save` 镜像到 settings.json）、
    文字缩放 `1.2`（`TextZoomService.SaveForCheck`，Mac `textZoom`）；
    再载入演示会话（`LoadDemo(Home)`）并再次保存设置，模拟「开一场会议后保存」。
  - 第二次进程（`k5-verify-run.json`，`phase=verify`，passed）：从磁盘读回四项全部一致（快捷键覆盖仍在、侧栏隐藏仍为真、透明度 0.5、textZoom 1.2）。
- 结果：**通过**，两阶段全部步骤 ok。
- 附加：`zoom-persistence.json` 是此前 `--self-test=zoom` 的独立文字缩放持久化记录。
- 留给人：真实录音会议中修改设置→结束会议→重启的完整链路（演示会话已覆盖设置保存与重启；真实会议需要音频设备与模型）；步骤见 `RELEASE.md §9 K5`。
