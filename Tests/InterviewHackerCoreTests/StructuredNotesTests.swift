import Foundation
import Testing
@testable import InterviewHackerCore

/// Notes formats: fishbone, outline and viewpoint matrix (parsing, check step, Markdown, prompts, storage).
struct StructuredNotesTests {
    private func clean(_ payload: StructuredNotesPayload, offsets: [Int] = [], duration: TimeInterval = 600) -> StructuredNotesPayload? {
        StructuredNotesCleaner.clean(payload, lineOffsets: offsets, duration: duration)
    }

    // MARK: Formats

    @Test func everyTypeHasARecommendedFormat() {
        #expect(NotesFormat.recommended(for: .lesson) == .outline)
        #expect(NotesFormat.recommended(for: .labMeeting) == .fishbone)
        #expect(NotesFormat.recommended(for: .discussion) == .viewpoints)
        for mode in [SessionMode.meeting, .client, .academic, .general, .interview, .custom] {
            #expect(NotesFormat.recommended(for: mode) == .standard, "\(mode)")
        }
        // The user's choice wins; an unknown or missing one falls back to the recommendation.
        #expect(NotesFormat.resolve(for: .lesson, overrides: ["lesson": "standard"]) == .standard)
        #expect(NotesFormat.resolve(for: .meeting, overrides: ["meeting": "fishbone"]) == .fishbone)
        #expect(NotesFormat.resolve(for: .lesson, overrides: ["lesson": "mindmap"]) == .outline)
        #expect(NotesFormat.resolve(for: .discussion, overrides: [:]) == .viewpoints)
        #expect(!NotesFormat.standard.isStructured && NotesFormat.fishbone.isStructured)
    }

    @Test func unknownFormatReadsAsStandard() throws {
        #expect(try JSONDecoder().decode(NotesFormat.self, from: Data(#""outline""#.utf8)) == .outline)
        #expect(try JSONDecoder().decode(NotesFormat.self, from: Data(#""mindmap""#.utf8)) == .standard)
        let all = try JSONDecoder().decode([NotesFormat].self, from: JSONEncoder().encode(NotesFormat.allCases))
        #expect(all == NotesFormat.allCases)
    }

    // MARK: Lenient parsing

    @Test func parsesFishboneInsideFencesAndTalk() throws {
        let reply = """
        好的，下面是鱼骨图：
        ```json
        {"head": "上线延期", "categories": [
          {"name": "人员", "causes": [{"text": "后端缺人", "time": "02:10"}, "测试排期冲突 [05:30]"]},
          {"name": "流程", "causes": [{"text": "评审太晚", "time": 400}]},
        ]}
        ```
        以上，供参考。
        """
        let payload = try #require(StructuredNotesParser.parse(reply, format: .fishbone))
        #expect(payload.format == .fishbone && payload.language == "zh")
        let fishbone = try #require(payload.fishbone)
        #expect(fishbone.head == "上线延期")
        #expect(fishbone.categories.map(\.name) == ["人员", "流程"])
        #expect(fishbone.categories[0].causes == [TimedText(text: "后端缺人", at: 130), TimedText(text: "测试排期冲突", at: 330)])
        #expect(fishbone.categories[1].causes == [TimedText(text: "评审太晚", at: 400)])
    }

    @Test func timesComeInManyShapes() {
        #expect(StructuredNotesParser.seconds("12:41") == 761)
        #expect(StructuredNotesParser.seconds("[12:41]") == 761)
        #expect(StructuredNotesParser.seconds("1:02:05") == 3725)
        #expect(StructuredNotesParser.seconds("12:41-13:05") == 761)
        #expect(StructuredNotesParser.seconds("761") == 761)
        #expect(StructuredNotesParser.seconds(NSNumber(value: 761)) == 761)
        #expect(StructuredNotesParser.seconds("稍后") == nil)
        #expect(StructuredNotesParser.seconds(nil) == nil)
    }

