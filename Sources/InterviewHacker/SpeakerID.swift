import Accelerate
import CoreML
import Foundation
import InterviewHackerCore
import Observation

// Experimental 区分对方的每个人 (Settings → 语音识别 → 区分对方的每个人). Each sentence heard from the
// other side (online) or from the room (in person) is cut out of a short in-memory audio window,
// turned into a voiceprint (WeSpeaker ResNet34-LM, VoxCeleb, CC BY 4.0, bundled as Core ML),
// and the voiceprints are grouped as they arrive (`VoiceClusterer` in InterviewHackerCore). The
// feature extraction, model loading and tagger are adapted from cicada (Apache-2.0,
// https://github.com/wquguru/cicada, `SpeakerID.swift`); the model comes from
// `scripts/convert-voiceprint.py`.
//
// Privacy, on purpose: everything runs on this Mac; the audio window lives in memory for about a
// minute; a voiceprint is only a 256-number vector held while the meeting runs. Nothing about a
// voice of a meeting is saved or reused across meetings: when a meeting ends the grouping is dropped, and
// only the meeting-local voice number of each line (and any name the user typed) stays in the record.
// The one exception is 我的声音 (MyVoice.swift): the user's own voiceprint, recorded on purpose, kept in
// one local file and deletable; a voice of a meeting close to it is shown as "我".

/// Kaldi log-mel filterbank, as `torchaudio.compliance.kaldi.fbank` computes it with the settings the
/// model was trained on: 16 kHz, 25 ms Povey windows every 10 ms, DC removed, pre-emphasis 0.97,
/// 512-point power spectrum, 80 mel bins from 20 Hz to 8 kHz, natural log, samples scaled to 16-bit.
enum Fbank {
    static let bins = 80
    static let frameLength = 400
    static let shift = 160
    private static let fftSize = 512

    private static let window: [Float] = (0..<frameLength).map {
        pow(0.5 - 0.5 * cos(2 * Float.pi * Float($0) / Float(frameLength - 1)), 0.85)
    }

    /// `bins` rows of `fftSize / 2 + 1` weights (the Nyquist column is always zero, as in Kaldi).
    private static let melBanks: [Float] = {
        func mel(_ f: Double) -> Double { 1127 * log(1 + f / 700) }
        let cols = fftSize / 2 + 1
        let lo = mel(20), hi = mel(8000), delta = (hi - lo) / Double(bins + 1)
        var w = [Float](repeating: 0, count: bins * cols)
        for m in 0..<bins {
            let left = lo + Double(m) * delta, center = left + delta, right = center + delta
            for k in 0..<(fftSize / 2) {
                let x = mel(Double(k) * 16000 / Double(fftSize))
                let v = min((x - left) / (center - left), (right - x) / (right - center))
                if v > 0 { w[m * cols + k] = Float(v) }
            }
        }
        return w
    }()

    private static let setup = vDSP_create_fftsetup(9, FFTRadix(kFFTRadix2))!

    /// Frames × `bins`, row-major. `samples` are 16 kHz mono in -1...1.
    static func compute(_ samples: [Float]) -> [Float] {
        guard samples.count >= frameLength else { return [] }
        let frames = 1 + (samples.count - frameLength) / shift
        let cols = fftSize / 2 + 1
        var out = [Float](repeating: 0, count: frames * bins)
        var frame = [Float](repeating: 0, count: fftSize)
        var re = [Float](repeating: 0, count: fftSize / 2), im = re
        var power = [Float](repeating: 0, count: cols)
        var mels = [Float](repeating: 0, count: bins)
        for f in 0..<frames {
            let start = f * shift
            for i in 0..<frameLength { frame[i] = samples[start + i] * 32768 }
            for i in frameLength..<fftSize { frame[i] = 0 }
            var mean: Float = 0
            vDSP_meanv(frame, 1, &mean, vDSP_Length(frameLength))
            for i in 0..<frameLength { frame[i] -= mean }
            for i in stride(from: frameLength - 1, to: 0, by: -1) { frame[i] -= 0.97 * frame[i - 1] }
            frame[0] -= 0.97 * frame[0]
            vDSP_vmul(frame, 1, window, 1, &frame, 1, vDSP_Length(frameLength))
            re.withUnsafeMutableBufferPointer { rp in
                im.withUnsafeMutableBufferPointer { ip in
                    var split = DSPSplitComplex(realp: rp.baseAddress!, imagp: ip.baseAddress!)
                    frame.withUnsafeBytes { raw in
                        vDSP_ctoz(raw.bindMemory(to: DSPComplex.self).baseAddress!, 2, &split, 1, vDSP_Length(fftSize / 2))
                    }
                    vDSP_fft_zrip(setup, &split, 1, 9, FFTDirection(FFT_FORWARD))
                }
            }
            // vDSP's real FFT returns twice the true values, Nyquist packed into im[0].
            power[0] = re[0] * re[0] / 4
            power[cols - 1] = im[0] * im[0] / 4
            for k in 1..<(fftSize / 2) { power[k] = (re[k] * re[k] + im[k] * im[k]) / 4 }
            vDSP_mmul(melBanks, 1, power, 1, &mels, 1, vDSP_Length(bins), 1, vDSP_Length(cols))
            for b in 0..<bins { out[f * bins + b] = log(max(mels[b], .ulpOfOne)) }
        }
        return out
    }
}

