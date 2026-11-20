using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>E26: one editable scene row in Settings 场景. The persisted values are the Mac keys; a
/// field is stored only when it differs from the built-in row (Mac SceneSettings.swift).</summary>
public sealed class SceneSettingRow : ObservableObject
{
    private static readonly string[] LiveKeys = ["自动回答", "只记录"];
    private static readonly string[] AnswerKeys = ["简洁", "详细"];
    private static readonly string[] CueKeys = ["只看要点", "逐句"];

    private readonly Action<SceneSettingRow> save;
    private string liveMode, answerStyle, notesTemplate, cueStyle;
    private readonly IReadOnlyList<SceneTemplateOption> templates = [];

    public SceneSettingRow(MeetingMode mode, SceneDefaults saved, IReadOnlyList<SceneTemplateOption> templates,
        Action<SceneSettingRow> save)
    {
        Mode = mode;
        this.save = save;
        this.templates = templates;
        var builtIn = SceneDefaults.For(mode);
        liveMode = Pick(saved.LiveMode, builtIn.LiveMode);
        answerStyle = Pick(saved.AnswerStyle, builtIn.AnswerStyle);
        notesTemplate = Pick(saved.NotesTemplate, builtIn.NotesTemplate);
        cueStyle = Pick(saved.CueStyle, builtIn.CueStyle);
        Tint = SceneTheme.For(mode).TintLight;
    }

    public MeetingMode Mode { get; }
    public string Label => UiText.T(LabelKey);
    public string Tint { get; }
    public IBrush TintBrush => new SolidColorBrush(Color.Parse(Tint));
    public IReadOnlyList<string> LiveOptions => LiveKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> AnswerOptions => AnswerKeys.Select(UiText.T).ToArray();
    public IReadOnlyList<string> CueOptions => CueKeys.Select(UiText.T).ToArray();

    /// <summary>纪要模板: all built-in selectable templates plus imported custom ones (Mac allNotesTemplates).</summary>
    public IReadOnlyList<string> TemplateOptions => templates.Select(template => template.Title).ToArray();

    /// <summary>Mac 恢复默认: shows only while a field of this row differs from the built-in row.</summary>
    public bool Customised => !Defaults.IsDefaultFor(Mode);

    public string LiveMode
    {
        get => UiText.T(liveMode);
        set => SetOption(ref liveMode, Resolve(value, LiveKeys), nameof(LiveMode));
    }

    public string AnswerStyle
    {
        get => UiText.T(answerStyle);
        set => SetOption(ref answerStyle, Resolve(value, AnswerKeys), nameof(AnswerStyle));
    }

    public string NotesTemplate
    {
        get => templates.FirstOrDefault(template => template.Id == notesTemplate)?.Title ?? UiText.T(notesTemplate);
        set
        {
            var id = templates.FirstOrDefault(template => template.Title == value || template.Id == value)?.Id ?? notesTemplate;
            if (id == notesTemplate) return;
            notesTemplate = id;
            Changed(nameof(NotesTemplate));
        }
    }

    public string CueStyle
    {
        get => UiText.T(cueStyle);
        set => SetOption(ref cueStyle, Resolve(value, CueKeys), nameof(CueStyle));
    }

    public SceneDefaults Defaults => new(liveMode, answerStyle, notesTemplate, cueStyle);

    /// <summary>Mac SceneSettings.reset: every field back to the built-in row.</summary>
    public void RestoreDefaults()
    {
        var builtIn = SceneDefaults.For(Mode);
        liveMode = builtIn.LiveMode;
        answerStyle = builtIn.AnswerStyle;
        notesTemplate = builtIn.NotesTemplate;
        cueStyle = builtIn.CueStyle;
        Changed(string.Empty);
    }

    private void SetOption(ref string field, string value, string property)
    {
        if (field == value) return;
        field = value;
        Changed(property);
    }

    private void Changed(string property)
    {
        if (property.Length > 0) OnPropertyChanged(property);
        OnPropertyChanged(nameof(Customised));
        save(this);
    }

    private static string Pick(string value, string fallback) => value.Length == 0 ? fallback : value;

    private static string Resolve(string value, IReadOnlyList<string> keys) =>
        keys.FirstOrDefault(key => key == value || UiText.T(key) == value) ?? value;

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
}

/// <summary>E26: one entry of the per-scene 纪要模板 picker (Mac allNotesTemplates).</summary>
public sealed record SceneTemplateOption(string Id, string Title);
