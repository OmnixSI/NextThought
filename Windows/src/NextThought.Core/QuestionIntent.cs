using System.Text.RegularExpressions;
using V = NextThought.Core.QuestionVocabulary;

namespace NextThought.Core;

public enum UtteranceIntent { Statement, Incomplete, Question, ExplanationRequest, FollowUp }
public sealed record IntentAssessment(UtteranceIntent Intent, double Score, IReadOnlyList<string> Reasons)
{
    public bool IsActionable => Intent is UtteranceIntent.Question or UtteranceIntent.ExplanationRequest or UtteranceIntent.FollowUp;
}
public static class QuestionIntentClassifier
{
    public static (double Actionable, double Uncertain) Thresholds(MeetingMode mode) => mode switch
    { MeetingMode.Lesson => (.6, .35), MeetingMode.Interview => (.45, .25), MeetingMode.LabMeeting => (.55, .3), _ => (.5, .3) };
    public static int CjkCount(string text) => text.Count(c => c >= 0x3400 && c <= 0x9fff);
    private static bool Separator(char c) => char.IsWhiteSpace(c) || char.IsPunctuation(c);
    private static string Strip(string text) => text.Trim().Trim('"', '\'', '?', '？', '.', '。', ',', '，', '!', '！', '：', ':', ';', '；', '…');
    private static string Compact(string text) => string.Concat(text.Where(c => !Separator(c)));
    private static string[] Words(string text) => Regex.Split(text, @"[^\p{L}\p{N}']+").Where(x => x.Length > 0).ToArray();
    private static bool Has(IEnumerable<string> values, string text) => values.Any(text.Contains);
    private static bool Starts(IEnumerable<string> values, string text) => values.Any(text.StartsWith);
    public static string StrippingFillers(string input)
    {
        var text = string.Concat(input.SkipWhile(Separator));
        bool changed;
        do
        {
            changed = false;
            foreach (var filler in V.ChineseFillers)
            {
                if (!text.StartsWith(filler, StringComparison.Ordinal) || text.Length == filler.Length) continue;
                var next = text[filler.Length];
                if (!V.ChineseInterjections.Contains(filler) && !Separator(next)) continue;
                if ("不吗嘛吧".Contains(next)) continue;
                text = string.Concat(text[filler.Length..].SkipWhile(Separator)); changed = true; break;
            }
            var pieces = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 2 && V.EnglishFillers.Contains(Strip(pieces[0])))
            { text = string.Concat(pieces[1].SkipWhile(Separator)); changed = true; }
        } while (changed && text.Length > 0);
        return text;
    }
    public static IntentAssessment Assess(string raw, MeetingMode mode = MeetingMode.Meeting, bool hasPriorAnswer = false)
    {
        var text = raw.Trim();
        if (text.Length == 0) return new(UtteranceIntent.Statement, 0, ["empty"]);
        var original = text.ToLowerInvariant(); var originalStripped = Strip(original); var originalCompact = Compact(originalStripped);
        var lower = StrippingFillers(original); var stripped = Strip(lower); var compact = Compact(stripped);
        var cjk = CjkCount(stripped); var words = Words(lower); var reasons = new List<string>();
        if (V.ShortFollowUps.Contains(stripped) || V.ShortFollowUps.Contains(compact) || V.ShortFollowUps.Contains(originalCompact)
            || PatternFollowUp(stripped, cjk, words) || PatternFollowUp(originalStripped, cjk, words))
            return new(UtteranceIntent.FollowUp, hasPriorAnswer ? .75 : .55, ["follow-up form"]);
        double score = 0; var request = false;
        var mark = text.LastIndexOfAny(['?', '？']); var endsMark = text.EndsWith('?') || text.EndsWith('？');
        if (endsMark) { score += .6; reasons.Add("ends with ?"); }
        else if (mark >= 0)
        {
            var tail = text[(mark+1)..].Trim().ToLowerInvariant();
            if (Starts(V.RhetoricalAnswerStarts, tail) && tail.Length >= 4) { score -= .35; reasons.Add("question answered by speaker"); }
            else { score += .4; reasons.Add("contains ?"); }
        }
        var addressed = Has(["你", "您", "大家", "各位"], compact) || words.Any(w => new[]{"you","your","you're","you've","you'd","anyone"}.Contains(w));
        if (words.Length > 0)
        {
            var first = words[0];
            string[] subjects = ["you","we","they","it","i","there","this","that","he","she","your","the","a","an","any","anyone","someone","everyone","anybody"];
            if (V.EnglishWh.Contains(first))
            { var cleft = words.Length >= 3 && new[]{"i","we","they","he","she"}.Contains(words[1]); score += cleft ? .2 : .55; reasons.Add(cleft ? "wh cleft" : "wh-start"); }
            else if (V.EnglishAux.Contains(first) && words.Length >= 3)
            { var strong = subjects.Contains(words[1]) && !(first == "do" && words[1] == "it"); score += strong ? .55 : .35; reasons.Add(strong ? "aux inversion" : "aux inversion weak"); }
            else if (words.Length >= 4 && new[]{"so","and","but","okay","ok","now","then","also"}.Contains(first)
                && (V.EnglishWh.Contains(words[1]) || (V.EnglishAux.Contains(words[1]) && new[]{"you","we","they","it","i","there","this","that"}.Contains(words[2]))))
            { score += .55; reasons.Add("wh after discourse marker"); }
        }
        if (cjk == 0 && words.Length >= 4 && addressed && (new[]{"right","correct","yeah","no"}.Contains(words[^1])
            || new[]{"isn't it","aren't you","don't you","didn't you","haven't you"}.Any(lower.EndsWith)))
        { score += .45; reasons.Add("tag question"); }
        var englishRequest = V.EnglishRequests.Any(x => lower.StartsWith(x) || lower.Contains(" " + x));
        if (englishRequest) { score += .5; request = true; reasons.Add("english request"); }
        if (!englishRequest && Starts(V.EnglishSelfNarration, lower)) { score -= .55; reasons.Add("speaker narration"); }
        if (Starts(["i wonder","i don't know why","i'm not sure why"], lower)) score -= .2;
        if (cjk == 0 && addressed) score += .05;
        if (cjk > 0)
        {
            var falsePositive = Has(V.ChineseFalseInterrogatives, compact);
            if (!falsePositive && Has(V.ChineseInterrogatives, compact)) { score += .55; reasons.Add("chinese interrogative"); }
            else if (falsePositive) reasons.Add("interrogative filler");
            var alt = compact.IndexOf("还是", StringComparison.Ordinal);
            if (addressed && alt >= 0 && compact[..alt].Contains('是') && compact[(alt+2)..].Length >= 2) { score += .5; reasons.Add("alternative interrogative"); }
            if (compact.Length > 0)
            {
                switch (compact[^1])
                {
                    case '吗': case '么': score += .5; reasons.Add("particle 吗"); break;
                    case '嘛': score += addressed ? .45 : .25; reasons.Add("particle 嘛"); break;
                    case '呢': score += compact.StartsWith('那') || addressed ? .5 : .3; reasons.Add("particle 呢"); break;
                    case '吧':
                        if (compact.EndsWith("对吧") || compact.EndsWith("是吧")) { score += .35; reasons.Add("particle 吧"); }
                        else if (addressed && compact.Length >= 6 && !new[]{"好吧","行吧","算了吧"}.Any(compact.EndsWith)) { score += .45; reasons.Add("particle 吧"); }
                        break;
                }
            }
            if (Has(V.ChineseRequests, compact)) { score += .5; request = true; reasons.Add("chinese request"); }
            else if (addressed && Has(V.ChineseAddressedRequests, compact)) { score += .5; request = true; reasons.Add("addressed request"); }
            if (V.ChineseSelfNarration.Any(x => compact.StartsWith(x) || compact.Contains("，"+x))) { score -= .55; reasons.Add("speaker narration"); }
            if (addressed) score += .05;
        }
        if (V.Logistics.Any(x => lower.Contains(x) || compact.Contains(x))) { score -= .6; reasons.Add("audio / screen check"); }
        if (compact.Length < 2) score = Math.Min(score, .2);
        score = Math.Clamp(score, 0, 1);
        if (Incomplete(lower, compact, words, cjk, endsMark)) return new(UtteranceIntent.Incomplete, score, reasons.Append("incomplete").ToArray());
        if (score >= Thresholds(mode).Uncertain)
        {
            var shape = reasons.Any(x => new[]{"?","wh","interrogative","particle","inversion","tag"}.Any(x.Contains));
            return new(request && !shape ? UtteranceIntent.ExplanationRequest : UtteranceIntent.Question, score, reasons);
        }
        return new(UtteranceIntent.Statement, score, reasons);
    }
    private static bool PatternFollowUp(string text, int cjk, string[] words)
    {
        var compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        return (cjk > 0 && compact.StartsWith('那') && !compact.StartsWith("那么") && !compact.StartsWith("那个") && compact.EndsWith('呢') && compact.Length <= 40)
            || ((text.StartsWith("what about ") || text.StartsWith("how about ")) && words.Length <= 6);
    }
    private static bool Incomplete(string lower, string compact, string[] words, int cjk, bool endsMark)
    {
        if (endsMark) return false;
        if (new[]{",","，","、","...","…"}.Any(lower.TrimEnd().EndsWith)) return true;
        if (cjk == 0)
        {
            if (words.Length == 0) return false;
            var last = words[^1]; string[] always = ["the","a","an","and","or","but","because","if","than","your","my"];
            if (words.Length >= 2 && V.DanglingEnglish.Contains(last) && (words.Length < 6 || always.Contains(last))
                && !(new[]{"you","this","that","these","those","is","are","so"}.Contains(last) && words.Length >= 5)) return true;
            if (words.Length >= 2 && new[]{"can","could","would","will"}.Contains(words[0]) && words[1] == "you") return words.Length <= 3;
            if (V.EnglishWh.Contains(words[0])) return words.Length <= 2;
            return Starts(["please explain","explain","tell me"], lower) && words.Length <= 2;
        }
        if (V.DanglingChinese.Any(compact.EndsWith) && compact.Length >= 3 && !compact.EndsWith("的吗") && !compact.EndsWith("是的")
            && !(Has(V.ChineseInterrogatives, compact) && compact.EndsWith('的'))) return true;
        if (compact.StartsWith("请解释") || compact.StartsWith("解释一下") || new[]{"介绍一下","讲一下","请问","我想问","我想问一下"}.Contains(compact)) return compact.Length <= 4;
        return (compact.StartsWith("请问") || compact.StartsWith("我想问")) && compact.Length <= 5;
    }
}
