using System.Collections.Specialized;
using Avalonia.Controls;

namespace NextThought.Desktop.Views;

/// <summary>E23: Mac DocumentComponentsAdvanced — installed components, fallback, Codex fields and the
/// semantic search section. One ManagedModelRow per component (Mac ManagedModelRow).</summary>
public partial class DocumentComponentsAdvanced : UserControl
{
    private AppState? state;

    public DocumentComponentsAdvanced()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (state is not null) state.DocumentComponentRows.CollectionChanged -= OnRowsChanged;
        state = DataContext as AppState;
        if (state is null) return;
        state.DocumentComponentRows.CollectionChanged += OnRowsChanged;
        BuildRows();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildRows();

    private void BuildRows()
    {
        ComponentRows.Children.Clear();
        if (state is null) return;
        foreach (var row in state.DocumentComponentRows)
            ComponentRows.Children.Add(new ManagedModelRow { DataContext = row, Tag = state });
    }
}
