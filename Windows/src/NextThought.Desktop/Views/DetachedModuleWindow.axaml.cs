using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E5: a popped-out answer module (DetachedModules.swift DetachedModuleView). Closing the window
/// docks the module back, exactly like Mac's windowWillClose.
/// </summary>
public partial class DetachedModuleWindow : Window
{
    private AssistantModule module = AssistantModule.Answer;

    public DetachedModuleWindow()
    {
        InitializeComponent();
        ApplyModule();
    }

    /// <summary>Which module this window shows; set before Show().</summary>
    public AssistantModule Module
    {
        get => module;
        set
        {
            if (module == value) return;
            module = value;
            ApplyModule();
        }
    }

    private void ApplyModule()
    {
        ModuleTitle.Text = UiText.T(TitleFor(module));
        ModuleIcon.Text = Application.Current?.TryFindResource(IconFor(module), out var icon) == true
            ? icon as string ?? ""
            : "";
        QuestionPanel.IsVisible = module == AssistantModule.Question;
        HistoryPanel.IsVisible = module == AssistantModule.History;
        ConversationPanel.IsVisible = module == AssistantModule.Conversation;
        AnswerPanel.IsVisible = module == AssistantModule.Answer;
        InsightsPanelHost.IsVisible = module == AssistantModule.Insights;
        Width = module switch
        {
            AssistantModule.Question => 520,
            AssistantModule.History => 380,
            AssistantModule.Conversation => 420,
            AssistantModule.Insights => 440,
            _ => 560
        };
        Height = module switch
        {
            AssistantModule.Question => 200,
            AssistantModule.History => 460,
            AssistantModule.Conversation => 560,
            AssistantModule.Insights => 520,
            _ => 480
        };
    }

    /// <summary>The Mac module titles (AssistantModule.title).</summary>
    public static string TitleFor(AssistantModule module) => module switch
    {
        AssistantModule.Conversation => "实时对话",
        AssistantModule.Question => "问题",
        AssistantModule.Answer => "建议回答",
        AssistantModule.History => "历史回答",
        _ => "屏幕与讲稿"
    };

    private static string IconFor(AssistantModule module) => module switch
    {
        AssistantModule.Conversation => "IconBubbleLeftAndBubbleRight",
        AssistantModule.Question => "IconQuestionmarkBubble",
        AssistantModule.Answer => "IconSparkle",
        AssistantModule.History => "IconClock",
        _ => "IconDisplay"
    };

    private void Dock_Click(object? sender, RoutedEventArgs e) =>
        (DataContext as AppState)?.DockDetachedModule(module);
}
