import Foundation
import Testing
@testable import NextThoughtCore

@Suite struct ScenePresetTests {
    @Test func everySceneOfferedHasDefaultsAndAProfile() {
        #expect(SessionMode.builtIn.contains(.interview))
        #expect(SessionMode.builtIn.contains(.groupProject))
        #expect(!SessionMode.builtIn.contains(.custom))
        #expect(Set(SessionMode.builtIn).count == SessionMode.builtIn.count)
        for mode in SessionMode.builtIn {
            #expect(!ModeProfile.for(mode).templatePreferences.isEmpty)
            #expect(ScenePreset.for(mode).mode == mode)
            let template = MeetingTypeDefaults.for(mode).template
            #expect(!NotesTemplate.builtIn(template).sections.isEmpty)
            #expect(!NotesTemplate.builtIn(template, english: true).sections.isEmpty)
        }
    }

    @Test func scenesWhereTheUserIsQuestionedPromptBesideTheCamera() {
        #expect(ScenePreset.for(.interview).eyeLine)
        #expect(ScenePreset.for(.client).eyeLine)
        #expect(!ScenePreset.for(.lesson).eyeLine)
        #expect(ScenePreset.for(.lesson).coaching == .listening)
        #expect(MeetingTypeDefaults.for(.interview).liveMode == .prompting)
        #expect(MeetingTypeDefaults.for(.groupProject).template == .groupProject)
    }

    @Test func titlesPickTheScene() {
        #expect(MeetingTypeGuesser.guess("字节跳动 · 后端二面") == .interview)
        #expect(MeetingTypeGuesser.guess("数据库大作业 第二次碰头") == .groupProject)
        #expect(MeetingTypeGuesser.guess("COMP5318 group project sync") == .groupProject)
        #expect(MeetingTypeGuesser.guess("周三组会") == .labMeeting)
        #expect(MeetingTypeGuesser.guess("华东区客户沟通") == .client)
    }

    @Test func groupProjectUsesLabMeetingQuestionThresholds() {
        #expect(QuestionIntentClassifier.Thresholds.forMode(.groupProject) == .forMode(.labMeeting))
    }
}

@Suite struct CueScriptTests {
    static let answer = """
    我们采用**先更新数据库、再删除缓存**，并用 binlog 订阅兜底。
    - **Cache Aside**：写库成功后删除缓存，读请求未命中时回源并回填。
    - 删除失败兜底：失败的 key 进入重试队列。
    - 没有小标题的一条。
    ```
    DEL order:42
    ```
    2. **取舍：**这是最终一致方案。
    """

    @Test func keyPointsKeepTheLeadAndTheHeadOfEachPoint() {
        let lines = CueScript.lines(from: Self.answer, style: .keyPoints)
        #expect(lines.map(\.kind) == [.lead, .point, .point, .point, .point])
        #expect(lines[0].text == "我们采用先更新数据库、再删除缓存，并用 binlog 订阅兜底。")
        #expect(lines[1].head == "Cache Aside")
        #expect(lines[1].text == "写库成功后删除缓存，读请求未命中时回源并回填。")
        #expect(lines[2].head == "删除失败兜底")
        #expect(lines[3].head.isEmpty)
        #expect(lines[4].head == "取舍")
        #expect(lines[4].text == "这是最终一致方案。")
        #expect(lines.map(\.id) == Array(0..<5))
        #expect(!lines.contains { $0.text.contains("DEL") })
    }

    @Test func colonsInsideValuesAreNotLeadIns() {
        #expect(CueScript.splitHead("会议 10:30 开始").head.isEmpty)
        #expect(CueScript.splitHead("见 https://example.com").head.isEmpty)
        #expect(CueScript.splitHead("这是一个超过十四个字的很长很长的前半句：后半句").head.isEmpty)
    }

    @Test func scriptStyleIsSentenceBySentence() {
        let lines = CueScript.lines(from: "先说结论：这个方案可行。再说第一个理由，展开一点。最后收尾，把话交回去！", style: .script)
        #expect(lines.count == 3)
        #expect(lines.first?.kind == .lead)
        #expect(lines.last?.text == "最后收尾，把话交回去！")
    }

    @Test func emptyAnswersGiveNoLines() {
        #expect(CueScript.lines(from: "", style: .keyPoints).isEmpty)
        #expect(CueScript.lines(from: "\n\n", style: .script).isEmpty)
    }
}

@Suite struct SpeakingCoachTests {
    @Test func unitsCompareChineseAndEnglish() {
        #expect(SpeechUnits.count("我们先更新数据库") == 8)
        #expect(SpeechUnits.count("hello brave new world") == 6)
        #expect(SpeechUnits.count("用 Redis 做缓存") == 5.5)
        #expect(SpeechUnits.count("，。！") == 0)
    }

