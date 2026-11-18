using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// Built-in sample content for the demo mode (ported in spirit from DemoMode.swift):
/// the whole interface can be inspected without a microphone, models or an API key.
/// Nothing here is ever written to the data directory.
/// </summary>
public static class DemoContent
{
    private static readonly DateTimeOffset Base = DateTimeOffset.Now.AddMinutes(-38);

    public static IReadOnlyList<SessionMeta> Meetings() =>
    [
        new() { Id = Guid.NewGuid(), Title = "结构组会 · 连续刚构桥监测", Mode = "labMeeting", StartedAt = Base,
                EndedAt = Base.AddMinutes(46), TranscriptCount = 43, AnswerCount = 5, SummaryState = "saved" },
        new() { Id = Guid.NewGuid(), Title = "开题答辩演练", Mode = "academic", StartedAt = Base.AddDays(-2), EndedAt = Base.AddDays(-2).AddMinutes(52),
                TranscriptCount = 61, AnswerCount = 9, SummaryState = "saved" },
        new() { Id = Guid.NewGuid(), Title = "与导师讨论风振数据", Mode = "meeting", StartedAt = Base.AddDays(-5), EndedAt = Base.AddDays(-5).AddMinutes(31),
                TranscriptCount = 28, AnswerCount = 3, SummaryState = "none" }
    ];

    public static IReadOnlyList<TranscriptLine> Lines()
    {
        var lines = new List<TranscriptLine>();
        void Add(int seconds, string speaker, string text, string? translation = null)
        {
            lines.Add(new TranscriptLine
            {
                Id = Guid.NewGuid(),
                Date = Base.AddSeconds(seconds),
                StartedAt = Base.AddSeconds(seconds),
                EndedAt = Base.AddSeconds(seconds + 4),
                Speaker = speaker,
                Text = text,
                Translation = translation is null ? null : new SessionTranslation(Guid.NewGuid(), 1, "demo", "zh-en", translation, "demo", Base.AddSeconds(seconds))
            });
        }
        Add(0, "room", "我们今天先把监测方案过一遍，重点是大跨连续刚构的跨中挠度。");
        Add(14, "room", "上次说的那个温度补偿，你后来用的什么方法？");
        Add(29, "me", "我用了小波分解，把日温差引起的低频趋势和车辆荷载引起的突变分开了。");
        Add(47, "room", "分离之后残差里还有多少是真实的结构响应？");
        Add(63, "me", "残差大概占原始信号的百分之十二，主要集中在支座附近。",
            "The residual is about twelve percent of the original signal, mainly near the bearings.");
        Add(82, "room", "这个比例和有限元算出来的对不对得上？");
        Add(95, "me", "对得上，但支座模型用了简化弹簧，刚度取值还需要做一次敏感性分析。");
        Add(112, "room", "那你这周能不能把敏感性分析跑完，下周组会讲一下？");
        Add(126, "me", "可以，我用 OpenSees 批量跑 20 组刚度，周末出图。");
        Add(141, "room", "顺便把福建沿海那八站的风速数据也接进来，看看风和温度叠加的工况。");
        return lines;
    }

    public const string Summary = """
        ## 结构组会 · 连续刚构桥监测

        **一句话结论**：温度补偿方案可用，残差约 12%，但支座刚度需要敏感性分析后才能定稿。

        ### 讨论要点
        - 监测重点是大跨连续刚构的跨中挠度。
        - 温度补偿采用小波分解，把日温差低频趋势与车辆荷载突变分离。
        - 分离后残差约占原始信号 12%，集中在支座附近。
        - 与有限元的偏差来自支座简化弹簧模型。

        ### 决定
        1. 本周完成支座刚度敏感性分析（20 组，OpenSees 批量）。
        2. 下一步接入福建沿海八站风速数据，评估风与温度叠加工况。

        ### 待办
        - [ ] 周末前跑完 20 组刚度并出图（我）
        - [ ] 组会讲一次敏感性分析结果（我）
        - [ ] 整理八站风速数据接口（待定）

        ### 未决问题
        - 支座刚度的实测值如何校准？是否需要在现场做一次静载试验？
        - 风与温度叠加时，残差阈值是否需要按场景调整？

        > 纪要由回答服务根据转写生成，原始文字未改动；要点可跳回原话核对。
        """;

    public const string Question = "温度补偿之后残差还剩多少，和有限元对得上吗？";

    public const string Answer = """
        温度补偿后残差约占原始信号 **12%**，主要集中在支座附近（原话 05:03 起）。

        与有限元的对比：整体趋势对得上，偏差主要来自支座用简化弹簧建模，刚度取值尚未标定，因此需要做一次敏感性分析才能确认——这也是组会上定下的本周任务。

        建议：先跑 20 组刚度敏感性分析，再决定是否补做现场静载试验来校准支座刚度。
        """;
}
