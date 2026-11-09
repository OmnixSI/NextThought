import Foundation

public struct QuestionDetector {
    private var recentKeys: [(key: String, date: Date)] = []
    private var fragmentDate: Date?
    private let duplicateWindow: TimeInterval = 15
    private let fragmentWindow: TimeInterval = 8
    private var pendingFragment = ""

    public init() {}

    public mutating func detect(_ text: String, now: Date = Date()) -> String? {
        let duplicateCutoff = now.addingTimeInterval(-duplicateWindow)
        recentKeys.removeAll { $0.date <= duplicateCutoff }
        if let fragmentDate, now.timeIntervalSince(fragmentDate) > fragmentWindow {
            pendingFragment = ""
            self.fragmentDate = nil
        }
        let incoming = String(text.suffix(2_000)).trimmingCharacters(in: .whitespacesAndNewlines)
        guard !incoming.isEmpty else { return nil }

        var question = incoming
        if !pendingFragment.isEmpty {
            question = pendingFragment + " " + incoming
            pendingFragment = ""
            fragmentDate = nil
        }

        if !isShortFollowUp(question), isIncomplete(question) {
            pendingFragment = String(question.suffix(2_000))
            fragmentDate = now
            return nil
        }
        guard isShortFollowUp(question) || looksLikeQuestion(question) else { return nil }
        guard question.count >= 4 || isShortFollowUp(question) else { return nil }

        let key = normalize(question)
        guard !key.isEmpty else { return nil }
        // Time-bounded exact normalization does not collapse different entities,
        // word order, negation or repeated follow-ups throughout a whole session.
        guard !recentKeys.contains(where: { $0.key == key }) else { return nil }
        recentKeys.append((key, now))
        if recentKeys.count > 30 { recentKeys.removeFirst(recentKeys.count - 30) }
        return question
    }

    private func looksLikeQuestion(_ text: String) -> Bool {
        let lower = text.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        if lower.contains("?") || lower.contains("？") { return true }

        let englishStarts = [
            "what ", "what's", "why ", "how ", "when ", "where ", "who ", "which ",
            "can you ", "could you ", "would you ", "tell me ", "explain ",
            "describe ", "compare ", "please explain ", "walk me through ",
            "help me understand ", "difference between ", "do you ", "did you ", "have you ", "is there ", "are there ", "could we ", "can we "
        ]
        if englishStarts.contains(where: { lower.hasPrefix($0) }) { return true }

        let chineseMarkers = [
            "你觉得", "为什么", "怎么", "如何", "能不能", "可不可以", "请解释", "解释一下",
            "请说明", "说明一下", "分析一下", "比较一下", "讲讲", "谈谈", "什么是", "有什么",
            "哪些", "是否", "设计思路是什么", "怎么样", "介绍一下", "举个例子", "举一个例子", "能否", "对吗", "是不是", "有什么区别"
        ]
        return chineseMarkers.contains(where: { lower.contains($0) })
    }

    private func isShortFollowUp(_ text: String) -> Bool {
        let lower = text.lowercased().trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters))
        let followUps = ["为什么", "怎么", "然后呢", "那为什么", "那怎么", "why", "how", "and why", "what about that", "然后呢", "所以呢", "举例呢", "why not", "and then"]
        return followUps.contains(lower)
    }

    private func isIncomplete(_ text: String) -> Bool {
        let lower = text.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        if lower.contains("?") || lower.contains("？") { return false }
        let words = lower.split(whereSeparator: { !$0.isLetter && !$0.isNumber })
        if lower.hasPrefix("can you") || lower.hasPrefix("could you") || lower.hasPrefix("would you") {
            return words.count <= 4
        }
        if lower.hasPrefix("what ") || lower.hasPrefix("why ") || lower.hasPrefix("how ") || lower.hasPrefix("when ") || lower.hasPrefix("where ") || lower.hasPrefix("who ") || lower.hasPrefix("which ") {
            return words.count <= 3
        }
        if lower.hasPrefix("please explain") || lower.hasPrefix("explain") || lower.hasPrefix("请解释") || lower.hasPrefix("解释一下") {
            let cjkCount = lower.unicodeScalars.filter { (0x4E00...0x9FFF).contains($0.value) }.count
            return cjkCount > 0 ? cjkCount <= 7 : words.count <= 2
        }
        return false
    }

    private func normalize(_ text: String) -> String {
        let parts = text.lowercased().split(whereSeparator: { !$0.isLetter && !$0.isNumber })
        var tokens: [String] = []
        for part in parts {
            let word = String(part)
            if containsCJK(word) {
                let chars = Array(word)
                if chars.count == 1 {
                    tokens.append(word)
                } else {
                    for idx in 0..<(chars.count - 1) {
                        tokens.append(String(chars[idx...idx + 1]))
                    }
                }
            } else {
                tokens.append(word)
            }
        }
        return tokens.joined(separator: " ")
    }

    private func containsCJK(_ text: String) -> Bool {
        text.unicodeScalars.contains { (0x4E00...0x9FFF).contains($0.value) }
    }

}