    @Test func missingPiecesDegradeInsteadOfFailing() throws {
        // No head: the fallback title. No category name: "其他" / "Other". Plain strings have no time.
        let reply = #"{"categories": [{"causes": ["资源不足", {"text": "沟通少", "at": "03:00"}]}, {"name": "空的", "causes": []}, "oops"]}"#
        let zh = try #require(StructuredNotesParser.parse(reply, format: .fishbone, fallbackTitle: "项目复盘"))
        #expect(zh.fishbone?.head == "项目复盘")
        #expect(zh.fishbone?.categories.count == 1)
        #expect(zh.fishbone?.categories.first?.name == "其他")
        #expect(zh.fishbone?.categories.first?.causes == [TimedText(text: "资源不足", at: nil), TimedText(text: "沟通少", at: 180)])
        let en = try #require(StructuredNotesParser.parse(reply, format: .fishbone, english: true))
        #expect(en.language == "en" && en.fishbone?.categories.first?.name == "Other")
        // A branch written flat (label + detail) becomes a one-cause category.
        let flat = try #require(StructuredNotesParser.parse(#"{"question": "为什么慢", "branches": [{"label": "数据库", "detail": "缺索引", "t": 90}]}"#, format: .fishbone))
        #expect(flat.fishbone?.head == "为什么慢")
        #expect(flat.fishbone?.categories == [FishboneCategory(name: "数据库", causes: [TimedText(text: "缺索引", at: 90)])])
    }

    @Test func unreadableRepliesGiveNil() {
        #expect(StructuredNotesParser.parse("抱歉，我无法完成。", format: .fishbone) == nil)
        #expect(StructuredNotesParser.parse("{}", format: .fishbone) == nil)
        #expect(StructuredNotesParser.parse(#"{"head": "x", "categories": []}"#, format: .fishbone) == nil)
        #expect(StructuredNotesParser.parse("{ 这不是 JSON ", format: .fishbone) == nil)
        #expect(StructuredNotesParser.parse(#"{"head": "x"}"#, format: .standard) == nil)
        // Braces inside strings do not confuse the scan.
        let tricky = #"前言 {"head": "含}括号", "categories": [{"name": "甲", "causes": [{"text": "a{b", "time": "00:30"}]}]} 后记 {x}"#
        #expect(StructuredNotesParser.parse(tricky, format: .fishbone)?.fishbone?.head == "含}括号")
    }

    @Test func aWrapperKeyIsUnwrapped() throws {
        let reply = #"{"fishbone": {"head": "H", "categories": [{"name": "甲", "causes": [{"text": "a", "time": "00:30"}]}]}}"#
        let payload = try #require(StructuredNotesParser.parse(reply, format: .fishbone))
        #expect(payload.fishbone?.head == "H")
    }

