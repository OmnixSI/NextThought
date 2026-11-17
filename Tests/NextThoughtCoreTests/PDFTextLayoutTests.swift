import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct PDFTextLayoutTests {
    private let prose = [
        "JANE DOE / RESEARCH REPORT 2026",
        "1 Introduction",
        "1.1 Application and technical background",
        "Artificial intelligence is being introduced to physical layer techniques across many wire-",
        "less systems: the signal processing functions that turn radio hardware into a usable link,",
        "including channel estimation and acquisition, beamforming and beam management, and",
        "more recently radio sensing. Neural networks have been trained to replace the receiver.",
        "This report studies three questions that follow from this observation, one per chapter,",
        "and gives preliminary results for the first two of them.",
        "A second paragraph starts here and also runs over the full width of the printed page,",
        "so that its lines are joined into one sentence again.",
        "4",
    ].joined(separator: "\n")

    /// The page under test among others that share only the running header.
    private func pages(_ count: Int) -> [String] {
        [prose] + (2...count).map { "JANE DOE / RESEARCH REPORT 2026\nPage \($0) has its own text about topic number \($0).\n\($0)" }
    }

    @Test func runningHeaderIsFurnitureWhateverThePageNumber() {
        let furniture = PDFTextLayout.furniture(pages: pages(6))
        #expect(furniture.contains(PDFTextLayout.key("JANE DOE / RESEARCH REPORT 2026")))
        // Three pages are too few to tell a header from a coincidence.
        #expect(PDFTextLayout.furniture(pages: pages(3)).isEmpty)
    }

    @Test func contentThatRepeatsIsNotFurniture() {
        // Every page is made of the same kind of line (a log, a table): nothing may be dropped.
        let pages = (1...8).map { page in (0..<12).map { "Research page \(page), line \($0): alpha beta, evidence and next steps." }.joined(separator: "\n") }
        let furniture = PDFTextLayout.furniture(pages: pages)
        #expect(furniture.isEmpty)
        #expect(PDFTextLayout.markdown(pages[3], furniture: furniture).contains("Research page 4, line 11"))
        // Rows of numbers look alike once the digits are ignored.
        let tables = (1...8).map { "Results \($0)\n12.5 45.1 0.92\n13.5 46.1 0.93\nUnique text for page \($0) follows here." }
        #expect(!PDFTextLayout.furniture(pages: tables).contains(PDFTextLayout.key("12.5 45.1 0.92")))
        // And a page is never mostly removed, whatever was flagged.
        #expect(PDFTextLayout.markdown("Header\nHeader two\nBody", furniture: [PDFTextLayout.key("Header"), PDFTextLayout.key("Header two")]).contains("Header"))
    }

    @Test func proseBecomesHeadingsAndWholeSentences() {
        let text = PDFTextLayout.markdown(prose, furniture: PDFTextLayout.furniture(pages: pages(6)))
        #expect(!text.contains("JANE DOE"))
        #expect(text.contains("## 1 Introduction"))
        #expect(text.contains("### 1.1 Application and technical background"))
        // The word broken at the margin is whole again, and the sentence is on one line.
        #expect(text.contains("across many wireless systems: the signal processing functions"))
        #expect(!text.hasSuffix("4"))
        let blocks = DocumentStructure.parse(text, locator: .init(page: 4))
        #expect(blocks.last?.locator.headingPath == ["1 Introduction", "1.1 Application and technical background"])
    }

    @Test func slideLikePagesKeepTheirLines() {
        let slide = "Scenario\nA sub-6 GHz-aided mmWave site\nTwo co-sited arrays\n3.5 GHz fully digital, 28 GHz hybrid;\ninstalled with a yaw."
        #expect(PDFTextLayout.markdown(slide) == slide)
    }

    @Test func onlyRealTitlesBecomeHeadings() {
        #expect(PDFTextLayout.heading("3.2 Research questions", next: "The questions are") != nil)
        #expect(PDFTextLayout.heading("Abstract", next: nil)?.level == 2)
        #expect(PDFTextLayout.heading("参考文献", next: nil) != nil)
        // A contents entry, a sentence, a table row, a wrapped line that starts with a number.
        #expect(PDFTextLayout.heading("3.2 Research questions … 11", next: nil) == nil)
        #expect(PDFTextLayout.heading("5 GHz was used in all of the experiments.", next: nil) == nil)
        #expect(PDFTextLayout.heading("3 12.5 45.1 0.92", next: nil) == nil)
        #expect(PDFTextLayout.heading("28 GHz paths are easily blocked", next: "and reflected") == nil)
        #expect(PDFTextLayout.heading("2026 Annual Review", next: nil) == nil)
    }

    @Test func journalNumberingIsRecognised() {
        #expect(PDFTextLayout.heading("II. SYSTEM MODEL AND PROBLEM FORMULATION", next: "A. Array and Channel Model")?.level == 2)
        #expect(PDFTextLayout.heading("A. Array and Channel Model", next: "The base station has")?.level == 3)
        #expect(PDFTextLayout.heading("APPENDIX B", next: nil)?.level == 2)
        #expect(PDFTextLayout.heading("REFERENCES", next: nil)?.level == 2)
        // An author in the reference list, an initial in running text.
        #expect(PDFTextLayout.heading("G. Marcus, and A. Keller, “Sionna RT: Differentiable ray tracing for", next: nil) == nil)
        #expect(PDFTextLayout.heading("H. Yang is with the University of Technology Sydney, Sydney, NSW,", next: nil) == nil)
        #expect(PDFTextLayout.heading("I. Smith", next: nil) == nil)
    }

    @Test func chineseLinesJoinWithoutSpaces() {
        #expect(PDFTextLayout.join("这一页看部署条件越差，我们的收", "益越大。") == "这一页看部署条件越差，我们的收益越大。")
        #expect(PDFTextLayout.join("learned from", "service pilots") == "learned from service pilots")
        #expect(PDFTextLayout.isPageNumber("12") && PDFTextLayout.isPageNumber("Page 3 of 20") && PDFTextLayout.isPageNumber("- 7 -"))
        #expect(!PDFTextLayout.isPageNumber("2026 results"))
    }
}
