using System;
using System.Collections.Generic;
using System.Linq;

namespace NextThought.Core;

/// <summary>
/// G5/E25: the Mac <c>SettingsSearchItem</c> table (SettingsSearch.swift). The table is what makes
/// hidden words findable ("刘海", "钉住", "notch"…): matching looks at the item title, the keyword
/// list, the page title and their English translations. The page value is the Windows settings page
/// tag (Scene / Answer / Coach / Notes / General / Advanced).
/// </summary>
public sealed record SettingsSearchItem(string Page, string Title, string Anchor, string Words)
{
    /// <summary>Mac <c>SettingsSearchItem.matching</c>: title, words, page title and their localised forms.</summary>
    public static IReadOnlyList<SettingsSearchItem> Matching(string? query)
    {
        var term = (query ?? "").Trim();
        if (term.Length == 0) return [];
        return All.Where(item => Labels(item).Any(label => label.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    private static IEnumerable<string> Labels(SettingsSearchItem item)
    {
        yield return item.Title;
        yield return item.Words;
        yield return item.Page;
        yield return item.Anchor;
        yield return UiText.T(item.Title);
        if (PageTitles.TryGetValue(item.Page, out var pageTitle))
        {
            yield return pageTitle;
            yield return UiText.T(pageTitle);
        }
    }

    /// <summary>The Mac page titles (SettingsPage.title) keyed by the Windows page tag.</summary>
    public static readonly IReadOnlyDictionary<string, string> PageTitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Scene"] = "场景",
        ["Answer"] = "回答与资料",
        ["Coach"] = "提词与教练",
        ["Notes"] = "纪要",
        ["General"] = "通用",
        ["Advanced"] = "高级"
    };

    public static readonly IReadOnlyList<SettingsSearchItem> All =
    [
        // 场景
        new("Scene", "各场景的设置", "各场景开会时的设置", "场景 会中 自动回答 只记录 回答 简洁 详细 纪要模板 提词 要点 逐句 恢复默认 面试 组会 答辩 客户 例会 讲座 讨论 小组作业 scene defaults template cue style"),
        new("Scene", "各场景的默认模板", "各场景开会时的设置", "默认 模板 组会 课程 template defaults minutes meeting notes"),
        // 回答与资料
        new("Answer", "回答服务", "回答服务", "API Key 服务商 模型 连接 测试 Codex provider model answer 回答模型 登录"),
        new("Answer", "联网搜索", "联网搜索", "联网 总是 自动判断 关 互联网 搜索 web search"),
        new("Answer", "资料", "资料", "资料 读取 识别 OCR PDF 扫描 公式 表格 系统识别 本机增强 Codex 代读 Word Excel PPT PowerPoint Office 组件 document"),
        new("Answer", "技能", "技能", "技能库 技能 说明 SKILL instructions skills"),
        // 提词与教练
        new("Coach", "提词", "提词", "提词 提词岛 浮窗 刘海 摄像头 宽度 屏幕 字号 隐藏背景 开始一场 cue island notch teleprompter font"),
        new("Coach", "双屏演讲", "双屏演讲", "双屏 演讲 外接屏 外接显示器 幻灯片 PPT Keynote 演讲者视图 讲稿 跟随幻灯片 翻页 第几页 presenter dual screen slides speaker notes"),
        new("Coach", "提词", "提词", "一直展开 常驻 固定 不收起 钉住 pin always open 提词岛展开"),
        new("Coach", "临场教练", "临场教练", "教练 语速 说得太久 发言占比 轻提示 眼神 看镜头 摄像头 校准 灵敏度 视线 live coach pace gaze camera"),
        new("Coach", "屏幕共享", "提词", "共享 排除 录屏 可见性 测试 隐私 自动收回 capture privacy share visibility"),
        new("Coach", "字幕条", "字幕条", "字幕 位置 同声传译 恢复默认位置 subtitle captions"),
        new("Coach", "快捷键", "快捷键", "快捷键 键盘 组合键 shortcuts hotkeys keyboard"),
        // 纪要
        new("Notes", "会后整理", "会议自动收尾", "纪要 纪要语言 自动整理 会议结束后 语言 notes minutes meeting notes summary language auto summarize"),
        new("Notes", "会议自动收尾", "会议自动收尾", "自动结束 静音 没人说话 倒计时 等待时间 silence auto end"),
        new("Notes", "自定义模板", "自定义纪要模板", "模板 编辑 自定义 复制 custom template minutes"),
        // 通用
        new("General", "语言与外观", "界面语言 / Language", "语言 中文 英文 浅色 深色 跟随系统 外观 language appearance theme English"),
        new("General", "区分说话人", "区分说话人", "我的声音 声纹 谁在说 说话人 speaker voiceprint"),
        new("General", "术语和人名", "术语和人名", "术语 人名 纠错 听错 专业词 热词 vocabulary hotwords terms glossary names terminology"),
        new("General", "记录每次会议", "记录每次会议", "记录 保存 会议记录 转录 recording save sessions"),
        new("General", "备份与恢复", "备份与恢复", "导入 导出 备份 恢复 数据 立即备份 import export backup restore"),
        new("General", "权限", "权限", "权限 录屏 麦克风 摄像头 系统设置 授权 permission privacy screen recording microphone camera"),
        new("General", "更新", "更新", "版本 更新 自动检查 下载 升级 version update"),
        // 高级
        new("Advanced", "会议声音来自", "声音来源", "默认声音来源 会议声音 窗口 声音来源 同时识别我说的话 麦克风 录屏 音频 刷新 audio microphone permission"),
        new("Advanced", "语音识别引擎", "本机语音识别", "引擎 离线 云端 Apple Speech 模型 下载 识别语言 API Key speech transcription language"),
        new("Advanced", "流式模型与整句精修", "本机语音识别", "识别模型 流式 精修 整句 下载 删除全部模型 local model refine"),
        new("Advanced", "后台预加载与回声", "本机语音识别", "回声 耳机 预加载 后台预加载 扬声器 回声消除 麦克风回声消除 echo warmup"),
        new("Advanced", "临时专业词", "声音来源", "专业词 人名 热词 逗号 识别 vocabulary hotwords"),
        new("Advanced", "推理强度与预设", "回答服务（Codex）", "推理强度 思考强度 预设 服务商 reasoning effort preset"),
        new("Advanced", "二次判断与 Codex 运行方式", "回答服务（Codex）", "二次判断 自动回答 不确定 问题识别 Codex 运行方式 常驻服务 codex exec question detection automatic"),
        new("Advanced", "搜索方式与 Tavily", "回答服务", "搜索方式 Tavily Key 联网 搜索服务 web search provider"),
        new("Advanced", "屏幕与讲稿", "屏幕与讲稿", "屏幕感知 读屏幕 跟随演示文稿 讲稿 PPT 回答进度 图像输入 API 图像输入 screen awareness slides progress vision"),
        new("Advanced", "查找方式与语义增强", "资料组件", "关键词 向量 语义 检索 重建语义索引 embedding retrieval semantic"),
        new("Advanced", "组件管理", "资料组件", "下载 删除 修复 校验 OCR runtime models 复用已有组件 扫描 缓存 已下载 超时 补读 Codex 模型 reuse scan cache Word Excel LibreOffice"),
        new("Advanced", "纪要模型", "自定义纪要模板", "纪要 模型 推理强度 经验 答法候选 自动收集 notes model reasoning lessons"),
        new("Advanced", "提词浮窗与预览", "屏幕与讲稿", "预览提词岛 浮窗位置 移到 不透明度 透明度 鼠标穿透 穿透 opacity click through position"),
        new("Advanced", "手机伴侣", "手机伴侣", "手机 配对 二维码 蓝牙 中转 端口 phone QR Bluetooth relay companion"),
        new("Advanced", "更新渠道与 GitHub 访问", "更新（高级）", "更新渠道 预览版本 正式版本 令牌 token GitHub repository 仓库 channel"),
        new("Advanced", "自动备份与恢复点", "数据与关于", "自动备份 恢复点 数据目录 数据文件夹 打开数据文件夹 导入导出选项 包含密钥 recovery points"),
        new("Advanced", "维护", "设置清单", "新手引导 一键配置 一键重建环境 使用前检查 减少视觉特效 性能 流畅 省电 工作台置顶 窗口置顶 签名 绑定本机签名 onboarding setup repair performance keep on top signing")
    ];
}
