import Foundation

/// The final words of an utterance, chosen from the two passes of the local recognizer.
///
/// The live stream hears everything since the last utterance ended. The offline pass
/// re-decodes only the samples the voice-activity segmenter cut out, and the segmenter
/// goes by loudness: speech from across a room, or a quiet speaker, is recognised by the
/// stream but is not "speech" to it. When a loud sentence finally closed the segment, the
/// offline text of those few seconds used to replace everything the stream had shown for
/// minutes (395 s of a talk came out as 61 characters). The offline text is better, so it
/// wins whenever it covers what the stream heard; otherwise the stream's words are kept.
public enum UtteranceText {
    /// - Parameters:
    ///   - streaming: what the live stream decoded since the last utterance ended.
    ///   - streamSeconds: how much audio that is.
    ///   - refined: the offline pass over the segment.
    ///   - segmentSeconds: how much audio the segment holds.
    public static func final(streaming: String, streamSeconds: Double, refined: String, segmentSeconds: Double) -> String {
        let heard = units(streaming), redone = units(refined)
        if redone == 0 { return streaming }
        if heard == 0 { return refined }
        // The stream ran on well before the segment began, and the offline text is much
        // shorter than what was heard: it only has the end of it.
        if streamSeconds > segmentSeconds + 2, redone * 10 < heard * 7 { return streaming }
        return refined
    }

    /// Latin words and CJK characters, one each.
    public static func units(_ text: String) -> Int {
        var count = 0, inWord = false
        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 0x3400...0x4DBF, 0x4E00...0x9FFF, 0xF900...0xFAFF, 0x20000...0x2FA1F:
                count += 1; inWord = false
            case 0x30...0x39, 0x41...0x5A, 0x61...0x7A, 0xC0...0x24F, 0x27, 0x2019:
                if !inWord { count += 1; inWord = true }
            default:
                inWord = false
            }
        }
        return count
    }
}

/// When the live stream should be closed without the segmenter: the words that are on
/// screen but belong to no finished sentence yet.
///
/// Normally the segmenter closes an utterance 0.7 s after the speaker stops, or after
/// 12 s of talk. It never does for speech it does not count as speech, and then nothing
/// became a sentence for minutes: no line in the transcript, nothing for the notes,
/// nothing a question could be recognised in.
public struct PendingUtterance: Sendable, Equatable {
    /// The words have not changed for this long: the speaker stopped.
    public static let quiet: TimeInterval = 2.5
    /// The words have been pending this long: the speaker goes on, the line is cut here.
    public static let longest: TimeInterval = 20

    public private(set) var text = ""
    public private(set) var since: Date?
    public private(set) var changedAt: Date?

    public init() {}

    /// The live stream decoded `text` for the utterance in progress.
    public mutating func heard(_ text: String, at now: Date) {
        guard !text.isEmpty else { return }
        if since == nil { since = now }
        if text != self.text { self.text = text; changedAt = now }
    }

    /// The utterance was closed (by the segmenter, or here).
    public mutating func closed() { text = ""; since = nil; changedAt = nil }

    /// Nobody else is going to close it.
    public func isOverdue(at now: Date) -> Bool {
        guard let since, let changedAt, !text.isEmpty else { return false }
        return now.timeIntervalSince(changedAt) >= Self.quiet || now.timeIntervalSince(since) >= Self.longest
    }
}
