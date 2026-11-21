# E3 · 首次引导点按记录（第四轮 task-34 增补）

环境：`--demo`。

| 位置 | 操作 | 预期 | 观测 |
|---|---|---|---|
| 设置 → 通用 | 打开 | 出现「设置清单」卡（语音识别 / 回答引擎 / 当前场景 三行，状态点 + 说明 + 动作） | 卡在通用页，不再在高级页；行来自 `AppState.SetupChecklistRows` |
| 设置清单行 | 点击「下载/设置/编辑」 | 语音识别未就绪→下载模型；回答引擎→设置；当前场景→资料库 | `SetupChecklistActionCommand` |
| 场景页「看一段面试示例」 | 点击 | 展开示例面板：进度条启动，转写逐行出现（0.8/5/10.5/16/21/26.5/29.5 秒），笔记逐条出现（8.5~32 秒） | `OnboardingSampleVisibleLines/Takeaways`；34 秒后停止 |
| 示例面板「面试官在追问 · 参考回答」 | 等 13 秒 | 出现追问块（问题 + 参考回答） | `OnboardingSampleShowSuggestion` |
| 示例面板进度条 | 播放完 | 出现「重播」按钮 | `OnboardingSampleFinished`；点击从头播放 |
| 示例面板「收起示例」 | 点击 | 面板收起并停止计时 | `StopSample` |
| 关闭引导（稍后继续/完成） | 点击 | 回放停止；首页出现/清除 resume 行 | `OnOnboardingVisibleChanged` → `StopSample` |
| 步骤切换（上一步/下一步/跳过） | 点击 | 页面内容 460ms 淡入，内容不重叠 | `GuideMotion.PhaseIn`（Mac Motion.phase 近似） |
| 引导步骤 | 核对 | 场景/声音/回答/提词与开始四步文案、位置、预计时间、底部按钮 | `--demo --self-test onboarding` 通过 |
