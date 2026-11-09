import Foundation

// MARK: - Telling voices apart (区分对方的每个人)
//
// Each line heard from the other side (or from the room) becomes a 256-number voiceprint
// (WeSpeaker ResNet34-LM, computed in the app target), voiceprints are grouped as they
// arrive, and a voice is only given a number once it has a few substantive lines.
// Adapted from cicada (Apache-2.0, https://github.com/wquguru/cicada, `SpeakerID.swift`);
// the thresholds were tuned there on AMI meetings and are kept as they are. Pure logic with
// plain loops (no Accelerate), so it builds and is tested on Linux. Voiceprints exist only in
// memory for the length of one meeting; nothing here is ever written to disk.

/// Groups voiceprints as lines arrive, never revising a line already labelled. A line joins the
/// closest voice when similar enough; a long line unlike every voice starts a new one; anything
/// else stays unlabelled.
public struct VoiceClusterer: Sendable {
    /// A line must last this long (seconds) to start a new voice: short ones ("嗯", "Yeah.") are
    /// too unreliable to be taken for somebody new.
    public static let newSeconds = 2.0
    /// Cosine similarity to the closest voice at or above which a line joins it. (cicada, tuned on AMI.)
    public static let join: Float = 0.5
    /// A new voice starts only when the closest voice is less similar than this. (cicada, tuned on AMI.)
    public static let startNew: Float = 0.5

    /// Per voice, the sum of the (unit-length) voiceprints that joined it; the direction is
    /// what matters, so no averaging is needed. A merged-away voice becomes all zeros.
    public private(set) var sums: [[Float]] = []

    public init() {}

    /// How many voices exist, merged-away ones included (ids never change).
    public var voiceCount: Int { sums.count }

    /// The user said two voices are one: later lines like either go to `into`.
    public mutating func merge(_ from: Int, into: Int) {
        guard sums.indices.contains(from), sums.indices.contains(into), from != into else { return }
        guard sums[into].count == sums[from].count else { return }
        for index in sums[into].indices { sums[into][index] += sums[from][index] }
        // Never the closest again: similarity to a zero vector is 0.
        sums[from] = [Float](repeating: 0, count: sums[from].count)
    }

    /// The voice of a line, counting it towards that voice; nil when unsure.
    /// `v` is a unit-length voiceprint, `seconds` the length of the line's audio.
    public mutating func label(_ v: [Float], seconds: Double) -> Int? {
        var best = -1
        var bestSimilarity: Float = -1
        for (index, sum) in sums.enumerated() {
            let similarity = Self.similarity(v, to: sum)
            if similarity > bestSimilarity { bestSimilarity = similarity; best = index }
        }
        if best >= 0, bestSimilarity >= Self.join {
            for index in sums[best].indices where index < v.count { sums[best][index] += v[index] }
            return best
        }
        if seconds >= Self.newSeconds, sums.isEmpty || bestSimilarity < Self.startNew {
            sums.append(v)
            return sums.count - 1
        }
        return nil
    }

    /// Cosine similarity of a unit-length voiceprint to a voice's sum: dot(v, s) / |s|.
    /// A zero (merged-away) or empty sum gives 0.
    public static func similarity(_ v: [Float], to sum: [Float]) -> Float {
        let count = min(v.count, sum.count)
        var dot: Float = 0
        var squares: Float = 0
        for index in 0..<count { dot += v[index] * sum[index] }
        for value in sum { squares += value * value }
        return dot / max(squares.squareRoot(), Float.ulpOfOne)
    }
}

/// A voice assigned to a transcript line after the line was archived, appended to the
/// session's `voices.jsonl` (like translations, merged into the lines when read back).
public struct SessionVoiceTag: Codable, Sendable, Equatable {
    public var id: UUID
    public var voice: Int
    public init(id: UUID, voice: Int) { self.id = id; self.voice = voice }
}

/// What the transcript shows for lines from the other side (`.remote`) or the room (`.room`).
/// A voice gets a number only once it has `shownAfter` substantive lines, and only when at
/// least two voices have, so a one-to-one call stays "对方". Numbers follow the order voices
/// reached `shownAfter` lines, so a number never moves once shown. The user's own lines
/// (`.me`) are never told apart.
public enum SpeakerLabels {
    public static let shownAfter = 5

