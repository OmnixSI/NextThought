using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E26: one editable scene row in Settings ▸ 场景.</summary>
public sealed class SceneSettingRow : ObservableObject
{
    private readonly Action<SceneSettingRow> save;
    private string liveMode, answerStyle, notesTemplate, cueStyle;

    public SceneSettingRow(MeetingMode mode, SceneDefaults defaults, Action<SceneSettingRow> save)
    {
        Mode = mode;
        this.save = save;
        liveMode = defaults.LiveMode;
        answerStyle = defaults.AnswerStyle;
        notesTemplate = defaults.NotesTemplate;
        cueStyle = defaults.CueStyle;
        Label = mode switch
        {
            MeetingMode.General => "通用",
            MeetingMode.Meeting => "组会",
            MeetingMode.Client => "客户沟通",
            MeetingMode.Academic => "汇报 / 答辩",
            MeetingMode.LabMeeting => "组会讨论",
            MeetingMode.Lesson => "讲座 / 课程",
            MeetingMode.Discussion => "小组讨论",
            MeetingMode.Interview => "面试",
            MeetingMode.GroupProject => "工作布置",
            _ => mode.ToString()
        };
        Tint = SceneTheme.For(mode).TintLight;
    }

    public MeetingMode Mode { get; }
    public string Label { get; }
    public string Tint { get; }
    public IBrush TintBrush => new SolidColorBrush(Color.Parse(Tint));
    public IReadOnlyList<string> LiveOptions { get; } = ["自动回答", "只记录"];
    public IReadOnlyList<string> AnswerOptions { get; } = ["简洁", "详细"];
    public IReadOnlyList<string> TemplateOptions { get; } = ["标准纪要", "学术汇报", "工作会议"];
    public IReadOnlyList<string> CueOptions { get; } = ["只看要点", "逐句"];

    public string LiveMode { get => liveMode; set { if (SetProperty(ref liveMode, value)) save(this); } }
    public string AnswerStyle { get => answerStyle; set { if (SetProperty(ref answerStyle, value)) save(this); } }
    public string NotesTemplate { get => notesTemplate; set { if (SetProperty(ref notesTemplate, value)) save(this); } }
    public string CueStyle { get => cueStyle; set { if (SetProperty(ref cueStyle, value)) save(this); } }

    public SceneDefaults Defaults => new(liveMode, answerStyle, notesTemplate, cueStyle);
}