/// The bundled voiceprint model. Command Line Tools can't compile Core ML models, so the
/// `.mlpackage` is compiled on first use and the result kept in Application Support.
final class Voiceprints: @unchecked Sendable {
    static let minSeconds = 0.5
    /// Longer lines are cut: 20 s is plenty to know a voice and bounds the work.
    static let maxSeconds = 20.0

    private let model: MLModel

    private init(model: MLModel) { self.model = model }

    /// `Contents/Resources/models/SpeakerEmbedding.mlpackage` (scripts/build-app.sh).
    static var packageURL: URL? {
        Bundle.main.url(forResource: "SpeakerEmbedding", withExtension: "mlpackage", subdirectory: "models")
            ?? Bundle.main.url(forResource: "SpeakerEmbedding", withExtension: "mlpackage")
    }

    /// Whether this build carries the model (a `swift run` build does not).
    static var isBundled: Bool { packageURL != nil }

    static func load() throws -> Voiceprints {
        guard let pkg = packageURL else { throw CocoaError(.fileNoSuchFile) }
        let weights = pkg.appendingPathComponent("Data/com.apple.CoreML/weights/weight.bin").path
        let size = (try? FileManager.default.attributesOfItem(atPath: weights)[.size] as? Int) ?? 0
        let folder = AppPaths.root.appendingPathComponent("VoiceModel", isDirectory: true)
        let cache = folder.appendingPathComponent("SpeakerEmbedding-\(size).mlmodelc", isDirectory: true)
        if !FileManager.default.fileExists(atPath: cache.path) {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let compiled = try MLModel.compileModel(at: pkg)
            try? FileManager.default.removeItem(at: cache)
            try FileManager.default.moveItem(at: compiled, to: cache)
        }
        let config = MLModelConfiguration()
        // Variable-length input doesn't compile for the Neural Engine; the GPU path is quick enough.
        config.computeUnits = .cpuAndGPU
        return Voiceprints(model: try MLModel(contentsOf: cache, configuration: config))
    }

    /// A unit-length 256-d voiceprint for 16 kHz mono samples, or nil when too short.
    func embed(_ samples: [Float]) -> [Float]? {
        guard Double(samples.count) >= Self.minSeconds * 16000 else { return nil }
        let clip = samples.count > Int(Self.maxSeconds * 16000) ? Array(samples.suffix(Int(Self.maxSeconds * 16000))) : samples
        var feats = Fbank.compute(clip)
        let frames = feats.count / Fbank.bins
        guard frames >= 50 else { return nil }
        // Mean normalisation per bin over the clip, as in training.
        for b in 0..<Fbank.bins {
            var sum: Float = 0
            for f in 0..<frames { sum += feats[f * Fbank.bins + b] }
            let mean = sum / Float(frames)
            for f in 0..<frames { feats[f * Fbank.bins + b] -= mean }
        }
        guard let input = try? MLMultiArray(shape: [1, NSNumber(value: frames), NSNumber(value: Fbank.bins)], dataType: .float32) else { return nil }
        feats.withUnsafeBufferPointer { src in
            input.withUnsafeMutableBytes { dst, _ in
                dst.copyMemory(from: UnsafeRawBufferPointer(src))
            }
        }
        guard let out = try? model.prediction(from: MLDictionaryFeatureProvider(dictionary: ["feats": input])),
              let embs = out.featureValue(for: "embs")?.multiArrayValue else { return nil }
        var v = (0..<embs.count).map { Float(truncating: embs[$0]) }
        var norm: Float = 0
        vDSP_svesq(v, 1, &norm, vDSP_Length(v.count))
        guard norm > 0 else { return nil }
        vDSP_vsdiv(v, 1, [norm.squareRoot()], &v, 1, vDSP_Length(v.count))
        return v
    }
}

