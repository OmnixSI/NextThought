namespace NextThought.Core;

/// <summary>
/// Static interface strings for the English shell. Dynamic meeting content is never translated;
/// unknown strings pass through unchanged so a partial dictionary is safe.
/// </summary>
public static class UiText
{
    public static bool IsEnglish(string? language) =>
        !string.IsNullOrWhiteSpace(language) && language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["首页"] = "Home",
        ["会议"] = "Meetings",
        ["纪要"] = "Notes",
        ["历史"] = "History",
        ["系列"] = "Series",
        ["资料库"] = "Library",
        ["设置"] = "Settings",
        ["最近会议"] = "Recent meetings",
        ["数据目录"] = "Data folder",
        ["取消当前任务"] = "Cancel task",
        ["保存"] = "Save",
        ["取消"] = "Cancel",
        ["删除"] = "Delete",
        ["新建"] = "New",
        ["导入"] = "Import",
        ["导出"] = "Export",
        ["复制"] = "Copy",
        ["搜索"] = "Search",
        ["刷新"] = "Refresh",
        ["关闭"] = "Close",
        ["返回"] = "Back",
        ["上移"] = "Move up",
        ["下移"] = "Move down",
        ["添加"] = "Add",
        ["重试"] = "Retry",
        ["开始记录"] = "Start recording",
        ["停止记录"] = "Stop recording",
        ["声音来源"] = "Audio sources",
        ["本机语音识别"] = "On-device speech",
        ["术语表"] = "Glossary",
        ["全局快捷键"] = "Global shortcuts",
        ["自定义纪要模板"] = "Custom note templates",
        ["技能"] = "Skills",
        ["说话人与翻译"] = "Speakers and translation",
        ["双屏演讲"] = "Presenter mode",
        ["会议自动收尾"] = "Auto-end meetings",
        ["模型与接口"] = "Model and API",
        ["音频"] = "Audio",
        ["数据"] = "Data",
        ["关于"] = "About",
        ["系统输出设备混音"] = "System output mix",
        ["麦克风"] = "Microphone",
        ["保存术语表"] = "Save glossary",
        ["保存快捷键"] = "Save shortcuts",
        ["新建模板"] = "New template",
        ["添加章节"] = "Add section",
        ["保存结构"] = "Save structure",
        ["保存模板"] = "Save template",
        ["保存技能"] = "Save skills",
        ["刷新设备"] = "Refresh devices",
        ["刷新应用列表"] = "Refresh apps",
        ["分钟"] = "minutes",
        ["选择输出设备"] = "Select output device",
        ["选择麦克风"] = "Select microphone",
        ["整个输出设备混音"] = "Entire output device",
        ["准备语音组件"] = "Prepare speech components",
        ["准备声纹组件（约 26 MB）"] = "Prepare speaker components (~26 MB)",
        ["导入 .json…"] = "Import .json…",
        ["导入 .md…"] = "Import .md…"
    };

    public static string Translate(string text, bool english) =>
        english && English.TryGetValue(text, out var translated) ? translated : text;

    public static IReadOnlyDictionary<string, string> EnglishStrings => English;
}
