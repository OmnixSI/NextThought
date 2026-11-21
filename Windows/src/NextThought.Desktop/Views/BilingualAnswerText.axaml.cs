using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E6: an answer that is said in one language with the meaning alongside in another
/// (the Mac BilingualAnswer layout). The model marks the lines as Markdown quotes: "> 问：" once,
/// then each sentence followed by "> 译：" with its meaning. The page shows the asked question in
/// the user's language first, then every sentence with its meaning directly under it, so the
/// English is read out and the Chinese is understood at a glance.
/// </summary>
public partial class BilingualAnswerText : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<BilingualAnswerText, string>(nameof(Text), "");

    public static readonly StyledProperty<double> BaseFontSizeProperty =
        AvaloniaProperty.Register<BilingualAnswerText, double>(nameof(BaseFontSize), 17d);

    static BilingualAnswerText()
    {
        TextProperty.Changed.AddClassHandler<BilingualAnswerText>((view, _) => view.Rebuild());
        BaseFontSizeProperty.Changed.AddClassHandler<BilingualAnswerText>((view, _) => view.Rebuild());
    }

    public BilingualAnswerText()
    {
        InitializeComponent();
        Rebuild();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double BaseFontSize
    {
        get => GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    private IBrush Brush(string key, string fallback) =>
        this.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallback));

    private sealed record Group(string Sentence, List<string> Glosses);

    private void Rebuild()
    {
        Host.Children.Clear();
        var answer = Text ?? "";
        if (!BilingualAnswer.IsBilingual(answer)) return;

        var groups = new List<Group>();
        foreach (var raw in answer.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            switch (BilingualAnswer.GlossOf(raw))
            {
                case BilingualAnswer.Gloss.Line gloss:
                    if (groups.Count > 0) groups[^1].Glosses.Add(gloss.Text);
                    continue;
                case BilingualAnswer.Gloss.Question:
                    continue;
            }
            var sentence = MarkdownPlain.Strip(raw.Trim());
            if (sentence.Length > 0) groups.Add(new Group(sentence, []));
        }

        foreach (var group in groups)
        {
            Host.Children.Add(new SelectableTextBlock
            {
                Text = group.Sentence,
                FontSize = BaseFontSize,
                LineHeight = BaseFontSize * 1.35,
                TextWrapping = TextWrapping.Wrap
            });
            foreach (var gloss in group.Glosses)
            {
                Host.Children.Add(new TextBlock
                {
                    Text = gloss,
                    FontSize = Math.Max(11, BaseFontSize * 0.78),
                    LineHeight = BaseFontSize * 1.15,
                    Foreground = Brush("Secondary", "#6B6963"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }
        }
    }
}
