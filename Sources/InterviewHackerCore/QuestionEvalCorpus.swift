import Foundation

/// Labeled recognizer-style utterances (“does the other side expect me to answer now?”),
/// shared by the unit tests and the in-app comparison of rules and the decision model.
public enum QuestionEvalCorpus {
    /// Every sample, tuned and held-out.
    public static var all: [QuestionDetectionEvaluator.Sample] { extended + heldOut }
}