/// Everything about one meeting's voices: the audio windows of the two channels and the grouping
/// of voiceprints. It is dropped when the meeting ends, so voices are never carried to the next one.
/// `clusterer` is only touched on `SpeakerTagger`'s queue.
final class VoiceMeeting: @unchecked Sendable {
    /// The other side's computer audio (online meetings); nil in the room.
    let system: AudioTimeline?
    /// The room microphone (in-person meetings); nil online, where the microphone is "我" and is never told apart.
    let microphone: AudioTimeline?
    var clusterer = VoiceClusterer()
    /// The user's saved voiceprint (我的声音), read when the meeting began; nil when none is saved.
    let profile: VoiceProfile?

    init(inPerson: Bool, profile: VoiceProfile?) {
        system = inPerson ? nil : AudioTimeline()
        microphone = inPerson ? AudioTimeline() : nil
        self.profile = profile
    }
}

/// Runs voiceprints off the main thread and hands back a voice per sentence, in arrival order.
final class SpeakerTagger: @unchecked Sendable {
    private let queue = DispatchQueue(label: "app.interviewhacker.voiceprint", qos: .utility)
    private var model: Voiceprints?
    private var loadFailure: String?

    /// Cuts the sentence out of `channel`, computes its voiceprint and groups it. `done` runs on a
    /// background queue with the voice (nil when unsure: too short, too little audio, no model) and
    /// whether that voice is the user according to the saved voiceprint.
    func tag(_ meeting: VoiceMeeting, channel: AudioTimeline, start: Date, end: Date,
             failed: @escaping @Sendable (String) -> Void, done: @escaping @Sendable (Int?, Bool) -> Void) {
        queue.async { [self] in
            guard let clip = channel.clip(from: start, to: end) else { return done(nil, false) }
            let samples = AudioTimeline.trimmingSilence(clip)
            guard !samples.isEmpty else { return done(nil, false) }
            if model == nil, loadFailure == nil {
                do { model = try Voiceprints.load() } catch {
                    loadFailure = error.localizedDescription
                    Diagnostics.record("voiceprint.load_failed")
                }
            }
            if let loadFailure { failed(loadFailure); return done(nil, false) }
            let seconds = Double(samples.count) / Double(AudioTimeline.sampleRate)
            let voice = model.flatMap { $0.embed(samples) }.flatMap { meeting.clusterer.label($0, seconds: seconds) }
            // The whole voice is compared with the saved voiceprint, not just this sentence: its sum has
            // every sentence that joined it, so one noisy sentence cannot flip the answer.
            let mine = voice.flatMap { v in
                meeting.clusterer.sums.indices.contains(v) ? meeting.profile?.recognises(clusterSum: meeting.clusterer.sums[v]) : nil
            } ?? false
            done(voice, mine)
        }
    }

    /// The user said two voices are one: later sentences like either go to `into`.
    func merge(_ meeting: VoiceMeeting, _ from: Int, into: Int) {
        queue.async { meeting.clusterer.merge(from, into: into) }
    }

    /// Lets go of the loaded model (the setting was turned off).
    func unloadModel() {
        queue.async { [self] in model = nil; loadFailure = nil }
    }
}

/// The switch, the running meeting's voices and the tagger. Held by AppState as `speakers`.
@MainActor
@Observable
final class SpeakerVoices {
    static let defaultsKey = "speakerVoicesEnabled"

