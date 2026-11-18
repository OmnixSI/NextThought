namespace NextThought.Core;

/// <summary>One structured input of a mode ("应聘公司", "报告题目"…).</summary>
public sealed record ModeField(string Id, string Title, string Placeholder, bool Long = false)
{
    /// <summary>The field every scene has: what the meeting is about.</summary>
    public static readonly ModeField Domain = new("domain", "领域",
        "例如：无线通信物理层、机器学习。专业词会按这个领域来认和写");
}

/// <summary>
/// What each mode asks for: role, review focus, template fields, material categories and the
/// default answer preferences (port of ModeProfile).
/// </summary>
public sealed record ModeProfile(MeetingMode Mode, string Role, string ReviewFocus)
{
    public IReadOnlyList<ModeField> Fields { get; init; } = [];
    public IReadOnlyList<string> DocumentCategories { get; init; } = [];
    public string TemplatePreferences { get; init; } = "";

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
            "按问题逐条评价我的回答：答到了什么、遗漏了什么、更好的说法是什么；标出面试官追问的薄弱点和下一轮需要准备的内容。")
        {
            Fields =
            [
                ModeField.Domain,
                new("company", "应聘公司", "公司名称、业务、近期新闻、团队情况"),
                new("position", "岗位", "岗位名称、级别、JD 要点"),
                new("interviewer", "面试官 / HR", "面试官姓名、背景、风格；HR 关注点"),
                new("highlights", "我的亮点", "最想让对方记住的 3 个经历或数字", Long: true),
                new("salary", "期望与底线", "薪资期望、到岗时间、不能接受的条件")
            ],
            DocumentCategories = ["简历", "岗位 JD", "公司信息", "HR / 面试官", "八股 / 面经", "项目资料", "其他"],
            TemplatePreferences = "我是候选人。用第一人称、口语化回答，先给一句结论，再给 2–3 个要点；行为题用 STAR 结构，只使用我资料中的真实经历，没有的用【待补充】标出；技术题给关键原理与取舍；面经里准备过的题优先按我准备的答案说。"
        },
        MeetingMode.Academic => new(mode, "报告人",
            "列出听众和主持人提出的每个问题及我的回答质量；指出讲解中不清楚、被反复追问的部分；给出论文或 PPT 需要补充的内容。")
        {
            Fields =
            [
                ModeField.Domain,
                new("title", "报告题目", "论文 / 报告标题"),
                new("venue", "会议与场次", "会议名称、分会场、报告时长、主持人"),
                new("claims", "核心论点", "要讲清楚的贡献、方法和关键结果", Long: true),
                new("limitations", "已知局限", "实验不足、假设、未来工作；被问到时怎么回应", Long: true),
                new("expected", "预计会被问的问题", "审稿意见、同行常问的问题", Long: true)
            ],
            DocumentCategories = ["论文", "PPT / 幻灯片", "讲稿", "补充材料 / 附录", "相关工作", "审稿意见", "其他"],
            TemplatePreferences = "我是报告人，正在回答听众或主持人的提问。先用一句话正面回答，再给依据（论文中的方法、实验、数据并标注页码）；遇到质疑先承认合理之处再说明设计理由；不知道的诚实说明并给出后续计划；中英文提问分别用对应语言回答。"
        },
        MeetingMode.Meeting => new(mode, "会议参与者",
            "整理决定事项、每个人的承诺与截止时间、未解决的分歧；评价我方谈判表现和下次需要准备的数据。")
        {
            Fields =
            [
                ModeField.Domain,
                new("counterpart", "对方公司 / 参会人", "公司、职位、决策人、关注点"),
                new("goal", "会议目标", "这次会议要达成的结果"),
                new("position", "我方立场与底线", "可以让步的、不能让步的", Long: true),
                new("numbers", "关键数字", "价格、预算、指标、时间节点", Long: true)
            ],
            DocumentCategories = ["公司 / 产品资料", "对方信息", "议程", "报价 / 合同", "会议纪要", "其他"],
            TemplatePreferences = "我是会议参与者。回答聚焦决策、风险和下一步行动，给出简洁、自然的参考内容；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。"
        },
        MeetingMode.Client => new(mode, "我方代表",
            "整理客户的需求和痛点、顾虑与我们的回应、我们答应的事和时间、下一步和需要内部跟进的事项。")
        {
            Fields =
            [
                ModeField.Domain,
                new("counterpart", "客户 / 对方", "公司、参会人、决策人、关注点"),
                new("goal", "沟通目标", "这次沟通要达成的结果"),
                new("offer", "产品与报价", "我们能提供什么、价格和时间", Long: true),
                new("concerns", "客户可能的顾虑", "预算、风险、竞品，以及怎么回应", Long: true)
            ],
            DocumentCategories = ["产品资料", "报价 / 合同", "客户信息", "会议纪要", "其他"],
            TemplatePreferences = "我是我方代表，正在和客户沟通。回答聚焦客户的需求和顾虑，给出简洁、自然的参考回应；涉及价格、时间和承诺时以资料为准，不确定的说“会后确认”，不替公司做资料里没有的承诺。"
        },
        MeetingMode.Lesson => new(mode, "学生",
            "整理本节课的知识点、老师强调的重点和布置的任务；列出我还没弄懂的地方。")
        {
            Fields =
            [
                ModeField.Domain,
                new("course", "课程", "课程名称、章节、老师"),
                new("goals", "学习目标", "这节课需要掌握的内容", Long: true)
            ],
            DocumentCategories = ["课件", "笔记", "作业 / 题目", "教材", "其他"],
            TemplatePreferences = "我是学生。先用一句话解释概念，再给推导或例子；公式用 LaTeX 行内格式；指出与课件对应的页码。"
        },
        MeetingMode.LabMeeting => new(mode, "组会汇报人",
            "整理讨论过的问题、讨论得不够透彻的地方、导师的意见和要求，以及论文和实验的下一步。")
        {
            Fields =
            [
                ModeField.Domain,
                new("progress", "本周进展", "做了哪些实验、读了哪些论文、结果如何", Long: true),
                new("problems", "卡住的地方", "想请导师或同学帮忙判断的问题", Long: true),
                new("next", "下一步计划", "下周打算做什么")
            ],
            DocumentCategories = ["进展 / 讲稿", "论文", "实验数据", "相关工作", "其他"],
            TemplatePreferences = "我在组会上汇报研究进展。回答导师或同学的追问时先给结论，再说依据（实验、数据、论文，标注来源）；结果不好时如实说明原因和下一步；不确定的地方说明还需要做什么实验来确认。"
        },
        MeetingMode.Discussion => new(mode, "讨论参与者", "整理议题、各方观点、结论与分歧和分工。")
        {
            Fields =
            [
                ModeField.Domain,
                new("topic", "讨论题目", "要讨论的问题或案例"),
                new("position", "我的观点", "我想表达的看法和依据", Long: true)
            ],
            DocumentCategories = ["题目材料", "参考资料", "其他"],
            TemplatePreferences = "我在参加小组讨论。给出有依据的观点，回应别人的观点时先肯定合理之处再补充；需要时帮我总结各方观点、推动形成结论。"
        },
        MeetingMode.GroupProject => new(mode, "小组成员",
            "整理每个人领到的任务和截止时间、已经定下来的方案、还没有结论的分歧，以及下次碰头前各自要完成什么。")
        {
            Fields =
            [
                ModeField.Domain,
                new("assignment", "作业要求", "课程、题目、评分标准、交付物"),
                new("deadline", "截止时间", "最终提交时间和中间节点"),
                new("members", "成员与分工", "每个人负责什么、目前做到哪里", Long: true),
                new("mine", "我负责的部分", "我的任务、进度和需要别人配合的地方", Long: true)
            ],
            DocumentCategories = ["作业要求", "评分标准", "参考资料", "已有草稿", "其他"],
            TemplatePreferences = "我是小组作业的成员。回答围绕作业要求和评分标准：先给可以直接说出口的建议，再给理由；涉及分工时说清谁做什么、什么时候交；意见不一致时帮我找到双方都能接受的方案；不确定的地方建议向老师或助教确认。"
        },
        _ => new(mode, "参与者", "总结讨论要点和对方关心的问题，给出下次可以做得更好的地方。")
        {
            Fields =
            [
                ModeField.Domain,
                new("context", "场景说明", "这是什么场合、对方是谁、我希望达成什么", Long: true)
            ],
            DocumentCategories = ["资料", "其他"],
            TemplatePreferences = "直接回答当前问题：先给一句简洁结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。"
        }
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
