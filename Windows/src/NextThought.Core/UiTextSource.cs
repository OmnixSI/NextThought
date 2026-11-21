using System.ComponentModel;
using System.Globalization;

namespace NextThought.Core;

/// <summary>
/// G8: the one language source for the whole shell. UI files bind to <see cref="Instance"/>'s
/// indexer through the <c>{loc:Tr …}</c> markup extension and code calls <see cref="UiText.T"/>/
/// <see cref="UiText.Format"/>; changing the language raises <see cref="PropertyChanged"/> and every
/// binding re-reads its string, so no page has to be reopened.
/// </summary>
public sealed class UiTextSource : INotifyPropertyChanged
{
    /// <summary>Mac <c>appLanguagePreference</c> values: "system", "zh-Hans" or "en".</summary>
    public const string SystemPreference = "system";
    public const string ChinesePreference = "zh-Hans";
    public const string EnglishPreference = "en";

    public static UiTextSource Instance { get; } = new();

    private string preference = SystemPreference;
    private bool isEnglish;

    private UiTextSource() => isEnglish = UiText.ResolveEnglish(preference);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Mac <c>appLanguagePreference</c>: the user's choice, not the resolved language.</summary>
    public string Preference => preference;

    /// <summary>The resolved interface language.</summary>
    public bool IsEnglish => isEnglish;

    /// <summary>Bindings read <c>[原句]</c>; Chinese returns the key unchanged.</summary>
    public string this[string key] => UiText.Lookup(key ?? string.Empty, isEnglish);

    /// <summary>Applies a stored preference ("system"/"zh-Hans"/"en", also accepts "zh"/"中文"/"English").</summary>
    public void SetPreference(string? value)
    {
        var normalized = NormalizePreference(value);
        var english = UiText.ResolveEnglish(normalized);
        var changed = english != isEnglish;
        preference = normalized;
        isEnglish = english;
        UiText.EnglishUi = english;
        if (changed) Refresh();
    }

    /// <summary>Convenience for call sites that only know the resolved flag.</summary>
    public void SetEnglish(bool english) => SetPreference(english ? EnglishPreference : ChinesePreference);

    /// <summary>Re-reads every bound string (also used when the shell re-renders rows by hand).</summary>
    public void Refresh()
    {
        // Avalonia invalidates every indexer binding on "Item[]" and on the empty name.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>
    /// The observable the markup extension binds to: emits the current string once, then on every
    /// language change. Used instead of a binding path so keys may contain any character.
    /// </summary>
    public IObservable<string> Observe(string key) => new KeyObservable(this, key ?? string.Empty);

    public static string NormalizePreference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SystemPreference;
        var trimmed = value.Trim();
        if (trimmed.Equals(SystemPreference, StringComparison.OrdinalIgnoreCase)) return SystemPreference;
        if (trimmed.Equals("跟随系统", StringComparison.Ordinal)) return SystemPreference;
        if (trimmed.StartsWith("en", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("English", StringComparison.OrdinalIgnoreCase))
            return EnglishPreference;
        if (trimmed.StartsWith("zh", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("中文", StringComparison.Ordinal))
            return ChinesePreference;
        return SystemPreference;
    }

    private sealed class KeyObservable(UiTextSource source, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            observer.OnNext(source[key]);
            void Handler(object? sender, PropertyChangedEventArgs args) => observer.OnNext(source[key]);
            source.PropertyChanged += Handler;
            return new Subscription(source, Handler);
        }

        private sealed class Subscription(UiTextSource source, PropertyChangedEventHandler handler) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                source.PropertyChanged -= handler;
            }
        }
    }
}
