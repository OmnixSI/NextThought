import Foundation
import Testing
@testable import NextThoughtCore

/// The glossary of names and terms (“术语和人名”): search expansion, canonicalization, hotwords,
/// the prompt section and tolerant decoding.
struct GlossaryTests {
    private let kubernetes = GlossaryEntry(canonical: "Kubernetes", kind: .term, variants: ["库伯奈提斯", "K8s"])
    private let jiangChong = GlossaryEntry(canonical: "江充", kind: .person, variants: ["江松", "江冲"])

    private func glossary(_ entries: GlossaryEntry...) -> Glossary { Glossary(entries: entries).normalized() }

    // MARK: Keys

    @Test func looseKeyIgnoresCaseSpaceWidthAndPunctuationButNotPlusOrHash() {
        #expect(Glossary.looseKey("Open AI") == "openai")
        #expect(Glossary.looseKey("open-ai") == "openai")
        #expect(Glossary.looseKey("ＯｐｅｎＡＩ") == "openai")
        #expect(Glossary.looseKey("Ｋ８ｓ") == "k8s")
        #expect(Glossary.looseKey("O'Reilly") == "oreilly")
        #expect(Glossary.looseKey("马克·扎克伯格") == "马克扎克伯格")
        #expect(Glossary.looseKey("C++") == "c++")
        #expect(Glossary.looseKey("C#") == "c#")
        #expect(Glossary.looseKey("C++") != Glossary.looseKey("C"))
        #expect(Glossary.looseKey(" ，。 ") == "")
        #expect(Glossary.strictKey("  Open   AI ") == "open ai")
    }

    // MARK: expand

    @Test func expandWorksFromTheCanonicalSpellingAndFromAnyVariant() {
        let g = glossary(kubernetes, jiangChong)
        #expect(g.expand(query: "Kubernetes") == ["Kubernetes", "库伯奈提斯", "K8s"])
        #expect(g.expand(query: "k8s") == ["k8s", "Kubernetes", "库伯奈提斯"])
        #expect(g.expand(query: "  库伯奈提斯 ") == ["库伯奈提斯", "Kubernetes", "K8s"])
        // Full-width input is the same key.
        #expect(g.expand(query: "Ｋ８ｓ") == ["Ｋ８ｓ", "Kubernetes", "库伯奈提斯"])
        #expect(g.expand(query: "江冲") == ["江冲", "江充", "江松"])
        #expect(g.expand(query: "无关词") == ["无关词"])
        #expect(g.expand(query: "   ").isEmpty)
        #expect(Glossary().expand(query: "abc") == ["abc"])
    }

    @Test func expandReplacesASpellingInsideALongerQuery() {
        let g = glossary(kubernetes, jiangChong)
        #expect(g.expand(query: "k8s 部署") == ["k8s 部署", "Kubernetes 部署", "库伯奈提斯 部署"])
        #expect(g.expand(query: "江充的观点") == ["江充的观点", "江松的观点", "江冲的观点"])
        // The word boundary rule applies to queries too.
        #expect(g.expand(query: "mk8sx") == ["mk8sx"])
    }

    @Test func searchMarksOnlyMisheardHits() {
        let g = glossary(kubernetes)
        let byCanonical = g.search(for: "Kubernetes")
        #expect(byCanonical.firstMatch(in: "我们用库伯奈提斯部署") == GlossarySearchHit(form: "库伯奈提斯", isMisheard: true))
        #expect(byCanonical.firstMatch(in: "上了 K8s 集群")?.isMisheard == true)
        #expect(byCanonical.firstMatch(in: "kubernetes 很好")?.isMisheard == false)
        #expect(byCanonical.firstMatch(in: "完全无关") == nil)
        // Searching for the variant: finding it literally is a plain hit, finding the canonical is not misheard.
        let byVariant = g.search(for: "K8s")
        #expect(byVariant.firstMatch(in: "K8s 集群") == GlossarySearchHit(form: "K8s", isMisheard: false))
        #expect(byVariant.firstMatch(in: "Kubernetes 集群") == GlossarySearchHit(form: "Kubernetes", isMisheard: false))
        #expect(byVariant.firstMatch(in: "库伯奈提斯")?.isMisheard == true)
        #expect(g.search(for: "").firstMatch(in: "anything") == nil)
        // Full-width and case differences still match.
        #expect(g.search(for: "kubernetes").matches("ＫＵＢＥＲＮＥＴＥＳ"))
    }

    // MARK: canonicalize

