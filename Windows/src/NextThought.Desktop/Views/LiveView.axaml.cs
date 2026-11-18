using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class LiveView : UserControl
{
    private AppState? state;

    public LiveView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (state is not null) state.Transcript.CollectionChanged -= OnTranscriptChanged;
        state = DataContext as AppState;
        if (state is not null) state.Transcript.CollectionChanged += OnTranscriptChanged;
    }

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (TranscriptList.ItemCount == 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                TranscriptList.ScrollIntoView(TranscriptList.ItemCount - 1);
            }
            catch
            {
                // Scrolling is cosmetic; a stale item during a reset must not break recording.
            }
        }, DispatcherPriority.Background);
    }

    private void Question_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
        if (DataContext is not AppState state) return;
        e.Handled = true;
        if (state.AskCommand.CanExecute(null)) state.AskCommand.Execute(null);
    }

    private void ViewSource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state) return;
        if (SourcesBox.SelectedItem is SourceCitation citation) state.OpenMaterial(citation);
        else state.SetStatus("当前回答尚无资料引用。");
    }
}
