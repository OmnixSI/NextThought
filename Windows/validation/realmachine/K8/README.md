# K8 手机页面（无头浏览器）本机实测记录

- 日期：2026-11-21
- 版本：`NextThought 0.9.0`（`windows/parity`，被测提交见 `POST /HEAD` 记录于 `test-output.txt` 同期构建）
- 机器：Windows 11 10.0.26200，交互式 Session 1
- 步骤：
  1. 确认 Edge 存在：`C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`。
  2. 运行 `dotnet test Windows/tests/NextThought.Tests -c Release --filter "FullyQualifiedName~CompanionPageTests|FullyQualifiedName~CompanionTests"`。
  3. `CompanionPageTests.PhonePageRendersTheSnapshotInChromium` 启动真实 `CompanionServer`（配对 URL）并用 Edge `--headless=new` + CDP 打开手机页面：断言页面出现问题/回答/状态文字，服务器推送更新后页面跟着更新（`更新问题`/`更新回答`），点击 `[data-action="previous"]` 后服务器收到动作。
  4. `CompanionTests.AuthCommandDedupSseReconnectRotationAndShutdownUseRealTcp` 用真实 TCP 覆盖鉴权、同一动作编号去重、SSE 重连、配对码轮换与关闭。
- 结果：**通过**，13/13（见 `test-output.txt`）。
- 证据：`test-output.txt`（完整测试输出）、本 README。
- 备注：这是无头浏览器自动化记录，不是真机手机记录；真机配对仍列在 `OPEN_QUESTIONS.md`（K13–K16 之一）。