    @Test func paceNeedsEnoughSpeechAndFlagsRushing() {
        var coach = SpeakingCoach(profile: CoachingProfile(monologueLimit: 120))
        coach.ingest(SpokenSegment(isMe: true, start: 0, end: 3, text: String(repeating: "字", count: 12)))
        #expect(coach.snapshot(at: 3).pace == nil)
        // 60 characters in 10 seconds = 360 per minute: too fast.
        coach.ingest(SpokenSegment(isMe: true, start: 4, end: 14, text: String(repeating: "字", count: 60)))
        let snapshot = coach.snapshot(at: 14)
        #expect((snapshot.pace ?? 0) > 310)
        #expect(snapshot.nudge == .slowDown)
    }

    @Test func monologueRunsUntilTheOtherSideSpeaksOrAPause() {
        var coach = SpeakingCoach(profile: CoachingProfile(monologueLimit: 60))
        coach.ingest(SpokenSegment(isMe: false, start: 0, end: 5, text: "你介绍一下项目"))
        for index in 0..<8 {
            let start = 6 + Double(index) * 10
            coach.ingest(SpokenSegment(isMe: true, start: start, end: start + 9, text: String(repeating: "字", count: 36)))
        }
        let talking = coach.snapshot(at: 85)
        #expect(talking.monologue == 79)
        #expect(talking.nudge == .wrapUp)
        // Silence ends it.
        #expect(coach.snapshot(at: 95).monologue == 0)
        coach.ingest(SpokenSegment(isMe: false, start: 96, end: 99, text: "好的明白"))
        #expect(coach.snapshot(at: 99).monologue == 0)
    }

    @Test func talkShareNudgesBothWays() {
        var listener = SpeakingCoach(profile: CoachingProfile(monologueLimit: nil, talkShare: 0.25...0.6))
        listener.ingest(SpokenSegment(isMe: true, start: 0, end: 50, text: String(repeating: "字", count: 190)))
        listener.ingest(SpokenSegment(isMe: false, start: 51, end: 61, text: String(repeating: "字", count: 40)))
        let dominating = listener.snapshot(at: 70)
        #expect((dominating.talkShare ?? 0) > 0.8)
        #expect(dominating.nudge == .letThemTalk)

        var quiet = SpeakingCoach(profile: CoachingProfile(monologueLimit: nil, talkShare: 0.25...0.6))
        quiet.ingest(SpokenSegment(isMe: false, start: 0, end: 60, text: String(repeating: "字", count: 200)))
        quiet.ingest(SpokenSegment(isMe: true, start: 61, end: 73, text: String(repeating: "字", count: 44)))
        // Not in the first three minutes.
        #expect(quiet.snapshot(at: 80).talkShare != nil)
        #expect(quiet.snapshot(at: 80).nudge == nil)
        #expect(quiet.snapshot(at: 200).nudge == .speakUp)
    }

    @Test func noShareUntilBothSidesSpokeAndTheUserSaidEnough() {
        let profile = CoachingProfile(monologueLimit: nil, talkShare: 0.25...0.6)
        // An interviewer still on the first question: the user has not spoken at all.
        var onlyThem = SpeakingCoach(profile: profile)
        onlyThem.ingest(SpokenSegment(isMe: false, start: 0, end: 60, text: String(repeating: "字", count: 200)))
        #expect(onlyThem.snapshot(at: 200).talkShare == nil)
        #expect(onlyThem.snapshot(at: 200).nudge == nil)
        // Only the user so far.
        var onlyMe = SpeakingCoach(profile: profile)
        onlyMe.ingest(SpokenSegment(isMe: true, start: 0, end: 60, text: String(repeating: "字", count: 200)))
        #expect(onlyMe.snapshot(at: 200).talkShare == nil)
        // A few seconds from the user is not enough to judge.
        var brief = SpeakingCoach(profile: profile)
        brief.ingest(SpokenSegment(isMe: false, start: 0, end: 60, text: String(repeating: "字", count: 200)))
        brief.ingest(SpokenSegment(isMe: true, start: 61, end: 65, text: String(repeating: "字", count: 14)))
        #expect(brief.snapshot(at: 200).talkShare == nil)
    }

    @Test func aListeningSceneNeverNudges() {
        var coach = SpeakingCoach(profile: .listening)
        coach.ingest(SpokenSegment(isMe: false, start: 0, end: 5, text: "同学来回答一下"))
        coach.ingest(SpokenSegment(isMe: true, start: 6, end: 16, text: String(repeating: "字", count: 80)))
        let snapshot = coach.snapshot(at: 16)
        #expect((snapshot.pace ?? 0) > 310)
        #expect(snapshot.nudge == nil)
        coach.ingest(SpokenSegment(isMe: true, start: 17, end: 400, text: String(repeating: "字", count: 900)))
        #expect(coach.snapshot(at: 400).nudge == nil)
    }
}

