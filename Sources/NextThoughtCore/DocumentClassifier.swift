import Foundation

/// Guesses which material slot a file belongs to, so the user can drop a whole
/// folder into a scenario instead of filing every file by hand.
///
/// The signal is the file name first (people name files after their content) and
/// the beginning of the text second. Scoring is deliberately simple and offline:
/// a wrong guess is harmless because the user can still change the slot by hand.
public enum DocumentClassifier {
    /// Keyword groups per material slot. The label is matched against the
    /// categories of the current mode, so the same table serves every scenario.
    private struct Group {
        let label: String
        let terms: [String]
    }

    private static let groups: [Group] = [
        Group(label: "简历", terms: ["简历", "resume", "curriculum vitae", "cv_", "_cv", "个人简历"]),
        Group(label: "岗位 JD", terms: ["岗位", "职位", "招聘", "jd", "job description", "任职要求", "岗位描述", "职位描述"]),
        Group(label: "公司信息", terms: ["公司", "企业", "company", "about us", "组织架构", "业务介绍", "company info"]),
        Group(label: "HR / 面试官", terms: ["hr", "面试官", "招聘官", "interviewer", "hr 面试", "面试流程"]),
        Group(label: "八股 / 面经", terms: ["八股", "面经", "算法题", "真题", "笔试题", "interview experience", "八股文"]),
        Group(label: "项目资料", terms: ["项目", "项目经历", "作品集", "project", "作品"]),
        Group(label: "论文", terms: ["论文", "paper", "arxiv", "abstract", "manuscript", "预印本"]),
        Group(label: "PPT / 幻灯片", terms: ["ppt", "pptx", "slide", "幻灯片", "演示文稿", "slides", "deck"]),
        Group(label: "讲稿", terms: ["讲稿", "逐字稿", "演讲稿", "talk script", "speech", "presentation notes"]),
        Group(label: "补充材料 / 附录", terms: ["附录", "补充材料", "supplement", "appendix", "附加材料"]),
        Group(label: "相关工作", terms: ["相关工作", "related work", "参考文献", "references", "引用文献"]),
        Group(label: "审稿意见", terms: ["审稿", "审稿意见", "评审意见", "reviewer", "peer review", "rebuttal"]),
        Group(label: "公司 / 产品资料", terms: ["产品", "产品资料", "白皮书", "whitepaper", "product", "产品手册"]),
        Group(label: "对方信息", terms: ["对方", "参会人", "客户", "counterpart", "甲方", "乙方"]),
        Group(label: "议程", terms: ["议程", "agenda", "流程安排", "会议流程", "schedule"]),
        Group(label: "报价 / 合同", terms: ["报价", "合同", "价格", "预算", "quote", "contract", "proposal", "方案报价"]),
        Group(label: "会议纪要", terms: ["纪要", "会议记录", "minutes", "会议总结", "meeting notes"]),
        Group(label: "课件", terms: ["课件", "讲义", "课程", "lecture", "course", "ppt 课件", "教学"]),
        Group(label: "笔记", terms: ["笔记", "note", "notebook", "手记"]),
        Group(label: "作业 / 题目", terms: ["作业", "习题", "题目", "homework", "exercise", "assignment"]),
        Group(label: "教材", terms: ["教材", "课本", "textbook", "book", "教程", "reference book"])
    ]

    /// Returns the best matching category of `categories`, or nil when nothing
    /// matched with enough confidence.
    ///
    /// - Parameters:
    ///   - name: File name, the strongest signal.
    ///   - content: The beginning of the converted Markdown, used as a fallback.
    ///   - categories: The slots of the current mode, e.g. ["简历", "岗位 JD", …].
    public static func classify(name: String, content: String, categories: [String]) -> String? {
        guard !categories.isEmpty else { return nil }
        let fileName = name.lowercased()
        let head = String(content.prefix(2_000)).lowercased()

        var scores: [(group: Group, score: Int)] = []
        for group in groups {
            var score = 0
            for term in group.terms {
                let needle = term.lowercased()
                if fileName.contains(needle) { score += 6 }
                // Only the head of the document is scanned: a keyword deep inside
                // a long file says more about the file than about its slot.
                if head.contains(needle) { score += 1 }
            }
            if score > 0 { scores.append((group, score)) }
        }
        guard let best = scores.max(by: { $0.score < $1.score }), best.score >= 3 else { return nil }

        // Map the winning group onto a slot of this mode.
        if categories.contains(best.group.label) { return best.group.label }
        for category in categories {
            if category.contains(best.group.label) || best.group.label.contains(category) { return category }
        }
        return categories.contains("其他") ? "其他" : nil
    }
}
