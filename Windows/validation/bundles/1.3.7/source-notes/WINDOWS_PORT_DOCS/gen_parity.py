import pathlib, io
src = pathlib.Path(r"D:\NextThought开发\src\NextThought\Sources\NextThoughtCore")
out = pathlib.Path(r"D:\NextThought开发\src\NextThought\Windows\CORE_PARITY.md")
ported = {
 "SubtitleBar.swift": "Core/SubtitleBar.cs（12 项测试）",
 "AudioTimeline.swift": "Core/AudioTimeline.cs（10 项测试）",
 "TranscriptDeduplicator.swift": "Core/TranscriptDeduplicator.cs（8 项测试）",
 "EchoSuppression.swift": "Core/EchoSuppression.cs（8 项测试；已接入转写回声判定）",
 "MeetingEndWatch.swift": "Core/MeetingEndWatch.cs（8 项测试；已接入会议自动收尾）",
 "Scene.swift": "Core/Scene.cs（11 项测试；场景/提词/教练已接入界面）",
 "CoachSummary.swift": "Core/CoachSummary.cs（随 Scene 测试覆盖）",
 "StructuredNotes.swift": "Core/StructuredNotes.cs + StructuredNotesCleaner.cs（16 项测试；鱼骨图/大纲/观点对照已接入纪要页，含核对与丢弃）",
}
partial = {
 "LiveInsights.swift": "Core/AnswerPoints.cs（仅 Split / StripMarkup，供提词使用）",
 "NotesTemplates.swift": "Core/NotesTemplates.cs + NotesTime.cs + NotesDates.cs + TranscriptCorrections.cs（模板/解析/渲染/提示/校对/到期日）",
 "Workspace.swift": "Core/Workspace.cs（系列、经验与 workspace.json；知识库/候选人/合并未移植）",
 "Glossary.swift": "Core/Glossary.cs（9 项测试；规范写法已接入热词与转写纠正）",
 "UpdateCatalog.swift": "Core/UpdateCatalog.cs（7 项测试；设置页「检查更新」已接入）",
 "MeetingType.swift": "Core.MeetingTypeDefaults（场景 → 纪要模板与实时模式；自定义类型未移植）",
}
baseline = {
 "QuestionIntent.swift": "QuestionIntent.cs + QuestionCandidateTracker.cs + QuestionVocabulary.cs（精简重写）",
 "VoiceActivitySegmenter.swift": "VoiceActivitySegmenter.cs（简化版）",
 "SemanticRetrieval.swift": "DocumentChunks.cs + KeywordIndex.cs（简化版）",
 "PDFTextLayout.swift": "LayoutReadingOrder.cs（简化版）",
 "SessionRecord.swift": "Models.cs（只覆盖部分字段）",
 "SpeakerClustering.swift": "SpeakerFeatures.cs（仅特征）",
 "QuestionTurn.swift": "AutomaticAnswerScheduler.cs（部分）",
}
rows=[]; total=0
for f in sorted(src.glob("*.swift")):
    lines=len(io.open(f,encoding="utf-8",errors="ignore").read().splitlines()); total+=lines
    if f.name in ported: status="已移植"; note=ported[f.name]
    elif f.name in partial: status="部分移植"; note=partial[f.name]
    elif f.name in baseline: status="基线简化版"; note=baseline[f.name]
    else: status="未移植"; note=""
    rows.append((f.name,lines,status,note))
body="\n".join(f"| {n} | {l} | {s} | {note} |" for n,l,s,note in rows)
cp=sum(1 for r in rows if r[2]=="已移植"); cpa=sum(1 for r in rows if r[2]=="部分移植"); cb=sum(1 for r in rows if r[2]=="基线简化版")
header=f"""# Core 移植对照（CORE_PARITY）

来源 `Sources/NextThoughtCore`，共 {len(rows)} 个文件、约 {total:,} 行（含注释与提示词文本）。
状态含义：

- **已移植**：C# 侧逐文件移植，带 xunit 测试，行为与 Swift 对齐。
- **部分移植**：只移植了其它文件依赖的那几个函数，其余随该文件的完整移植补齐。
- **基线简化版**：基线 0.4.0 已有的 C# 实现，只覆盖一部分字段或规则，后续按方案 P1 被逐文件移植取代。
- **未移植**：尚无 C# 对应物。

| Swift 文件 | 行数 | 状态 | C# 对应 / 备注 |
| --- | ---: | --- | --- |
"""
summary=f"""
统计：已移植 {cp}，部分移植 {cpa}，基线简化版 {cb}，未移植 {len(rows)-cp-cpa-cb}。

测试：`tests/NextThought.Core.Tests` 116 项通过（SubtitleBar 12、AudioTimeline 10、
TranscriptDeduplicator 8、EchoSuppression 8、MeetingEndWatch 8、Scene/Coach 11、结构化纪要 16、Workspace 6、Glossary 9）。

已接入运行时：回声过滤、会议自动收尾、场景预设与提词、临场教练、结构化纪要
（鱼骨图 / 要点大纲 / 观点对照，含提示词、逐条核对丢弃与鱼骨图、观点矩阵绘制）、
系列与经验（workspace.json；系列页可新建系列、把会议归档进系列、记一句话经验）、\n术语表（正确写法作为识别热词，错写法在转写里被改回）、\n检查更新（正式/main 频道、语义版本比较、只提示并打开下载页）、\n模板纪要（九个模板、按场景默认、按模板整理并保存 summary.md）、\n转写校对（按行定位、修订指纹、原子重写 transcript.jsonl 并留 corrections.json）。

未接入/未移植的重点：`Workspace` 的知识库、变更候选与 `KnowledgeMerger`、
`SpeakerClustering`、双屏演讲、同传、应用内自动替换安装包。
"""
io.open(out,"w",encoding="utf-8",newline="\n").write(header+body+"\n"+summary)
print("ok", cp, cpa, cb, len(rows)-cp-cpa-cb)
