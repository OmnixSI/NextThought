# K7 两小时长会议 / K6 采音识别 · 运行记录

- 日期：2026-11-21
- 系统：Windows 11（10.0.26200），主屏 1488×880，交互式桌面（Session 1）
- 启动脚本：`Windows/scripts/realmachine/k7-long-meeting.ps1`
- 被测版本：`NextThought 0.9.0+…`（Release 自包含 exe，来自同一次最终构建）
- 数据目录：隔离 `%TEMP%\nt-k7-*`，`models/`、`runtimes/` 用 junction 复用本机
  `%LOCALAPPDATA%\NextThought` 里的模型（识别模型：sherpa-onnx X-ASR 480ms streaming + 整句精修，
  说话人模型 wespeaker）；`settings.json` 关闭麦克风（`microphoneEnabled=false`）与自动回答，
  只录系统声音。
- 已知语音：`known-speech.wav`（SAPI 中文语音 Huihui 合成，5 句：
  「这是长会议测试的第一句…」「第二句：缓存命中率目标是百分之九十二。」「第三句：灰度期间比较
  P95 与错误率。」「第四句：会后补一张读写时序图。」「最后一句：这是测试的最后一句。」），
  通过默认扬声器循环播放。

## 状态：运行中（2 小时）

- 启动：`2026-11-21 16:19:07`；开始记录：`16:19:21`（会话目录已创建，`Ctrl+Alt+M` 生效）。
- 第 1 分钟采样：工作集 909.8 MB，转写 11 行，`transcript.jsonl` 2.9 KB，持续增长。
- **K6 的 60 秒证据已经拿到**（同一次录制）：第 1 分钟转写里命中
  「缓存 / 命中率 / 灰度 / 时序图」四个关键词 → `K6/report-part1.json`、`K6/transcript-first-60s.txt`。
  K6 的第二部分（停止录音后最后一句仍在转写）会在 2 小时结束、脚本停止录音后写入
  `K6/report.json`。
- 每分钟采样写入 `memory.csv`（minute, workingSetMB, privateMB, transcriptLines, sessionBytes）、
  每份快照写 `report-partial.json`；2 小时结束时脚本写最终 `report.json`（含内存趋势、
  转写停顿样本、会话可读性、失败原因）。
- 判据：应用全程存活；结束采样 ≥ 119/120；最终转写 > 0 且没有超过 3 个连续停顿样本；末三分之一
  内存均值 / 首三分之一 < 1.5；结束后 `meta.json` 与 `transcript.jsonl` 可读。

## 说明

- 本项与 K6 共用一次运行：K6 需要「已知语音 60 秒 + 停止后最后一句仍在转写」，正好由这次 2 小时
  录制的前 60 秒快照与停止后检查给出；两者各自写自己的报告目录。
- 运行结束后应重新执行一次脚本即可完整复现（`known-speech.wav` 已入库，脚本会补生成）。

## 运行事故记录（2026-11-21）

- 第 1 次：16:19 起跑，第 7 分钟应用被共享机的 host-wide kill 清扫杀掉（采样 6/120）。
- 第 2 次：16:25 修复收尾分析后重跑；应用后来再次被杀，runner 自动重启继续到 17:12。
- 第 3 次期间：Lead 误杀 NTK7.exe 一次（原意只杀 NextThought.exe），runner 进程本身被终止；随后清理了重复 runner。
- **最终单实例运行**：17:20:35 启动、17:20:50 开始录制（数据目录 nt-k7-4dd5564b），预计 **19:21** 写终报；README 与 report.json 以此轮为准，前几轮仅作事故记录。

## 最终成功运行（2026-11-21 19:21）

- 运行：17:20:35 启动、17:20:50 开始录制，120 分钟、120 次采样；私有 `NTK7.exe` + 自动重启保护。
- 结果：**passed=true，failures=[]**；工作集首三分位 930.7 MB、末三分位 927.5 MB（ratio 1，无持续上涨）；最长停顿 0 采样；转写 **1,391 行**；会话目录可重新打开。
- K6 同一次运行的 60 秒快照与停止检查均通过（关键词命中、最后一句仍在转写）。
- 证据：`report.json`、`memory.csv`（逐分钟）、`runner.log`、`transcript-final.txt`（K6 目录）。
