namespace NextThought.Core;

/// <summary>
/// Guesses which material slot a file belongs to (port of DocumentClassifier.swift): the file
/// name is the strongest signal, the beginning of the text the fallback. A wrong guess is
/// harmless because the user can still change the slot by hand.
/// </summary>
public static class DocumentClassifier
{
    private static readonly (string Label, string[] Terms)[] Groups =
    [
        ("简历", ["简历", "resume", "curriculum vitae", "cv_", "_cv", "个人简历"]),
        ("岗位 JD", ["岗位", "职位", "招聘", "jd", "job description", "任职要求", "岗位描述", "职位描述"]),
        ("公司信息", ["公司", "企业", "company", "about us", "组织架构", "业务介绍", "company info"]),
        ("HR / 面试官", ["hr", "面试官", "招聘官", "interviewer", "hr 面试", "面试流程"]),
        ("八股 / 面经", ["八股", "面经", "算法题", "真题", "笔试题", "interview experience", "八股文"]),
        ("项目资料", ["项目", "项目经历", "作品集", "project", "作品"]),
        ("论文", ["论文", "paper", "arxiv", "abstract", "manuscript", "预印本"]),
        ("PPT / 幻灯片", ["ppt", "pptx", "slide", "幻灯片", "演示文稿", "slides", "deck"]),
        ("讲稿", ["讲稿", "逐字稿", "演讲稿", "talk script", "speech", "presentation notes"]),
        ("补充材料 / 附录", ["附录", "补充材料", "supplement", "appendix", "附加材料"]),
        ("相关工作", ["相关工作", "related work", "参考文献", "references", "引用文献"]),
        ("审稿意见", ["审稿", "审稿意见", "评审意见", "reviewer", "peer review", "rebuttal"]),
        ("公司 / 产品资料", ["产品", "产品资料", "白皮书", "whitepaper", "product", "产品手册"]),
        ("对方信息", ["对方", "参会人", "客户", "counterpart", "甲方", "乙方"]),
        ("议程", ["议程", "agenda", "流程安排", "会议流程", "schedule"]),
        ("报价 / 合同", ["报价", "合同", "价格", "预算", "quote", "contract", "proposal", "方案报价"]),
        ("会议纪要", ["纪要", "会议记录", "minutes", "会议总结", "meeting notes"]),
        ("课件", ["课件", "讲义", "课程", "lecture", "course", "ppt 课件", "教学"]),
        ("笔记", ["笔记", "note", "notebook", "手记"]),
        ("作业 / 题目", ["作业", "习题", "题目", "homework", "exercise", "assignment"]),
        ("教材", ["教材", "课本", "textbook", "book", "教程", "reference book"])
    ];

    /// <summary>Returns the best matching category, or null when nothing matched confidently.</summary>
    public static string? Classify(string name, string content, IReadOnlyList<string> categories)
    {
        if (categories.Count == 0) return null;
        var fileName = name.ToLowerInvariant();
        var head = (content.Length > 2000 ? content[..2000] : content).ToLowerInvariant();

        string? bestLabel = null;
        var bestScore = 0;
        foreach (var (label, terms) in Groups)
        {
            var score = 0;
            foreach (var term in terms)
            {
                var needle = term.ToLowerInvariant();
                if (fileName.Contains(needle, StringComparison.Ordinal)) score += 6;
                // Only the head of the document is scanned: a keyword deep inside a long file
                // says more about the file than about its slot.
                if (head.Contains(needle, StringComparison.Ordinal)) score += 1;
            }
            if (score > bestScore) { bestScore = score; bestLabel = label; }
        }
        if (bestLabel is null || bestScore < 3) return null;

        if (categories.Contains(bestLabel)) return bestLabel;
        foreach (var category in categories)
            if (category.Contains(bestLabel, StringComparison.Ordinal) || bestLabel.Contains(category, StringComparison.Ordinal))
                return category;
        return categories.Contains("其他") ? "其他" : null;
    }
}
