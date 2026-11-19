namespace NextThought.Core;

public sealed record QuestionEvaluationReport(
    int Questions, int Detected, int Statements, int FalseTriggers, double SimulatedHours,
    IReadOnlyList<string> Missed, IReadOnlyList<string> FalsePositives)
{
    public double Recall => Questions == 0 ? 1 : (double)Detected / Questions;
    public double FalseTriggersPerHour => SimulatedHours == 0 ? 0 : FalseTriggers / SimulatedHours;
}

/// <summary>
/// Offline evaluation of the first-layer rules over the labeled corpus (port of
/// QuestionDetectionEvaluator): each sample is one final utterance, spaced seconds apart;
/// uncertain decisions count as not triggered.
/// </summary>
public static class QuestionDetectionEvaluator
{
    public static QuestionEvaluationReport Evaluate(
        IReadOnlyList<QuestionDetectionSample> samples, MeetingMode mode = MeetingMode.Meeting, double spacing = 6)
    {
        var tracker = new QuestionCandidateTracker { Mode = mode, HasPriorAnswer = true };
        var now = DateTimeOffset.UnixEpoch;
        var questions = 0;
        var detected = 0;
        var statements = 0;
        var falseTriggers = 0;
        var missed = new List<string>();
        var falsePositives = new List<string>();
        foreach (var sample in samples)
        {
            now = now.AddSeconds(spacing);
            var decision = tracker.Ingest(sample.Text, true, Guid.NewGuid(), now);
            var triggered = decision.Kind == CandidateKind.Trigger;
            if (sample.IsQuestion)
            {
                questions++;
                if (triggered) detected++;
                else missed.Add(sample.Text);
            }
            else
            {
                statements++;
                if (triggered) { falseTriggers++; falsePositives.Add(sample.Text); }
            }
        }
        return new QuestionEvaluationReport(questions, detected, statements, falseTriggers,
            samples.Count * spacing / 3600, missed, falsePositives);
    }
}
