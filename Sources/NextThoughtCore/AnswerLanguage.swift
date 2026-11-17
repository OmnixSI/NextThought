import Foundation

/// The language an answer is written in when the user left it to follow the question.
///
/// Left to the model, an English question asked of a Chinese prompt came back in Chinese
/// now and then (a doubt put as a statement did, in a defense held in English). What was
/// asked decides, and it is counted here: recognised English with a Chinese term or two
/// in it is still English, a Chinese question that names "XL-MIMO" is still Chinese.
public enum AnswerLanguage {
    /// "English", "简体中文", or nil when the words do not say (too short, or neither).
    public static func spoken(in question: String) -> String? {
        var han = 0, words = 0, inWord = false
        for scalar in question.unicodeScalars {
            switch scalar.value {
            case 0x3400...0x4DBF, 0x4E00...0x9FFF, 0xF900...0xFAFF, 0x20000...0x2FA1F:
                han += 1; inWord = false
            case 0x41...0x5A, 0x61...0x7A, 0xC0...0x24F, 0x27, 0x2019:
                if !inWord { words += 1; inWord = true }
            default:
                inWord = false
            }
        }
        // A Chinese sentence has several characters for each foreign term it names.
        if words >= 3, words > han { return "English" }
        if han >= 2, han >= words { return "简体中文" }
        return nil
    }

    /// For the prompt: the language to answer in, and the language of the meaning lines
    /// under an answer that is not in Chinese.
    /// - Parameters:
    ///   - choice: "auto", "en" or "zh".
    ///   - gloss: the user wants the meaning of a foreign answer in Chinese.
    public static func fields(choice: String, gloss: Bool, question: String) -> (answer: String?, gloss: String?) {
        let answer: String? = choice == "en" ? "English" : choice == "zh" ? "简体中文" : spoken(in: question)
        return (answer, gloss && answer != "简体中文" && choice != "zh" ? "简体中文" : nil)
    }
}
