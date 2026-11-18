using System.Text;

namespace NextThought.Core;

/// <summary>
/// The language an answer follows when the user left it to the question
/// (port of AnswerLanguage.swift). An English question with a Chinese term is still
/// English; a Chinese question naming "XL-MIMO" is still Chinese.
/// </summary>
public static class AnswerLanguage
{
    /// <summary>"English", "简体中文", or null when the words do not decide.</summary>
    public static string? Spoken(string question)
    {
        var han = 0;
        var words = 0;
        var inWord = false;
        foreach (var rune in question.EnumerateRunes())
        {
            var value = rune.Value;
            if (value is >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x2FA1F)
            {
                han++;
                inWord = false;
            }
            else if (value is >= 0x41 and <= 0x5A or >= 0x61 and <= 0x7A or >= 0xC0 and <= 0x24F or 0x27 or 0x2019)
            {
                if (!inWord) { words++; inWord = true; }
            }
            else
            {
                inWord = false;
            }
        }
        // A Chinese sentence has several characters for each foreign term it names.
        if (words >= 3 && words > han) return "English";
        if (han >= 2 && han >= words) return "简体中文";
        return null;
    }

    /// <summary>
    /// For the prompt: the answer language, and the language of the meaning lines under an
    /// answer that is not in Chinese. <paramref name="choice"/> is "auto", "en" or "zh".
    /// </summary>
    public static (string? Answer, string? Gloss) Fields(string choice, bool gloss, string question)
    {
        var answer = choice switch
        {
            "en" => "English",
            "zh" => "简体中文",
            _ => Spoken(question)
        };
        var glossLanguage = gloss && answer != "简体中文" && choice != "zh" ? "简体中文" : null;
        return (answer, glossLanguage);
    }
}
