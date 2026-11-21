using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// G4 / E25: the shortcut editor from Mac ShortcutSettingsView.swift. One row per
/// <see cref="NextThought.Core.ShortcutKeys"/> action; clicking a binding records a new combination,
/// and the bottom row restores all Mac defaults. It lives inside <see cref="ShortcutsSettingsRow"/>,
/// which is what the 提词与教练 page shows.
/// </summary>
public partial class ShortcutSettingsView : UserControl
{
    private string? recordingShortcutId;

    public ShortcutSettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BuildShortcutRows();
        // The editor keeps the buttons it built, so a language switch has to rebuild the labels.
        NextThought.Core.UiTextSource.Instance.PropertyChanged += OnLanguageChanged;
        DetachedFromVisualTree += (_, _) =>
            NextThought.Core.UiTextSource.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        BuildShortcutRows();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        BuildShortcutRows();
    }

    /// <summary>G4: one row per Mac hotkey action; click the binding to record a key combination.</summary>
    private void BuildShortcutRows()
    {
        if (DataContext is not AppState state) return;
        ShortcutRecorderPanel.Children.Clear();
        var bindings = state.ShortcutText.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
        foreach (var action in NextThought.Core.ShortcutKeys.Actions)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
            grid.Children.Add(new TextBlock
            {
                Text = UiText.T(action.Label), FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            var value = new Button
            {
                Content = bindings.TryGetValue(action.Id, out var text)
                    ? text
                    : NextThought.Core.ShortcutKeys.Format(action.DefaultModifiers, action.DefaultKey),
                Tag = action.Id,
                FontFamily = new Avalonia.Media.FontFamily("Consolas,monospace"),
                FontSize = 11,
                MinWidth = 140
            };
            value.Click += (_, _) => BeginShortcutRecording(action.Id, value);
            value.AddHandler(InputElement.KeyDownEvent, ShortcutRecorder_KeyDown, RoutingStrategies.Tunnel);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            var clear = new Button { Content = UiText.T("清除"), Tag = action.Id, FontSize = 11, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
            clear.Click += (_, _) => ApplyShortcut(action.Id, action.DefaultModifiers, action.DefaultKey);
            Grid.SetColumn(clear, 2);
            grid.Children.Add(clear);
            var restore = new Button { Content = UiText.T("默认"), Tag = action.Id, FontSize = 11, Margin = new Avalonia.Thickness(4, 0, 0, 0) };
            restore.Click += (_, _) => ApplyShortcut(action.Id, action.DefaultModifiers, action.DefaultKey);
            Grid.SetColumn(restore, 3);
            grid.Children.Add(restore);
            ShortcutRecorderPanel.Children.Add(grid);
        }
    }

    private void BeginShortcutRecording(string id, Button button)
    {
        recordingShortcutId = id;
        button.Content = UiText.T("按下组合键…");
        button.Focus();
        ShortcutRecorderStatus.Text = UiText.T("按下带 Ctrl/Alt/Shift/Win 的组合键；Esc 取消。");
    }

    private void ShortcutRecorder_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Button { Tag: string id } button || recordingShortcutId != id) return;
        e.Handled = true;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            recordingShortcutId = null;
            ShortcutRecorderStatus.Text = "";
            BuildShortcutRows();
            return;
        }
        var modifiers = 0u;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= NextThought.Core.ShortcutKeys.ControlModifier;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= NextThought.Core.ShortcutKeys.AltModifier;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= NextThought.Core.ShortcutKeys.ShiftModifier;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= NextThought.Core.ShortcutKeys.WindowsModifier;
        var key = ToVirtualKey(e.Key);
        if (key == 0) { ShortcutRecorderStatus.Text = UiText.T("这个键不能用作全局快捷键。"); return; }
        if (modifiers == 0)
        {
            ShortcutRecorderStatus.Text = UiText.T("快捷键必须包含 Ctrl、Alt 或 Shift，不能只使用普通按键。");
            return;
        }
        ApplyShortcut(id, modifiers, key);
        ShortcutRecorderStatus.Text = UiText.T("已改为 ") + NextThought.Core.ShortcutKeys.Format(modifiers, key) + UiText.T("，立即生效。");
    }

    private void ApplyShortcut(string id, uint modifiers, uint key)
    {
        if (DataContext is not AppState state) return;
        recordingShortcutId = null;
        var lines = state.ShortcutText.Replace("\r\n", "\n").Split('\n').ToList();
        var replacement = id + "=" + NextThought.Core.ShortcutKeys.Format(modifiers, key);
        var index = lines.FindIndex(line => line.TrimStart().StartsWith(id + "=", StringComparison.OrdinalIgnoreCase));
        if (index >= 0) lines[index] = replacement; else lines.Add(replacement);
        state.ShortcutText = string.Join("\n", lines);
        state.SaveShortcutsCommand.Execute(null);
        BuildShortcutRows();
    }

    /// <summary>Mac "恢复默认快捷键": write every action's Mac default back and register them once.</summary>
    private void ResetShortcuts_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        recordingShortcutId = null;
        state.ShortcutText = string.Join("\n", NextThought.Core.ShortcutKeys.Actions.Select(action =>
            action.Id + "=" + NextThought.Core.ShortcutKeys.Format(action.DefaultModifiers, action.DefaultKey)));
        state.SaveShortcutsCommand.Execute(null);
        ShortcutRecorderStatus.Text = "";
        BuildShortcutRows();
    }

    private static uint ToVirtualKey(Key key) => key switch
    {
        Key.OemComma => 0xBC,
        Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF,
        Key.OemMinus => 0xBD,
        Key.OemPlus => 0xBB,
        Key.OemOpenBrackets => 0xDB,
        Key.OemCloseBrackets => 0xDD,
        Key.OemBackslash => 0xDC,
        Key.OemSemicolon => 0xBA,
        Key.OemQuotes => 0xDE,
        Key.Enter or Key.Return => 0x0D,
        Key.Escape => 0x1B,
        Key.Space => 0x20,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        _ => NextThought.Core.ShortcutKeys.Parse(key.ToString(), 0)
    };
}
