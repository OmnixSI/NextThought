import Foundation

/// Resolves evidence without turning a missing passage into the nearest unrelated utterance.
public enum SessionTranscriptLocator {
    public static let timestampTolerance: TimeInterval = 3

    public static func index(lineID: UUID? = nil, offsetSeconds: Int? = nil,
                             startedAt: Date, lines: [SessionTranscriptLine]) -> Int? {
        if let lineID, let exact = lines.firstIndex(where: { $0.id == lineID }) { return exact }
        guard let offsetSeconds, offsetSeconds >= 0 else { return nil }
        let requested = startedAt.addingTimeInterval(TimeInterval(offsetSeconds))
        var best: (index: Int, distance: TimeInterval, anchorDistance: TimeInterval)?
        for (index, line) in lines.enumerated() {
            let distance: TimeInterval
            if let start = line.startedAt, let end = line.endedAt, start <= end {
                if requested < start { distance = start.timeIntervalSince(requested) }
                else if requested > end { distance = requested.timeIntervalSince(end) }
                else { distance = 0 }
            } else {
                // Older archives have only the timestamp printed beside the transcript line.
                distance = abs(line.date.timeIntervalSince(requested))
            }
            guard distance <= timestampTolerance else { continue }
            let anchorDistance = abs(line.date.timeIntervalSince(requested))
            if best == nil || distance < best!.distance || (distance == best!.distance && anchorDistance < best!.anchorDistance) {
                best = (index, distance, anchorDistance)
            }
        }
        return best?.index
    }
}