    /// A line long enough to say whose voice it is: three words, or four characters of Chinese or
    /// Japanese or Korean. "Yeah." or a stray "." can make a group of their own; they don't count
    /// towards showing a name.
    public static func substantive(_ text: String) -> Bool {
        let words = text.split(whereSeparator: { $0.isWhitespace }).filter { $0.contains(where: \.isLetter) }
        if words.count >= 3 { return true }
        let wide = text.unicodeScalars.filter {
            $0.properties.isIdeographic || (0x3040...0x30FF).contains($0.value) || (0xAC00...0xD7A3).contains($0.value)
        }
        return wide.count >= 4
    }

    /// Voice id → 1-based speaker number, for the voices on show.
    /// `meVoices`: voices recognised as the user ("我"); they are never numbered and don't count towards
    /// the two people needed.
    public static func voiceNumbers(_ lines: [SessionTranscriptLine], meVoices: Set<Int> = []) -> [Int: Int] {
        var count: [Int: Int] = [:]
        var reached: [Int: Int] = [:]
        for (index, line) in lines.enumerated() where line.speaker != .me {
            guard let voice = line.voice, !meVoices.contains(voice), substantive(line.text) else { continue }
            count[voice, default: 0] += 1
            if count[voice] == shownAfter { reached[voice] = index }
        }
        let shown = reached.keys.sorted { (reached[$0] ?? 0) < (reached[$1] ?? 0) }
        guard shown.count >= 2 else { return [:] }
        var numbers: [Int: Int] = [:]
        for (position, voice) in shown.enumerated() { numbers[voice] = position + 1 }
        return numbers
    }

    /// Line id → 1-based speaker number; lines without one keep showing as 对方 / 现场.
    public static func numbers(_ lines: [SessionTranscriptLine], meVoices: Set<Int> = []) -> [UUID: Int] {
        let number = voiceNumbers(lines, meVoices: meVoices)
        var out: [UUID: Int] = [:]
        for line in lines where line.speaker != .me {
            if let voice = line.voice, let n = number[voice] { out[line.id] = n }
        }
        return out
    }

    /// Speaker number → the name the user gave (empty names are ignored).
    public static func names(_ voiceNames: [String: String]?, lines: [SessionTranscriptLine], meVoices: Set<Int> = []) -> [Int: String] {
        guard let voiceNames, !voiceNames.isEmpty else { return [:] }
        var out: [Int: String] = [:]
        for (voice, n) in voiceNumbers(lines, meVoices: meVoices) {
            if let name = voiceNames[String(voice)], !name.isEmpty { out[n] = name }
        }
        return out
    }

    /// Lines of the other side or the room that are in a voice recognised as the user.
    public static func meLines(_ lines: [SessionTranscriptLine], meVoices: Set<Int>) -> Set<UUID> {
        guard !meVoices.isEmpty else { return [] }
        return Set(lines.compactMap { line in
            guard line.speaker != .me, let voice = line.voice, meVoices.contains(voice) else { return nil }
            return line.id
        })
    }

    /// The user marked or unmarked a voice as themselves; `into` keeps the mark when either had it.
    public static func mergedMe(_ meVoices: [Int]?, from: Int, into: Int) -> [Int]? {
        guard var set = meVoices.map(Set.init) else { return nil }
        if set.remove(from) != nil { set.insert(into) }
        return set.isEmpty ? nil : set.sorted()
    }

    /// Lines where the speaker changes: the transcript shows the name there only, as chat apps
    /// do. Keys compared: me (including lines in `mine`), a speaker number, or "not on show".
    public static func runStarts(_ lines: [SessionTranscriptLine], _ numbers: [UUID: Int], mine: Set<UUID> = []) -> Set<UUID> {
        var out: Set<UUID> = []
        var last: String?
        for line in lines {
            let key = line.speaker == .me || mine.contains(line.id) ? "me" : (numbers[line.id].map { "n\($0)" } ?? "?")
            if key != last { out.insert(line.id) }
            last = key
        }
        return out
    }

    /// "George", else "说话人 2" / "Speaker 2". (The interface localizes its own label.)
    public static func label(_ number: Int, _ names: [Int: String], english: Bool = false) -> String {
        names[number] ?? (english ? "Speaker \(number)" : "说话人 \(number)")
    }

