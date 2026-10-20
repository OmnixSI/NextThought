import Foundation
import Testing
@testable import InterviewHackerCore

struct DocumentStructureTests {
    @Test func physicalAndLegacyLocationsStayDistinct() throws {
        let old=ContextDocument(name:"old.pdf",pages:["section one","section two"])
        let chunks=ContextChunker.chunks(documents:[old])
        #expect(chunks[1].locator?.section == 2)
        #expect(chunks[1].locator?.page == nil)
        #expect(chunks[1].locator?.label == "第 2 段")
        let new=ContextDocument(name:"scan.pdf",pages:["text"],content:DocumentContent(pipeline:"system",blocks:DocumentStructure.parse("## Heading\n\ntext",locator:.init(page:7))))
        #expect(ContextChunker.chunks(documents:[new]).first?.locator?.page == 7)
        #expect(ContextChunker.chunks(documents:[new]).first?.text.contains("scan.pdf > Heading") == true)
    }
    @Test func htmlRowsRepeatHeaderAndKeepSpans() {
        let rows=(0..<80).map { "<tr><td>row\($0)</td><td>12345</td></tr>" }
        let text="<table><tr><th>A</th><th>B</th></tr>"+rows.joined()+"</table>"
        let block=DocumentBlock(kind:"table",markdown:text)
        let pieces=DocumentStructure.pieces(block,target:300)
        #expect(pieces.count>1)
        #expect(pieces.allSatisfy { $0.contains("<th>A</th>") && $0.hasSuffix("</table>") })
        #expect((0..<80).allSatisfy { number in pieces.filter { $0.contains("<td>row\(number)</td>") }.count == 1 })
        let merged="<table><tr><th>A</th></tr><tr><td rowspan=\"2\">joined</td><td>x</td></tr><tr><td>y</td></tr><tr><td>z</td></tr></table>"
        #expect(DocumentStructure.pieces(.init(kind:"table",markdown:merged),target:80).first?.contains("<td>y</td>") == true)
    }
    @Test func markdownTablesAndFormulasAreNeverCutByPromptBudgets() {
        let table="| Name | Value |\n| --- | --- |\n"+(0..<30).map { "| item\($0) | \($0) |" }.joined(separator:"\n")
        let pieces=DocumentStructure.pieces(.init(kind:"table",markdown:table),target:150)
        #expect(pieces.count>1)
        #expect(pieces.allSatisfy { $0.hasPrefix("| Name | Value |\n| --- | --- |") })
        let formula="$$\n"+String(repeating:"x",count:1500)+"\n$$"
        #expect(DocumentStructure.pieces(.init(kind:"formula",markdown:formula),target:50)==[formula])
        let ex=ContextExcerpt(source:"math",page:1,text:formula,locator:.init(page:1))
        #expect(ContextExcerpt.budget([ex],characters:100).isEmpty)
    }
    @Test func everyAcceptedBlockRemainsSearchableBeyondTheOldChunkCap() {
        let blocks=(0..<2105).map { i in DocumentBlock(id: "block-\(i)", markdown: i == 2104 ? "terminal-needle-zebra" : "paragraph \(i)", locator: .init(section: i+1)) }
        let document=ContextDocument(name:"many-sections.md",pages:blocks.map(\.markdown),content:.init(pipeline:"fixture",blocks:blocks))
        let index=HybridContextIndex(documents:[document])
        #expect(index.chunks.count==2105)
        #expect(index.retrieve(question:"terminal-needle-zebra").first?.locator?.section==2105)
    }
    @Test func vectorsRejectChangedChunksAndInvalidNumbers() {
        let index=SemanticIndex(entries:[.init(fingerprint:"old",vector:[1,0]),.init(fingerprint:"same",vector:[.nan,0])])
        #expect(index.rankedOrders(query:[1,0],fingerprints:["changed","same"]).isEmpty)
        #expect(index.rankedOrders(query:[1],fingerprints:["old","same"]).isEmpty)
    }
}
