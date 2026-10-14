import Foundation

public struct QuestionDetector {
    private var recentKeys: [String] = []
    private var pendingFragment = ""

    public init() {}

    public mutating func detect(_ text: String) -> String? {
        let incoming = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !incoming.isEmpty else { return nil }

        var question = incoming
        if !pendingFragment.isEmpty {
            question = pendingFragment + " " + incoming
            pendingFragment = ""
        }

        if isIncomplete(question) {
            pendingFragment = question
            return nil
        }
        guard looksLikeQuestion(question) else { return nil }
        guard question.count >= 4 || isShortFollowUp(question) else { return nil }

        let key = normalize(question)
        guard !key.isEmpty else { return nil }
        for previous in recentKeys.suffix(12) {
            if previous == key || similarity(previous, key) >= 0.78 {
                return nil
            }
        }
        recentKeys.append(key)
        if recentKeys.count > 30 {
            recentKeys.removeFirst(recentKeys.count - 30)
        }
        return question
    }

    private func looksLikeQuestion(_ text: String) -> Bool {
        let lower = text.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        if lower.contains("?") || lower.contains("？") { return true }

        let englishStarts = [
            "what ", "what's", "why ", "how ", "when ", "where ", "who ", "which ",
            "can you ", "could you ", "would you ", "tell me ", "explain ",
            "describe ", "compare ", "please explain ", "walk me through ",
            "help me understand ", "difference between "
        ]
        if englishStarts.contains(where: { lower.hasPrefix($0) }) { return true }

        let chineseMarkers = [
            "你觉得", "为什么", "怎么", "如何", "能不能", "可不可以", "请解释", "解释一下",
            "请说明", "说明一下", "分析一下", "比较一下", "讲讲", "谈谈", "什么是", "有什么",
            "哪些", "是否", "设计思路是什么", "怎么样"
        ]
        return chineseMarkers.contains(where: { lower.contains($0) })
    }

    private func isShortFollowUp(_ text: String) -> Bool {
        let lower = text.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
        let followUps = ["为什么", "怎么", "然后呢", "那为什么", "那怎么", "why", "how", "and why", "what about that"]
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
        tokens.sort()
        return tokens.joined(separator: " ")
    }

    private func containsCJK(_ text: String) -> Bool {
        text.unicodeScalars.contains { (0x4E00...0x9FFF).contains($0.value) }
    }

    private func similarity(_ first: String, _ second: String) -> Double {
        let left = Set(first.split(separator: " "))
        let right = Set(second.split(separator: " "))
        let total = left.union(right).count
        return total == 0 ? 0 : Double(left.intersection(right).count) / Double(total)
    }
}
