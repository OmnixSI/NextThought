import Foundation

// MARK: - "我的声音": recognising the user among the voices of a meeting
//
// The user records their voice once; the app keeps one averaged voiceprint (256 numbers from the
// same model that tells voices apart in a meeting) on this Mac, and a voice of a meeting that is
// close enough to it is shown as "我". The voiceprint is biometric data: it never leaves the Mac,
// is not part of backups or exports, and can be deleted at any time (see VoiceProfileStore in the
// app target). Pure logic with plain loops, tested on Linux.

public struct VoiceProfile: Codable, Sendable, Equatable {
    /// The model the voiceprint belongs to; a different model makes the stored one useless.
    public static let modelID = "wespeaker-resnet34-lm"
    /// A voice of a meeting counts as the user when its cosine similarity to the profile is at least this.
    /// Higher than the grouping threshold (0.5) on purpose: calling somebody else "我" is worse than missing.
    public static let threshold: Float = 0.55
    /// Recordings needed to build a profile.
    public static let minClips = 3

    public var model: String
    /// Unit-length average of the recordings.
    public var embedding: [Float]
    /// How many recordings went into it.
    public var clips: Int
    public var createdAt: Date

    public init(model: String = VoiceProfile.modelID, embedding: [Float], clips: Int, createdAt: Date) {
        self.model = model; self.embedding = embedding; self.clips = clips; self.createdAt = createdAt
    }

    /// Made by the current model and not empty or all zeros.
    public var isUsable: Bool {
        model == Self.modelID && embedding.contains { $0 != 0 }
    }

    /// Whether a voice of a meeting is the user, given the sum of the voiceprints that joined it
    /// (`VoiceClusterer.sums[voice]`; only its direction matters).
    public func recognises(clusterSum: [Float]) -> Bool {
        guard isUsable, clusterSum.count == embedding.count else { return false }
        return VoiceClusterer.similarity(embedding, to: clusterSum) >= Self.threshold
    }
}

public enum VoiceEnrollment {
    public enum Failure: Error, Equatable, Sendable {
        /// Fewer usable recordings than `VoiceProfile.minClips`.
        case tooFewClips(have: Int, need: Int)
        /// The recordings don't sound like one voice (somebody else spoke, or a lot of noise).
        case inconsistent
    }

    /// Each recording must be at least this similar to the average direction of the others.
    public static let consistency: Float = 0.5

    /// Averages the voiceprints of several recordings of one person. A recording that doesn't match
    /// the rest is dropped; fewer than `minClips` left is a failure.
    public static func make(from clips: [[Float]], now: Date = Date()) -> Result<VoiceProfile, Failure> {
        let size = clips.first?.count ?? 0
        var units: [[Float]] = []
        for clip in clips where size > 0 && clip.count == size {
            guard let unit = normalized(clip) else { continue }
            units.append(unit)
        }
        guard units.count >= VoiceProfile.minClips else {
            return .failure(.tooFewClips(have: units.count, need: VoiceProfile.minClips))
        }
        let kept = units.filter { VoiceClusterer.similarity($0, to: sum(units)) >= consistency }
        guard kept.count >= VoiceProfile.minClips, let mean = normalized(sum(kept)) else { return .failure(.inconsistent) }
        return .success(VoiceProfile(embedding: mean, clips: kept.count, createdAt: now))
    }

    static func sum(_ vectors: [[Float]]) -> [Float] {
        guard let first = vectors.first else { return [] }
        var out = [Float](repeating: 0, count: first.count)
        for vector in vectors { for index in out.indices { out[index] += vector[index] } }
        return out
    }

    static func normalized(_ v: [Float]) -> [Float]? {
        var squares: Float = 0
        for value in v { squares += value * value }
        let length = squares.squareRoot()
        guard length > Float.ulpOfOne, length.isFinite else { return nil }
        return v.map { $0 / length }
    }
}
