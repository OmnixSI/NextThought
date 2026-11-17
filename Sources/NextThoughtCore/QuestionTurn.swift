import Foundation

/// The other side's turn in a question round: everything they have said since I last
/// spoke. After a talk, an examiner's turn is the question — often several sentences,
/// more than one question, or a doubt put as a statement — so the answer is for the
/// whole turn, and it is asked for once they stop.
public struct QuestionTurn: Sendable, Equatable {
    public struct Config: Sendable, Equatable {
        /// Silence after their last sentence that ends the turn.
        public var quiet: TimeInterval = 2
        /// A pause this long between two of their sentences starts a new turn.
        public var gap: TimeInterval = 25
        /// An unanswered turn is dropped after this long.
        public var patience: TimeInterval = 30
        /// Words the turn must hold before an answer is asked for.
        public var minimumWords = 8
        /// Words said after an answer was asked for that make it worth asking again.
        public var growth = 8
        /// Words of mine that end their turn; "yes" and "mm" do not.
        public var reply = 3
        public var limit = 1_600
        public init() {}
    }

    public var config = Config()
    public private(set) var sentences: [String] = []
    public private(set) var lastHeardAt: Date?
    /// Words of the turn that the latest answer request covered.
    public private(set) var answeredWords = 0
    public private(set) var requests = 0

    public init() {}

    public var isEmpty: Bool { sentences.isEmpty }
    public var words: Int { sentences.reduce(0) { $0 + Self.words($1) } }
    /// Words they have said that no answer covers yet.
    public var unanswered: Int { max(0, words - answeredWords) }

    /// The turn as one question, with the sentence still being said.
    public func text(with partial: String = "") -> String {
        let live = partial.trimmingCharacters(in: .whitespacesAndNewlines)
        let all = live.isEmpty ? sentences : sentences + [live]
        return String(all.joined(separator: " ").suffix(config.limit)).trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// A finished sentence of theirs.
    public mutating func heard(_ sentence: String, at date: Date) {
        let text = sentence.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty else { return }
        if let lastHeardAt, date.timeIntervalSince(lastHeardAt) > config.gap { reset() }
        sentences.append(text)
        lastHeardAt = max(lastHeardAt ?? date, date)
    }

    /// A finished sentence of mine: a real reply ends their turn.
    public mutating func spoke(_ sentence: String) {
        if Self.words(sentence) >= config.reply { reset() }
    }

    public mutating func reset() {
        sentences = []; lastHeardAt = nil; answeredWords = 0; requests = 0
    }

    /// Whether an answer for the turn is worth asking for now that a question was heard
    /// in it: the first one always is, a later one only when they have said more.
    public func worthAsking(with partial: String = "") -> Bool {
        let total = words + Self.words(partial)
        return requests == 0 ? total >= 2 : total - answeredWords >= config.growth
    }

    /// They have stopped (or I have started to reply) and what they said has no answer.
    public func isDue(now: Date, partial: String, replying: Bool) -> Bool {
        guard let lastHeardAt, !sentences.isEmpty,
              partial.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return false }
        let waited = now.timeIntervalSince(lastHeardAt)
        guard waited <= config.patience, replying || waited >= config.quiet else { return false }
        return requests == 0 ? words >= config.minimumWords : unanswered >= config.growth
    }

    /// An answer was asked for with everything said so far.
    public mutating func asked(with partial: String = "") {
        answeredWords = words + Self.words(partial)
        requests += 1
    }

    /// Words in Latin script count one each, Chinese characters one each.
    public static func words(_ text: String) -> Int {
        var count = 0, inWord = false
        for scalar in text.unicodeScalars {
            if (0x3400...0x9FFF).contains(scalar.value) { count += 1; inWord = false }
            else if CharacterSet.alphanumerics.contains(scalar) { if !inWord { count += 1; inWord = true } }
            else if scalar != "'" && scalar != "-" { inWord = false }
        }
        return count
    }
}
