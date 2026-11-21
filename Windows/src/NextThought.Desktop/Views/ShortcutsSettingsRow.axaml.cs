using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>
/// E25 / Mac SettingsPresenter.swift ShortcutsSettingsRow: the 提词与教练 page ends with a
/// collapsible 快捷键… row that expands <see cref="ShortcutSettingsView"/> in place (chevron
/// rotates), instead of a permanently open shortcut card.
/// </summary>
public partial class ShortcutsSettingsRow : UserControl
{
    public ShortcutsSettingsRow() => InitializeComponent();

    public bool IsExpanded => Editor.IsVisible;

    private void Toggle_Click(object? sender, RoutedEventArgs e) => SetExpanded(!Editor.IsVisible);

    public void SetExpanded(bool expanded)
    {
        Editor.IsVisible = expanded;
        Chevron.Text = expanded ? "\uF2A4" : "\uF2B1";
        if (expanded) Editor.Focus();
    }
}
