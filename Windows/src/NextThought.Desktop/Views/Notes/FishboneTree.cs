using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E15 / FishboneView.swift FishboneTree: the narrow-width fallback of the fishbone — the head,
/// then one collapsible row per cause category (chevron, name, cause count) and the causes as a
/// list with a hairline guide. Causes with a time jump back to the transcript; text is selectable.
/// </summary>
public class FishboneTree : UserControl
{
    public static readonly StyledProperty<FishboneNotes?> NotesProperty =
        AvaloniaProperty.Register<FishboneTree, FishboneNotes?>(nameof(Notes));

    private readonly StackPanel root = new() { Spacing = 10 };
    private readonly HashSet<int> folded = [];

    public FishboneTree()
    {
        Content = new ScrollViewer { Content = root, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Rebuild();
    }

    public FishboneNotes? Notes
    {
        get => GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    /// <summary>Raised for a cause/category whose time was clicked (jump to the transcript).</summary>
    public event Action<int>? Jump;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NotesProperty) Rebuild();
    }

    private static IBrush Brush(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true && value is IBrush brush ? brush : Brushes.Gray;

    private static FontFamily Serif => new("Source Serif 4, Noto Serif SC, SimSun, Georgia");
    private static FontFamily Sans => new("Inter, Noto Sans SC, Microsoft YaHei UI, Segoe UI");

    private void Rebuild()
    {
        root.Children.Clear();
        if (Notes is not { } notes) return;
        var ink = Brush("InkBrush");
        var secondary = Brush("Secondary");
        var hairline = Brush("Hairline");

        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        head.Children.Add(new SelectableTextBlock
        {
            Text = notes.Head.Length > 0 ? notes.Head : UiText.T("议题"),
            FontFamily = Serif,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = ink,
            TextWrapping = TextWrapping.Wrap
        });
        if (notes.HeadAt is { } headAt) head.Children.Add(TimeLink(headAt, secondary));
        root.Children.Add(head);

        for (var index = 0; index < notes.Categories.Count; index++)
        {
            var category = notes.Categories[index];
            var isFolded = folded.Contains(index);
            var header = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerRow.Children.Add(new TextBlock
            {
                Text = Icon(isFolded ? "IconChevronRight" : "IconChevronDown"),
                FontFamily = IconFont(),
                FontSize = 9,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center
            });
            headerRow.Children.Add(new TextBlock
            {
                Text = category.Name,
                FontFamily = Sans,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = ink
            });
            headerRow.Children.Add(new TextBlock
            {
                Text = UiText.Format("%lld 条", category.Causes.Count),
                FontSize = 11,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Content = headerRow;
            var capturedIndex = index;
            header.Click += (_, _) =>
            {
                if (!folded.Add(capturedIndex)) folded.Remove(capturedIndex);
                Rebuild();
            };
            root.Children.Add(header);

            if (isFolded) continue;
            var causes = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(14, 0, 0, 4)
            };
            foreach (var cause in category.Causes)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                row.Children.Add(new SelectableTextBlock
                {
                    Text = cause.Text,
                    FontFamily = Sans,
                    FontSize = 13,
                    Foreground = ink,
                    TextWrapping = TextWrapping.Wrap
                });
                if (cause.At is { } at) row.Children.Add(TimeLink(at, secondary));
                causes.Children.Add(row);
            }
            root.Children.Add(new Border
            {
                BorderBrush = hairline,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Padding = new Thickness(10, 0, 0, 0),
                Child = causes
            });
        }
    }

    private Control TimeLink(int seconds, IBrush secondary)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = "[" + NotesTime.Text(seconds) + "]",
                FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                FontSize = 12,
                Foreground = secondary
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        var captured = seconds;
        button.Click += (_, _) => Jump?.Invoke(captured);
        ToolTip.SetTip(button, UiText.T("跳到转写里的这句话"));
        return button;
    }

    private static string Icon(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true && value is string glyph ? glyph : "";

    private static FontFamily IconFont() =>
        Application.Current?.TryFindResource("IconFont", out var value) == true && value is FontFamily font
            ? font
            : new FontFamily("Segoe Fluent Icons");
}
