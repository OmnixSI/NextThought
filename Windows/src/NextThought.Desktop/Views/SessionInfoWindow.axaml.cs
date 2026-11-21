using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>E15: Finder-style 详细信息 sheet for one archived meeting (SessionInfoView.swift).</summary>
public partial class SessionInfoWindow : Window
{
    public SessionInfoWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Load();
    }

    private void Load()
    {
        if (DataContext is not AppState state) return;
        var items = new List<SessionInfoRow>();
        string? group = null;
        foreach (var (rowGroup, label, value) in state.CurrentSessionInfoGroups())
        {
            items.Add(new SessionInfoRow(rowGroup, label, value, group != rowGroup));
            group = rowGroup;
        }
        InfoItems.ItemsSource = items;
        InfoItems.IsVisible = items.Count > 0;
        EmptyPanel.IsVisible = items.Count == 0;
    }

    private void Retry_Click(object? sender, RoutedEventArgs e) => Load();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}

/// <summary>One 详细信息 row; the first row of a group carries its heading.</summary>
public sealed record SessionInfoRow(string Group, string Label, string Value, bool StartsGroup);
