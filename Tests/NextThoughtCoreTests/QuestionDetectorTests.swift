import Testing
@testable import NextThoughtCore

struct QuestionDetectorTests {
    @Test func detectsEnglishQuestionWithoutQuestionMark() {
        var detector = QuestionDetector()
        #expect(detector.detect("How does a database index improve lookup speed") != nil)
    }

    @Test func detectsChineseFollowUp() {
        var detector = QuestionDetector()
        #expect(detector.detect("数据库索引有什么缺点") != nil)
    }

    @Test func ignoresStatementsAndDeduplicatesRepeatedQuestions() {
        var detector = QuestionDetector()
        #expect(detector.detect("The database index improves lookup speed") == nil)
        #expect(detector.detect("What are the drawbacks of a database index?") != nil)
        #expect(detector.detect("What are the drawbacks of a database index?") == nil)
    }

    @Test func joinsQuestionFragmentsAcrossSegments() {
        var detector = QuestionDetector()
        #expect(detector.detect("Can you") == nil)
        #expect(detector.detect("explain this design") == "Can you explain this design")
    }

    @Test func recognizesChineseRequestAndMixedTerminology() {
        var detector = QuestionDetector()
        #expect(detector.detect("解释一下这个设计") == "解释一下这个设计")
        #expect(detector.detect("解释一下这个 database index 设计") != nil)
        #expect(detector.detect("这个 database index 会提升 lookup speed") == nil)
    }

    @Test func joinsLongerQuestionThatWasSplitBetweenSegments() {
        var detector = QuestionDetector()
        #expect(detector.detect("How does database") == nil)
        #expect(detector.detect("index improve lookup speed") == "How does database index improve lookup speed")
    }

    @Test func recognizesShortFollowUpAndDeduplicatesChinese() {
        var detector = QuestionDetector()
        #expect(detector.detect("为什么？") != nil)
        #expect(detector.detect("为什么？") == nil)
        #expect(detector.detect("它有什么缺点") != nil)
        #expect(detector.detect("它有什么缺点") == nil)
    }

    @Test func recognizesShortFollowUpWithoutQuestionMark() {
        var detector = QuestionDetector()
        #expect(detector.detect("为什么") == "为什么")
        #expect(detector.detect("普通陈述") == nil)
    }
}
