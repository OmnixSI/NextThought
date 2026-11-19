namespace NextThought.Core;

/// <summary>
/// Rebindable global shortcut keys. Windows keeps Ctrl+Alt as the modifier (Mac uses its
/// ShortcutBinding); only the key is configurable, in settings.json under "shortcuts".
/// </summary>
public static class ShortcutKeys
{
    public static readonly IReadOnlyList<(string Id, string Label, uint Default)> Actions =
    [
        ("record", "开始或停止记录", 0x52),
        ("island", "显示或隐藏提示岛", 0x49),
        ("live", "切换会中窗口", 0x4D),
        ("region", "区域截图", 0x53),
        ("teleprompter", "提词器", 0x55),
        ("hide", "隐藏或恢复所有窗口", 0xDC),
        ("main", "唤起主窗口", 0x4E),
        ("copy", "复制当前回答", 0x48),
        ("regenerate", "重新生成上一题", 0x47),
        ("shorter", "把回答改短", 0x4B),
        ("cancel", "取消当前回答", 0x58),
        ("previous", "上一条回答", 0x25),
        ("next", "下一条回答", 0x27),
        ("through", "提词浮窗鼠标穿透", 0x4A),
        ("slideNext", "下一页幻灯片", 0x22),
        ("slidePrevious", "上一页幻灯片", 0x21),
        ("force", "立即回答最新转写", 0x51),
        ("window", "窗口截图", 0x57),
        ("pause", "暂停或恢复聆听", 0x20)
    ];

    public static uint Parse(string? name, uint fallback)
    {
        var text = (name ?? "").Trim().ToUpperInvariant();
        if (text.Length == 0) return fallback;
        var alias = text switch
        {
            "LEFT" => 0x25u, "RIGHT" => 0x27u, "UP" => 0x26u, "DOWN" => 0x28u,
            "PAGEUP" or "PGUP" => 0x21u, "PAGEDOWN" or "PGDN" => 0x22u,
            "SPACE" => 0x20u, "BACKSLASH" or "\\" => 0xDCu,
            "ENTER" => 0x0Du, "ESC" or "ESCAPE" => 0x1Bu, "TAB" => 0x09u,
            _ => 0u
        };
        if (alias != 0) return alias;
        if (text.Length is 2 or 3 && text[0] == 'F' && int.TryParse(text[1..], out var function)
            && function is >= 1 and <= 24) return (uint)(0x70 + function - 1);
        if (text.Length == 1 && char.IsAsciiLetterOrDigit(text[0])) return char.ToUpperInvariant(text[0]);
        return fallback;
    }

    public static string Name(uint virtualKey) => virtualKey switch
    {
        0x25 => "Left", 0x27 => "Right", 0x26 => "Up", 0x28 => "Down",
        0x21 => "PageUp", 0x22 => "PageDown", 0x20 => "Space", 0xDC => "\\",
        0x0D => "Enter", 0x1B => "Esc", 0x09 => "Tab",
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x70 + 1),
        _ => ((char)virtualKey).ToString()
    };

    public static string DefaultText() =>
        string.Join("\n", Actions.Select(action => action.Id + "=" + Name(action.Default)));
}