    @Test func parsesOutlineAndViewpoints() throws {
        let outlinePayload = try #require(StructuredNotesParser.parse("""
        {"sections": [{"title": "缓存", "time": "01:00", "points": [
            {"text": "击穿与穿透", "time": "01:15", "children": [{"text": "击穿是热点失效", "time": "01:30"}, "雪崩是批量失效"]},
            "TTL 的设置 [02:00]"]}],
         "concepts": [{"term": "TTL", "meaning": "过期时间", "time": "02:00"}, {"term": "无含义"}],
         "review": [{"text": "复习布隆过滤器", "time": "30:05"}]}
        """, format: .outline))
        let outline = outlinePayload.outline
        let section = try #require(outline?.sections.first)
        #expect(section.title == "缓存" && section.at == 60)
        #expect(section.points.map(\.text) == ["击穿与穿透", "TTL 的设置"])
        #expect(section.points[0].children == [TimedText(text: "击穿是热点失效", at: 90), TimedText(text: "雪崩是批量失效", at: nil)])
        #expect(section.points[1].at == 120)
        #expect(outline?.concepts == [OutlineConcept(term: "TTL", meaning: "过期时间", at: 120)])
        #expect(outline?.review == [TimedText(text: "复习布隆过滤器", at: 1805)])

        let matrixPayload = try #require(StructuredNotesParser.parse("""
        {"topic": "是否上线", "parties": ["小王", {"name": "小李"}],
         "issues": [{"title": "时间", "positions": [{"party": "小王", "text": "下周上线", "time": "01:40"}, {"party": "小李", "text": "", "time": "02:00"}]}],
         "consensus": [{"text": "先灰度", "time": "03:20"}], "disagreements": ["发布时间 [03:50]"],
         "owners": [{"who": "小王", "task": "写方案", "due": "周四", "time": "03:40"}, {"who": "小李"}]}
        """, format: .viewpoints))
        let matrix = matrixPayload.viewpoints
        #expect(matrix?.topic == "是否上线" && matrix?.parties == ["小王", "小李"])
        #expect(matrix?.issues == [ViewpointIssue(title: "时间", positions: [ViewpointPosition(party: "小王", text: "下周上线", at: 100)])])
        #expect(matrix?.consensus == [TimedText(text: "先灰度", at: 200)])
        #expect(matrix?.disagreements == [TimedText(text: "发布时间", at: 230)])
        #expect(matrix?.owners == [ViewpointOwner(who: "小王", task: "写方案", due: "周四", at: 220)])
    }

    @Test func theSchemaInThePromptIsReadableByTheParser() throws {
        for format in [NotesFormat.fishbone, .outline, .viewpoints] {
            let payload = StructuredNotesParser.parse(StructuredNotesPrompt.schema(format), format: format)
            #expect(payload?.hasContent == true, "\(format)")
        }
        #expect(StructuredNotesParser.parse(StructuredNotesPrompt.schema(.fishbone), format: .fishbone)?.fishbone?.categories.first?.causes.first?.at == 761)
    }

    // MARK: Check step

    @Test func unsupportedNodesAreDropped() throws {
        let raw = StructuredNotesPayload(format: .fishbone, fishbone: FishboneNotes(head: "上线延期", headAt: 5_000, categories: [
            FishboneCategory(name: "人员", causes: [
                TimedText(text: "后端缺人", at: 130),
                TimedText(text: "后端缺人！", at: 140),          // the same cause again: kept once, not counted as dropped
                TimedText(text: "没有时间点的说法", at: nil),      // dropped
                TimedText(text: "太远的时间点", at: 5_000),        // dropped: after the meeting
            ]),
            FishboneCategory(name: "人员", causes: [TimedText(text: "测试排期冲突", at: 330)]),  // same category: merged
            FishboneCategory(name: "流程", causes: [TimedText(text: "评审太晚", at: 400)]),
            FishboneCategory(name: "空类别", causes: [TimedText(text: "无依据", at: 10_000)]),   // dropped, and the category with it
        ]), model: "m")
        let cleaned = try #require(clean(raw, offsets: [130, 330, 400]))
        let fishbone = try #require(cleaned.fishbone)
        #expect(fishbone.head == "上线延期" && fishbone.headAt == nil)
        #expect(fishbone.categories.map(\.name) == ["人员", "流程"])
        #expect(fishbone.categories[0].causes == [TimedText(text: "后端缺人", at: 130), TimedText(text: "测试排期冲突", at: 330)])
        #expect(fishbone.categories[1].causes == [TimedText(text: "评审太晚", at: 400)])
        #expect(cleaned.droppedCount == 3)
        #expect(cleaned.model == "m" && cleaned.failed == nil)
    }

    @Test func aTimeFarFromEveryLineHasNoBacking() {
        // Same rule as the template notes: within 45 s of a transcript line.
        #expect(NotesVerifier.isSupported(100, lineOffsets: [], duration: 50))        // inside duration + 90
        #expect(!NotesVerifier.isSupported(200, lineOffsets: [], duration: 50))
        #expect(!NotesVerifier.isSupported(100, lineOffsets: [160], duration: 600))   // 60 s away
        #expect(NotesVerifier.isSupported(100, lineOffsets: [160], duration: 600, tolerance: 60))
        let raw = StructuredNotesPayload(format: .fishbone, fishbone: FishboneNotes(head: "h", categories: [
            FishboneCategory(name: "甲", causes: [TimedText(text: "a", at: 130), TimedText(text: "b", at: 200), TimedText(text: "c", at: 400)]),
        ]))
        let cleaned = clean(raw, offsets: [130, 330, 400])
        #expect(cleaned?.fishbone?.categories.first?.causes.map(\.text) == ["a", "c"])
        #expect(cleaned?.droppedCount == 1)
    }

    @Test func tooLittleLeftMeansNoDiagram() {
        let one = StructuredNotesPayload(format: .fishbone, fishbone: FishboneNotes(head: "h", categories: [
            FishboneCategory(name: "甲", causes: [TimedText(text: "a", at: 10)]),
        ]))
        #expect(clean(one) == nil)
        let none = StructuredNotesPayload(format: .fishbone, fishbone: FishboneNotes(head: "h", categories: [
            FishboneCategory(name: "甲", causes: [TimedText(text: "a", at: nil), TimedText(text: "b", at: nil)]),
        ]))
        #expect(clean(none) == nil)
        #expect(clean(StructuredNotesPayload(format: .fishbone)) == nil)
        #expect(clean(StructuredNotesPayload(format: .standard)) == nil)
    }

    @Test func sizesAreCapped() throws {
        let many = (0..<10).map { TimedText(text: "原因\($0)", at: $0 * 10) }
        let one = StructuredNotesPayload(format: .fishbone, fishbone: FishboneNotes(head: String(repeating: "长", count: 100), categories: [
            FishboneCategory(name: "A", causes: many),
            FishboneCategory(name: "B", causes: [TimedText(text: "另一个", at: 50)]),
        ]))
        let cleaned = try #require(clean(one))
        #expect(cleaned.fishbone?.categories[0].causes.count == StructuredNotesLimits.causesPerCategory)
        #expect(cleaned.fishbone?.head.count == StructuredNotesLimits.headCharacters)
        #expect(cleaned.fishbone?.head.hasSuffix("…") == true)

        let wide = StructuredNotesPayload(format: .fishbone, fishbone: FishboneNotes(head: "h", categories: (0..<9).map {
            FishboneCategory(name: "类\($0)", causes: [TimedText(text: "甲\($0)", at: 10), TimedText(text: "乙\($0)", at: 20)])
        }))
        let narrowed = try #require(clean(wide))
        #expect(narrowed.fishbone?.categories.count == StructuredNotesLimits.categories)
    }

    @Test func outlineIsCheckedAndBounded() throws {
        let raw = StructuredNotesPayload(format: .outline, outline: OutlineNotes(
            sections: [
                OutlineSection(title: "缓存", at: 99_999, points: [
                    OutlinePoint(text: "击穿与穿透", at: 75, children: [TimedText(text: "击穿是热点失效", at: 90), TimedText(text: "没依据", at: nil)]),
                    OutlinePoint(text: "没有时间", at: nil),
                ]),
                OutlineSection(title: "", at: nil, points: [OutlinePoint(text: "TTL", at: 120)]),
                OutlineSection(title: "全被丢掉", at: 10, points: [OutlinePoint(text: "无依据", at: 50_000)]),
            ],
            concepts: [OutlineConcept(term: "TTL", meaning: "过期时间", at: 120), OutlineConcept(term: "ttl", meaning: "重复", at: 121),
                       OutlineConcept(term: "X", meaning: "无依据", at: nil)],
            review: [TimedText(text: "复习布隆过滤器", at: 300)]))
        let cleaned = try #require(clean(raw))
        let outline = try #require(cleaned.outline)
        #expect(outline.sections.count == 2)
        #expect(outline.sections[0].title == "缓存")
        #expect(outline.sections[0].at == 75)                                   // the section's own time was unsupported: first point's time
        #expect(outline.sections[0].points.map(\.text) == ["击穿与穿透"])
        #expect(outline.sections[0].points[0].children == [TimedText(text: "击穿是热点失效", at: 90)])
        #expect(outline.sections[1].title == "第 2 部分")
        #expect(outline.concepts == [OutlineConcept(term: "TTL", meaning: "过期时间", at: 120)])
        #expect(outline.review == [TimedText(text: "复习布隆过滤器", at: 300)])
        // dropped: 没依据 child, 没有时间 point, 无依据 point, X concept = 4
        #expect(cleaned.droppedCount == 4)
        #expect(cleaned.fishbone == nil && cleaned.viewpoints == nil)

        let big = OutlineNotes(sections: (0..<14).map { section in
            OutlineSection(title: "章\(section)", at: nil, points: (0..<9).map { OutlinePoint(text: "点\(section)-\($0)", at: 10) })
        })
        let boundedPayload = try #require(clean(StructuredNotesPayload(format: .outline, outline: big)))
        let bounded = boundedPayload.outline
        #expect(bounded?.sections.count == StructuredNotesLimits.sections)
        #expect(bounded?.sections.allSatisfy { $0.points.count <= StructuredNotesLimits.pointsPerSection } == true)
        let nodes = bounded?.sections.reduce(0) { $0 + $1.points.count } ?? 0
        #expect(nodes <= StructuredNotesLimits.outlineNodes)
    }

    @Test func viewpointMatrixKeepsOnlySidesThatSpoke() throws {
        let raw = StructuredNotesPayload(format: .viewpoints, viewpoints: ViewpointMatrix(
            topic: "是否上线", parties: ["小王", "小李", "老张"],
            issues: [
                ViewpointIssue(title: "时间", positions: [
                    ViewpointPosition(party: "小王", text: "下周上线", at: 100),
                    ViewpointPosition(party: "小李", text: "再等两周", at: 160),
                    ViewpointPosition(party: "小赵", text: "无所谓", at: 170),   // not in parties: added
                ]),
                ViewpointIssue(title: "范围", positions: [ViewpointPosition(party: "小王", text: "先做核心", at: nil)]),  // dropped
            ],
            consensus: [TimedText(text: "先灰度", at: 200)],
            owners: [ViewpointOwner(who: "小王", task: "写方案", due: "周四", at: 220), ViewpointOwner(who: "小李", task: "没依据", at: nil)]))
        let cleaned = try #require(clean(raw))
        let matrix = try #require(cleaned.viewpoints)
        #expect(matrix.parties == ["小王", "小李", "小赵"])          // 老张 never spoke: no column
        #expect(matrix.issues.map(\.title) == ["时间"])
        #expect(matrix.positions(issue: matrix.issues[0], party: "小李") == [ViewpointPosition(party: "小李", text: "再等两周", at: 160)])
        #expect(matrix.consensus == [TimedText(text: "先灰度", at: 200)])
        #expect(matrix.owners == [ViewpointOwner(who: "小王", task: "写方案", due: "周四", at: 220)])
        #expect(cleaned.droppedCount == 2)

        let crowd = ViewpointMatrix(topic: "t", parties: (0..<7).map { "人\($0)" }, issues: [
            ViewpointIssue(title: "议题", positions: (0..<7).map { ViewpointPosition(party: "人\($0)", text: "观点\($0)", at: 10) }),
        ])
        let limitedPayload = try #require(clean(StructuredNotesPayload(format: .viewpoints, viewpoints: crowd)))
        let limited = limitedPayload.viewpoints
        #expect(limited?.parties.count == StructuredNotesLimits.parties)
        #expect(limited?.issues.first?.positions.count == StructuredNotesLimits.parties)
    }

    // MARK: Markdown

    @Test func rendersFishboneMarkdown() {
        let fishbone = FishboneNotes(head: "上线延期", categories: [
            FishboneCategory(name: "人员", causes: [TimedText(text: "后端缺人", at: 130), TimedText(text: "测试排期冲突", at: 330)]),
            FishboneCategory(name: "流程", causes: [TimedText(text: "评审太晚", at: 400)]),
        ])
        let zh = StructuredNotesMarkdown.render(StructuredNotesPayload(format: .fishbone, fishbone: fishbone))
        #expect(zh == "## 因果鱼骨图：上线延期\n\n### 人员\n- 后端缺人 [02:10]\n- 测试排期冲突 [05:30]\n\n### 流程\n- 评审太晚 [06:40]\n")
        let en = StructuredNotesMarkdown.render(StructuredNotesPayload(format: .fishbone, fishbone: fishbone, language: "en"))
        #expect(en.hasPrefix("## Cause-and-effect fishbone: 上线延期\n\n### 人员\n- 后端缺人 [02:10]"))
    }

    @Test func rendersOutlineMarkdown() {
        let outline = OutlineNotes(
            sections: [OutlineSection(title: "缓存", at: 60, points: [
                OutlinePoint(text: "击穿与穿透的区别", at: 75, children: [TimedText(text: "击穿是热点失效", at: 90)]),
            ])],
            concepts: [OutlineConcept(term: "TTL", meaning: "过期时间", at: 120)],
            review: [TimedText(text: "复习布隆过滤器", at: 3_725)])
        let text = StructuredNotesMarkdown.render(StructuredNotesPayload(format: .outline, outline: outline))
        #expect(text == "## 要点大纲\n\n### 1. 缓存 [01:00]\n- 击穿与穿透的区别 [01:15]\n  - 击穿是热点失效 [01:30]\n\n### 关键概念\n- **TTL**：过期时间 [02:00]\n\n### 待复习\n- 复习布隆过滤器 [1:02:05]\n")
    }

    @Test func rendersViewpointMarkdown() {
        let matrix = ViewpointMatrix(
            topic: "是否上线", parties: ["小王", "小李"],
            issues: [ViewpointIssue(title: "时间", positions: [
                ViewpointPosition(party: "小李", text: "再等两周", at: 160),
                ViewpointPosition(party: "小王", text: "下周上线", at: 100),
            ])],
            consensus: [TimedText(text: "先灰度", at: 200)],
            disagreements: [TimedText(text: "发布时间", at: 230)],
            owners: [ViewpointOwner(who: "小王", task: "写方案", due: "周四", at: 220)])
        let text = StructuredNotesMarkdown.render(StructuredNotesPayload(format: .viewpoints, viewpoints: matrix))
        // Columns follow the order of `parties`, not the order the sides spoke in.
        #expect(text == "## 观点对照：是否上线\n\n### 时间\n- **小王**：下周上线 [01:40]\n- **小李**：再等两周 [02:40]\n\n### 共识\n- 先灰度 [03:20]\n\n### 分歧\n- 发布时间 [03:50]\n\n### 分工\n- 小王 · 写方案 · 周四 [03:40]\n")
    }

    @Test func emptyOrFailedPayloadsRenderNothing() {
        #expect(StructuredNotesMarkdown.render(.plain(english: false)) == "")
        #expect(StructuredNotesMarkdown.render(.failure(format: .fishbone, english: false)) == "")
        #expect(!StructuredNotesPayload.failure(format: .outline, english: true).hasContent)
        #expect(StructuredNotesPayload.failure(format: .outline, english: true).failed == true)
    }

    // MARK: Prompts

    @Test func promptsNameTheSchemaInBothLanguages() {
        for format in [NotesFormat.fishbone, .outline, .viewpoints] {
            let zh = StructuredNotesPrompt.system(format: format, english: false)
            let en = StructuredNotesPrompt.system(format: format, english: true)
            #expect(zh.contains(StructuredNotesPrompt.schema(format)) && en.contains(StructuredNotesPrompt.schema(format)), "\(format)")
            #expect(zh.contains("简体中文") && zh.contains("time") && zh.contains(format.title(english: false)), "\(format)")
            #expect(en.contains("English") && en.contains("time") && en.contains(format.title(english: true)), "\(format)")
            #expect(!en.contains("简体中文"), "\(format)")
        }
        #expect(StructuredNotesPrompt.system(format: .fishbone, english: false).contains("categories"))
        #expect(StructuredNotesPrompt.system(format: .outline, english: true).contains("sections"))
        #expect(StructuredNotesPrompt.system(format: .viewpoints, english: false).contains("positions"))
        #expect(StructuredNotesPrompt.system(format: .standard, english: false) == "")
        #expect(StructuredNotesPrompt.schema(.standard) == "")
    }

    @Test func userPromptsCarryTranscriptAndParts() {
        let user = StructuredNotesPrompt.user(header: "场景：组会", transcript: "[00:10] 我：你好")
        #expect(user == "场景：组会\n\n## 完整转写\n[00:10] 我：你好")
        #expect(StructuredNotesPrompt.user(header: "", transcript: "x") == "## 完整转写\nx")
        let part = StructuredNotesPrompt.partialUser(header: "H", chunk: "[00:10] 我：你好", index: 1, count: 3)
        #expect(part.contains("第 2/3 部分") && part.contains("[00:10] 我：你好"))
        let merge = StructuredNotesPrompt.mergeUser(header: "H", partials: ["{a}", "{b}"], format: .outline)
        #expect(merge.contains("### 第 1 部分\n{a}") && merge.contains("### 第 2 部分\n{b}") && merge.contains("要点大纲"))
    }

    // MARK: Storage

    @Test func oldSummariesWithoutStructuredFieldsStillDecode() throws {
        let old = ##"{"title":"周会","markdown":"# 标题\n周会","questions":[],"generatedAt":"2026-10-26T10:00:00Z","model":"m"}"##
        let summary = try JSONLines.decoder().decode(SessionSummary.self, from: Data(old.utf8))
        #expect(summary.title == "周会" && summary.structured == nil && summary.lessons == nil)
    }

    @Test func structuredPayloadRoundTripsInASummary() throws {
        let when = Date(timeIntervalSince1970: 1_790_000_000)
        let payload = StructuredNotesPayload(
            format: .fishbone,
            fishbone: FishboneNotes(head: "上线延期", headAt: 12, categories: [FishboneCategory(name: "人员", causes: [TimedText(text: "后端缺人", at: 130)])]),
            language: "zh", droppedCount: 2, generatedAt: when, model: "m")
        let summary = SessionSummary(title: "组会", markdown: "# 组会", questions: [], generatedAt: when, model: "m", structured: payload)
        let data = try JSONLines.encoder().encode(summary)
        let decoded = try JSONLines.decoder().decode(SessionSummary.self, from: data)
        #expect(decoded == summary)
        #expect(decoded.structured?.fishbone?.categories.first?.causes.first?.at == 130)
        // A summary without it encodes without the key, so older versions read the file unchanged.
        let plain = try JSONLines.encoder().encode(SessionSummary(title: "t", markdown: "m", questions: [], generatedAt: when, model: "m"))
        #expect(!String(decoding: plain, as: UTF8.self).contains("structured"))
    }

    @Test func damagedOrNewerStructuredDataDoesNotBreakTheSummary() throws {
        let json = #"{"title":"t","markdown":"m","questions":[],"generatedAt":"2026-10-26T10:00:00Z","model":"m","structured":{"format":"mindmap","fishbone":{"head":5},"language":7,"droppedCount":"x"}}"#
        let summary = try JSONLines.decoder().decode(SessionSummary.self, from: Data(json.utf8))
        let structured = try #require(summary.structured)
        #expect(structured.format == .standard && structured.fishbone == nil && structured.language == "zh" && structured.droppedCount == 0)
        #expect(!structured.hasContent)
        // A failure marker keeps its format and says so.
        let failed = try JSONLines.decoder().decode(StructuredNotesPayload.self, from: Data(#"{"format":"outline","failed":true,"language":"en"}"#.utf8))
        #expect(failed.format == .outline && failed.failed == true && failed.isEnglish && !failed.hasContent)
    }
}
