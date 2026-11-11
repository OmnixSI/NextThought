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

/// Destinations refer to the same section titles used as scroll anchors in SettingsSection;
/// `advanced:<id>` points at a collapsed group and opens it.
struct SettingsSearchItem: Identifiable {
    let page: SettingsPage
    let title: String
    let anchor: String
    let words: String
    var id: String { page.id + "." + title }
    init(_ page: SettingsPage, _ title: String, anchor: String? = nil, _ words: String) {
        self.page = page; self.title = title; self.anchor = anchor ?? title; self.words = words
    }
    static func matching(_ query: String) -> [Self] {
        let term = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !term.isEmpty else { return [] }
        return all.filter { item in
            let labels = [item.title, item.words, item.page.title,
                          String(localized: String.LocalizationValue(item.title)),
                          String(localized: String.LocalizationValue(item.page.title))]
            return labels.contains { $0.localizedCaseInsensitiveContains(term) }
        }
    }
    static let all: [Self] = [
        // 场景
        .init(.scenes, "各场景的设置", anchor: "各场景开会时的设置", "场景 会中 自动回答 只记录 回答 简洁 详细 纪要模板 提词 要点 逐句 恢复默认 面试 组会 答辩 客户 例会 讲座 讨论 小组作业 scene defaults template cue style"),
        .init(.scenes, "各场景的默认模板", anchor: "各场景开会时的设置", "默认 模板 组会 课程 template defaults minutes meeting notes"),
        // 声音
        .init(.sound, "会议声音与麦克风", "麦克风 录屏 音频 窗口 声音来源 刷新 audio microphone permission"),
        .init(.sound, "语音识别", "引擎 离线 云端 Apple Speech 模型 下载 识别语言 API Key speech transcription language"),
        .init(.sound, "流式模型与整句精修", anchor: "advanced:sound-recognition", "识别模型 流式 精修 整句 下载 删除 local model refine"),
        .init(.sound, "启动与回声", anchor: "advanced:sound-recognition", "回声 耳机 预加载 后台预加载 扬声器 回声消除 echo warmup"),
        .init(.sound, "区分说话人", "我的声音 声纹 谁在说 说话人 speaker voiceprint"),
        .init(.sound, "术语和人名", "术语 人名 纠错 听错 专业词 临时专业词 热词 vocabulary hotwords terms glossary names terminology"),
        // 回答
        .init(.answers, "回答服务", "API Key 服务商 模型 连接 测试 Codex provider model answer 推理强度 预设 快速模式 Fast"),
        .init(.answers, "推理强度与预设", anchor: "advanced:answers-provider", "推理强度 思考强度 预设 快速模式 Fast reasoning effort preset"),
        .init(.answers, "联网搜索", "联网 Tavily 互联网 搜索 搜索方式 web search"),
        .init(.answers, "屏幕与讲稿", "屏幕感知 读屏幕 跟随演示文稿 讲稿 PPT 回答进度 图像输入 screen awareness slides progress vision"),
        .init(.answers, "技能", "技能库 技能 说明 SKILL instructions skills"),
        .init(.answers, "问题识别与运行方式", anchor: "advanced:answers-detection", "二次判断 自动回答 不确定 问题识别 Codex 运行方式 常驻服务 question detection automatic"),
        // 资料
        .init(.materials, "读取资料的方式", "识别 OCR PDF 扫描 公式 表格 Paddle Codex 系统识别 document"),
        .init(.materials, "查找方式", "关键词 向量 语义 检索 embedding retrieval semantic"),
        .init(.materials, "Word、Excel 与 PowerPoint", "Word Excel PPT PowerPoint Office 组件"),
        .init(.materials, "组件管理", anchor: "advanced:materials-components", "下载 删除 修复 校验 OCR runtime models 复用已有组件 扫描 缓存 已下载 重建语义索引 超时 reuse scan cache"),
        // 提词与教练
        .init(.presenter, "提词", "提词 提词岛 浮窗 刘海 摄像头 宽度 预览 屏幕 字号 透明度 穿透 隐藏背景 cue island notch teleprompter opacity font"),
        .init(.presenter, "临场教练", "教练 语速 说得太久 发言占比 轻提示 眼神 看镜头 摄像头 校准 灵敏度 视线 live coach pace gaze camera"),
        .init(.presenter, "屏幕共享", "共享 排除 录屏 可见性 测试 隐私 自动收回 capture privacy share visibility"),
        .init(.presenter, "字幕条", "字幕 位置 同声传译 恢复默认位置 subtitle captions"),
        // 纪要
        .init(.notes, "纪要模型", "纪要 模型 推理强度 语言 纪要语言 notes minutes meeting notes summary language"),
        .init(.notes, "会后整理", "自动整理 会议结束后 经验 答法候选 收集 auto summarize lessons"),
        .init(.notes, "自定义模板", "模板 编辑 自定义 复制 custom template minutes"),
        .init(.notes, "会议自动收尾", "自动结束 静音 没人说话 倒计时 silence auto end"),
        // 通用
        .init(.general, "语言与外观", "语言 中文 英文 浅色 深色 跟随系统 外观 language appearance theme English"),
        .init(.general, "快捷键", "快捷键 键盘 组合键 shortcuts hotkeys keyboard"),
        .init(.general, "手机伴侣", "手机 配对 二维码 蓝牙 中转 端口 phone QR Bluetooth relay companion"),
        .init(.general, "更新", "版本 更新 渠道 自动检查 预览版本 下载 升级 version update channel download"),
        .init(.general, "GitHub 访问", anchor: "advanced:general-updates", "令牌 token GitHub repository 仓库"),
        // 数据与权限
        .init(.data, "记录每次会议", "记录 保存 会议记录 转录 recording save sessions"),
        .init(.data, "备份与恢复", "导入 导出 备份 恢复 数据 密钥 import export backup restore"),
        .init(.data, "自动备份", "自动备份 恢复点 数据目录 recovery points"),
        .init(.data, "权限", "权限 录屏 麦克风 摄像头 系统设置 授权 permission privacy screen recording microphone camera"),
        .init(.data, "维护", "新手引导 一键配置 一键重建环境 使用前检查 减少视觉特效 性能 流畅 省电 工作台置顶 窗口置顶 签名 绑定本机签名 onboarding setup repair performance keep on top signing"),
    ]
}
