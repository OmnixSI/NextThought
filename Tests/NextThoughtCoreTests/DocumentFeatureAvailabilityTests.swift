import Testing
@testable import NextThoughtCore

struct DocumentFeatureAvailabilityTests {
    @Test func testEveryDependencyIsRequiredBeforeCallingFeatureReady() {
        for mask in 0..<64 {
            let runtime = mask & 1 != 0, ocr = mask & 2 != 0, layout = mask & 4 != 0
            let embedding = mask & 8 != 0, connected = mask & 16 != 0, checking = mask & 32 != 0
            let status = DocumentFeatureAvailability(checking: checking, runtime: runtime, ocr: ocr, layout: layout, embedding: embedding, codexConnected: connected)
            #expect(status.ready(.paddle) == (!checking && runtime && ocr && layout))
            #expect(status.ready(.codex) == (!checking && runtime && connected))
            #expect(status.ready(.semantic) == (!checking && runtime && embedding))
            #expect(status.ready(.office) == (!checking && runtime))
        }
    }
}
