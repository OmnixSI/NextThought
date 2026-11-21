namespace NextThought.Core;

/// <summary>
/// E11/Mac TranslationDirection: the destination language is chosen from the recognised utterance,
/// never from which audio device produced it. Script counting keeps the common monolingual case
/// cheap for frequently revised partials.
/// </summary>
public enum LiveTranslationDirection
{
    EnglishToChinese,
    ChineseToEnglish
}

public static class LiveTranslationDirectionDetector
{
    /// <summary>Mac TranslationDirection.detect (en→zh or zh→en), null when undecidable.</summary>
    public static LiveTranslationDirection? Detect(string text)
    {
        var value = text.Trim();
        if (value.Length == 0) return null;
        var han = 0;
        var latin = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var code = rune.Value;
            if (code is >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF
                or >= 0x20000 and <= 0x2FA1F) han++;
            else if (code is (>= 0x0041 and <= 0x005A) or (>= 0x0061 and <= 0x007A)
                     or (>= 0x00C0 and <= 0x024F) or (>= 0x1E00 and <= 0x1EFF)) latin++;
        }
        if (han > 0 && latin == 0) return LiveTranslationDirection.ChineseToEnglish;
        if (latin > 0 && han == 0) return LiveTranslationDirection.EnglishToChinese;
        if (han > 0 && latin > 0)
        {
            // A clearly dominant script decides; the remainder falls back to simple counts
            // (Windows has no NaturalLanguage recogniser available for this cheap path).
            if (han * 5 >= latin * 7) return LiveTranslationDirection.ChineseToEnglish;
            if (latin * 5 >= han * 7) return LiveTranslationDirection.EnglishToChinese;
            return han > latin ? LiveTranslationDirection.ChineseToEnglish : LiveTranslationDirection.EnglishToChinese;
        }
        return null;
    }

    /// <summary>Mac "听错写法的词数" gate for the automatic turn-on: four space-separated words.</summary>
    public static bool HasEnoughEnglishWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 4;
}

/// <summary>
/// E11/Mac turnOnForForeignSpeech: until the user touches the switch (choice is null), hearing the
/// other side speak English turns live interpretation on by itself — only when the UI is Chinese and
/// both language packs are installed, and only once per meeting (checkedForeign).
/// </summary>
public sealed class LiveTranslationAutoOpen
{
    public bool? Choice { get; private set; }
    private bool checkedForeign;

    public LiveTranslationAutoOpen(bool? choice = null) => Choice = choice;

    /// <summary>Mac beginMeeting: the remembered choice holds; a new meeting re-checks foreign speech.</summary>
    public void BeginMeeting(bool? savedChoice)
    {
        Choice = savedChoice;
        checkedForeign = false;
    }

    /// <summary>Mac setEnabled(remember: true): the user's toggle writes the remembered choice.</summary>
    public void Remember(bool enabled) => Choice = enabled;

    public void Forget() => Choice = null;

    /// <summary>
    /// One system-channel utterance arrived. True when the caller should turn interpretation on by
    /// itself (remembering nothing). The English-word gate runs before the language-pack check so a
    /// meeting full of short remarks does not trigger the check at all.
    /// </summary>
    public bool ShouldTurnOn(string text, bool enabled, bool uiIsChinese, bool packsInstalled)
    {
        if (enabled || Choice is not null || checkedForeign || !uiIsChinese) return false;
        if (LiveTranslationDirectionDetector.Detect(text) != LiveTranslationDirection.EnglishToChinese) return false;
        if (!LiveTranslationDirectionDetector.HasEnoughEnglishWords(text)) return false;
        if (!packsInstalled) return false;
        checkedForeign = true;
        return true;
    }

    /// <summary>Delivery channel → the two Mac hosts (system/remote lane and microphone lane).</summary>
    public static string LaneFor(bool microphone) => microphone ? "microphone" : "system";
}