    /// Distinct voices on show, for the people row.
    public static func shownCount(_ lines: [SessionTranscriptLine]) -> Int { Set(numbers(lines).values).count }

    /// A name worth keeping: one line, trimmed, at most 24 characters; nil when nothing is left.
    public static func cleanName(_ raw: String) -> String? {
        let single = raw.components(separatedBy: .newlines).joined(separator: " ")
        let trimmed = String(single.trimmingCharacters(in: .whitespacesAndNewlines).prefix(24))
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    /// The user merged voice `from` into `into`: lines move over, and the name of `from` is kept
    /// only when `into` has none.
    public static func merged(_ voiceNames: [String: String]?, from: Int, into: Int) -> [String: String]? {
        guard var names = voiceNames else { return nil }
        if let moved = names.removeValue(forKey: String(from)), names[String(into)] == nil { names[String(into)] = moved }
        return names.isEmpty ? nil : names
    }

    // MARK: Notes

    /// Line id → the speaker as the notes model reads it: the user's name, else "说话人 N" /
    /// "Speaker N". Only lines whose voice is on show; others keep 对方 / 现场.
    /// Lines in a voice recognised as the user (`meVoices`) read "我" / "Me", always.
    public static func promptLabels(_ lines: [SessionTranscriptLine], voiceNames: [String: String]?, meVoices: Set<Int> = [],
                                    english: Bool) -> [UUID: String] {
        let shown = numbers(lines, meVoices: meVoices)
        let named = names(voiceNames, lines: lines, meVoices: meVoices).compactMapValues(cleanName)
        var out = shown.mapValues { label($0, named, english: english) }
        for id in meLines(lines, meVoices: meVoices) { out[id] = english ? "Me" : "我" }
        return out
    }

    /// A paragraph for the notes prompt when voices are on show; nil otherwise. It tells the
    /// model how to read the labels and to name people on action items.
    public static func promptNote(_ lines: [SessionTranscriptLine], voiceNames: [String: String]?, meVoices: Set<Int> = [],
                                  english: Bool, inPerson: Bool) -> String? {
        let labels = promptLabels(lines, voiceNames: voiceNames, meVoices: meVoices, english: english)
        guard !labels.isEmpty else { return nil }
        let shown = numbers(lines, meVoices: meVoices)
        let hasMe = !meLines(lines, meVoices: meVoices).isEmpty
        if shown.isEmpty {
            // Only the user was recognised: say so, nothing else was told apart.
            return english
                ? "Lines labelled \"Me\" are the user's own words, recognised by voice (occasionally wrong)."
                : "转写里标着“我”的行是用户自己的发言，按声音认出，偶尔会有误判。"
        }
        var seen: [Int: String] = [:]
        for (id, text) in labels { if let n = shown[id] { seen[n] = text } }
        let people = seen.keys.sorted().compactMap { seen[$0] }.joined(separator: english ? ", " : "、")
        let unknown = inPerson ? (english ? "Room" : "现场") : (english ? "Them" : "对方")
        let meNote = hasMe ? (english ? " Lines labelled \"Me\" are the user's own words." : "标着“我”的行是用户自己的发言。") : ""
        if english {
            return """
            Speakers (this paragraph overrides what the system instructions say about not telling people apart): lines labelled with a person's name \
            or "Speaker N" were told apart by voice, which is occasionally wrong. Lines labelled "\(unknown)" are speech that could not be matched to a person. \
            People in this meeting: \(people). Where the transcript makes it clear, say who is responsible on action items (owner) and who made each point. \
            "Speaker N" is not a real name: never guess a name from it; use a name only when the words themselves give it.\(meNote)
            """
        }
        return """
        说话人区分（本段优先于系统说明里“无法区分说话人”的描述）：转写里标着人名或“说话人 N”的行，是按声音区分出的不同的人，偶尔会有误判；\
        标着“\(unknown)”的行是没能认出是谁的发言。本场出现的人：\(people)。在转写能看清的情况下，待办的 owner 和各条要点都写明是谁；\
        “说话人 N”不是真名，不要凭它猜名字，只有原话里点了名才用名字。\(meNote)
        """
    }
}
