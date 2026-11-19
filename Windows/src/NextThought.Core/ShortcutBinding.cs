namespace NextThought.Core;

/// <summary>
/// A keyboard shortcut as the Mac app stores it (port of ShortcutBinding.swift). Key codes are
/// macOS virtual-key codes; the Windows port only uses the model for validation and labels.
/// </summary>
public sealed record ShortcutBinding(uint KeyCode, bool Command = true, bool Shift = true, bool Option = false, bool Control = false)
{
    private static readonly Dictionary<uint, string> Names = new()
    {
        [0] = "A", [1] = "S", [8] = "C", [11] = "B", [15] = "R", [16] = "Y", [18] = "1", [19] = "2", [20] = "3",
        [31] = "O", [35] = "P", [36] = "↩", [40] = "K", [42] = "\\", [46] = "M", [49] = "Space", [53] = "Esc",
        [123] = "←", [124] = "→", [125] = "↓", [126] = "↑"
    };

    public bool IsSafe => KeyCode <= 127 && (Command || Option || Control);

    public string Label =>
        (Control ? "⌃" : "") + (Option ? "⌥" : "") + (Shift ? "⇧" : "") + (Command ? "⌘" : "") +
        (Names.TryGetValue(KeyCode, out var name) ? name : $"Key {KeyCode}");

    /// <summary>Null when every binding is safe and unique; otherwise the reason for the user.</summary>
    public static string? Validate(IReadOnlyDictionary<string, ShortcutBinding> bindings)
    {
        if (!bindings.Values.All(binding => binding.IsSafe))
            return "快捷键必须包含 Command、Option 或 Control，不能只使用普通按键。";
        var seen = new List<ShortcutBinding>();
        foreach (var binding in bindings.Values)
        {
            if (seen.Contains(binding)) return "两个动作不能使用相同的快捷键。";
            seen.Add(binding);
        }
        return null;
    }
}