@Suite struct GazeEstimatorTests {
    /// An almond-shaped eye 30 wide and 12 tall at `center`, rotated by `roll`.
    static func eye(center: GazePoint, pupil: GazePoint, openness: Double = 0.4, roll: Double = 0) -> EyeGeometry {
        let half = 15.0, height = 30.0 * openness / 2
        let raw: [GazePoint] = [GazePoint(-half, 0), GazePoint(-half / 2, height), GazePoint(0, height * 1.1),
                                GazePoint(half / 2, height), GazePoint(half, 0), GazePoint(half / 2, -height),
                                GazePoint(0, -height * 1.1), GazePoint(-half / 2, -height)]
        func place(_ point: GazePoint) -> GazePoint {
            GazePoint(center.x + point.x * cos(roll) - point.y * sin(roll), center.y + point.x * sin(roll) + point.y * cos(roll))
        }
        return EyeGeometry(contour: raw.map(place), pupil: place(pupil))
    }

    static func frame(pupil: GazePoint = GazePoint(0, 0), yaw: Double = 0, pitch: Double = 0,
                      openness: Double = 0.4, roll: Double = 0, time: TimeInterval) -> GazeFrame {
        GazeFrame(leftEye: eye(center: GazePoint(100, 100), pupil: pupil, openness: openness, roll: roll),
                  rightEye: eye(center: GazePoint(160, 100), pupil: pupil, openness: openness, roll: roll),
                  yaw: yaw, pitch: pitch, time: time)
    }

    @Test func centredPupilsReadAsNeutral() throws {
        let signature = try #require(GazeEstimator.signature(of: Self.frame(time: 0)))
        #expect(abs(signature.x) < 1e-9)
        #expect(abs(signature.y) < 1e-9)
    }

    @Test func aTiltedHeadDoesNotLookLikeAGlance() throws {
        let signature = try #require(GazeEstimator.signature(of: Self.frame(roll: 0.3, time: 0)))
        #expect(abs(signature.x) < 1e-9)
        #expect(abs(signature.y) < 1e-9)
    }

    @Test func closedEyesGiveNoSignature() {
        #expect(GazeEstimator.signature(of: Self.frame(openness: 0.05, time: 0)) == nil)
    }

    @Test func lookingDownAtTheScreenBreaksContactAndComingBackRestoresIt() {
        var estimator = GazeEstimator()
        for index in 0..<10 { estimator.addCalibration(Self.frame(pupil: GazePoint(0.4, -0.3), time: Double(index) * 0.1)) }
        let calibrated = estimator.finishCalibration()
        #expect(calibrated)
        var time = 1.0
        func feed(_ pupil: GazePoint, yaw: Double = 0, frames: Int) -> GazeReading {
            var reading = GazeReading(state: .noFace)
            for _ in 0..<frames {
                time += 0.1
                reading = estimator.ingest(Self.frame(pupil: pupil, yaw: yaw, time: time))
            }
            return reading
        }
        #expect(feed(GazePoint(0.4, -0.3), frames: 40).state == .onCamera)
        // Pupils drop a fifth of the eye width: reading something lower on the screen.
        let reading = feed(GazePoint(0.4, -6), frames: 40)
        #expect(reading.state == .away)
        #expect(reading.offset.y < 0)
        #expect(feed(GazePoint(0.4, -0.3), frames: 40).state == .onCamera)
        // Turning the head to a second display, eyes still centred in the head.
        #expect(feed(GazePoint(0.4, -0.3), yaw: 0.45, frames: 40).state == .away)
        let ratio = estimator.contactRatio ?? 0
        #expect(ratio > 0.35 && ratio < 0.75)
    }

    @Test func aBlinkKeepsTheVerdict() {
        var estimator = GazeEstimator()
        for index in 0..<10 { _ = estimator.ingest(Self.frame(time: Double(index) * 0.1)) }
        let blink = estimator.ingest(Self.frame(openness: 0.04, time: 1.1))
        #expect(blink.state == .onCamera)
        let missing = estimator.ingest(nil)
        #expect(missing.state == .noFace)
    }

    @Test func calibrationNeedsEnoughOpenEyedFrames() {
        var estimator = GazeEstimator()
        for index in 0..<3 { estimator.addCalibration(Self.frame(time: Double(index))) }
        let calibrated = estimator.finishCalibration()
        #expect(!calibrated)
        #expect(!estimator.isCalibrated)
    }
}
