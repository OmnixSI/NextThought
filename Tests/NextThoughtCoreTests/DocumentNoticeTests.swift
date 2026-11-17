import Testing
@testable import NextThoughtCore

@Suite struct DocumentNoticeTests {
    private let figure = { (page: Int) in "p.\(page)：已提取原生文字；页内图像中的文字、图表或公式尚未读取，请核对原件。" }

    @Test func unreadPicturesAreOneQuietLine() {
        let lines = [figure(7), figure(15), figure(17), figure(21)]
        #expect(DocumentNotice.warnings(in: lines).isEmpty)
        #expect(DocumentNotice.note(for: lines) == "p.7、15、17、21 的图片没有读取，正文已完整提取。")
    }

    @Test func aFailedPageStillNeedsALook() {
        let lines = [figure(7), "p.9：识别失败，其他页面已保留；timeout"]
        #expect(DocumentNotice.warnings(in: lines) == ["p.9：识别失败，其他页面已保留；timeout"])
        #expect(DocumentNotice.note(for: lines) == "p.7 的图片没有读取，正文已完整提取。")
    }

    @Test func theSameLineIsSaidOnce() {
        let line = "幻灯片 3：请核对空白页或不可提取图形。"
        #expect(DocumentNotice.unique([line, line, "p.1：未识别到文字，请核对原件。"]).count == 2)
        #expect(DocumentNotice.note(for: [line]) == nil)
    }

    @Test func theFoldedLineIsItselfANote() {
        let folded = DocumentNotice.note(for: [figure(2)])!
        #expect(DocumentNotice.isNote(folded))
        #expect(DocumentNotice.warnings(in: [folded]).isEmpty)
    }
}

@Suite struct WholeMaterialTests {
    private func block(_ text: String, slide: Int? = nil, page: Int? = nil) -> DocumentBlock {
        DocumentBlock(markdown: text, locator: DocumentLocator(page: page, slide: slide))
    }

    @Test func blocksOfOneSlideShareItsMark() {
        let blocks = [block("Title", slide: 1), block("First point", slide: 1), block("Results", slide: 2)]
        let deck = ContextDocument(name: "deck.pptx", pages: blocks.map(\.markdown), content: DocumentContent(pipeline: "system", blocks: blocks))
        let text = AnswerPromptBuilder.wholeMaterial(of: [deck])
        #expect(text == "【deck.pptx · 幻灯片 1】\nTitle\nFirst point\n\n【deck.pptx · 幻灯片 2】\nResults")
    }

    @Test func pagesOfAReportKeepTheirNumbers() {
        let blocks = [block("Intro", page: 1), block("Method", page: 7), block("More method", page: 7)]
        let report = ContextDocument(name: "report.pdf", pages: blocks.map(\.markdown), content: DocumentContent(pipeline: "system", blocks: blocks))
        let text = AnswerPromptBuilder.wholeMaterial(of: [report]) ?? ""
        #expect(text.contains("【report.pdf · p.7】\nMethod\nMore method"))
        #expect(!text.contains("第 2"))
    }

    @Test func plainNotesAndTheLimit() {
        let note = ContextDocument(name: "notes.md", pages: ["Hello"])
        #expect(AnswerPromptBuilder.wholeMaterial(of: [note]) == "【notes.md】\nHello")
        #expect(AnswerPromptBuilder.wholeMaterial(of: [note], limit: 3) == nil)
        var off = note; off.enabled = false
        #expect(AnswerPromptBuilder.wholeMaterial(of: [off]) == nil)
    }
}

@Suite struct AnswerLanguageTests {
    @Test func anEnglishDoubtIsAnsweredInEnglish() {
        let doubt = "I am not fully convinced by the term physics informed. It seems to me that many model-based deep learning methods already use the array response."
        #expect(AnswerLanguage.spoken(in: doubt) == "English")
        let fields = AnswerLanguage.fields(choice: "auto", gloss: true, question: doubt)
        #expect(fields.answer == "English")
        #expect(fields.gloss == "简体中文")
    }

    @Test func recognisedEnglishWithAChineseWordIsStillEnglish() {
        #expect(AnswerLanguage.spoken(in: "Could you explain the 消融 study on the adaptation slide?") == "English")
    }

    @Test func aChineseQuestionNamingTermsIsChinese() {
        let question = "XL-MIMO 的 near field 效应你是怎么考虑的？"
        #expect(AnswerLanguage.spoken(in: question) == "简体中文")
        #expect(AnswerLanguage.fields(choice: "auto", gloss: true, question: question).gloss == nil)
    }

    @Test func aFixedChoiceWinsAndShortRemarksDecideNothing() {
        #expect(AnswerLanguage.fields(choice: "en", gloss: true, question: "你怎么看这个问题？").answer == "English")
        #expect(AnswerLanguage.fields(choice: "zh", gloss: true, question: "What is new here?").gloss == nil)
        #expect(AnswerLanguage.spoken(in: "OK.") == nil)
        #expect(AnswerLanguage.fields(choice: "auto", gloss: false, question: "What is new in your method?").gloss == nil)
    }
}
