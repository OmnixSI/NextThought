import Foundation

/// How much was recorded: meetings, the time they ran, and what was asked in them.
///
/// The time is recording time, from the start of a meeting to its end; a meeting that
/// has no end yet (still running, or interrupted) counts as a meeting but adds no time.
public struct MeetingStats: Equatable, Sendable {
    public var meetings = 0
    public var seconds: TimeInterval = 0
    /// Questions recognised in what was said.
    public var questions = 0
    /// Answers written, automatic or asked for.
    public var answers = 0

    public init(meetings: Int = 0, seconds: TimeInterval = 0, questions: Int = 0, answers: Int = 0) {
        self.meetings = meetings; self.seconds = seconds; self.questions = questions; self.answers = answers
    }

    /// The meetings that started inside `interval` (all of them without one), optionally
    /// of one series only.
    public static func of(_ metas: [SessionMeta], in interval: DateInterval? = nil, series: UUID? = nil) -> MeetingStats {
        var stats = MeetingStats()
        for meta in metas {
            if let interval, !interval.contains(meta.startedAt) { continue }
            if let series, meta.workspaceID != series { continue }
            stats.meetings += 1
            stats.seconds += meta.duration
            stats.questions += meta.questionCount
            stats.answers += meta.answerCount
        }
        return stats
    }

    /// The calendar month that holds `date`.
    public static func month(of date: Date, calendar: Calendar = .current) -> DateInterval {
        calendar.dateInterval(of: .month, for: date) ?? DateInterval(start: date, duration: 0)
    }

    /// Whole hours and minutes: (5, 12) for 5 h 12 min 40 s.
    public var hoursAndMinutes: (hours: Int, minutes: Int) {
        let minutes = Int((seconds / 60).rounded(.down))
        return (minutes / 60, minutes % 60)
    }
}
