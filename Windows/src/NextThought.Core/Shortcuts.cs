namespace NextThought.Core;

/// <summary>
/// Rebindable global shortcut keys. Windows keeps Ctrl+Alt as the modifier (Mac uses its
/// ShortcutBinding); only the key is configurable, in settings.json under "shortcuts".
/// </summary>
public static class ShortcutKeys
{
    public sealed record ActionDefinition(string Id, string Label, uint DefaultKey, uint DefaultModifiers);

    /// <summary>G2: the Mac app's 18 hotkey actions, in its raw-value order, with the Mac defaults
    /// translated to Windows modifiers (⌘⇧→Ctrl+Alt, ⌘⌃→Ctrl+Alt, ⌃⌥→Ctrl+Alt+Shift).</summary>
    public static readonly IReadOnlyList<ActionDefinition> Actions =
    [
        new("window", "窗口截图", 0x41, ControlModifier | AltModifier),
        new("region", "区域截图", 0x53, ControlModifier | AltModifier),
        new("record", "开始 / 停止记录", 0x4D, ControlModifier | AltModifier),
        new("regenerate", "重新生成上一题", 0x52, ControlModifier | AltModifier),
        new("force", "立即回答最新转写", 0x0D, ControlModifier | AltModifier),
        new("pause", "暂停 / 恢复聆听", 0x50, ControlModifier | AltModifier),
        new("teleprompter", "显示 / 隐藏提词", 0x4F, ControlModifier | AltModifier),
        new("cancel", "取消当前回答", 0x1B, ControlModifier | AltModifier),
        new("copy", "复制当前回答", 0x59, ControlModifier | AltModifier),
        new("through", "提词浮窗鼠标穿透", 0x4B, ControlModifier | AltModifier),
        new("hide", "隐藏 / 恢复所有窗口", 0x47, ControlModifier | AltModifier),
        new("previous", "上一条回答", 0x25, ControlModifier | AltModifier),
        new("next", "下一条回答", 0x27, ControlModifier | AltModifier),
        new("shorter", "把回答改短", 0x31, ControlModifier | AltModifier),
        new("live", "切换会中窗口（纪要 / 字幕条 / 隐藏）", 0x53, ControlModifier | AltModifier | ShiftModifier),
        new("island", "提词岛一直展开 / 恢复", 0x45, ControlModifier | AltModifier),
        new("slidePrevious", "讲稿上一页", 0xBC, ControlModifier | AltModifier | ShiftModifier),
        new("slideNext", "讲稿下一页", 0xBE, ControlModifier | AltModifier | ShiftModifier)
    ];

    public static ActionDefinition? Find(string id) =>
        Actions.FirstOrDefault(action => string.Equals(action.Id, id, StringComparison.OrdinalIgnoreCase));

    public static uint Parse(string? name, uint fallback)
    {
        var text = (name ?? "").Trim().ToUpperInvariant();
        if (text.Length == 0) return fallback;
        var alias = text switch
        {
            "LEFT" => 0x25u, "RIGHT" => 0x27u, "UP" => 0x26u, "DOWN" => 0x28u,
            "PAGEUP" or "PGUP" => 0x21u, "PAGEDOWN" or "PGDN" => 0x22u,
            "SPACE" => 0x20u, "BACKSLASH" or "\\" => 0xDCu,
            "COMMA" or "," => 0xBCu, "PERIOD" or "DOT" or "." => 0xBEu,
            "ENTER" => 0x0Du, "ESC" or "ESCAPE" => 0x1Bu, "TAB" => 0x09u,
            _ => 0u
        };
        if (alias != 0) return alias;
        if (text.Length is 2 or 3 && text[0] == 'F' && int.TryParse(text[1..], out var function)
            && function is >= 1 and <= 24) return (uint)(0x70 + function - 1);
        if (text.Length == 1 && char.IsAsciiLetterOrDigit(text[0])) return char.ToUpperInvariant(text[0]);
        return fallback;
    }

    public const uint ControlModifier = 0x0002;
    public const uint AltModifier = 0x0001;
    public const uint ShiftModifier = 0x0004;
    public const uint WindowsModifier = 0x0008;

    /// <summary>"Ctrl+Shift+R" → modifiers and key; a bare "R" keeps the Ctrl+Alt default.</summary>
    public static (uint Modifiers, uint Key) ParseBinding(string? text, uint fallbackKey) =>
        ParseBinding(text, ControlModifier | AltModifier, fallbackKey);

    public static (uint Modifiers, uint Key) ParseBinding(string? text, uint fallbackModifiers, uint fallbackKey)
    {
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0) return (fallbackModifiers, fallbackKey);
        var parts = trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return (fallbackModifiers, fallbackKey);
        if (parts.Length == 1) return (fallbackModifiers, Parse(parts[0], fallbackKey));
        var modifiers = 0u;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            modifiers |= parts[index].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ControlModifier,
                "ALT" => AltModifier,
                "SHIFT" => ShiftModifier,
                "WIN" or "WINDOWS" or "META" => WindowsModifier,
                _ => 0u
            };
        }
        return (modifiers, Parse(parts[^1], fallbackKey));
    }

    public static string Format(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & ControlModifier) != 0) parts.Add("Ctrl");
        if ((modifiers & AltModifier) != 0) parts.Add("Alt");
        if ((modifiers & ShiftModifier) != 0) parts.Add("Shift");
        if ((modifiers & WindowsModifier) != 0) parts.Add("Win");
        parts.Add(Name(key));
        return string.Join("+", parts);
    }

    public static string Name(uint virtualKey) => virtualKey switch
    {
        0x25 => "Left", 0x27 => "Right", 0x26 => "Up", 0x28 => "Down",
        0x21 => "PageUp", 0x22 => "PageDown", 0x20 => "Space", 0xDC => "\\", 0xBC => ",", 0xBE => ".",
        0x0D => "Enter", 0x1B => "Esc", 0x09 => "Tab",
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x70 + 1),
        _ => ((char)virtualKey).ToString()
    };

    /// <summary>G3: Mac ShortcutBinding.validate messages, applied to Windows bindings.</summary>
    public static string? Validate(IReadOnlyDictionary<string, (uint Modifiers, uint Key)> bindings)
    {
        if (bindings.Values.Any(binding => binding.Modifiers == 0))
            return "快捷键必须包含 Command、Option 或 Control，不能只使用普通按键。";
        var seen = new HashSet<(uint, uint)>();
        foreach (var binding in bindings.Values)
            if (!seen.Add(binding)) return "两个动作不能使用相同的快捷键。";
        return null;
    }

    public static string DefaultText() =>
        string.Join("\n", Actions.Select(action => action.Id + "=" + Format(action.DefaultModifiers, action.DefaultKey)));
}