    @Test func canonicalizeReplacesChineseAndEnglishVariantsInMixedText() {
        let g = glossary(kubernetes, jiangChong)
        let result = g.canonicalizeDetailed("我们用库伯奈提斯部署，江松说K8s不错")
        #expect(result.text == "我们用Kubernetes部署，江充说Kubernetes不错")
        #expect(result.replacementCount == 3)
        #expect(result.replacements.map(\.from) == ["库伯奈提斯", "江松", "K8s"])
        #expect(g.canonicalize(text: "我用k8s和Kubernetes还有库伯奈提斯") == "我用Kubernetes和Kubernetes还有Kubernetes")
    }

    @Test func englishVariantsRespectWordBoundaries() {
        let g = glossary(kubernetes)
        #expect(g.canonicalize(text: "MyK8sCluster") == "MyK8sCluster")
        #expect(g.canonicalize(text: "k8s2") == "k8s2")
        #expect(g.canonicalize(text: "(K8s) k8S is fine") == "(Kubernetes) Kubernetes is fine")
        // Full-width letters match too.
        #expect(g.canonicalize(text: "用Ｋ８ｓ") == "用Kubernetes")
        let gpt = glossary(GlossaryEntry(canonical: "GPT-4", variants: ["GPT4", "GPT 4"]))
        #expect(gpt.canonicalize(text: "GPT4 和 GPT 4 都很强，GPT-4o 也是") == "GPT-4 和 GPT-4 都很强，GPT-4o 也是")
    }

    @Test func longestMatchWinsAndCanonicalSpellingsAreProtected() {
        // 江松山 is another person; the variant 江松 must not eat the start of that name.
        let g = glossary(GlossaryEntry(canonical: "江充", kind: .person, variants: ["江松"]),
                         GlossaryEntry(canonical: "江松山", kind: .person))
        #expect(g.canonicalize(text: "江松山来了，江松也来了") == "江松山来了，江充也来了")
        // A variant that differs from the canonical only in case still fixes the wrong case, and leaves the right one.
        let openAI = glossary(GlossaryEntry(canonical: "OpenAI", variants: ["openai"]))
        #expect(openAI.canonicalize(text: "openai and OpenAI") == "OpenAI and OpenAI")
        #expect(openAI.canonicalizeDetailed("openai and OpenAI").replacementCount == 1)
        // A longer canonical spelling keeps the shorter variant out of it.
        let both = glossary(GlossaryEntry(canonical: "OpenAI", variants: ["Open AI"]), GlossaryEntry(canonical: "OpenAI Gym"))
        #expect(both.canonicalize(text: "Open AI 发布了 OpenAI Gym") == "OpenAI 发布了 OpenAI Gym")
    }

    @Test func canonicalizeIsANoOpWithoutMatches() {
        let g = glossary(kubernetes)
        #expect(g.canonicalize(text: "") == "")
        #expect(g.canonicalize(text: "没有任何术语") == "没有任何术语")
        #expect(Glossary().canonicalize(text: "K8s") == "K8s")
        #expect(g.canonicalizeDetailed("没有任何术语").replacements.isEmpty)
        // Single-character variants are ignored, they would match everywhere.
        let single = Glossary(entries: [GlossaryEntry(canonical: "江充", variants: ["江"])])
        #expect(single.canonicalize(text: "江边") == "江边")
    }

    // MARK: Hotwords

