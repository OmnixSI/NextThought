namespace NextThought.Core;

/// <summary>G5: the closed set of value shapes a Mac preference key can hold.</summary>
public enum MacPreferenceKind { Bool, String, Integer, Real, Array, Dict }

/// <summary>
/// One of the 113 Mac preference keys: its type, Mac default, the Mac settings/additional view
/// that owns it, the verbatim Swift expression the default comes from, and the allowed values
/// or numeric range the Mac interface uses.
/// </summary>
public sealed record MacPreference(
    string Key,
    MacPreferenceKind Kind,
    object? Default,
    string MacView,
    string MacDefault,
    IReadOnlyList<string>? AllowedValues = null,
    double? Minimum = null,
    double? Maximum = null);

/// <summary>
/// G5: the single registry of the Mac preference keys. Every key keeps the Mac name, type and
/// default; the same table drives the settings store, the Mac plist import/export and the parity
/// tests. The entries themselves are generated from the Swift sources by
/// <c>Windows/scripts/settings_parity.py --write</c> (see <see cref="MacPreferencesData"/>).
/// </summary>
public static class MacPreferences
{
    public static readonly IReadOnlyList<MacPreference> All = MacPreferencesData.Entries;

    public static readonly IReadOnlyDictionary<string, MacPreference> ByKey =
        All.ToDictionary(preference => preference.Key, StringComparer.Ordinal);

    public static readonly IReadOnlyList<string> Keys = All.Select(preference => preference.Key).ToList();

    public static MacPreference? Find(string key) => ByKey.GetValueOrDefault(key);

    public static bool IsKnown(string key) => ByKey.ContainsKey(key);

    /// <summary>The Mac default for one key, deep copied so callers cannot modify the registry.</summary>
    public static object? Default(string key) => Find(key) is { } preference ? Clone(preference.Default) : null;

    /// <summary>Every key with its Mac default, in registry order.</summary>
    public static Dictionary<string, object?> CreateDefaults()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var preference in All) values[preference.Key] = Clone(preference.Default);
        return values;
    }

    /// <summary>
    /// Normalises a stored value the way the Mac app behaves when it reads the key: an unknown
    /// enum/string falls back to the Mac default and a numeric value is clamped to the Mac slider
    /// range. Unsupported types also fall back to the default; nil stays nil for keys whose Mac
    /// default is nil (absent from UserDefaults).
    /// </summary>
    public static object? Normalize(string key, object? value)
    {
        if (Find(key) is not { } preference) return value;
        return Normalize(preference, value);
    }

    public static object? Normalize(MacPreference preference, object? value)
    {
        switch (preference.Kind)
        {
            case MacPreferenceKind.Bool:
                return value is bool flag ? flag : preference.Default;
            case MacPreferenceKind.String:
            {
                if (value is not string text) return preference.Default;
                return preference.AllowedValues is { } allowed && !allowed.Contains(text, StringComparer.Ordinal)
                    ? preference.Default
                    : text;
            }
            case MacPreferenceKind.Integer:
            {
                if (!TryToLong(value, out var number)) return preference.Default;
                if (preference.Minimum is { } minimum && number < minimum) number = (long)minimum;
                if (preference.Maximum is { } maximum && number > maximum) number = (long)maximum;
                return number;
            }
            case MacPreferenceKind.Real:
            {
                if (!TryToDouble(value, out var number)) return preference.Default;
                if (preference.Minimum is { } minimum && number < minimum) number = minimum;
                if (preference.Maximum is { } maximum && number > maximum) number = maximum;
                return number;
            }
            case MacPreferenceKind.Array:
                return value is System.Collections.IEnumerable and not string
                    ? Clone(value)
                    : preference.Default;
            case MacPreferenceKind.Dict:
                return value is IReadOnlyDictionary<string, object?> or System.Collections.IDictionary
                    ? Clone(value)
                    : preference.Default;
            default:
                return preference.Default;
        }
    }

    /// <summary>True when the value equals the Mac default (after normalisation).</summary>
    public static bool IsDefault(string key, object? value)
    {
        if (Find(key) is not { } preference) return false;
        return ValuesEqual(Normalize(preference, value), preference.Default);
    }

    public static bool ValuesEqual(object? left, object? right)
    {
        if (left is null || right is null) return left is null && right is null;
        // Dictionaries are enumerable too; compare them as maps before falling into the sequence branch.
        if (left is IReadOnlyDictionary<string, object?> leftMap && right is IReadOnlyDictionary<string, object?> rightMap)
        {
            if (leftMap.Count != rightMap.Count) return false;
            foreach (var (mapKey, mapValue) in leftMap)
            {
                if (!rightMap.TryGetValue(mapKey, out var other) || !ValuesEqual(mapValue, other)) return false;
            }
            return true;
        }
        if (left is System.Collections.IDictionary leftDictionary && right is System.Collections.IDictionary rightDictionary)
        {
            if (leftDictionary.Count != rightDictionary.Count) return false;
            foreach (System.Collections.DictionaryEntry entry in leftDictionary)
            {
                var mapKey = entry.Key.ToString() ?? "";
                if (!rightDictionary.Contains(mapKey) || !ValuesEqual(entry.Value, rightDictionary[mapKey])) return false;
            }
            return true;
        }
        if (left is System.Collections.IEnumerable leftItems and not string
            && right is System.Collections.IEnumerable rightItems and not string)
        {
            var leftList = leftItems.Cast<object?>().ToList();
            var rightList = rightItems.Cast<object?>().ToList();
            if (leftList.Count != rightList.Count) return false;
            for (var index = 0; index < leftList.Count; index++)
                if (!ValuesEqual(leftList[index], rightList[index])) return false;
            return true;
        }
        if (IsNumber(left) && IsNumber(right)) return Convert.ToDouble(left).Equals(Convert.ToDouble(right));
        return left.Equals(right);
    }

    public static object? Clone(object? value) => value switch
    {
        null => null,
        IReadOnlyDictionary<string, object?> map => map.ToDictionary(pair => pair.Key, pair => Clone(pair.Value), StringComparer.Ordinal),
        System.Collections.IDictionary dictionary => dictionary.Keys.Cast<object>()
            .ToDictionary(key => key.ToString() ?? "", key => Clone(dictionary[key]), StringComparer.Ordinal),
        System.Collections.IEnumerable items when value is not string => items.Cast<object?>().Select(Clone).ToList(),
        _ => value
    };

    private static bool TryToLong(object? value, out long number)
    {
        switch (value)
        {
            case sbyte or byte or short or ushort or int or uint or long:
                number = Convert.ToInt64(value);
                return true;
            case ulong unsigned when unsigned <= long.MaxValue:
                number = (long)unsigned;
                return true;
            case float or double or decimal:
                var real = Convert.ToDouble(value);
                if (double.IsFinite(real) && real is >= long.MinValue and <= long.MaxValue)
                {
                    number = (long)Math.Round(real);
                    return true;
                }
                break;
        }
        number = 0;
        return false;
    }

    private static bool TryToDouble(object? value, out double number)
    {
        if (value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            var real = Convert.ToDouble(value);
            if (double.IsFinite(real)) { number = real; return true; }
        }
        number = 0;
        return false;
    }

    private static bool IsNumber(object? value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
