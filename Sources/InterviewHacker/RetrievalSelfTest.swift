import Foundation
import InterviewHackerCore

enum RetrievalSelfTest {
    static func run(_ options: [String: String]) async throws -> String {
        guard let input = options["input"], let output = options["output"] else { throw SelfTest.Failure("retrieval-eval needs --input and --output") }
        struct Query: Decodable { var question: String; var expected: Int?; var split: String }
        struct Corpus: Decodable { var documents: [String]; var queries: [Query] }
        let bytes = try Data(contentsOf: URL(fileURLWithPath: input)), corpus = try JSONDecoder().decode(Corpus.self, from: bytes)
        let docs = corpus.documents.enumerated().map { i, text in ContextDocument(id: UUID(uuidString: String(format: "00000000-0000-0000-0000-%012d", i+1))!, name: "evidence-\(i).md", pages: [text]) }
        let chunks = ContextChunker.chunks(documents: docs)
        var semantic = SemanticIndex()
        let embedder = await SemanticRetrievalService.shared.embedder()
        for chunk in chunks { semantic.setVector(try await embedder.embedPassage(chunk.text), fingerprint: chunk.fingerprint, for: chunk.order) }
        let index = HybridContextIndex(documents: docs, semantic: semantic)
        var vectors: [[Float]] = []
        for q in corpus.queries { vectors.append(try await embedder.embedQuery(q.question, timeout: 120)) }
        func score(_ threshold: Double, split: String) -> (recall: Double, falsePositive: Double, rows: [[String: Any]]) {
            var positive = 0, hits = 0, negative = 0, falsePositive = 0, rows: [[String: Any]] = []
            for (position, query) in corpus.queries.enumerated() where query.split == split {
                let result = index.retrieve(question: query.question, queryVector: vectors[position], minimumSemanticScore: threshold)
                if let expected = query.expected { positive += 1; if result.contains(where: { $0.source == docs[expected].name }) { hits += 1 } }
                else { negative += 1; if !result.isEmpty { falsePositive += 1 } }
                rows.append(["question": query.question, "expected": query.expected.map(String.init) ?? "none", "sources": result.map(\.source)])
            }
            return (Double(hits)/Double(max(1,positive)), Double(falsePositive)/Double(max(1,negative)), rows)
        }
        let candidates = (10...90).map { Double($0)/100 }
        let accepted = candidates.filter { let s = score($0, split: "validation"); return s.recall >= 0.9 && s.falsePositive <= 0.05 }
        // Select the center of the validation feasible interval to avoid the
        // original maximum-threshold selection overfitting its boundary.
        let threshold = accepted.isEmpty ? RetrievalPolicy.minimumSemanticScore : ((accepted.first! + accepted.last!) / 2)
        let sweep = candidates.map { t -> [String: Any] in let s = score(t, split: "validation"); return ["threshold": t, "recall": s.recall, "unrelated": s.falsePositive] }
        let validation = score(threshold, split: "validation"), heldout = score(threshold, split: "heldout")
        let report: [String: Any] = ["corpusSHA256": IncrementalBackupStore.digest(bytes), "modelRevision": ModelCatalog.qwen3Embedding.revision,
            "selection": "validation-feasible midpoint v2; previously inspected heldout is regression-only", "validationSweep": sweep, "threshold": threshold, "validationTargetMet": !accepted.isEmpty,
            "validation": ["recallAt8": validation.recall, "unrelatedReturned": validation.falsePositive],
            "heldout": ["recallAt8": heldout.recall, "unrelatedReturned": heldout.falsePositive, "rows": heldout.rows]]
        try JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]).write(to: URL(fileURLWithPath: output))
        await SemanticRetrievalService.shared.unload()
        return "threshold=\(threshold); validation recall=\(validation.recall), unrelated=\(validation.falsePositive); heldout recall=\(heldout.recall), unrelated=\(heldout.falsePositive)"
    }
}