    @Test func hotwordsAreCanonicalSpellingsInAFormatTheHotwordsFileKeeps() {
        let g = Glossary(entries: [
            GlossaryEntry(canonical: "Rust"), GlossaryEntry(canonical: "Go"), GlossaryEntry(canonical: "a:b"),
            GlossaryEntry(canonical: "x"), GlossaryEntry(canonical: "李,四"), GlossaryEntry(canonical: "#hash"),
            GlossaryEntry(canonical: "C#"), kubernetes, jiangChong,
        ]).normalized()
        // Entries with misheard spellings first; unusable words dropped; variants never included.
        #expect(g.hotwords() == ["Kubernetes", "江充", "Rust", "Go", "C#"])
        #expect(g.hotwords(limit: 3) == ["Kubernetes", "江充", "Rust"])
        #expect(g.hotwords(limit: 0).isEmpty)
        #expect(!g.hotwords().contains("K8s"))
        for word in g.hotwords() { #expect(HotwordsFile.clean(word) == [word]) }
    }

    @Test func hotwordStringKeepsTheUsersWordsAndAvoidsDuplicates() {
        let g = glossary(kubernetes, jiangChong)
        let merged = g.hotwordString(merging: "Alice，Bob;kubernetes")
        #expect(merged == "Alice,Bob,kubernetes,江充")
        #expect(HotwordsFile.clean(merged) == ["Alice", "Bob", "kubernetes", "江充"])
        #expect(g.hotwordString(merging: "", limit: 1) == "Kubernetes")
        #expect(Glossary().hotwordString(merging: "甲, 乙") == "甲,乙")
        // The user's own words are never dropped by the limit.
        #expect(g.hotwordString(merging: "甲,乙,丙", limit: 2) == "甲,乙,丙")
    }

    // MARK: Prompt section

    private var promptGlossary: Glossary {
        Glossary(entries: [GlossaryEntry(canonical: "Rust", note: "语言"), kubernetes, jiangChong]).normalized()
    }

    @Test func promptSectionListsMisheardSpellingsFirst() {
        let header = "术语与人名对照（规范写法 ← 语音识别常听错的写法）。遇到听错的写法，按规范写法理解；你输出的内容一律写规范写法："
        #expect(promptGlossary.promptSection() == [
            header,
            "- Kubernetes ← 库伯奈提斯、K8s",
            "- 江充（人名） ← 江松、江冲",
            "- Rust：语言",
        ].joined(separator: "\n"))
        #expect(Glossary().promptSection().isEmpty)
        let english = promptGlossary.promptSection(english: true)
        #expect(english.contains("- 江充 (person) ← 江松, 江冲"))
        #expect(english.contains("- Rust — 语言"))
    }

    @Test func promptSectionIsCutToTheBudgetWithACount() {
        // Header 57 characters, lines 24 / 16 / 9; everything is 57 + 25 + 17 + 10 = 109.
        let full = promptGlossary.promptSection(maxCharacters: 109)
        #expect(full.count == 109 && !full.contains("未列出"))
        let cut = promptGlossary.promptSection(maxCharacters: 100)
        #expect(cut.hasSuffix("- Kubernetes ← 库伯奈提斯、K8s\n（另有 2 条未列出）"))
        #expect(!cut.contains("江充"))
        #expect(cut.count <= 100)
        // Not even the header and one line fit.
        #expect(promptGlossary.promptSection(maxCharacters: 60).isEmpty)
        // Long lists stay inside the default budget.
        let many = Glossary(entries: (0..<400).map { GlossaryEntry(canonical: "术语编号\($0)", variants: ["变体甲\($0)", "变体乙\($0)"]) })
        let section = many.promptSection()
        #expect(section.count <= 1_500 && section.contains("未列出"))
    }

    // MARK: Merging and cleaning

    @Test func mergingUnitesVariantsAndKeepsTheFirstEntry() {
        let series = Glossary(entries: [
            GlossaryEntry(canonical: "Kubernetes", variants: ["K8s", "库伯", "Kubernetes"]),
            GlossaryEntry(canonical: "Raft"),
        ])
        let global = Glossary(entries: [kubernetes])
        let merged = global.merging(series)
        #expect(merged.entries.count == 2)
        #expect(merged.entries[0].id == kubernetes.id)
        #expect(merged.entries[0].variants == ["库伯奈提斯", "K8s", "库伯"])
        #expect(merged.entries[1].canonical == "Raft")
        #expect(Glossary.layered(global: global, series: nil).entries == [kubernetes])
        #expect(Glossary.layered(global: global, series: Glossary()).entries == [kubernetes])
        #expect(Glossary.layered(global: global, series: series) == merged)
    }

    @Test func normalizedCleansEntriesAndVariants() {
        let messy = Glossary(entries: [
            GlossaryEntry(canonical: "  Kubernetes ", variants: ["K8s", "k8s", "  k8s ", "K", "库", "", "Kubernetes"], note: "  "),
            GlossaryEntry(canonical: "   "), GlossaryEntry(canonical: "，。"),
            GlossaryEntry(canonical: "OpenAI", variants: ["Open-AI"]),
            GlossaryEntry(canonical: "Open AI", variants: ["openai"], note: "公司"),
        ]).normalized()
        #expect(messy.entries.count == 2)
        #expect(messy.entries[0].canonical == "Kubernetes" && messy.entries[0].variants == ["K8s"] && messy.entries[0].note == nil)
        // The same name spelled two ways becomes one entry; the other spelling becomes a variant.
        #expect(messy.entries[1].canonical == "OpenAI")
        #expect(messy.entries[1].variants == ["Open-AI", "Open AI", "openai"])
        #expect(messy.entries[1].note == "公司")
        #expect(messy.normalized() == messy)
        #expect(messy.entry(matching: "open ai")?.canonical == "OpenAI")
        #expect(messy.entry(matching: "nothing") == nil)
    }

    // MARK: Decoding

    @Test func decodingToleratesOldAndBrokenData() throws {
        let json = #"{"entries":[{"canonical":"江充"},{"id":"not-a-uuid","canonical":"X","kind":"alien","variants":"oops"},{"foo":1},5]}"#
        let decoded = try JSONDecoder().decode(Glossary.self, from: Data(json.utf8))
        #expect(decoded.entries.map(\.canonical) == ["江充", "X"])
        #expect(decoded.entries.allSatisfy { $0.kind == .term && $0.variants.isEmpty && $0.note == nil })
        #expect(try JSONDecoder().decode(Glossary.self, from: Data(#"[{"canonical":"A1","kind":"person"}]"#.utf8)).entries.first?.kind == .person)
        #expect(try JSONDecoder().decode(Glossary.self, from: Data("{}".utf8)).isEmpty)
        #expect(try JSONDecoder().decode(Glossary.self, from: Data(#"{"entries":"oops"}"#.utf8)).isEmpty)
        #expect(try JSONDecoder().decode(GlossaryKind.self, from: Data(#""人名""#.utf8)) == .person)

        let g = glossary(kubernetes, jiangChong)
        let again = try JSONDecoder().decode(Glossary.self, from: JSONEncoder().encode(g))
        #expect(again == g)
        let viaLines = try JSONDecoder().decode(Glossary.self, from: JSONLines.encoder().encode(g))
        #expect(viaLines == g)
    }

    @Test func workspacesKeepAnOptionalSeriesGlossary() throws {
        let id = UUID()
        let old = try JSONDecoder().decode(Workspace.self, from: Data(#"{"id":"\#(id.uuidString)","name":"组会","mode":"labMeeting"}"#.utf8))
        #expect(old.glossary == nil)
        var workspace = Workspace(name: "ACL 报告", mode: .academic)
        #expect(workspace.glossary == nil)
        workspace.glossary = Glossary(entries: [kubernetes])
        let again = try JSONDecoder().decode(Workspace.self, from: JSONEncoder().encode(workspace))
        #expect(again.glossary == Glossary(entries: [kubernetes]))
    }

    // MARK: Text import and export

    @Test func parseAndExportRoundTrip() {
        let text = """
        # 注释行
        Kubernetes | 库伯奈提斯, K8s
        江充 | 江松，江冲 | 人名
        Raft | | 术语 | 共识算法

        只有名字
        ｜忽略
        """
        let entries = Glossary.parse(lines: text)
        #expect(entries.map(\.canonical) == ["Kubernetes", "江充", "Raft", "只有名字"])
        #expect(entries.map(\.kind) == [.term, .person, .term, .term])
        #expect(entries[0].variants == ["库伯奈提斯", "K8s"])
        #expect(entries[1].variants == ["江松", "江冲"])
        #expect(entries[2].variants.isEmpty && entries[2].note == "共识算法")
        #expect(entries[3].variants.isEmpty && entries[3].note == nil)

        let exported = Glossary(entries: entries).exportText()
        #expect(exported == "Kubernetes | 库伯奈提斯, K8s\n江充 | 江松, 江冲 | 人名\nRaft |  | 术语 | 共识算法\n只有名字")
        let reparsed = Glossary.parse(lines: exported)
        #expect(reparsed.map(\.canonical) == entries.map(\.canonical))
        #expect(reparsed.map(\.variants) == entries.map(\.variants))
        #expect(reparsed.map(\.kind) == entries.map(\.kind))
        #expect(reparsed.map(\.note) == entries.map(\.note))
        // A third column that is not a type is the note; the default type applies.
        let person = Glossary.parse(lines: "李雷 | 李磊 | 同事", defaultKind: .person)
        #expect(person.first?.kind == .person && person.first?.note == "同事")
    }

    // MARK: Answer prompt

    @Test func answerPromptCarriesTheGlossaryOnlyWhenGiven() throws {
        let section = promptGlossary.promptSection()
        let prompt = AnswerPromptBuilder.make(question: "什么是它", context: "", profile: "", turns: [], hasScreenshot: false, glossary: section)
        let json = String(prompt.dropFirst(AnswerPromptBuilder.instructions.count + 2))
        let object = try #require(JSONSerialization.jsonObject(with: Data(json.utf8)) as? [String: Any])
        #expect(object["term_glossary"] as? String == section)
        let plain = AnswerPromptBuilder.makeParts(question: "q", context: "", profile: "", turns: [], hasScreenshot: false)
        #expect(!plain.user.contains("term_glossary"))
        let parts = AnswerPromptBuilder.makeParts(question: "q", context: "", profile: "", turns: [], hasScreenshot: false, glossary: section)
        #expect(parts.user.contains("term_glossary") && parts.system == AnswerPromptBuilder.instructions)
        #expect(AnswerPromptBuilder.instructions.contains("term_glossary"))
    }
}