    /// 区分对方的每个人 (experimental, off by default). Takes effect from the next meeting.
    /// Change it with `setEnabled` (it is saved there).
    private(set) var enabled = UserDefaults.standard.bool(forKey: SpeakerVoices.defaultsKey)
    /// Names the user gave to the voices of the meeting in progress, by voice id.
    var names: [String: String] = [:]
    /// Voices of the meeting in progress that are the user: recognised from 我的声音, or marked by hand.
    var meVoices: Set<Int> = []
    /// Voices the user said are not them; the saved voiceprint no longer marks these.
    @ObservationIgnored var notMe: Set<Int> = []
    /// Why the model could not be used, for the settings page.
    var lastFailure: String?

    @ObservationIgnored private(set) var meeting: VoiceMeeting?
    @ObservationIgnored private let tagger = SpeakerTagger()

    func setEnabled(_ on: Bool) {
        enabled = on
        UserDefaults.standard.set(on, forKey: Self.defaultsKey)
        if !on { tagger.unloadModel(); lastFailure = nil }
    }

    /// This build includes the voiceprint model.
    var modelAvailable: Bool { Voiceprints.isBundled }

    /// Voices are being told apart in the meeting in progress.
    var isActive: Bool { meeting != nil }

    /// A new meeting: forget the voices of the last one.
    func beginMeeting(inPerson: Bool) {
        names = [:]
        meVoices = []
        notMe = []
        lastFailure = nil
        let profile = MyVoice.shared.usableProfile
        meeting = enabled && modelAvailable ? VoiceMeeting(inPerson: inPerson, profile: profile) : nil
    }

    /// The meeting ended: drop its voices (sentences still being worked on keep their own reference
    /// until done, then everything is released).
    func endMeeting() {
        meeting = nil
    }

    /// Computes the voice of a finished sentence; `apply` runs on the main actor with the voice.
    func identify(_ entry: TranscriptEntry, apply: @escaping @MainActor (_ voice: Int, _ isMe: Bool) -> Void) {
        guard let meeting, let start = entry.startedAt, let end = entry.endedAt else { return }
        let channel: AudioTimeline?
        if entry.source == CaptureSource.system.rawValue { channel = meeting.system }
        else if entry.onSite == true { channel = meeting.microphone }
        else { channel = nil }
        guard let channel else { return }
        tagger.tag(meeting, channel: channel, start: start, end: end, failed: { [weak self] message in
            DispatchQueue.main.async { MainActor.assumeIsolated { self?.lastFailure = message } }
        }, done: { voice, mine in
            guard let voice else { return }
            DispatchQueue.main.async { MainActor.assumeIsolated { apply(voice, mine) } }
        })
    }

    func mergeInGrouping(_ from: Int, into: Int) {
        guard let meeting else { return }
        tagger.merge(meeting, from, into: into)
    }
}

// MARK: - Wiring into the meeting

@MainActor
extension AppState {
    /// Called when a final sentence arrives (not echo): tag it with a voice in the background.
    func identifySpeaker(of entry: TranscriptEntry) {
        guard speakers.isActive else { return }
        let sessionID = activeArchiveID
        speakers.identify(entry) { [weak self] voice, mine in
            guard let self else { return }
            if let index = self.entries.firstIndex(where: { $0.id == entry.id }) { self.entries[index].voice = voice }
            if mine, !self.speakers.meVoices.contains(voice), !self.speakers.notMe.contains(voice) {
                self.markVoice(voice, isMe: true, in: nil)
            }
            // The transcript line was appended before its voice was known; the voice follows
            // as its own record (voices.jsonl), merged into the line when the meeting is read back.
            if let sessionID { self.archive.appendVoice(sessionID: sessionID, lineID: entry.id, voice: voice) }
        }
    }

    /// Names of the voices on show: the meeting in progress, or the saved meeting's.
    func voiceNames(for sessionID: UUID?) -> [String: String]? {
        if let sessionID, sessionID != activeArchiveID { return archive.sessions.first { $0.id == sessionID }?.voiceNames }
        return speakers.names.isEmpty ? nil : speakers.names
    }

    /// Voices that are the user, for the meeting in progress or a saved one.
    func meVoices(for sessionID: UUID?) -> Set<Int> {
        if let sessionID, sessionID != activeArchiveID { return Set(archive.sessions.first { $0.id == sessionID }?.meVoices ?? []) }
        return speakers.meVoices
    }

