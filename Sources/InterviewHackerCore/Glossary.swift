import Foundation

// MARK: - Entries

/// What a glossary entry names: a technical term or a person.
public enum GlossaryKind: String, Codable, Sendable, CaseIterable {
    case term, person

    /// Unknown values (from a newer version) read as `.term`; “人名” is accepted too.
    public init(from decoder: Decoder) throws {
        let raw = (try? decoder.singleValueContainer().decode(String.self)) ?? ""
        self = GlossaryKind.parse(raw) ?? .term
    }

    /// “人名 / person / name” → person, “术语 / term / 词” → term, anything else nil.
    public static func parse(_ text: String) -> GlossaryKind? {
        switch text.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
        case "person", "人名", "姓名", "name", "people": return .person
        case "term", "术语", "词", "专有名词", "word": return .term
        default: return nil
        }
    }

    public var title: String { self == .person ? "人名" : "术语" }
    public var englishTitle: String { self == .person ? "person" : "term" }
}

/// One name or term: its canonical spelling and the spellings speech recognition got wrong.
public struct GlossaryEntry: Codable, Identifiable, Equatable, Sendable {
    public var id: UUID
    /// The spelling every transcript, answer and note should use.
    public var canonical: String
    public var kind: GlossaryKind
    /// Wrong spellings the recognizer produced (“库伯奈提斯”, “K8s”). Never used as hotwords.
    public var variants: [String]
    public var note: String?

    public init(id: UUID = UUID(), canonical: String, kind: GlossaryKind = .term, variants: [String] = [], note: String? = nil) {
        self.id = id; self.canonical = canonical; self.kind = kind; self.variants = variants; self.note = note
    }

    enum CodingKeys: String, CodingKey { case id, canonical, kind, variants, note }

    // Tolerant decoding: only `canonical` is required.
    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = (try? c.decodeIfPresent(UUID.self, forKey: .id)) ?? UUID()
        canonical = try c.decode(String.self, forKey: .canonical)
        kind = (try? c.decodeIfPresent(GlossaryKind.self, forKey: .kind)) ?? .term
        variants = (try? c.decodeIfPresent([String].self, forKey: .variants)) ?? []
        note = try? c.decodeIfPresent(String.self, forKey: .note)
    }

    /// The canonical spelling followed by the wrong ones.
    public var spellings: [String] { [canonical] + variants }
}

// MARK: - Search

public struct GlossarySearchHit: Equatable, Sendable {
    /// The spelling found in the text.
    public let form: String
    /// True when `form` is a recorded wrong spelling the person did not type themselves.
    public let isMisheard: Bool

    public init(form: String, isMisheard: Bool) { self.form = form; self.isMisheard = isMisheard }
}

/// A search term together with every spelling the glossary knows for it.
public struct GlossarySearch: Equatable, Sendable {
    /// What the person typed (trimmed).
    public let query: String
    /// `query` first, then the other spellings; no duplicates.
    public let forms: [String]
    let misheardKeys: Set<String>

    /// The first spelling found in `text`. The typed query wins over the other spellings, so a
    /// plain hit is never reported as a misheard one. Matching ignores case and full/half width.
    public func firstMatch(in text: String) -> GlossarySearchHit? {
        guard !forms.isEmpty else { return nil }
        for (index, form) in forms.enumerated() where Self.contains(text, form) {
            let misheard = index > 0 && misheardKeys.contains(Glossary.strictKey(form))
            return GlossarySearchHit(form: form, isMisheard: misheard)
        }
        return nil
    }

    public func matches(_ text: String) -> Bool { firstMatch(in: text) != nil }

    private static func contains(_ text: String, _ form: String) -> Bool {
        text.range(of: form, options: [.caseInsensitive, .widthInsensitive]) != nil
    }
}

// MARK: - Canonicalization report

public struct GlossaryReplacement: Equatable, Sendable {
    public let from: String
    public let to: String
    public var count: Int
}

public struct GlossaryCanonicalization: Equatable, Sendable {
    public let text: String
    public let replacements: [GlossaryReplacement]
    public var replacementCount: Int { replacements.reduce(0) { $0 + $1.count } }
}

// MARK: - Glossary

