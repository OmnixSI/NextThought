using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// 识别热词 = 术语表 + 资料里的技术术语（MaterialTerms）。
/// 术语表是人手维护的；资料术语来自已导入的文档，导入后自动参与识别。
/// </summary>
public partial class AppState
{
    private IReadOnlyList<string> materialTerms = [];
    private string materialTermsFingerprint = "";

    /// <summary>Technical terms of the material library, cached by document hashes.</summary>
    public IReadOnlyList<string> MaterialHotwords()
    {
        var documents = Library.DocumentsForRetrieval();
        var fingerprint = string.Join(":", documents.Select(document => document.Sha256 + ":" + document.Blocks.Count));
        if (fingerprint == materialTermsFingerprint) return materialTerms;
        materialTerms = MaterialTerms.Extract(documents.SelectMany(document => document.Blocks).Select(block => block.Text).ToList(), 24);
        materialTermsFingerprint = fingerprint;
        return materialTerms;
    }

    /// <summary>Drops the cache after an import or a removal so the next recognizer sees new terms.</summary>
    public void RefreshMaterialTerms()
    {
        materialTerms = [];
        materialTermsFingerprint = "";
    }

    /// <summary>Hotwords for the recognizer: the glossary first, then material terms, without repeats.</summary>
    public IReadOnlyList<string> RecognitionHotwords()
    {
        var result = new List<string>();
        foreach (var word in GlossaryHotwords())
            if (!result.Contains(word, StringComparer.OrdinalIgnoreCase)) result.Add(word);
        foreach (var word in MaterialHotwords())
            if (!result.Contains(word, StringComparer.OrdinalIgnoreCase)) result.Add(word);
        return result;
    }
}
