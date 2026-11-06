import AppKit
import Foundation
import NextThoughtCore

@MainActor enum ScreenSelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true,
              let fixtures = options["fixtures"] else { throw SelfTest.Failure("screen-context requires isolated .ci, DEMO=1 and --fixtures") }
        let root = URL(fileURLWithPath: fixtures)
        func image(_ name: String) throws -> CGImage {
            guard let cg = NSImage(contentsOf: root.appendingPathComponent(name))?.cgImage(forProposedRect: nil, context: nil, hints: nil) else { throw SelfTest.Failure("fixture unreadable") }
            return cg
        }
        // Verify preview/crop orientation using actual CoreGraphics pixel data.
        func pixels(_ patch: Bool) -> CGImage {
            var data = [UInt8](repeating: 255, count: 160*90*4)
            if patch { for y in 10..<20 { for x in 100..<120 { let n = (y*160+x)*4; data[n] = 0; data[n+1] = 0; data[n+2] = 0 } } }
            let provider = CGDataProvider(data: Data(data) as CFData)!
            return CGImage(width: 160,height: 90,bitsPerComponent: 8,bitsPerPixel: 32,bytesPerRow: 640,space: CGColorSpaceCreateDeviceRGB(),bitmapInfo: .init(rawValue: CGImageAlphaInfo.premultipliedLast.rawValue),provider: provider,decode: nil,shouldInterpolate: false,intent: .defaultIntent)!
        }
        let basePixels = ScreenContextService.preview(pixels(false))!, changedPixels = ScreenContextService.preview(pixels(true))!
        guard let delta = ScreenDelta.region(previous: basePixels,current: changedPixels,width: 160,height: 90),
              delta[0] <= 100.0/160, delta[0]+delta[2] >= 120.0/160,
              delta[1] <= 10.0/90, delta[1]+delta[3] >= 20.0/90 else { throw SelfTest.Failure("changed crop coordinates flipped") }
        var frame = try image("03-bilingual-columns.png")
        var capturedTargets: [String] = []
        let service = ScreenContextService(capture: { target, _ in capturedTargets.append(target); return frame })
        guard let first = try await service.refresh(targetID: "window:41", source: "Fixture window", region: nil, force: true) else { throw SelfTest.Failure("no OCR") }
        guard first.text.contains("PostgreSQL"), !first.regions.isEmpty,
              first.regions.allSatisfy({ $0.bounds.count == 4 && $0.bounds.allSatisfy { $0 >= 0 && $0 <= 1 } }),
              ScreenReference.isRelated("这一页这里怎么理解？", context: first) else { throw SelfTest.Failure("real OCR/region/reference failure") }
        guard let pinned = try service.questionImage(frameID: first.frameID) else { throw SelfTest.Failure("question image missing") }
        let pinnedHash = ManagedModelStore.sha256(of: pinned)
        defer { try? FileManager.default.removeItem(at: pinned) }
        frame = try image("04-merged-table.png")
        guard let second = try await service.refresh(targetID: "display:99", source: "Fixture display", region: nil, force: true),
              first.frameID != second.frameID, capturedTargets == ["window:41", "display:99"],
              ManagedModelStore.sha256(of: pinned) == pinnedHash,
              try service.questionImage(frameID: first.frameID) == nil else { throw SelfTest.Failure("stale target/frame leaked") }
        service.invalidate()
        guard service.latest == nil else { throw SelfTest.Failure("screen invalidation") }
        let state = AppState.shared
        state.currentAnswerID = nil; state.historyCursor = nil; state.answer = "legacy answer"
        guard state.displayedAnswer == "legacy answer" else { throw SelfTest.Failure("nil request IDs hid legacy answer") }
        let id = UUID(); state.answerCoordinator.begin(id: id, question: "new")
        state.currentAnswerID = id; state.historyCursor = nil; state.answer = "old answer"
        guard state.displayedAnswer.isEmpty else { throw SelfTest.Failure("new question received old answer") }
        state.answerCoordinator.draft(id: UUID(), text: "late old answer")
        guard state.displayedAnswer.isEmpty else { throw SelfTest.Failure("late producer crossed request") }
        state.answerCoordinator.finish(id: id, text: "quick answer")
        state.webLookups[id] = WebLookup(status: .done, question: "new", answer: "verified answer", provider: .codex)
        guard state.displayedAnswer == "quick answer", !state.showsWebAnswer else { throw SelfTest.Failure("web answer replaced spoken paragraph") }
        state.toggleWebAnswer()
        guard state.displayedAnswer == "verified answer", state.currentAnswerID == id else { throw SelfTest.Failure("web revision changed answer identity") }
        state.decisionAppServer.shutdown()
        return "real Vision OCR, normalized regions, explicit window/display routing, deictic relevance, frozen image, stale frame invalidation, request isolation and user-applied web revision passed; no physical screen capture claimed"
    }
    static func search(_ options: [String: String]) async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.nextthought.app.ci") == true,
              let input = options["input"], let output = options["output"] else { throw SelfTest.Failure("search-eval requires .ci, DEMO=1, --input and --output") }
        struct Sample: Decodable { var question: String; var needsSearch: Bool; var split: String }
        let samples = try JSONDecoder().decode([Sample].self, from: Data(contentsOf: URL(fileURLWithPath: input)))
        let state = AppState.shared; state.answerBackend = .codexAppServer; state.codexOCRModel = options["model"] ?? "gpt-6-luna"; state.webSearchMode = .auto
        await state.decisionAppServer.prewarm()
        defer { state.decisionAppServer.shutdown() }
        var rows: [[String: Any]] = []
        for sample in samples {
            let started = Date(), decision = await state.assessWebSearch(sample.question)
            rows.append(["question": sample.question, "expected": sample.needsSearch, "actual": decision.search,
                "reason": decision.reason, "mechanism": decision.mechanism, "split": sample.split, "seconds": Date().timeIntervalSince(started)])
            try JSONSerialization.data(withJSONObject: rows, options: [.prettyPrinted, .sortedKeys]).write(to: URL(fileURLWithPath: output), options: .atomic)
        }
        return "\(rows.count) real routing decisions written to \(output)"
    }
}