/// The user's table of names and terms (“术语和人名”). Pure values and functions: a global table and
/// an optional table per series are combined with `merging`.
public struct Glossary: Codable, Equatable, Sendable {
    public var entries: [GlossaryEntry]

    public init(entries: [GlossaryEntry] = []) { self.entries = entries }

    public var isEmpty: Bool { entries.isEmpty }

    enum CodingKeys: String, CodingKey { case entries }

    private struct Lossy<Value: Decodable>: Decodable {
        let value: Value?
        init(from decoder: Decoder) throws { value = try? Value(from: decoder) }
    }

    /// Accepts `{"entries": [...]}` or a bare array, and skips entries it cannot read.
    public init(from decoder: Decoder) throws {
        if let keyed = try? decoder.container(keyedBy: CodingKeys.self) {
            let items = try? keyed.decodeIfPresent([Lossy<GlossaryEntry>].self, forKey: .entries)
            entries = (items ?? []).compactMap { $0.value }
        } else if let single = try? decoder.singleValueContainer(), let items = try? single.decode([Lossy<GlossaryEntry>].self) {
            entries = items.compactMap { $0.value }
        } else {
            entries = []
        }
    }

    // MARK: Comparison keys

    /// Equality key for lookups: width-folded (NFKC), lowercased, with whitespace and punctuation
    /// removed. “Open AI”, “open-ai” and “ＯｐｅｎＡＩ” are the same key; “+” and “#” are kept so
    /// “C++” and “C#” stay different from “C”.
    public static func looseKey(_ text: String) -> String {
        let folded = text.precomposedStringWithCompatibilityMapping.lowercased()
        var scalars = String.UnicodeScalarView()
        for scalar in folded.unicodeScalars {
            if CharacterSet.whitespacesAndNewlines.contains(scalar) { continue }
            if scalar.properties.generalCategory == .format { continue }
            if CharacterSet.punctuationCharacters.contains(scalar), scalar != "#" { continue }
            scalars.append(scalar)
        }
        return String(scalars)
    }

