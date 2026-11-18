namespace NextThought.Core;

/// <summary>
/// The role and review focus each meeting mode uses in prompts (port of the ModeProfile fields
/// this port needs; template fields and answer preferences stay app-side yet).
/// </summary>
public sealed record ModeProfile(MeetingMode Mode, string Role, string ReviewFocus)
{
    public static string Title(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => "面试",
        MeetingMode.Academic => "学术报告",
        MeetingMode.Meeting => "会议",
        MeetingMode.Client => "客户沟通",
        MeetingMode.Lesson => "课程",
        MeetingMode.LabMeeting => "组会",
        MeetingMode.Discussion => "讨论",
        MeetingMode.GroupProject => "小组作业",
        MeetingMode.Custom => "自定义场景",
        _ => "通用场景"
    };

    public static ModeProfile For(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => new(mode, "候选人",
            "按问题逐条评价我的回答：答到了什么、遗漏了什么、更好的说法是什么；标出面试官追问的薄弱点和下一轮需要准备的内容。"),
        MeetingMode.Academic => new(mode, "报告人",
            "列出听众和主持人提出的每个问题及我的回答质量；指出讲解中不清楚、被反复追问的部分；给出论文或 PPT 需要补充的内容。"),
        MeetingMode.Meeting => new(mode, "会议参与者",
            "整理决定事项、每个人的承诺与截止时间、未解决的分歧；评价我方谈判表现和下次需要准备的数据。"),
        MeetingMode.Client => new(mode, "我方代表",
            "整理客户的需求和痛点、顾虑与我们的回应、我们答应的事和时间、下一步和需要内部跟进的事项。"),
        MeetingMode.Lesson => new(mode, "学生",
            "整理本节课的知识点、老师强调的重点和布置的任务；列出我还没弄懂的地方。"),
        MeetingMode.LabMeeting => new(mode, "组会汇报人",
            "整理讨论过的问题、讨论得不够透彻的地方、导师的意见和要求，以及论文和实验的下一步。"),
        MeetingMode.Discussion => new(mode, "讨论参与者", "整理议题、各方观点、结论与分歧和分工。"),
        MeetingMode.GroupProject => new(mode, "小组成员",
            "整理每个人领到的任务和截止时间、已经定下来的方案、还没有结论的分歧，以及下次碰头前各自要完成什么。"),
        _ => new(mode, "参与者", "总结讨论要点和对方关心的问题，给出下次可以做得更好的地方。")
    };
}

/// <summary>
/// The prompt of "整理讨论重点" before a meeting: the role and the two sections follow the scene
/// (port of PreparationPrompt). The result is shown as Markdown; nothing parses the headings.
/// </summary>
public static class PreparationPrompt
{
    public sealed record Sections(string First, string FirstHint, string Second, string SecondHint);

    public static Sections For(MeetingMode mode) => mode switch
    {
        MeetingMode.Interview => new("资料里的亮点",
            "3 条最值得在面试里讲的经历、数字或亮点，引用资料名称和页码（若有）。",
            "可能被问到的问题",
            "5 个面试官可能追问的问题，每个附一两句基于资料的回答方向；资料里没有的经历标出【待补充】。"),
        MeetingMode.Client => new("客户关心什么",
            "3 条客户最可能关心的需求、顾虑或风险，引用资料名称和页码（若有）。",
            "可能被问到的问题",
            "5 个客户可能问到的问题，每个附一两句有依据的回应方向；价格、时间和承诺以资料为准。"),
        MeetingMode.Lesson => new("这节课的要点",
            "3 到 5 个要点和关键概念，引用资料名称和页码（若有）。",
            "我可能不懂的地方",
            "5 个读资料时容易卡住的地方，每个附一两句帮助理解的提示。"),
        MeetingMode.Academic => new("要讲清楚的重点",
            "3 条核心论点和对应的依据，引用资料名称和页码（若有）。",
            "可能被问到的问题",
            "5 个听众或评委可能提出的问题，每个附一两句有依据的回答方向，资料不支持的地方直接指出。"),
        MeetingMode.LabMeeting => new("进展和证据",
            "3 条最值得讨论的进展、证据或不足，引用资料名称和页码（若有）。",
            "可能被追问的问题",
            "5 个导师或同学可能追问的问题，每个附一两句有依据的分析方向。"),
        MeetingMode.Meeting => new("要定下来的事",
            "3 条这次会议要做出的决定和关键数字，引用资料名称和页码（若有）。",
            "可能被问到的问题",
            "5 个对方可能问到的问题，每个附一两句有依据的回应方向。"),
        MeetingMode.GroupProject => new("要求和分工",
            "3 条作业要求、评分标准或分工里最该先落实的事，引用资料名称和页码（若有）。",
            "需要先问清楚的问题",
            "5 个需要向组员或老师确认的问题，每个说明为什么要问。"),
        MeetingMode.Discussion => new("可以讲的观点",
            "3 个有资料依据的观点，引用资料名称和页码（若有）。",
            "可能被反驳的地方",
            "5 个别人可能反驳或追问的地方，每个附一两句回应方向。"),
        _ => new("资料重点",
            "3 条资料里最重要的内容，引用资料名称和页码（若有）。",
            "可能被问到的问题",
            "5 个可能被问到的问题，每个附一两句有依据的回答方向。")
    };

    public static string System(MeetingMode mode)
    {
        var profile = ModeProfile.For(mode);
        return $"你是严谨的会议准备助手，帮我准备这场{ModeProfile.Title(mode)}，我的身份是{profile.Role}。"
               + "仅使用本次选择的资料；不补造资料里没有的事实、数字、引文或他人的意见。证据不足请明确说明。"
               + $"准备时关注：{profile.ReviewFocus}";
    }

    public static string Output(MeetingMode mode)
    {
        var sections = For(mode);
        return $"严格按两节输出：\n## {sections.First}\n{sections.FirstHint}\n## {sections.Second}\n{sections.SecondHint}资料仅展示有限摘录，不声称已完整读懂。";
    }
}
