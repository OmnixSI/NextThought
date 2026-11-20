using Avalonia;
using Avalonia.Markup.Xaml;
using NextThought.Core;

namespace NextThought.Desktop.Localization;

/// <summary>
/// G8: <c>{loc:Tr 中文原句}</c> — the explicit way XAML asks for interface text. The key is the
/// Mac original Chinese sentence; <see cref="UiTextSource"/> decides whether to render it or its
/// English translation and raises a change notification on language switch, so the value updates
/// in place without reopening the page.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    /// <summary>The Mac original Chinese sentence.</summary>
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = Key ?? string.Empty;
        // A binding rather than a string: every bound control re-reads the table when the
        // language changes (UiTextSource raises PropertyChanged for the indexer).
        return UiTextSource.Instance.Observe(key).ToBinding();
    }
}