    /// Says a voice is (or is not) the user. `sessionID` nil means the meeting in progress.
    func markVoice(_ voice: Int, isMe: Bool, in sessionID: UUID?) {
        let id = sessionID ?? activeArchiveID
        if sessionID == nil || sessionID == activeArchiveID {
            if isMe { speakers.meVoices.insert(voice); speakers.notMe.remove(voice) }
            else { speakers.meVoices.remove(voice); speakers.notMe.insert(voice) }
        }
        if let id { archive.setMeVoice(id, voice: voice, isMe: isMe) }
    }

    /// Gives a voice a name (nil or empty: back to "说话人 N"). `sessionID` nil means the meeting in progress.
    func renameVoice(_ voice: Int, to name: String?, in sessionID: UUID?) {
        let id = sessionID ?? activeArchiveID
        if sessionID == nil || sessionID == activeArchiveID {
            if let clean = name.flatMap(SpeakerLabels.cleanName) { speakers.names[String(voice)] = clean }
            else { speakers.names[String(voice)] = nil }
        }
        if let id { archive.setVoiceName(id, voice: voice, name: name) }
    }

    /// The user says voice `from` is voice `into`.
    func mergeVoices(_ from: Int, into: Int, in sessionID: UUID?) {
        let live = sessionID == nil || sessionID == activeArchiveID
        let id = sessionID ?? activeArchiveID
        if live {
            for index in entries.indices where entries[index].voice == from { entries[index].voice = into }
            speakers.mergeInGrouping(from, into: into)
            speakers.names = SpeakerLabels.merged(speakers.names, from: from, into: into) ?? [:]
            speakers.meVoices = Set(SpeakerLabels.mergedMe(Array(speakers.meVoices), from: from, into: into) ?? [])
        }
        if let id { Task { await archive.mergeVoice(id, from: from, into: into) } }
    }

    /// The lines of the meeting in progress as archive lines, for numbering and labels.
    var liveSpeakerLines: [SessionTranscriptLine] {
        entries.filter { $0.suspectedEcho != true }.map {
            SessionTranscriptLine(id: $0.id, date: $0.date, speaker: $0.speaker, text: $0.text, voice: $0.voice)
        }
    }
}

@MainActor
extension SessionArchive {
    /// Records which voice said a line that was archived before its voice was known.
    func appendVoice(sessionID: UUID, lineID: UUID, voice: Int) {
        guard sessionID == activeID || sessions.contains(where: { $0.id == sessionID }) else { return }
        appendLine(SessionVoiceTag(id: lineID, voice: voice), file: "voices.jsonl", session: sessionID)
    }

    /// The name of a voice, kept in the meeting's `meta.json` (nil or blank removes it).
    func setVoiceName(_ id: UUID, voice: Int, name: String?) {
        guard var meta = sessions.first(where: { $0.id == id }) else { return }
        var names = meta.voiceNames ?? [:]
        names[String(voice)] = name.flatMap(SpeakerLabels.cleanName)
        meta.voiceNames = names.isEmpty ? nil : names
        update(meta)
        notesRevision &+= 1
    }

    /// Marks a voice of the meeting as the user, or takes the mark off.
    func setMeVoice(_ id: UUID, voice: Int, isMe: Bool) {
        guard var meta = sessions.first(where: { $0.id == id }) else { return }
        var voices = Set(meta.meVoices ?? [])
        if isMe { voices.insert(voice) } else { voices.remove(voice) }
        meta.meVoices = voices.isEmpty ? nil : voices.sorted()
        update(meta)
        notesRevision &+= 1
    }

    /// Moves every line of voice `from` to `into` (new tags win over the old), and the name with it.
    func mergeVoice(_ id: UUID, from: Int, into: Int) async {
        await flush()
        guard let detail = await detail(id) else { return }
        for line in detail.lines where line.voice == from {
            appendLine(SessionVoiceTag(id: line.id, voice: into), file: "voices.jsonl", session: id)
        }
        if var meta = sessions.first(where: { $0.id == id }) {
            meta.voiceNames = SpeakerLabels.merged(meta.voiceNames, from: from, into: into)
            meta.meVoices = SpeakerLabels.mergedMe(meta.meVoices, from: from, into: into)
            update(meta)
        }
        notesRevision &+= 1
    }
}
