import SwiftUI

private struct SettingsSearchHighlightKey: EnvironmentKey { static let defaultValue: String? = nil }
private struct SettingsSearchDestinationKey: EnvironmentKey { static let defaultValue: String? = nil }
extension EnvironmentValues {
    var settingsSearchHighlight: String? {
        get { self[SettingsSearchHighlightKey.self] }
        set { self[SettingsSearchHighlightKey.self] = newValue }
    }
    var settingsSearchDestination: String? {
        get { self[SettingsSearchDestinationKey.self] }
        set { self[SettingsSearchDestinationKey.self] = newValue }
    }
}

/// Destinations refer to the same section titles used as scroll anchors in SettingsSection.
struct SettingsSearchItem: Identifiable {
    let group: SettingsGroup
    let title: String
    let anchor: String
    let words: String
    var id: String { group.id + "." + title }
    init(_ group: SettingsGroup, _ title: String, anchor: String? = nil, _ words: String) {
        self.group = group; self.title = title; self.anchor = anchor ?? title; self.words = words
    }
    static func matching(_ query: String) -> [Self] {
        let term = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !term.isEmpty else { return [] }
        return all.filter { item in
            let category = SettingsCategory.containing(item.group).title
            let labels = [item.title, item.words, item.group.displayTitle, category,
                          String(localized: String.LocalizationValue(category)),
                          String(localized: String.LocalizationValue(item.title)),
                          String(localized: String.LocalizationValue(item.group.displayTitle))]
            return labels.contains { $0.localizedCaseInsensitiveContains(term) }
        }
    }
    static let all: [Self] = [
        .init(.essentials, "语言与外观", anchor: "界面与外观", "语言 中文 英文 浅色 深色 跟随系统 language appearance theme English"),
        .init(.essentials, "工作台功能", "简洁 完整 功能显示 simplified workspace"),
        .init(.privacy, "窗口", "置顶 keep on top window"),
        .init(.privacy, "环境与模型", "一键配置 修复 重建 setup repair"),
        .init(.privacy, "使用前检查", "准备 引导 状态 readiness onboarding"),
        .init(.recognition, "会议声音与麦克风", "麦克风 录屏 音频 窗口 声音来源 audio microphone permission"),
        .init(.recognition, "语音识别", "离线 云端 Apple Speech 模型 下载 speech transcription language"),
        .init(.recognition, "会议自动收尾", "自动结束 静音 倒计时 silence auto end"),
        .init(.recognition, "区分说话人", "我的声音 声纹 谁在说 说话人 speaker voiceprint"),
        .init(.recognition, "专业词", "专业词 人名 热词 vocabulary hotwords"),
        .init(.recognition, "本地模型", "识别模型 精修 下载 删除 local model refine"),
        .init(.recognition, "启动与回声", "回声 耳机 预加载 echo warmup"),
        .init(.answers, "回答引擎", "API Key 服务商 模型 连接 测试 Codex provider model answer"),
        .init(.answers, "问题识别", "自动回答 不确定 question detection automatic"),
        .init(.answers, "联网搜索", "联网 Tavily 互联网 搜索 web search"),
        .init(.notes, "纪要模型", "纪要 自动整理 语言 模型 notes minutes meeting notes summary language"),
        .init(.notes, "各类型的默认模板", "默认 模板 组会 课程 template defaults minutes meeting notes"),
        .init(.notes, "自定义模板", "模板 编辑 自定义 custom template minutes meeting notes"),
        .init(.documents, "读取资料", anchor: "现在用什么读取资料", "识别 OCR PDF 扫描 公式 表格 Paddle Codex document"),
        .init(.documents, "复用已有组件", anchor: "已经在这台 Mac 下载过？", "扫描 本机 缓存 已下载 reuse scan cache"),
        .init(.documents, "资料检索", anchor: "怎样从资料中找答案", "关键词 向量 语义 检索 embedding retrieval semantic"),
        .init(.documents, "Office 支持", anchor: "Word、Excel 与 PowerPoint", "Word Excel PPT PowerPoint Office 组件"),
        .init(.documents, "模型与组件管理", anchor: "组件管理与高级设置", "下载 删除 修复 校验 OCR runtime models"),
        .init(.documents, "术语和人名", "术语 人名 纠错 听错 terms glossary names terminology"),
        .init(.overlay, "回答浮窗", "浮窗 字号 显示 共享 屏幕 overlay font sharing"),
        .init(.overlay, "字幕条", "字幕 位置 同声传译 subtitle captions"),
        .init(.overlay, "位置与外观", "透明度 穿透 隐藏背景 恢复位置 opacity placement"),
        .init(.skills, "技能库", "技能 说明 SKILL instructions"),
        .init(.shortcuts, "全局快捷键", "快捷键 键盘 组合键 shortcuts hotkeys keyboard"),
        .init(.companion, "手机伴侣", "手机 配对 二维码 蓝牙 中转 phone QR Bluetooth relay"),
        .init(.recording, "会话记录", "记录 保存 自动 经验 recording save"),
        .init(.recording, "备份与恢复", "导入 导出 备份 恢复 数据 import export backup restore"),
        .init(.recording, "自动备份", "自动备份 恢复点 数据目录 recovery points"),
        .init(.privacy, "权限与签名", "权限 录屏 麦克风 签名 permission privacy signing"),
        .init(.privacy, "性能", "省电 透明 效果 流畅 performance transparency"),
        .init(.more, "自动更新", "版本 更新 下载 升级 version update download"),
        .init(.more, "GitHub 访问", "令牌 token GitHub repository")
    ]
}
