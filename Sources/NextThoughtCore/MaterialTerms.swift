import Foundation

/// The technical terms of a meeting's materials: "XL-MIMO", "TSIR", "DeepMIMO", "RQ2".
///
/// Nobody fills in a glossary before a talk, but the slides and the paper already hold
/// the words the recogniser will get wrong ("r q two", "x l my mo"). Taken from the
/// materials, they become hotwords for recognition and the spellings the answer and
/// notes models write. Only tokens that can hardly be anything but a term are taken:
/// acronyms, words with a capital inside, and letters joined to digits.
public enum MaterialTerms {
    public static func extract(from texts: [String], limit: Int = 60) -> [String] {
        var counts: [String: Int] = [:], order: [String] = []
        for text in texts {
            var token = ""
            func flush() {
                defer { token = "" }
                let word = token.trimmingCharacters(in: CharacterSet(charactersIn: "-/"))
                guard isTerm(word) else { return }
                if counts[word] == nil { order.append(word) }
                counts[word, default: 0] += 1
            }
            for character in text {
                if character.isASCII, character.isLetter || character.isNumber || character == "-" || character == "/" { token.append(character) }
                else { flush() }
            }
            flush()
        }
        // Said once, a term is as likely a citation or a typo as a word of the talk.
        let repeated = order.filter { counts[$0, default: 0] >= 2 }
        let ranked = (repeated.isEmpty ? order : repeated).sorted { counts[$0, default: 0] > counts[$1, default: 0] }
        return Array(ranked.prefix(limit))
    }

    static let ordinary: Set<String> = ["PDF", "PPT", "URL", "HTTP", "HTTPS", "WWW", "USA", "UK", "OK", "II", "III", "IV", "VI", "VII", "VIII", "IX",
                                        "FIG", "TABLE", "IEEE", "ACM", "DOI", "ISBN", "ET", "AL"]

    static func isTerm(_ word: String) -> Bool {
        guard (2...24).contains(word.count), let first = word.first, first.isLetter || first.isNumber else { return false }
        let letters = word.filter(\.isLetter)
        guard letters.count >= 2, !ordinary.contains(word.uppercased()) else { return false }
        let capitals = letters.filter(\.isUppercase).count
        let digits = word.filter(\.isNumber).count
        // A unit ("bit/s/Hz") or a double-barrelled surname ("Nimier-David", or its tail "lez-Prelcic").
        if word.contains("/") { return false }
        let parts = word.split(separator: "-")
        if parts.count > 1, parts.contains(where: { part in
            part.count > 2 && part.first?.isUppercase == true && part.dropFirst().allSatisfy(\.isLowercase)
        }) { return false }
        // A year, a page range, a version: not a term.
        if digits > letters.count { return false }
        // XL-MIMO, TSIR, DeepMIMO, RQ2; not a HEADING IN CAPITALS.
        if capitals >= 2 { return !(capitals == letters.count && letters.count > 8) }
        // mmWave, eNodeB: a capital inside a word that starts small.
        if capitals == 1, !first.isUppercase { return true }
        return false
    }
}
