using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E26: one editable scene row in Settings ▸ 场景. The persisted values are the Mac Chinese keys;
/// the getters localize them so a language switch only has to rebuild the rows.
/// </summary>
public sealed class SceneSettingRow : ObservableObject
{
    private static readonly string[] LiveKeys = ["自动回答", "只记录"];
    private static readonly string[] AnswerKeys = ["简洁", "详细"];
    private static readonly string[] TemplateKeys = ["标准纪要", "学术汇报", "工作会议"];
    private static readonly string[] CueKeys = ["只看要点", "逐句"];

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
        Tint = SceneTheme.For(mode).TintLight;
    }

    public MeetingMode Mode { get; }
    public string Label => UiText.T(LabelKey);
    public string Tint { get; }
    public IBrush TintBrush => new SolidColorBrush(Color.Parse(Tint));
    public IReadOnlyList<string> LiveOptions => LiveKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> AnswerOptions => AnswerKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> TemplateOptions => TemplateKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> CueOptions => CueKeys.Select(UiText.T).ToArray();

    private string LabelKey => Mode switch
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
        _ => Mode.ToString()
    };

    public string LiveMode
    {
        get => UiText.T(liveMode);
        set
        {
            var key = Resolve(value, LiveKeys);
            if (SetProperty(ref liveMode, key)) save(this);
        }
    }

    public string AnswerStyle
    {
        get => UiText.T(answerStyle);
        set
        {
            var key = Resolve(value, AnswerKeys);
            if (SetProperty(ref answerStyle, key)) save(this);
        }
    }

    public string NotesTemplate
    {
        get => UiText.T(notesTemplate);
        set
        {
            var key = Resolve(value, TemplateKeys);
            if (SetProperty(ref notesTemplate, key)) save(this);
        }
    }

    public string CueStyle
    {
        get => UiText.T(cueStyle);
        set
        {
            var key = Resolve(value, CueKeys);
            if (SetProperty(ref cueStyle, key)) save(this);
        }
    }

    public SceneDefaults Defaults => new(liveMode, answerStyle, notesTemplate, cueStyle);

    private static string Resolve(string value, IReadOnlyList<string> keys) =>
        keys.FirstOrDefault(key => key == value || UiText.T(key) == value) ?? value;
}
