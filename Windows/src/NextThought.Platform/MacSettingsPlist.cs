using System.Security;
using System.Xml.Linq;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// The small, documented subset of the Mac settings.plist that maps onto AppSettings.
/// macOS UserDefaults stores these keys; unknown keys are ignored on both sides.
/// </summary>
public static class MacSettingsPlist
{
    public static byte[] Write(AppSettings settings)
    {
        var values = new List<(string Key, string Type, string Value)>
        {
            ("apiBaseURL", "string", settings.BaseUrl),
            ("apiModel", "string", settings.Model),
            ("notesModelID", "string", settings.NotesModel),
            ("localStreamingModel", "string", settings.LocalModel),
            ("secondPassEnabled", "bool", settings.SecondPass ? "true" : "false"),
            ("microphoneEnabled", "bool", settings.CaptureMicrophone ? "true" : "false"),
            ("automaticAnswers", "bool", settings.AutomaticAnswers ? "true" : "false"),
            ("autoSummarize", "bool", settings.AutoSummarize ? "true" : "false"),
            ("semanticRetrievalEnabled", "bool", settings.UseSemanticRetrieval ? "true" : "false"),
            ("webSearchMode", "string", settings.WebSearch ? "automatic" : "off"),
            ("codexModelID", "string", settings.CodexModel),
        };
        var xml = new XElement("plist", new XAttribute("version", "1.0"),
            new XElement("dict", values.SelectMany(value => new object[]
            {
                new XElement("key", value.Key),
                value.Type == "bool" ? new XElement(value.Value == "true" ? "true" : "false") : new XElement("string", value.Value)
            })));
        return System.Text.Encoding.UTF8.GetBytes(new XDocument(new XDeclaration("1.0", "UTF-8", null), xml).ToString());
    }

    public static AppSettings Apply(byte[] plist, AppSettings current)
    {
        try
        {
            var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(plist));
            var dictionary = document.Root?.Element("dict");
            if (dictionary is null) return current;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var children = dictionary.Elements().ToList();
            for (var index = 0; index + 1 < children.Count; index += 2)
            {
                if (children[index].Name != "key") continue;
                var key = children[index].Value;
                var value = children[index + 1];
                values[key] = value.Name == "true" ? "true" : value.Name == "false" ? "false" : value.Value;
            }
            bool Bool(string key, bool fallback) => values.TryGetValue(key, out var text) ? text == "true" : fallback;
            string Text(string key, string fallback) => values.TryGetValue(key, out var text) && text.Length > 0 ? text : fallback;
            return current with
            {
                BaseUrl = Text("apiBaseURL", current.BaseUrl),
                Model = Text("apiModel", current.Model),
                NotesModel = Text("notesModelID", current.NotesModel),
                LocalModel = Text("localStreamingModel", current.LocalModel),
                SecondPass = Bool("secondPassEnabled", current.SecondPass),
                CaptureMicrophone = Bool("microphoneEnabled", current.CaptureMicrophone),
                AutomaticAnswers = Bool("automaticAnswers", current.AutomaticAnswers),
                AutoSummarize = Bool("autoSummarize", current.AutoSummarize),
                UseSemanticRetrieval = Bool("semanticRetrievalEnabled", current.UseSemanticRetrieval),
                WebSearch = values.TryGetValue("webSearchMode", out var search) ? search != "off" : current.WebSearch,
                CodexModel = Text("codexModelID", current.CodexModel),
            };
        }
        catch (System.Xml.XmlException) { return current; }
    }
}
