using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views.Assistant;

/// <summary>
/// E5: Mac ConversationPanel (AssistantView.swift) — the live conversation module: 实时对话 header
/// with the entry count, the transcript status line, one bubble per entry (mine right, the other
/// side left), the empty hint before the first line and the in-progress partial bubbles.
/// The follow toggle scrolls to the newest bubble unless paused (Mac ConversationPanel.follow).
/// </summary>
public partial class ConversationPanel : UserControl
{
    private bool follow = true;
    private AppState? state;

    public ConversationPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        DataContextChanged += (_, _) => Attach();
        UpdateFollowButton();
    }

    private void Attach()
    {
        if (state is not null) state.Transcript.CollectionChanged -= OnTranscriptChanged;
        state = DataContext as AppState;
        if (state is null) return;
        state.Transcript.CollectionChanged += OnTranscriptChanged;
        if (follow) ScrollToBottom();
    }

    private void OnTranscriptChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (follow) ScrollToBottom();
    }

    private void Follow_Click(object? sender, RoutedEventArgs e)
    {
        follow = !follow;
        UpdateFollowButton();
        if (follow) ScrollToBottom();
    }

    private void UpdateFollowButton()
    {
        if (FollowButton is null) return;
        FollowButton.Content = UiText.T(follow ? "自动滚动到最新（点按暂停）" : "已暂停自动滚动（点按恢复）");
        ToolTip.SetTip(FollowButton, UiText.T(follow ? "自动滚动到最新" : "已暂停自动滚动"));
    }

    private void ScrollToBottom() => Dispatcher.UIThread.Post(() =>
    {
        try { ConversationScroll.ScrollToEnd(); } catch (InvalidOperationException) { }
    }, DispatcherPriority.Background);
}