    /// Like `looseKey` but keeps punctuation and single spaces: used to tell two spellings apart.
    public static func strictKey(_ text: String) -> String {
        let folded = text.precomposedStringWithCompatibilityMapping.lowercased()
        return folded.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    /// Trimmed, inner whitespace runs collapsed to one space.
    static func tidy(_ text: String) -> String {
        text.split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
    }

    // MARK: Cleaning and merging

    /// Trims, drops empty entries, merges entries whose canonical spellings share a `looseKey`
    /// (the first keeps its id, kind and note; variants are united) and cleans each variant list:
    /// no empties, none equal to the canonical spelling, none shorter than two characters (a single
    /// character would match everywhere), duplicates (ignoring case and width) removed.
    public func normalized() -> Glossary {
        var result: [GlossaryEntry] = []
        var indexByKey: [String: Int] = [:]
        for var entry in entries {
            entry.canonical = Self.tidy(entry.canonical)
            let key = Self.looseKey(entry.canonical)
            guard !entry.canonical.isEmpty, !key.isEmpty else { continue }
            let note = entry.note.map(Self.tidy)
            entry.note = (note?.isEmpty ?? true) ? nil : note
            if let index = indexByKey[key] {
                // The same name spelled differently (“OpenAI” / “Open AI”): the later spelling becomes a variant.
                result[index].variants += [entry.canonical] + entry.variants
                if result[index].note == nil { result[index].note = entry.note }
            } else {
                indexByKey[key] = result.count
                result.append(entry)
            }
        }
        for index in result.indices {
            result[index].variants = Self.cleanVariants(result[index].variants, canonical: result[index].canonical)
        }
        return Glossary(entries: result)
    }

    static func cleanVariants(_ variants: [String], canonical: String) -> [String] {
        var seen = Set<String>()
        var cleaned: [String] = []
        for variant in variants.map(tidy) {
            guard variant.count >= 2, variant != canonical, !looseKey(variant).isEmpty else { continue }
            if seen.insert(strictKey(variant)).inserted { cleaned.append(variant) }
        }
        return cleaned
    }

    /// This table followed by `other`, normalized. On the same canonical spelling the entry of
    /// this table keeps its id, kind and note; the variants are united.
    public func merging(_ other: Glossary) -> Glossary {
        Glossary(entries: entries + other.entries).normalized()
    }

    /// The table in force for a series: the global table plus the series' own.
    public static func layered(global: Glossary, series: Glossary?) -> Glossary {
        guard let series, !series.isEmpty else { return global.normalized() }
        return global.merging(series)
    }

    /// The entry whose canonical spelling or a variant equals `text` under `looseKey`.
    public func entry(matching text: String) -> GlossaryEntry? {
        let key = Self.looseKey(text)
        guard !key.isEmpty else { return nil }
        return entries.first { entry in entry.spellings.contains { Self.looseKey($0) == key } }
    }

    // MARK: Search

    static let maxExpansion = 32

    /// Every spelling to look for when the person searches for `query`.
    ///
    /// - The query comes first, trimmed.
    /// - When the query equals (under `looseKey`) the canonical spelling or a variant of an entry,
    ///   all spellings of that entry follow, canonical first.
    /// - Otherwise, when the query contains a spelling (“k8s 部署”), the same query with that
    ///   spelling replaced by each other spelling of its entry follows (“Kubernetes 部署”).
    /// At most 32 forms are returned.
    public func expand(query: String) -> [String] { search(for: query).forms }

    /// `expand`, plus the knowledge of which spellings are recorded misheard ones.
    public func search(for query: String) -> GlossarySearch {
        let typed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !typed.isEmpty else { return GlossarySearch(query: "", forms: [], misheardKeys: []) }
        var forms = [typed]
        var seen: Set<String> = [Self.strictKey(typed)]
        var misheard = Set<String>()
        func add(_ form: String) {
            let key = Self.strictKey(form)
            guard !key.isEmpty, forms.count < Self.maxExpansion, seen.insert(key).inserted else { return }
            forms.append(form)
        }
        let canonicalKeys = Set(entries.map { Self.strictKey($0.canonical) })
        for entry in entries { for variant in entry.variants where !canonicalKeys.contains(Self.strictKey(variant)) { misheard.insert(Self.strictKey(variant)) } }

        let key = Self.looseKey(typed)
        var found = false
        if !key.isEmpty {
            for entry in entries where entry.spellings.contains(where: { Self.looseKey($0) == key }) {
                found = true
                entry.spellings.forEach(add)
            }
        }
        if !found {
            let chars = Array(typed)
            for span in scan(chars, caseSensitiveCanonical: false) {
                let entry = entries[span.candidate.entryIndex]
                let before = String(chars[0..<span.start]), after = String(chars[span.end...])
                let original = Self.strictKey(String(chars[span.start..<span.end]))
                for spelling in entry.spellings where Self.strictKey(spelling) != original {
                    let composed = before + spelling + after
                    if entry.variants.contains(spelling), !canonicalKeys.contains(Self.strictKey(spelling)) {
                        misheard.insert(Self.strictKey(composed))
                    }
                    add(composed)
                }
            }
        }
        return GlossarySearch(query: typed, forms: forms, misheardKeys: misheard)
    }

    // MARK: Canonicalization

    /// `text` with every recorded wrong spelling replaced by its canonical spelling.
    ///
    /// Rules:
    /// - Scanning is left to right; at a position the longest spelling wins. An entry's canonical
    ///   spelling takes part in the matching but is never rewritten, so “OpenAI Gym” is not
    ///   disturbed by a variant “Open”, and a canonical that contains another entry's variant
    ///   protects itself.
    /// - Variants match ignoring case and full/half width (“k8s” matches “K8S”). Canonical
    ///   spellings protect only when the case is exact, so a variant “openai” still fixes “openai”.
    /// - A spelling that starts or ends with a Latin letter or digit must not touch another Latin
    ///   letter, digit or underscore on that side (word boundary): the variant “GPT” does not match
    ///   inside “GPTs” or “MGPT”. Next to Chinese characters or punctuation it does match.
    /// - A spelling that starts or ends with a Chinese character matches as a plain substring on that side.
    /// - Whitespace must match exactly (“Open AI” and “OpenAI” are different variants).
    /// - When two entries list the same variant, the entry that comes first wins.
    public func canonicalize(text: String) -> String { canonicalizeDetailed(text).text }

    /// `canonicalize` with a count of what was replaced.
    public func canonicalizeDetailed(_ text: String) -> GlossaryCanonicalization {
        let chars = Array(text)
        let spans = scan(chars, caseSensitiveCanonical: true)
        guard !spans.isEmpty else { return GlossaryCanonicalization(text: text, replacements: []) }
        var output = ""
        var cursor = 0
        var replacements: [GlossaryReplacement] = []
        for span in spans where !span.candidate.isCanonical {
            let canonical = entries[span.candidate.entryIndex].canonical
            let original = String(chars[span.start..<span.end])
            guard original != canonical else { continue }
            output += String(chars[cursor..<span.start]) + canonical
            cursor = span.end
            if let index = replacements.firstIndex(where: { $0.from == original && $0.to == canonical }) {
                replacements[index].count += 1
            } else {
                replacements.append(GlossaryReplacement(from: original, to: canonical, count: 1))
            }
        }
        output += String(chars[cursor...])
        return GlossaryCanonicalization(text: output, replacements: replacements)
    }

    // MARK: Matching engine

    private struct Candidate {
        let entryIndex: Int
        let isCanonical: Bool
        let order: Int
        /// Width-folded and lowercased.
        let lower: [Character]
        /// Width-folded, case kept.
        let kept: [Character]
        let needsLeftBoundary: Bool
        let needsRightBoundary: Bool
    }

    private struct Span {
        let start: Int
        /// Exclusive.
        let end: Int
        let candidate: Candidate
    }

    /// Candidates by the first (lowercased) character, longest first; on equal length the canonical
    /// spelling first, then entry order.
    private func candidateIndex() -> [Character: [Candidate]] {
        var index: [Character: [Candidate]] = [:]
        var order = 0
        for (entryIndex, entry) in entries.enumerated() {
            let canonical = Self.tidy(entry.canonical)
            let spellings: [(String, Bool)] = [(canonical, true)] + entry.variants.map { (Self.tidy($0), false) }
            for (spelling, isCanonical) in spellings {
                let raw = Array(spelling)
                guard !raw.isEmpty, isCanonical || raw.count >= 2 else { continue }
                let lower = raw.map { Self.fold($0, lowercase: true) }
                let kept = raw.map { Self.fold($0, lowercase: false) }
                guard let first = lower.first, let last = lower.last else { continue }
                order += 1
                index[first, default: []].append(Candidate(
                    entryIndex: entryIndex, isCanonical: isCanonical, order: order, lower: lower, kept: kept,
                    needsLeftBoundary: Self.isLatinWord(first), needsRightBoundary: Self.isLatinWord(last)))
            }
        }
        for key in Array(index.keys) {
            index[key]?.sort { lhs, rhs in
                if lhs.lower.count != rhs.lower.count { return lhs.lower.count > rhs.lower.count }
                if lhs.isCanonical != rhs.isCanonical { return lhs.isCanonical }
                return lhs.order < rhs.order
            }
        }
        return index
    }

    private func scan(_ chars: [Character], caseSensitiveCanonical: Bool) -> [Span] {
        guard !chars.isEmpty, !entries.isEmpty else { return [] }
        let index = candidateIndex()
        guard !index.isEmpty else { return [] }
        let lower = chars.map { Self.fold($0, lowercase: true) }
        let kept = chars.map { Self.fold($0, lowercase: false) }
        var spans: [Span] = []
        var i = 0
        while i < lower.count {
            var hit: Candidate?
            for candidate in index[lower[i]] ?? [] {
                let n = candidate.lower.count
                guard i + n <= lower.count else { continue }
                let same: Bool
                if candidate.isCanonical && caseSensitiveCanonical {
                    same = kept[i..<(i + n)].elementsEqual(candidate.kept)
                } else {
                    same = lower[i..<(i + n)].elementsEqual(candidate.lower)
                }
                guard same else { continue }
                if candidate.needsLeftBoundary, i > 0, Self.isLatinWord(lower[i - 1]) { continue }
                if candidate.needsRightBoundary, i + n < lower.count, Self.isLatinWord(lower[i + n]) { continue }
                hit = candidate
                break
            }
            if let hit {
                spans.append(Span(start: i, end: i + hit.lower.count, candidate: hit))
                i += hit.lower.count
            } else {
                i += 1
            }
        }
        return spans
    }

    /// Full-width to half-width (NFKC) and, optionally, lowercase; characters that would change
    /// length are kept as they are.
    static func fold(_ character: Character, lowercase: Bool) -> Character {
        if let ascii = character.asciiValue {
            if lowercase, ascii >= 65, ascii <= 90 { return Character(UnicodeScalar(ascii + 32)) }
            return character
        }
        var text = String(character).precomposedStringWithCompatibilityMapping
        if lowercase { text = text.lowercased() }
        return text.count == 1 ? text[text.startIndex] : character
    }

    /// ASCII letters and digits, underscore and the Latin-1 / Latin Extended letters: the characters
    /// that make a Latin “word”.
    static func isLatinWord(_ character: Character) -> Bool {
        guard let value = character.unicodeScalars.first?.value else { return false }
        switch value {
        case 0x30...0x39, 0x41...0x5A, 0x61...0x7A, 0x5F: return true
        case 0xC0...0x24F: return value != 0xD7 && value != 0xF7
        default: return false
        }
    }

    // MARK: Hotwords

    /// Characters `HotwordsFile.clean` splits on; a hotword containing one would be cut in pieces.
    private static let hotwordSeparators = CharacterSet(charactersIn: ",，、;；\n\r")

    /// Whether `word` can go into a hotwords file as one line: 2 to 40 characters, none of the
    /// separators above, no “:” (the file's score separator) and no space-separated piece starting
    /// with “:”, “#” or “@” (score, threshold and tag markers of the sherpa-onnx hotwords format).
    static func isUsableHotword(_ word: String) -> Bool {
        guard word.count >= 2, word.count <= 40 else { return false }
        if word.rangeOfCharacter(from: hotwordSeparators) != nil { return false }
        if word.contains(":") { return false }
        for piece in word.split(separator: " ") where piece.hasPrefix("#") || piece.hasPrefix("@") { return false }
        return true
    }

    /// Canonical spellings for the recognizer's hotword list, at most `limit`. Entries that have
    /// recorded misheard spellings come first (those are the words the recognizer actually gets
    /// wrong), then the rest in table order. Variants are never included: boosting a wrong
    /// spelling would make the recognizer produce it more often.
    public func hotwords(limit: Int = 80) -> [String] {
        guard limit > 0 else { return [] }
        let ordered = entries.enumerated().sorted { lhs, rhs in
            let left = !lhs.element.variants.isEmpty, right = !rhs.element.variants.isEmpty
            return left != right ? left : lhs.offset < rhs.offset
        }.map(\.element)
        var seen = Set<String>()
        var words: [String] = []
        for entry in ordered {
            let word = Self.tidy(entry.canonical)
            guard Self.isUsableHotword(word), seen.insert(Self.strictKey(word)).inserted else { continue }
            words.append(word)
            if words.count >= limit { break }
        }
        return words
    }

    /// The text for `ZipformerRuntime.configure(hotwords:)`: the user's own vocabulary
    /// (comma-separated, as typed in Settings) followed by the glossary's words, de-duplicated
    /// ignoring case. The user's words are never dropped; the glossary's words fill the rest up to `limit`.
    public func hotwordString(merging base: String, limit: Int = 80) -> String {
        let own = base.components(separatedBy: CharacterSet(charactersIn: ",，、;；\n\r"))
            .map(Self.tidy).filter { !$0.isEmpty }
        var seen = Set<String>()
        var words: [String] = []
        for word in own where seen.insert(Self.strictKey(word)).inserted { words.append(word) }
        for word in hotwords(limit: max(0, limit - words.count)) where seen.insert(Self.strictKey(word)).inserted { words.append(word) }
        return words.joined(separator: ",")
    }

    // MARK: Prompt section

    /// A block for the model's prompt: canonical spellings and the misheard ones they replace.
    /// Entries with misheard spellings come first. When the block would exceed `maxCharacters`,
    /// later entries are left out and a last line says how many. Empty when there is nothing to
    /// say or nothing fits.
    public func promptSection(maxCharacters: Int = 1_500, english: Bool = false) -> String {
        let usable = entries.filter { !Self.tidy($0.canonical).isEmpty }
        guard !usable.isEmpty else { return "" }
        let header = english
            ? "Names and terms (canonical spelling ← spellings the speech recognizer often gets wrong). Treat a misheard spelling as the canonical one and always write the canonical spelling:"
            : "术语与人名对照（规范写法 ← 语音识别常听错的写法）。遇到听错的写法，按规范写法理解；你输出的内容一律写规范写法："
        func marker(_ omitted: Int) -> String { english ? "(\(omitted) more not shown)" : "（另有 \(omitted) 条未列出）" }
        let ordered = usable.enumerated().sorted { lhs, rhs in
            let left = !lhs.element.variants.isEmpty, right = !rhs.element.variants.isEmpty
            return left != right ? left : lhs.offset < rhs.offset
        }.map(\.element)
        let lines = ordered.map { Self.promptLine($0, english: english) }

        let total = header.count + lines.reduce(0) { $0 + $1.count + 1 }
        if total <= maxCharacters { return ([header] + lines).joined(separator: "\n") }

        let reserve = marker(lines.count).count + 1
        var used = header.count
        var included: [String] = []
        for line in lines {
            guard used + line.count + 1 + reserve <= maxCharacters else { break }
            included.append(line)
            used += line.count + 1
        }
        guard !included.isEmpty else { return "" }
        return ([header] + included + [marker(lines.count - included.count)]).joined(separator: "\n")
    }

    static func promptLine(_ entry: GlossaryEntry, english: Bool) -> String {
        var line = "- " + String(Self.tidy(entry.canonical).prefix(60))
        if entry.kind == .person { line += english ? " (person)" : "（人名）" }
        let variants = entry.variants.map { String(Self.tidy($0).prefix(30)) }.filter { !$0.isEmpty }.prefix(6)
        if !variants.isEmpty { line += " ← " + variants.joined(separator: english ? ", " : "、") }
        if let note = entry.note.map(Self.tidy), !note.isEmpty { line += (english ? " — " : "：") + String(note.prefix(40)) }
        return line
    }

    // MARK: Text import and export

    private static let variantSeparators = CharacterSet(charactersIn: ",，、;；")

    /// Entries from pasted text, one per line: `规范写法 | 变体1, 变体2 | 类型 | 备注`. Only the
    /// first column is required. The third column is a type (“人名” / “术语” / person / term);
    /// when it is anything else it is read as the note. Blank lines and lines starting with “#”
    /// are skipped. Entries are not merged or cleaned; pass the result through `normalized()`.
    public static func parse(lines text: String, defaultKind: GlossaryKind = .term) -> [GlossaryEntry] {
        var entries: [GlossaryEntry] = []
        for rawLine in text.components(separatedBy: CharacterSet.newlines) {
            let line = rawLine.trimmingCharacters(in: .whitespaces)
            guard !line.isEmpty, !line.hasPrefix("#") else { continue }
            let columns = line.replacingOccurrences(of: "｜", with: "|").components(separatedBy: "|")
                .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            let canonical = tidy(columns[0])
            guard !canonical.isEmpty else { continue }
            let variants = columns.count > 1
                ? columns[1].components(separatedBy: variantSeparators).map(tidy).filter { !$0.isEmpty } : []
            var kind = defaultKind
            var note: String?
            if columns.count > 2 {
                if let parsed = GlossaryKind.parse(columns[2]) {
                    kind = parsed
                    if columns.count > 3, !columns[3].isEmpty { note = columns[3] }
                } else if !columns[2].isEmpty {
                    note = columns[2]
                }
            }
            entries.append(GlossaryEntry(canonical: canonical, kind: kind, variants: variants, note: note))
        }
        return entries
    }

    /// The table as text that `parse(lines:)` reads back.
    public func exportText() -> String {
        func plain(_ text: String) -> String { Self.tidy(text.replacingOccurrences(of: "|", with: "/")) }
        return entries.map { entry in
            var columns = [plain(entry.canonical), entry.variants.map(plain).joined(separator: ", ")]
            let note = entry.note.map(plain) ?? ""
            columns.append(entry.kind == .person ? "人名" : (note.isEmpty ? "" : "术语"))
            columns.append(note)
            while let last = columns.last, last.isEmpty, columns.count > 1 { columns.removeLast() }
            return columns.joined(separator: " | ")
        }.joined(separator: "\n")
    }
}
