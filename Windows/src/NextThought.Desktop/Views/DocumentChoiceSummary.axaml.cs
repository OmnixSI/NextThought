using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NextThought.Desktop.Views;

/// <summary>E23: Mac DocumentChoiceSummary — the plain-language tradeoffs behind the reading method.
/// Shown as a small window here (the Mac version lives in the first-run guide).</summary>
public partial class DocumentChoiceSummary : Window
{
    public DocumentChoiceSummary() => InitializeComponent();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
