import Foundation
import Testing
@testable import NextThoughtCore

/// The subtitle bar: window modes, when the bar is up, which sentence it shows and how
/// the text is cut to fit.
struct SubtitleBarTests {
    private let origin = Date(timeIntervalSinceReferenceDate: 1_000)
    private func at(_ seconds: TimeInterval) -> Date { origin.addingTimeInterval(seconds) }

    // MARK: Window mode

    @Test func modesCycleNotesSubtitlesHiddenAndBack() {
        #expect(LiveWindowMode.notes.next() == .subtitles)
        #expect(LiveWindowMode.subtitles.next() == .hidden)
        #expect(LiveWindowMode.hidden.next() == .notes)
        #expect(LiveWindowMode.allCases == [.notes, .subtitles, .hidden])
        // Three presses are a full circle from any start.
        for mode in LiveWindowMode.allCases { #expect(mode.next().next().next() == mode) }
    }

    @Test func modeValuesAreStable() throws {
        #expect(LiveWindowMode.notes.rawValue == "notes")
        #expect(LiveWindowMode.subtitles.title == "字幕条")
        let data = try JSONEncoder().encode(LiveWindowMode.allCases)
        #expect(try JSONDecoder().decode([LiveWindowMode].self, from: data) == LiveWindowMode.allCases)
    }

    // MARK: Visibility

    @Test func barShowsForFourSecondsThenFadesAndGoes() {
        let timing = SubtitleBarTiming.standard
        #expect(timing.holdSeconds == 4 && timing.fadeSeconds == 0.5)
        #expect(timing.phase(now: at(0), lastActivity: nil) == .hidden)
        #expect(timing.opacity(now: at(0), lastActivity: nil) == 0)

        #expect(timing.phase(now: at(0), lastActivity: at(0)) == .visible)
        #expect(timing.phase(now: at(4), lastActivity: at(0)) == .visible)
        #expect(timing.opacity(now: at(4), lastActivity: at(0)) == 1)

        #expect(timing.phase(now: at(4.25), lastActivity: at(0)) == .fading)
        #expect(abs(timing.opacity(now: at(4.25), lastActivity: at(0)) - 0.5) < 1e-9)

        #expect(timing.phase(now: at(4.5), lastActivity: at(0)) == .hidden)
        #expect(timing.opacity(now: at(4.5), lastActivity: at(0)) == 0)
        #expect(timing.phase(now: at(60), lastActivity: at(0)) == .hidden)
    }

    @Test func newTextBringsTheBarBack() {
        let timing = SubtitleBarTiming.standard
        // Faded out after the first sentence, visible again right after the next one.
        #expect(timing.phase(now: at(10), lastActivity: at(0)) == .hidden)
        #expect(timing.phase(now: at(10), lastActivity: at(10)) == .visible)
    }

    @Test func aClockStepBackwardsDoesNotHideTheBar() {
        let timing = SubtitleBarTiming.standard
        #expect(timing.phase(now: at(0), lastActivity: at(5)) == .visible)
        #expect(timing.opacity(now: at(0), lastActivity: at(5)) == 1)
    }

    @Test func zeroFadeNeverDividesByZero() {
        let timing = SubtitleBarTiming(holdSeconds: 2, fadeSeconds: 0)
        #expect(timing.phase(now: at(2), lastActivity: at(0)) == .visible)
        #expect(timing.phase(now: at(2.1), lastActivity: at(0)) == .hidden)
        #expect(timing.opacity(now: at(2.1), lastActivity: at(0)) == 0)
        #expect(SubtitleBarTiming(holdSeconds: -3, fadeSeconds: -1) == SubtitleBarTiming(holdSeconds: 0, fadeSeconds: 0))
    }

    // MARK: Which sentence

    private func channels(remote: String = "", remoteTranslation: String = "",
                          me: String = "", meTranslation: String = "") -> [SubtitleChannel] {
        [SubtitleChannel(speaker: .remote, partial: remote, partialTranslation: remoteTranslation),
         SubtitleChannel(speaker: .me, partial: me, partialTranslation: meTranslation)]
    }

    @Test func aLineFromBeforeTheBarExistedIsNotNews() {
        var feed = SubtitleFeed()
        feed.update(channels: channels(), lastFinal: SubtitleLine(speaker: .remote, text: "old line"), now: at(100))
        #expect(feed.lastActivity == nil)
        #expect(feed.line?.text == "old line" && feed.line?.isPartial == false)
        #expect(SubtitleBarTiming.standard.phase(now: at(100), lastActivity: feed.lastActivity) == .hidden)
    }

    @Test func aHalfSentenceCountsAndEachRevisionKeepsTheBarUp() {
        var feed = SubtitleFeed()
        let old = SubtitleLine(speaker: .remote, text: "old line")
        feed.update(channels: channels(), lastFinal: old, now: at(100))
        feed.update(channels: channels(remote: "hello"), lastFinal: old, now: at(101))
        #expect(feed.lastActivity == at(101))
        #expect(feed.line == SubtitleLine(speaker: .remote, text: "hello", translation: "", isPartial: true))

        // The same text again is no news.
        feed.update(channels: channels(remote: "hello"), lastFinal: old, now: at(102))
        #expect(feed.lastActivity == at(101))

        // A longer partial with a translation is.
        feed.update(channels: channels(remote: "hello wor", remoteTranslation: "你好"), lastFinal: old, now: at(103))
        #expect(feed.lastActivity == at(103))
        #expect(feed.line == SubtitleLine(speaker: .remote, text: "hello wor", translation: "你好", isPartial: true))
    }

    @Test func whoeverSpokeLastIsShown() {
        var feed = SubtitleFeed()
        let old = SubtitleLine(speaker: .remote, text: "old line")
        feed.update(channels: channels(), lastFinal: old, now: at(100))
        feed.update(channels: channels(remote: "hello wor"), lastFinal: old, now: at(103))
        feed.update(channels: channels(remote: "hello wor", me: "hi"), lastFinal: old, now: at(104))
        #expect(feed.line?.speaker == .me && feed.line?.text == "hi")

        // Nothing changes: the later speaker stays.
        feed.update(channels: channels(remote: "hello wor", me: "hi"), lastFinal: old, now: at(105))
        #expect(feed.line?.speaker == .me && feed.lastActivity == at(104))

        // The other side goes on talking and takes the bar back.
        feed.update(channels: channels(remote: "hello wor!", me: "hi"), lastFinal: old, now: at(106))
        #expect(feed.line?.speaker == .remote && feed.line?.text == "hello wor!")
        #expect(feed.lastActivity == at(106))
    }

    @Test func aFinishedLineStaysUntilTheBarFadesAndItsLateTranslationRenewsIt() {
        var feed = SubtitleFeed()
        let old = SubtitleLine(speaker: .remote, text: "old line")
        feed.update(channels: channels(), lastFinal: old, now: at(100))
        feed.update(channels: channels(remote: "hello world"), lastFinal: old, now: at(106))

        // The recognizer finishes: the partial is gone, the finished line takes over.
        let finished = SubtitleLine(speaker: .remote, text: "hello world")
        feed.update(channels: channels(), lastFinal: finished, now: at(107))
        #expect(feed.line == finished)
        #expect(feed.lastActivity == at(107))

        // Its translation arrives two seconds later.
        let translated = SubtitleLine(speaker: .remote, text: "hello world", translation: "你好世界")
        feed.update(channels: channels(), lastFinal: translated, now: at(109))
        #expect(feed.line == translated)
        #expect(feed.lastActivity == at(109))
    }

    @Test func theFirstFinishedLineAfterAnEmptyStartCounts() {
        var feed = SubtitleFeed()
        feed.update(channels: channels(), lastFinal: nil, now: at(200))
        #expect(feed.line == nil && feed.lastActivity == nil)
        feed.update(channels: channels(), lastFinal: SubtitleLine(speaker: .remote, text: "first"), now: at(201))
        #expect(feed.line?.text == "first" && feed.lastActivity == at(201))
    }

    @Test func aPartialAlreadyUnderwayAtStartCounts() {
        var feed = SubtitleFeed()
        feed.update(channels: channels(remote: "already talking"), lastFinal: nil, now: at(300))
        #expect(feed.line?.text == "already talking" && feed.lastActivity == at(300))
    }

    @Test func spacesAndNewlinesInRecognizedTextAreTidied() {
        var feed = SubtitleFeed()
        feed.update(channels: channels(remote: "  hello \n  world  "), lastFinal: nil, now: at(1))
        #expect(feed.line?.text == "hello world")
    }

    // MARK: Fitting text

    @Test func shortTextIsKeptWholeAndWhitespaceIsCollapsed() {
        #expect(SubtitleText.tail("short", maxUnits: 10) == "short")
        #expect(SubtitleText.tail("hello\n  world", maxUnits: 50) == "hello world")
        #expect(SubtitleText.tail("hello world", maxUnits: 0) == "")
        #expect(SubtitleText.tail("   ", maxUnits: 5) == "")
        #expect(SubtitleText.tail("", maxUnits: 5) == "")
    }

    @Test func chineseAndEmojiAreTwoUnitsWide() {
        #expect(SubtitleText.units(of: "abc") == 3)
        #expect(SubtitleText.units(of: "你好") == 4)
        #expect(SubtitleText.units(of: "你好 ok") == 7)
        #expect(SubtitleText.units(of: "，") == 2)
    }

    @Test func englishKeepsTheNewestWholeWords() {
        // 43 characters into 20 units: the raw cut would start inside "jumps".
        #expect(SubtitleText.tail("the quick brown fox jumps over the lazy dog", maxUnits: 20) == "…over the lazy dog")
        // A contraction is one word.
        #expect(SubtitleText.tail("we really don't know", maxUnits: 10) == "…know")
        // A hyphenated word too.
        #expect(SubtitleText.tail("well-known fact about everything", maxUnits: 14) == "…everything")
    }

    @Test func aWordLongerThanTheWholeBudgetIsCutRatherThanDropped() {
        #expect(SubtitleText.tail("supercalifragilistic", maxUnits: 8) == "…ilistic")
    }

    @Test func chineseIsCutBetweenAnyTwoCharacters() {
        // 20 characters = 40 units; 10 units of text and the ellipsis fit in 11.
        #expect(SubtitleText.tail("今天我们来讨论一下分布式系统的一致性问题", maxUnits: 11) == "…一致性问题")
    }

    @Test func aCutNeverStartsWithAComma() {
        // The raw cut would be "，但是成本太高".
        #expect(SubtitleText.tail("我觉得这个方案不错，但是成本太高", maxUnits: 15) == "…但是成本太高")
    }

    @Test func mixedTextDoesNotBreakALatinWordNextToChinese() {
        // The raw cut would leave "etes 部署服务到生产环境".
        #expect(SubtitleText.tail("我们用 Kubernetes 部署服务到生产环境", maxUnits: 22) == "…部署服务到生产环境")
    }

    @Test func theResultNeverExceedsTheBudget() {
        let samples = [
            "the quick brown fox jumps over the lazy dog",
            "今天我们来讨论一下分布式系统的一致性问题",
            "我们用 Kubernetes 部署服务到生产环境",
            "Hello, world. This is a test of the subtitle bar."
        ]
        for text in samples {
            for budget in 4...60 {
                let cut = SubtitleText.tail(text, maxUnits: budget)
                #expect(SubtitleText.units(of: cut) <= budget, "\(text) @ \(budget) -> \(cut)")
                #expect(!cut.isEmpty)
            }
        }
    }

    // MARK: What is drawn

    @Test func withoutATranslationOnlyTheOriginalShows() {
        let line = SubtitleLine(speaker: .remote, text: "Could you walk me through the design?", isPartial: true)
        let display = SubtitleText.display(for: line, primaryUnits: 80, secondaryUnits: 40)
        #expect(display.primary == "Could you walk me through the design?")
        #expect(display.secondary == "")
        #expect(display.speaker == .remote && display.isPartial)
    }

    @Test func withATranslationTheTranslationLeadsAndTheOriginalFollows() {
        let line = SubtitleLine(speaker: .remote, text: "Could you walk me through the design?",
                                translation: "你能给我讲讲这个设计吗？")
        let display = SubtitleText.display(for: line, primaryUnits: 80, secondaryUnits: 40)
        #expect(display.primary == "你能给我讲讲这个设计吗？")
        #expect(display.secondary == "Could you walk me through the design?")
        #expect(!display.isPartial)
    }

    @Test func eachLineIsFittedToItsOwnBudget() {
        let line = SubtitleLine(speaker: .me, text: "the quick brown fox jumps over the lazy dog",
                                translation: "今天我们来讨论一下分布式系统的一致性问题")
        let display = SubtitleText.display(for: line, primaryUnits: 11, secondaryUnits: 20)
        #expect(display.primary == "…一致性问题")
        #expect(display.secondary == "…over the lazy dog")
        #expect(display.speaker == .me)
    }
}
