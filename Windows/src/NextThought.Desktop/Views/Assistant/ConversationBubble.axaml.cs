using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace NextThought.Desktop.Views.Assistant;

/// <summary>
/// E5: Mac ConversationBubble (AssistantView.swift) — one live-conversation line: the other side's
/// bubbles sit left in gray, mine sit right in the accent soft colour, questions carry a 问题 badge,
/// and hovering an other-side line offers 回答这句. Suspected echoes show as a dimmed line instead.
/// </summary>
public partial class ConversationBubble : UserControl
{
    public ConversationBubble()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Apply();
    }

    private TranscriptRow? Row => DataContext as TranscriptRow;

    private void Apply()
    {
        if (Row is not { } row) return;
        var isMe = row.Speaker == "me";
        var isEcho = row.IsEcho;
        EchoLine.IsVisible = isEcho;
        Bubble.IsVisible = !isEcho;
        EchoText.Text = row.Label.Length > 0 ? row.Label + " · " + row.Text : row.Text;
        if (isEcho) return;
        BodyText.Text = row.Text;
        TimeText.Text = row.Date.ToLocalTime().ToString("HH:mm:ss");
        QuestionBadge.IsVisible = row.IsQuestion;
        BubbleColumn.HorizontalAlignment = isMe ? Avalonia.Layout.HorizontalAlignment.Right
            : Avalonia.Layout.HorizontalAlignment.Left;
        Bubble.HorizontalAlignment = isMe ? Avalonia.Layout.HorizontalAlignment.Right
            : Avalonia.Layout.HorizontalAlignment.Left;
        BodyText.TextAlignment = isMe ? TextAlignment.Right : TextAlignment.Left;
        BubbleBorder.Background = Resource(isMe || row.IsQuestion ? "AiSoft" : "TranscriptBubble");
        AnswerThisButton.IsVisible = false;
    }

    private IBrush? Resource(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true ? value as IBrush : null;

    private void Root_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (Row is { IsEcho: false, Speaker: not "me" }) AnswerThisButton.IsVisible = true;
    }

    private void Root_PointerExited(object? sender, PointerEventArgs e) => AnswerThisButton.IsVisible = false;

    private void AnswerThis_Click(object? sender, RoutedEventArgs e)
    {
        if (Row is not { } row) return;
        if (TopLevel.GetTopLevel(this)?.DataContext is not AppState state) return;
        state.DetectedQuestion = row.Text;
        if (state.ForceAnswerCommand.CanExecute(null)) state.ForceAnswerCommand.Execute(null);
    }
}
