#!/usr/bin/env python3
"""G5: recompute the 113 Mac preference keys, their types/defaults/ranges and the Windows
status table from the Swift sources.

The Swift sources are the only standard.  The manifest below records, per key, what the
Swift source says (type, default, view) and what Windows does with the key.  The script

* extracts the key set from the Swift sources (UserDefaults/AppStorage/SceneSettings key
  plus the Core backup exportedKeys list) and fails when it is not exactly the manifest;
* checks that every ``swift`` evidence string still appears verbatim in Sources/;
* writes ``Windows/src/NextThought.Core/MacPreferences.g.cs`` (``--write``);
* writes ``Windows/SETTINGS_PARITY.md`` (``--write``);

Usage (from the repository root):

    python Windows/scripts/settings_parity.py            # verify only
    python Windows/scripts/settings_parity.py --write    # verify + regenerate code and table
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCES_DIR = ROOT / "Sources"
SWIFT = "\n".join(p.read_text(encoding="utf-8", errors="ignore") for p in SOURCES_DIR.rglob("*.swift"))
BACKUP = (ROOT / "Sources/NextThoughtCore/Backup.swift").read_text(encoding="utf-8")

EXPECTED_KEYS = 114

KIND_CS = {
    "bool": "MacPreferenceKind.Bool",
    "string": "MacPreferenceKind.String",
    "int": "MacPreferenceKind.Integer",
    "real": "MacPreferenceKind.Real",
    "array": "MacPreferenceKind.Array",
    "dict": "MacPreferenceKind.Dict",
}
KIND_LABEL = {
    "bool": "Bool",
    "string": "String",
    "int": "Integer",
    "real": "Real",
    "array": "Array",
    "dict": "Dict",
}


class P:
    """One Mac preference key as the Swift source defines it."""

    def __init__(self, key, kind, default, default_text, view, swift,
                 allowed=None, minimum=None, maximum=None, status="done",
                 evidence="", note=""):
        self.key = key
        self.kind = kind
        self.default = default
        self.default_text = default_text
        self.view = view
        self.swift = swift
        self.allowed = allowed
        self.minimum = minimum
        self.maximum = maximum
        self.status = status  # "done" -> 已实现、未验证, "skip" -> 本版不做
        self.evidence = evidence
        self.note = note


def done(key, kind, default, default_text, view, swift, evidence,
         allowed=None, minimum=None, maximum=None, note=""):
    return P(key, kind, default, default_text, view, swift, allowed, minimum, maximum,
             "done", evidence, note)


def skip(key, kind, default, default_text, view, swift, reason, allowed=None,
         minimum=None, maximum=None):
    return P(key, kind, default, default_text, view, swift, allowed, minimum, maximum,
             "skip", reason)


def todo(key, kind, default, default_text, view, swift, reason, allowed=None,
         minimum=None, maximum=None):
    """A Mac key whose Windows behaviour is not ported yet (status 未做)."""
    return P(key, kind, default, default_text, view, swift, allowed, minimum, maximum,
             "todo", reason)


SETTINGS_PAGE = "设置"
SCENES = "设置 → 场景（SettingsPages.swift SceneSettingsPage）"
ANSWERS = "设置 → 回答与资料（SettingsPages.swift AnswerSettings）"
PRESENTER = "设置 → 提词与教练（SettingsPresenter.swift PresenterSettings）"
NOTES = "设置 → 纪要（NotesSettingsView.swift）"
GENERAL = "设置 → 通用（SettingsGeneralData.swift GeneralSettings）"
ADVANCED = "设置 → 高级（SettingsAdvanced.swift AdvancedSettings）"

PREFERENCES = [
    # --- 0. system / legacy ------------------------------------------------
    skip("AppleLanguages", "array", None, "nil（删除即跟随系统）", "设置 → 通用 → 语言（AppLanguage.swift LanguageSettingsSection）",
         'removeObject(forKey: "AppleLanguages")',
         "Mac 用系统级 AppleLanguages 覆盖界面语言并重启生效；Windows 语言设置按 G8 用独立键并即时生效，机制不同（平台差异，见 FIX_TASKS G8）"),
    done("activeAPIPresetID", "string", None, "nil（未设置）", "设置 → 回答与资料 → 回答服务·预设（AnswerServiceViews.swift APIPresetManager）",
         'string(forKey: "activeAPIPresetID")',
         "MacPreferenceMap↔AppSettings.ActiveApiPresetId（E27：APIPresetLogic.Select/Add/Delete 维护，选中的预设地址/模型写回 apiBaseURL/apiModel）；SettingsStore 读写",
         note=""),
    done("activeWorkspaceID", "string", None, "nil（取第一个系列）", "资料夹列表·当前系列（WorkspacesView.swift WorkspaceList）",
         'preferences.string(forKey: "activeWorkspaceID").flatMap',
         "MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 资料夹当前系列未持久化（AppState 只在内存里选），无读写行为"),
    skip("advancedSettingsEnabled", "bool", False, "false", "内部派生（AppState.swift simplifiedInterface 取反；无设置控件）",
         'object(forKey: "advancedSettingsEnabled") as? Bool ?? false',
         "Mac 内部派生键：由 simplifiedInterface 取反写回，没有设置控件，读它只是初始化同一值；Windows 没有简化界面模式，本版不做"),
    done("answerBackend", "string", "openAICompatible", '"openAICompatible"（未装 Codex 时；装了为 "codexAppServer"）',
         "设置 → 回答与资料 → 回答服务（SettingsPages.swift AnswerServiceChooser；AnswerServiceViews.swift CodexEngineSection）",
         'AnswerBackendDefault.rawValue(saved:',
         "MacPreferenceMap↔AppSettings.UseCodex（openAICompatible=false，codexAppServer/codexExec=true）；SettingsStore 读写；行为：AppState.Live 按 UseCodex 选择回答服务",
         allowed=["codexAppServer", "codexExec", "openAICompatible"], note="缺口：Windows 只区分 API / Codex，不区分 codexAppServer 与 codexExec"),
    done("answerGloss", "bool", True, "true", "快速设置 / 会中顶栏（QuickSettings.swift、LiveSessionView.swift）",
         'object(forKey: "answerGloss") as? Bool ?? true',
         "AppSettings.AnswerGloss（默认已改为 true）；SettingsStore 读写；行为：AppState.StructuredNotes/AnswerPromptBuilder 生成中文对照",
         note=""),  # filled below
    done("answerLanguage", "string", "auto", '"auto"', "快速设置 / 会中顶栏（QuickSettings.swift、LiveSessionView.swift）",
         'string(forKey: "answerLanguage") ?? "auto"',
         "AppSettings.AnswerLanguage；SettingsStore 读写；行为：AnswerPromptBuilder/AnswerLanguage 生成回答语言字段",
         allowed=["auto", "zh", "en"]),
    skip("answerPreferences", "string", "", '""', "旧版单场景背景（WorkspaceStore.swift migrateLegacyContext；现为系列文件）",
         'string(forKey: "answerPreferences") ?? ""',
         "Mac 旧版单一场景迁移键；Windows 按系列文件直接读写（B1/A7），不做旧 UserDefaults 场景迁移，本版不做"),
    skip("answerStyle", "string", "concise", '"concise"', "设置 → 场景「回答」（SettingsPages.swift SceneRow）；旧键见 WorkspaceStore.swift",
         'string(forKey: "answerStyle") ?? ""',
         "Mac 旧版单场景键；Windows 的回答长度按系列文件与场景设置（AppSettings.SceneDefaults、SceneSettingRow）存储，本版不做旧键"),
    done("apiBaseURL", "string", "https://api.openai.com/v1", '"https://api.openai.com/v1"',
         "设置 → 回答与资料 → 回答服务（AnswerServiceViews.swift APIConnectView）",
         'string(forKey: "apiBaseURL") ?? "https://api.openai.com/v1"',
         "MacPreferenceMap↔AppSettings.BaseUrl；SettingsStore 读写；行为：ChatCompletionClient/AnswerStreaming 使用"),
    done("apiModel", "string", "", '""', "设置 → 回答与资料 → 回答服务（AnswerServiceViews.swift APIConnectView）",
         'string(forKey: "apiModel") ?? ""',
         "MacPreferenceMap↔AppSettings.Model；SettingsStore 读写；行为：AppState.Live → ChatCompletionClient"),
    done("apiPresets", "array", [], "[]", "设置 → 高级 → 回答服务·预设（AnswerServiceViews.swift APIPresetManager）",
         'data(forKey: "apiPresets")',
         "MacPreferenceMap↔AppSettings.ApiPresets（E27：APIPreset 数组，Mac 字段名 id/name/baseURL/model/effort/keyAccount；APIPresetLogic 增删改选 + 首次运行迁移）；SettingsStore 读写",
         note="数据形状与 Mac 相同（数组）；Mac 备份里是 Data，Windows plist 导入/导出按数组处理（已有数据格式差异记录）"),
    done("apiVisionEnabled", "bool", False, "false", "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift）",
         'bool(forKey: "apiVisionEnabled")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 回答请求尚未按该开关附加截图（E4/E13 截图提问独立），无行为消费者"),
    done("appearance", "string", "system", '"system"', "设置 → 通用 → 语言与外观（SettingsGeneralData.swift GeneralSettings）",
         'string(forKey: "appearance")',
         "AppSettings.Appearance；SettingsStore 读写；行为：AppState 应用浅色/深色/跟随系统",
         allowed=["system", "light", "dark"]),
    done("assistantLayout", "dict", {"slots": [{"module": "conversation", "column": 0, "weight": 1.0},
                                             {"module": "history", "column": 0, "weight": 0.45},
                                             {"module": "question", "column": 1, "weight": 1.0},
                                             {"module": "answer", "column": 1, "weight": 1.0}],
                                     "detached": [], "split": 0.44},
         "AssistantLayout.standard（两栏；split 0.44）", "助手页模块布局（AssistantLayout.swift；AssistantView.swift 拖拽）",
         'data(forKey: "assistantLayout")',
         "MacPreferences+SettingsStore（Mac 键名/默认 AssistantLayout.standard，JSON 原样保存）；缺口：Windows 助手页无模块化布局（只有 ContextSidebarHidden），无行为消费者"),
    done("autoApplyLessons", "bool", True, "true", "旧键→autoCollectKnowledgeCandidates（Backup.swift SettingsSnapshot.valuesForRestore）",
         'removeObject(forKey: "autoApplyLessons")',
         "SettingsSnapshot.ValuesForRestore（Backup.cs 与 Mac 相同的重命名迁移：autoApplyLessons → autoCollectKnowledgeCandidates）；默认 true 与 Mac 一致"),
    done("autoBackup", "bool", True, "true", "设置 → 高级 → 数据与维护（SettingsGeneralData.swift DataMaintenanceAdvanced）",
         'object(forKey: "autoBackup") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 尚未按该开关在会后自动创建恢复点（BackupService 只有手动/导入导出路径）"),
    done("autoCollectKnowledgeCandidates", "bool", True, "true", "设置 → 高级 → 纪要（NotesSettingsView.swift NotesAdvanced）",
         'object(forKey: "autoCollectKnowledgeCandidates") as? Bool',
         "MacPreferences+SettingsStore（Mac 键名/默认 true；含 autoApplyLessons 迁移）；缺口：Windows 纪要整理尚未按该开关收集经验/答法候选"),
    done("autoSummarize", "bool", True, "true", "设置 → 纪要 → 会后整理（NotesSettingsView.swift）",
         'object(forKey: "autoSummarize") as? Bool ?? true',
         "MacPreferenceMap↔AppSettings.AutoSummarize；SettingsStore 读写；行为：AppState.StructuredNotes 会议结束后自动整理"),
    done("automaticAnswers", "bool", True, "true", "会中顶栏 / 场景「会中」（AppState.swift；无设置页控件）",
         'var automaticAnswers = true',
         "AppSettings.AutomaticAnswers（默认改为 true）；SettingsStore 读写；行为：AppState.Live 自动回答开关（每场会开始时按场景覆盖）"),
    done("automaticUpdateChecks", "bool", True, "true", "设置 → 通用 → 更新（SettingsView.swift UpdateSettings）",
         'object(forKey: "automaticUpdateChecks") as? Bool ?? true',
         "MacPreferenceMap↔AppSettings.AutoCheckUpdates；SettingsStore 读写；行为：AppState.Update 启动后/每 6 小时检查"),
    done("capturePrivacy", "bool", True, "true", "设置 → 提词与教练 → 屏幕共享（SettingsPresenter.swift）",
         'object(forKey: "capturePrivacy") as? Bool ?? true',
         "AppSettings.CapturePrivacy；SettingsStore 读写；行为：WindowCaptureHiding 每秒对全部窗口施加/撤销 WDA_EXCLUDEFROMCAPTURE"),
    done("cloudConsent", "bool", False, "false", "设置 → 高级 → 声音与识别（SettingsView.swift CloudSettings）",
         'bool(forKey: "cloudConsent")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 云端识别界面没有单独的同意开关（设置页以说明文字提示），无行为消费者"),
    done("cloudModel", "string", "", '""', "设置 → 高级 → 声音与识别（SettingsView.swift CloudSettings）",
         'string(forKey: "cloudModel") ?? ""',
         "MacPreferenceMap↔AppSettings.CloudSttModel；SettingsStore 读写；行为：CloudSttSession 使用云端模型名"),
    done("cloudProvider", "string", "soniox", '"soniox"', "设置 → 高级 → 声音与识别（SettingsView.swift CloudSettings）",
         'string(forKey: "cloudProvider") ?? ""',
         "MacPreferenceMap↔AppSettings.CloudStt（soniox/openai/deepgram，与 transcriptionBackend=cloud 配合）；SettingsStore 读写；行为：CloudSttSession",
         allowed=["soniox", "openai", "deepgram"]),
    done("coachEnabled", "bool", True, "true", "设置 → 提词与教练 → 临场教练（SettingsPresenter.swift）",
         'object(forKey: "coachEnabled") as? Bool ?? true',
         "已实现、未验证｜AppSettings.CoachEnabled←Mac 键名/默认 true（SettingsStore）；设置 → 提词与教练 → 临场教练卡首个开关，切换即写 Mac key；行为：关闭时停摄像头并清 CoachHint，岛提示/每秒 tick/逐行 ingest/纪要教练摘要全部短路（Mac LiveCoach.isEnabled 语义）；测试：CoachGazeSettingsTests、SettingsStructureTests"),
    done("coachGazeSensitivity", "string", "standard", '"standard"', "设置 → 提词与教练 → 临场教练（SettingsPresenter.swift）",
         'string(forKey: "coachGazeSensitivity") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"standard\"，允许值 relaxed/standard/strict 已写入注册表）；缺口：E10 未做，等待模型方案答复",
         allowed=["relaxed", "standard", "strict"]),
    done("coachWatchesGaze", "bool", False, "false", "设置 → 提词与教练 → 临场教练（SettingsPresenter.swift）",
         'object(forKey: "coachWatchesGaze") as? Bool ?? false',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：E10 未做，等待模型方案答复"),
    done("codexFastMode", "bool", None, "nil（跟随模型默认）", "设置 → 回答与资料 → Codex 快速模式（AnswerServiceViews.swift CodexFastModeToggle）",
         'object(forKey: "codexFastMode") as? Bool',
         "MacPreferenceMap↔AppSettings.CodexFastMode（E27：设置模型与 Codex 状态/快速模式标签已接入）；缺口：Windows CodexAnswerClient 尚未实现服务档位参数（Mac activeServiceTier），行为待 Codex app-server 协议核对"),
    done("codexModelID", "string", "", '""', "设置 → 回答与资料 → Codex（AnswerServiceViews.swift CodexEngineSection）",
         'string(forKey: "codexModelID") ?? ""',
         "MacPreferenceMap↔AppSettings.CodexModel；SettingsStore 读写；行为：CodexAnswerClient"),
    done("codexOCREffort", "string", "medium", '"medium"', "设置 → 高级 → 资料组件（DocumentModelSettings.swift DocumentComponentsAdvanced）",
         'string(forKey: "codexOCREffort") ?? "medium"',
         "MacPreferenceMap↔AppSettings.CodexOcrEffort（E23/E23b）；settings.json 用 Mac 键名；设置页 高级→资料组件 在读取方式=Codex 时提供选择；行为：CodexDocumentReader 每次请求传 effort；缺口：未对真实 Codex 账号跑大文档",
         allowed=["low", "medium", "high", "xhigh"]),
    done("codexOCRModel", "string", "gpt-6-luna", '"gpt-6-luna"', "设置 → 高级 → 资料组件（DocumentModelSettings.swift）",
         'string(forKey: "codexOCRModel") ?? "gpt-6-luna"',
         "MacPreferenceMap↔AppSettings.CodexOcrModel（E23/E23b）；settings.json 用 Mac 键名；设置页 高级→资料组件 Codex 模型输入；行为：CodexDocumentReader 用该模型发起代读；缺口：未对真实 Codex 账号验证"),
    done("codexOCRTimeout", "real", 600.0, "600", "设置 → 高级 → 资料组件（DocumentModelSettings.swift）",
         'object(forKey: "codexOCRTimeout") as? Double ?? 600',
         "MacPreferenceMap↔AppSettings.CodexOcrTimeout（E23/E23b，30–3600 归一化）；settings.json 用 Mac 键名；设置页 高级→资料组件 每文件超时输入；行为：CodexDocumentReader 每单元请求的超时；缺口：未实跑超时边界",
         minimum=30.0, maximum=3600.0),
    done("codexReasoning", "string", "low", '"low"', "设置 → 高级 → 回答服务·Codex（SettingsView.swift CodexReasoningPicker；AnswerServiceViews.swift）",
         'string(forKey: "codexReasoning") ?? "low"',
         "MacPreferenceMap↔AppSettings.ReasoningEffort（空值按 Mac 默认 \"low\"）；SettingsStore 读写；行为：AnswerStreaming 推理强度"),
    done("companionPort", "int", 8765, "8_765", "设置 → 高级 → 手机伴侣（SettingsView.swift CompanionSection）",
         'object(forKey: "companionPort") as? Int ?? 8_765',
         "MacPreferences+SettingsStore（Mac 键名/默认 8765）；缺口：Windows CompanionServer 由 G3/F10 绑定固定端口策略选择，未读取该首选端口",
         note="缺口：首选端口未接 CompanionServer"),
    done("companionRelayURL", "string", "", '""', "设置 → 高级 → 手机伴侣（SettingsView.swift CompanionSection）",
         'string(forKey: "companionRelayURL") ?? ""',
         "MacPreferenceMap↔AppSettings.CompanionRelayUrl；SettingsStore 读写；行为：CompanionRelayClient 中转通道"),
    done("cueAlwaysOpen", "bool", False, "false", "设置 → 提词与教练 → 提词（SettingsPresenter.swift）",
         'bool(forKey: "cueAlwaysOpen")',
         "AppSettings.CueAlwaysOpen；SettingsStore 读写；行为：CueIslandWindow 会中保持展开（C4）"),
    done("cueAvoidsSharing", "bool", True, "true", "设置 → 提词与教练 → 屏幕共享（SettingsPresenter.swift）",
         'object(forKey: "cueAvoidsSharing") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 屏幕共享时按 CapturePrivacy/C19 收回，但没有独立 avoidsSharing 设置，无行为消费者"),
    done("cueLauncher", "bool", True, "true", "设置 → 提词与教练 → 提词（SettingsPresenter.swift）",
         'object(forKey: "cueLauncher") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 提词岛 Launcher 阶段始终可用（C5），没有该开关，无行为消费者"),
    done("cueScreen", "string", "", '""', "设置 → 提词与教练 → 提词（SettingsPresenter.swift）",
         'string(forKey: "cueScreen") ?? ""',
         "AppSettings.CueScreen；SettingsStore 读写；行为：CueIslandWindow.SelectedScreen 顶部居中（C10）"),
    done("cueStyle", "string", "scene", '"scene"', "默认跟随场景；按场景在设置 → 场景「提词」（SettingsPages.swift SceneRow）",
         'string(forKey: "cueStyle") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"scene\"）；Windows 按场景存 SceneDefaults.CueStyle（SceneSettingRow），全局键无行为消费者",
         allowed=["scene", "keyPoints", "script"]),
    done("cueSurface", "string", "island", '"island"', "设置 → 提词与教练 → 提词·显示方式（SettingsPresenter.swift）",
         'string(forKey: "cueSurface") ?? ""',
         "MacPreferenceMap↔AppSettings.CueSurface（E26b）；settings.json 用 Mac 键名；行为：快速设置/场景页选「提词岛/浮窗」，MainWindow 按该键在提词岛与浮窗之间切换会中提示；缺口：未实机双屏观感核对",
         allowed=["island", "window"]),
    done("cueWidth", "string", "regular", '"regular"', "设置 → 提词与教练 → 提词·宽度（SettingsPresenter.swift）",
         'string(forKey: "cueWidth") ?? ""',
         "AppSettings.CueWidth；SettingsStore 读写；行为：CueIslandWindow 宽度 440/540/660",
         allowed=["narrow", "regular", "wide"]),
    done("customNotesTemplates", "array", [], "[]", "设置 → 纪要 → 自定义模板（NotesSettingsView.swift）",
         'data(forKey: "customNotesTemplates")',
         "MacPreferences+SettingsStore（Mac 键名/默认 []，JSON 原样保存）；Windows 自定义模板存 NotesTemplateStore 文件（AppState.Templates），该键无行为消费者",
         note="缺口：Windows 模板另存文件"),
    done("customShortcuts", "dict", {}, "{}", "设置 → 提词与教练 → 快捷键（ShortcutSettingsView.swift）",
         'data(forKey: "customShortcuts")',
         "MacPreferences+SettingsStore（Mac 键名/默认 {}，Mac 原始 JSON 原样保留）；缺口：Windows 快捷键存 AppSettings.Shortcuts（动作 id→按键文本，Ctrl+Alt 体系，见 G1–G4），Mac 的 ShortcutBinding 码不互通"),
    skip("darkMode", "bool", False, "false", "旧外观迁移（AppState.swift appearance 读取）",
         'object(forKey: "darkMode") as? Bool == true',
         "Mac 旧版深色开关迁移键（appearance 已取代）；Windows 从未使用该键，外观直接存 appearance，本版不做"),
    skip("demoNotes", "string", "", '""', "演示模式（DemoMode.swift）",
         'string(forKey: "demoNotes")',
         "Mac 演示/截图模式专用键；Windows 演示与截图入口按 H1 用 --demo/--screenshots 启动参数，不读写这些用户偏好键，本版不做"),
    skip("demoOnboarding", "int", 0, "0", "演示模式（DemoMode.swift）",
         'integer(forKey: "demoOnboarding")',
         "Mac 演示模式专用键（H1：Windows 演示入口为 --demo 启动参数），本版不做"),
    skip("demoSettings", "int", 0, "0", "演示模式（DemoMode.swift）",
         'integer(forKey: "demoSettings")',
         "Mac 演示模式专用键（H1：Windows 演示入口为 --demo 启动参数），本版不做"),
    done("documentOCRProvider", "string", "system", '"system"', "设置 → 回答与资料 → 资料（DocumentModelSettings.swift DocumentReadingSettings）",
         'string(forKey: "documentOCRProvider") ?? ""',
         "MacPreferenceMap↔AppSettings.DocumentOcrProvider（E23/E23b）；设置页 回答与资料→资料 的「读取资料的方式」；DocumentPreparation 导入队列按该键选择系统识别/本机增强/Codex 文件代读（CodexDocumentReader：本机提取+回答服务 JSON 合同），失败按 ocrFallbackToVision 回退系统识别；缺口：未对真实 Codex 账号与大文档实跑",
         allowed=["system", "paddle", "codex"]),
    done("documentProviderWasSelected", "bool", False, "false", "AppState+Models.swift（自动选择后置位，无设置控件）",
         'bool(forKey: "documentProviderWasSelected")',
         "MacPreferenceMap↔AppSettings.DocumentProviderWasSelected（E23）；设置页选择读取资料的方式时置位并随保存写入；缺口：首次引导的自动选择尚未接（无引导步骤）"),
    done("echoSuppression", "bool", True, "true", "设置 → 高级 → 声音与识别（SettingsAdvanced.swift SoundAdvanced）",
         'object(forKey: "echoSuppression") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows EchoSuppression 逻辑已移植（Core/EchoSuppression.cs）但始终启用，没有该开关的行为消费者"),
    done("globalSkillIDs", "array", [], "[]", "技能（SkillLibrary；Backup 导出键，无独立控件）",
         'exportedKeys',
         "MacPreferences+SettingsStore（Mac 键名/默认 []）；缺口：Windows 技能的「所有系列」标记存在 Skill 文件字段（SkillStore），该键无行为消费者"),
    done("insightAnswerProgress", "bool", True, "true", "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift）",
         'object(forKey: "insightAnswerProgress") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 回答进度勾选（AnswerPoints）尚未按该开关启停，无行为消费者"),
    done("insightFollowSlides", "bool", True, "true", "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift）",
         'object(forKey: "insightFollowSlides") as? Bool ?? true',
         "AppSettings.InsightFollowSlides；SettingsStore 读写；行为：AppState.ScreenWatch 跟页（E9）"),
    done("insightScreenInterval", "real", 1.0, "1", "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift）",
         'object(forKey: "insightScreenInterval") as? Double ?? 1',
         "AppSettings.InsightScreenInterval（最小 1 秒）；SettingsStore 读写；行为：AppState.ScreenWatch 定时读屏（E9）",
         minimum=1.0),
    done("insightScreenRegion", "array", None, "nil（整屏/整窗）", "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift 框选区域）",
         'array(forKey: "insightScreenRegion")',
         "MacPreferenceMap↔AppSettings.InsightScreenRegion（Mac [x,y,w,h] ↔ Windows \"x,y,w,h\"）；SettingsStore 读写；行为：AppState.ScreenWatch 区域截图"),
    done("insightScreenTarget", "string", "", '""', "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift 读取来源）",
         'string(forKey: "insightScreenTarget") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"）；缺口：Windows 读屏来源用 InsightScreenSource + 系统显示器，目标 id 与 Mac capture target 体系不同，无行为消费者"),
    done("insightScreenWatch", "bool", False, "false", "设置 → 高级 → 回答服务·屏幕与讲稿（InsightSettingsView.swift）",
         'bool(forKey: "insightScreenWatch")',
         "AppSettings.InsightScreenWatch；SettingsStore 读写；行为：AppState.ScreenWatch 会中读屏（E9，默认关闭）"),
    done("keepOnTop", "bool", True, "true", "设置 → 高级 → 数据与维护（SettingsGeneralData.swift）",
         'object(forKey: "keepOnTop") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 主窗口没有「工作台置顶」开关或置顶行为，无行为消费者"),
    done("keepRecognizerWarm", "bool", True, "true", "设置 → 高级 → 声音与识别·后台预加载（SettingsAdvanced.swift）",
         'object(forKey: "keepRecognizerWarm") as? Bool ?? true',
         "MacPreferences+SettingsStore（Mac 键名/默认 true）；缺口：Windows 识别器预热由「准备语音组件」流程决定，没有该开关，无行为消费者"),
    done("lastScene", "string", "interview", '"interview"', "首页场景（Onboarding.swift rememberGuideScene；AppState.swift 读取）",
         'string(forKey: "lastScene") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"interview\"）；缺口：Windows 首页场景取系列/场景选择，不持久化最后场景，无行为消费者",
         allowed=["general", "interview", "academic", "labMeeting", "meeting", "client", "lesson", "discussion", "groupProject", "custom"]),
    done("liveTranslation", "bool", None, "nil（未选择）", "会中翻译开关「记住选择」（LiveTranslation.swift）",
         'object(forKey: "liveTranslation") as? Bool',
         "MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 有 AppSettings.AutoTranslateForeign（自动翻译外语）与 TranslateSpeech（逐句互译），但未读写该 Mac「记住会中翻译选择」键"),
    done("localOCRModelID", "string", "paddleocr-vl-1-6-mlx-8bit", '"paddleocr-vl-1-6-mlx-8bit"（ModelCatalog.paddleOCR.id）',
         "设置 → 高级 → 资料组件·本机识别模型（DocumentModelSettings.swift）",
         'string(forKey: "localOCRModelID") ?? ModelCatalog.paddleOCR.id',
         "MacPreferenceMap↔AppSettings.LocalOcrModelId（E23）；settings.json 用 Mac 键名；Windows 本机增强固定 PaddleOCR-VL 1.6 + PP-DocLayoutV3（DocumentAssets），设置页显示实际模型与安装状态；缺口：不提供多档本机模型切换",
         allowed=["paddleocr-vl-1-6-mlx-8bit", "paddleocr-vl-1-6-mlx-4bit"]),
    done("localStreamingModel", "string", "xasr480", '"xasr480"', "设置 → 高级 → 声音与识别·流式模型（SettingsAdvanced.swift）",
         'string(forKey: "localStreamingModel") ?? ""',
         "MacPreferenceMap↔AppSettings.LocalModel；SettingsStore 读写；行为：AppState.Audio/SpeechRecognizerSession 选择识别模型",
         allowed=["xasr480", "xasr160", "legacy"]),
    done("meetingVenue", "string", "online", '"online"', "首页/引导 线上·线下（AppState+Meeting.swift；Onboarding.swift）",
         'string(forKey: "meetingVenue") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"online\"）；缺口：Windows 每场会议按采集目标选择系统声/麦克风，没有线上/线下持久化选项，无行为消费者",
         allowed=["online", "offline"]),
    done("microphoneEnabled", "bool", True, "true", "设置 → 高级 → 声音与识别·麦克风（SettingsAdvanced.swift）",
         'object(forKey: "microphoneEnabled") as? Bool ?? true',
         "MacPreferenceMap↔AppSettings.CaptureMicrophone；SettingsStore 读写；行为：AppState.Audio 麦克风采集"),
    done("microphoneVoiceProcessing", "bool", False, "false", "设置 → 高级 → 声音与识别（SettingsAdvanced.swift）",
         'bool(forKey: "microphoneVoiceProcessing")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 麦克风没有 Apple 语音处理（平台差异），无行为消费者"),
    done("notesLanguage", "string", "auto", '"auto"', "设置 → 纪要·纪要语言（NotesSettingsView.swift）",
         'string(forKey: "notesLanguage") ?? "auto"',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"auto\"）；缺口：Windows 纪要生成沿用界面语言，没有独立纪要语言开关，无行为消费者",
         allowed=["auto", "zh", "en"]),
    done("notesModelID", "string", "", '""', "设置 → 高级 → 纪要·模型（NotesSettingsView.swift NotesAdvanced）",
         'string(forKey: "notesModelID") ?? ""',
         "MacPreferenceMap↔AppSettings.NotesModel；SettingsStore 读写；行为：SummaryService 纪要模型"),
    done("notesReasoning", "string", "high", '"high"', "设置 → 高级 → 纪要·推理强度（NotesSettingsView.swift NotesAdvanced）",
         'string(forKey: "notesReasoning") ?? "high"',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"high\"）；缺口：Windows 纪要共用 AppSettings.ReasoningEffort，没有独立推理强度，无行为消费者"),
    done("notesTemplateByType", "dict", {}, "{}", "设置 → 场景「纪要模板」（SettingsPages.swift SceneRow）",
         'dictionary(forKey: "notesTemplateByType")',
         "MacPreferences+SettingsStore（Mac 键名/默认 {}）；缺口：Windows 按场景模板选择存 SceneDefaults.NotesTemplate（SceneSettingRow）+ NotesTemplateStore，该键无行为消费者"),
    done("ocrFallbackToVision", "bool", True, "true", "设置 → 高级 → 资料组件（DocumentModelSettings.swift）",
         'object(forKey: "ocrFallbackToVision") as? Bool ?? true',
         "MacPreferenceMap↔AppSettings.OcrFallbackToVision（E23）；设置页 高级→资料组件 的降级开关；LibraryViewModel 按该键决定缺组件时是否回退系统识别（不回退则导入报错）；缺口：DocumentReader 单页失败时的逐页降级未接入该开关"),
    done("onboardingCompleted", "bool", False, "false", "引导完成（Onboarding.swift finishOnboarding）",
         'set(true, forKey: "onboardingCompleted")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 引导完成版本存 AppSettings.OnboardingCompletedVersion（映射见 onboardingGuideCompletedVersion），旧布尔键无行为消费者"),
    done("onboardingGuideCompletedVersion", "string", "", '""', "引导（Onboarding.swift；HomeView.swift @AppStorage）",
         'string(forKey: "onboardingGuideCompletedVersion")',
         "MacPreferenceMap↔AppSettings.OnboardingCompletedVersion；SettingsStore 读写；行为：AppState.Onboarding 判定是否重开引导"),
    done("onboardingGuideDeferredVersion", "string", "", '""', "首页继续行（HomeView.swift @AppStorage）",
         '@AppStorage("onboardingGuideDeferredVersion")',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"）；缺口：Windows 引导只有完成版本（OnboardingCompletedVersion），无「稍后继续」版本，无行为消费者"),
    done("onboardingGuideDismissedVersion", "string", "", '""', "引导（Onboarding.swift deferOnboarding）",
         'string(forKey: "onboardingGuideDismissedVersion")',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"）；缺口：同上，Windows 无该引导状态"),
    done("onboardingGuideEnabled", "bool", False, "false", "引导（Onboarding.swift setOnboardingGuideEnabled）",
         'bool(forKey: "onboardingGuideEnabled")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 引导开关由 OnboardingVisible/settings 完成版本驱动，无该键行为"),
    done("onboardingGuideLayout", "int", None, "nil（未记录）", "引导（Onboarding.swift；当前 layout=2）",
         'object(forKey: "onboardingGuideLayout") as? Int',
         "MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 引导没有 layout 版本检测，无行为消费者"),
    done("onboardingGuideStartedVersion", "string", None, "nil（未记录）", "引导（Onboarding.swift）",
         'string(forKey: "onboardingGuideStartedVersion")',
         "MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows 引导没有开始版本追踪，无行为消费者"),
    done("onboardingResumeRowDismissedVersion", "string", "", '""', "首页（HomeView.swift @AppStorage）",
         '@AppStorage("onboardingResumeRowDismissedVersion")',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"）；缺口：Windows 首页没有引导继续行，无行为消费者"),
    done("pinnedWorkspaceIDs", "string", "", '""', "资料夹列表·置顶（WorkspacesView.swift WorkspaceList）",
         'AppStorage("pinnedWorkspaceIDs"',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"，\"id|id\" 串）；缺口：Windows 资料夹列表没有置顶功能，无行为消费者"),
    done("presenterMode", "bool", False, "false", "设置 → 提词与教练 → 双屏演讲（SettingsPresenter.swift）",
         'bool(forKey: "presenterMode")',
         "AppSettings.PresenterMode；SettingsStore 读写；行为：AppState.Presenter/主窗口收起（C 组双屏演讲）"),
    done("presenterSlideNotes", "bool", True, "true", "设置 → 提词与教练 → 双屏演讲（SettingsPresenter.swift）",
         'object(forKey: "presenterSlideNotes") as? Bool ?? true',
         "MacPreferenceMap↔AppSettings.PresenterSlideNotes（E26b）；settings.json 用 Mac 键名；行为：双屏演讲时快速设置开关决定提词岛是否显示当前页讲稿要点；缺口：未实机双屏核对"),
    done("presenterSlidesScreen", "string", "", '""', "设置 → 提词与教练 → 双屏演讲·幻灯片所在屏（SettingsPresenter.swift）",
         'string(forKey: "presenterSlidesScreen") ?? ""',
         "AppSettings.PresenterSlidesScreen；SettingsStore 读写；行为：PresenterCheck/AppState 双屏演讲选屏"),
    skip("profile", "string", "", '""', "旧版单场景背景（WorkspaceStore.swift；现为 Workspace.notes）",
         'string(forKey: "profile") ?? ""',
         "Mac 旧版单一场景背景字段；Windows 按系列文件的 Workspace.Notes 存储，不做旧 UserDefaults 迁移，本版不做"),
    done("projectLibrarySort", "string", "recent", '"recent"', "资料夹列表·排序（WorkspacesView.swift WorkspaceList）",
         'AppStorage("projectLibrarySort"',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"recent\"，允许值 recent/name 已写入注册表）；缺口：E18 资料库排序控件未实现，无行为消费者"),
    done("quickNotesWorkspaceID", "string", None, "nil（首次创建「随手记录」）", "立即记录·随手记录系列（AppState+Prepare.swift）",
         'string(forKey: "quickNotesWorkspaceID")',
         "MacPreferences+SettingsStore（Mac 键名/默认 nil）；缺口：Windows「立即记录」没有固定的随手记录系列，无行为消费者"),
    done("quickStartStep", "int", 0, "0", "引导步骤（Onboarding.swift；HomeView.swift @AppStorage）",
         'AppStorage("quickStartStep"',
         "MacPreferences+SettingsStore（Mac 键名/默认 0）；缺口：Windows 引导没有分步进度，无行为消费者",
         minimum=0.0),
    done("recognitionLanguage", "string", "auto", '"auto"', "设置 → 高级 → 声音与识别·识别语言（SettingsView.swift LanguagePicker）",
         'string(forKey: "recognitionLanguage") ?? "auto"',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"auto\"）；缺口：Windows 本机 X-ASR 固定中英混说，云端语言另有 CloudSttLanguage，无本机识别语言消费者",
         allowed=["auto", "zh", "en"]),
    done("recognitionVocabulary", "string", "", '""', "设置 → 高级 → 声音与识别·临时专业词（SettingsAdvanced.swift）",
         'string(forKey: "recognitionVocabulary") ?? ""',
         "MacPreferenceMap↔AppSettings.Vocabulary；SettingsStore 读写；行为：AppState.Hotwords 识别热词"),
        done("recordSessions", "bool", True, "true", "设置 → 通用 → 记录每次会议（SettingsGeneralData.swift）",
         'object(forKey: "recordSessions") as? Bool ?? true',
         "已实现、未验证｜MacPreferenceMap↔AppSettings.RecordSessions（Mac 键名/默认 true，SettingsStore 读写）；行为：StartRecording 读取，关闭时不保留会议（结束时删除刚才写入的会话目录），且跳过自动备份与自动整理；设置 → 通用 → 记录每次会议 开关（录音中禁用，Mac 同句说明）",
         ),

done("recordingNoticeShown", "bool", False, "false", "录音提示（AppState+Prepare.swift、AppState+Launcher.swift）",
         'bool(forKey: "recordingNoticeShown")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 引导已含录音说明，没有一次性录音提示状态，无行为消费者"),
    done("reducedEffects", "bool", False, "false", "设置 → 高级 → 数据与维护·减少视觉特效（SettingsGeneralData.swift）",
         'bool(forKey: "reducedEffects")',
         "AppSettings.ReducedEffects；SettingsViewModel 启动/保存时同步 Motion.AppReducedEffects；行为：Motion.Reduce = 系统关闭动画 或 本设置（MotionModel.ShouldReduce）；设置页开关待 E29"),
    done("sceneOverrides", "dict", {}, "{}", "设置 → 场景（SettingsPages.swift SceneSettingsPage；SceneSettings.swift）",
         'static let key = "sceneOverrides"',
         "MacPreferenceMap↔AppSettings.SceneDefaults（仅 liveMode/style/cueStyle 三字段，与 Mac 场景覆盖同义；Windows 场景行另存模板名）；SettingsStore 读写；行为：AppState.Live 按场景覆盖"),
    done("secondPassEnabled", "bool", True, "true", "设置 → 高级 → 声音与识别·整句精修（SettingsAdvanced.swift）",
         'object(forKey: "secondPassEnabled") as? Bool ?? true',
         "MacPreferenceMap↔AppSettings.SecondPass；SettingsStore 读写；行为：AppState.Audio 整句精修"),
    done("selectedTargetID", "string", "", '""', "设置 → 高级 → 声音与识别·会议声音来自（SettingsAdvanced.swift SoundAdvanced）",
         'string(forKey: "selectedTargetID") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"）；缺口：Windows 采集目标存进程 id/启动时间（AppState.CaptureProcessId，不落盘）并按输出设备选择，capture target 体系不同，无行为消费者"),
    done("semanticRetrievalEnabled", "bool", False, "false", "设置 → 高级 → 资料组件·查找方式（DocumentModelSettings.swift）",
         'object(forKey: "semanticRetrievalEnabled") as? Bool ?? false',
         "MacPreferenceMap↔AppSettings.UseSemanticRetrieval；SettingsStore 读写；行为：RetrievalService 语义+关键词混合检索"),
    skip("sessionMode", "string", "general", '"general"', "旧版单场景模式（WorkspaceStore.swift；现为 Workspace.mode）",
         'string(forKey: "sessionMode") ?? ""',
         "Mac 旧版单一场景模式键；Windows 按系列文件 Workspace.Mode 存储（A7/B1），不做旧 UserDefaults 迁移，本版不做"),
    done("settingsGroup", "string", "", '""', "设置页导航记忆（AppState+Navigation.swift）",
         'string(forKey: "settingsGroup") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"\"）；缺口：Windows 设置页的页面切换在界面代码里（不持久化），无该键行为"),
    skip("simplifiedInterface", "bool", True, "true", "内部派生（AppState+Live.swift 写回；无设置控件）",
         'set(simplifiedInterface, forKey: "simplifiedInterface")',
         "Mac 内部派生键：只在 saveAnswerSettings 里按 simplifiedInterface 写回，没有设置控件也没有实际行为；Windows 没有该模式，本版不做"),
    done("subtitleBarFontSize", "real", 24.0, "24", "设置 → 提词与教练 → 字幕条·字号（SubtitleBarSettingsView.swift）",
         'object(forKey: "subtitleBarFontSize") as? Double ?? 24',
         "MacPreferenceMap↔AppSettings.SubtitleFontSize（默认改 24、范围 16–44）；SettingsStore 读写；行为：字幕条字号（SettingsViewModel）",
         minimum=16.0, maximum=44.0),
    done("teleprompterFontSize", "real", 22.0, "22", "设置 → 提词与教练 → 提词·字号（SettingsPresenter.swift）",
         'object(forKey: "teleprompterFontSize") as? Double ?? 22',
         "AppSettings.TeleprompterFontSize（Mac 默认 22，范围 14–36）；WindowLayout 启动从 settings 读取、改动回写 settings.json；行为：TeleprompterWindow 字号（Views/TeleprompterWindow.axaml.cs）；设置页控件待 E29",
         minimum=14.0, maximum=36.0),
    done("teleprompterHideBackground", "bool", False, "false", "设置 → 提词与教练 → 提词（SettingsPresenter.swift）",
         'object(forKey: "teleprompterHideBackground") as? Bool ?? false',
         "AppSettings.TeleprompterHideBackground；SettingsStore 读写；行为：TeleprompterWindow 隐藏背景只留文字/边框/按钮（C15）"),
    done("teleprompterOpacity", "real", 1.0, "1.0", "设置 → 高级 → 提词·不透明度（SettingsPresenter.swift CueAdvanced）",
         'object(forKey: "teleprompterOpacity") as? Double ?? 1.0',
         "AppSettings.TeleprompterOpacity（Mac 默认 1.0，范围 0.35–1）；WindowLayout 启动从 settings 读取、改动回写 settings.json；行为：TeleprompterWindow 不透明度（Views/TeleprompterWindow.axaml.cs 保留 0.35 下限）；设置页控件待 E29",
         minimum=0.35, maximum=1.0),
    done("textZoom", "real", 1.0, "1（档位 0.9 / 1 / 1.1 / 1.2 / 1.3 / 1.45）",
         "全局文字缩放（DesignSystem.swift TextZoom；菜单 Ctrl + / − / 0）",
         'UserDefaults.standard.set(value, forKey: "textZoom")',
         "J7：Core `TextZoom` 六档（0.9/1/1.1/1.2/1.3/1.45）、`TextZoom.Scaled` 取整；Desktop 全局 Ctrl+/-/0 缩放，MacPreferenceMap↔AppSettings.TextZoom 与 SettingsStore 的 settings.json 同名键持久化（旧 `TextZoom.json` 仅作迁移兜底）；提词岛与回答浮窗保留自己的大小按钮（§11 J7）；SETTINGS_PARITY 此行为「已实现、未验证」；E25：textZoom 已并入 114 键注册表与 SETTINGS_PARITY（此前缺行）",
         minimum=0.9, maximum=1.45),
    done("transcriptionBackend", "string", "zipformer", '"zipformer"', "设置 → 高级 → 声音与识别·引擎（SettingsAdvanced.swift SoundAdvanced）",
         'string(forKey: "transcriptionBackend")',
         "MacPreferenceMap↔AppSettings.CloudStt（zipformer=本地、cloud=CloudStt 云端；apple 在 Windows 无对应——允许的差异「系统语音识别」不显示）；SettingsStore 读写；行为：AppState.Audio",
         allowed=["zipformer", "apple", "cloud"], note="允许的差异：apple（系统语音识别）在 Windows 不显示"),
    done("updateChannel", "string", "stable", '"stable"', "设置 → 高级 → 更新·更新渠道（SettingsView.swift UpdateAdvanced）",
         'string(forKey: "updateChannel") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"stable\"）；缺口：Windows 更新渠道在 AppState.Update 的运行时选择（UpdateChannels 列表）里，未落盘读取该键",
         allowed=["stable", "main"]),
    done("updateRepository", "string", "OmnixSI/NextThought", '"OmnixSI/NextThought"', "设置 → 高级 → 更新 → GitHub 访问·仓库（SettingsView.swift GitHubAccessSection）",
         'string(forKey: "updateRepository") ?? "OmnixSI/NextThought"',
         "MacPreferenceMap↔AppSettings.UpdateRepository（E27 GitHub 访问区：仓库输入 + 访问令牌存 DPAPI 凭据，Mac github.token）；SettingsStore 读写；缺口：Windows 更新源仍由 UpdateFeedPolicy 固定仓库，更新客户端未读取该设置（等待 F 组更新任务）"),
    done("verifyUncertainWithModel", "bool", False, "false", "设置 → 高级 → 回答服务（SettingsAdvanced.swift AnswerAdvanced）",
         'bool(forKey: "verifyUncertainWithModel")',
         "MacPreferences+SettingsStore（Mac 键名/默认 false）；缺口：Windows 追问判断只走本机规则（QuestionDetector/QuestionIntent），没有二次模型判断开关，无行为消费者"),
    done("webSearchMode", "string", "auto", '"auto"', "设置 → 回答与资料 → 联网搜索（SettingsView.swift WebSearchSettings）",
         'string(forKey: "webSearchMode") ?? ""',
         "MacPreferenceMap↔AppSettings.WebSearch（off=false、auto/always=true）；SettingsStore 读写；行为：AppState.Live 联网回答",
         allowed=["off", "auto", "always"], note="缺口：Windows 只有开/关两档（always 归为开），无「每次必查」档"),
    done("webSearchProvider", "string", "codex", '"codex"', "设置 → 高级 → 回答服务·联网搜索（SettingsView.swift WebSearchAdvanced）",
         'string(forKey: "webSearchProvider") ?? ""',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"codex\"）；缺口：Windows 联网搜索固定 Tavily（TavilyKey + WebSearch），没有 Codex/Tavily 选择，无行为消费者",
         allowed=["codex", "tavily"]),
    done("workspaceEditorFocusSection", "string", "records", '"records"', "资料夹编辑器（WorkspacesView.swift WorkspaceEditor）",
         'AppStorage("workspaceEditorFocusSection"',
         "MacPreferences+SettingsStore（Mac 键名/默认 \"records\"）；缺口：Windows 资料夹编辑器页面切换不持久化，无行为消费者",
         allowed=["records", "documents", "knowledge", "basic", "requirements", "skills", "review"]),
    skip("workspaceMigrationCompleted", "bool", False, "false", "旧版迁移标记（WorkspaceStore.swift）",
         'legacyMigrationCompletedKey = "workspaceMigrationCompleted"',
         "Mac 旧版单一场景迁移标记；Windows 直接读 Mac 系列文件（B1/A7），不跑旧 UserDefaults 迁移，本版不做"),
]


# E/F tasks that still have to wire a key whose storage/read-write path is done.  The table
# shows them so a reader can see which follow-up task owns the remaining behaviour.
WIRING_TASK = {
    "activeWorkspaceID": "E18",
    "apiVisionEnabled": "E4",
    "assistantLayout": "E15",
    "autoBackup": "E30",
    "autoCollectKnowledgeCandidates": "E16",
    "cloudConsent": "E25",
    "codexFastMode": "E25",
    "coachEnabled": "E10",
    "coachGazeSensitivity": "E10",
    "coachWatchesGaze": "E10",
    "companionPort": "F10/F12",
    "cueAvoidsSharing": "E25",
    "cueLauncher": "E25",
    "cueStyle": "E25",
    "customNotesTemplates": "E16",
    "echoSuppression": "E25",
    "globalSkillIDs": "E28",
    "insightAnswerProgress": "E7",
    "insightScreenTarget": "E4",
    "keepOnTop": "E29",
    "keepRecognizerWarm": "E25",
    "lastScene": "E3",
    "liveTranslation": "E6",
    "meetingVenue": "E2",
    "microphoneVoiceProcessing": "E25",
    "notesLanguage": "E16",
    "notesReasoning": "E16",
    "notesTemplateByType": "E16",
    "onboardingCompleted": "E3",
    "onboardingResumeRowDismissedVersion": "E3",
    "pinnedWorkspaceIDs": "E18",
    "projectLibrarySort": "E18",
    "quickNotesWorkspaceID": "E4",
    "quickStartStep": "E3",
    "recognitionLanguage": "E25",
    "recordSessions": "E25",
    "recordingNoticeShown": "E3",
    "reducedEffects": "E29（开关控件；Motion.Reduce 已接入）",
    "selectedTargetID": "E4",
    "settingsGroup": "E25",
    "teleprompterFontSize": "E29（设置页控件；提词浮窗已接入）",
    "teleprompterOpacity": "E29（设置页控件；提词浮窗已接入）",
    "updateChannel": "E25",
    "updateRepository": "E25",
    "webSearchProvider": "E25",
    "workspaceEditorFocusSection": "E18",
    "onboardingGuideDeferredVersion": "E3",
    "onboardingGuideDismissedVersion": "E3",
    "onboardingGuideEnabled": "E3",
    "onboardingGuideLayout": "E3",
    "onboardingGuideStartedVersion": "E3",
}


# --- key extraction (the same union the previous table used) --------------
def extract_keys() -> set[str]:
    pattern = re.compile(r'(?:forKey:|object\(forKey:|bool\(forKey:|string\(forKey:|integer\(forKey:|double\(forKey:)\s*"([^"]+)"')
    keys: set[str] = set(pattern.findall(SWIFT))
    keys.update(re.findall(r'@AppStorage\("([^"]+)"', SWIFT))
    keys.update(re.findall(r'static let key = "([^"]+)"', SWIFT))
    exported = re.search(r"exportedKeys[^=]*=\s*\[(.*?)\]", BACKUP, re.S)
    if exported:
        keys.update(re.findall(r'"([^"]+)"', exported.group(1)))
    keys -= {"CODEX_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"}
    return keys


def verify() -> None:
    extracted = extract_keys()
    manifest = {entry.key for entry in PREFERENCES}
    problems: list[str] = []
    if len(extracted) != EXPECTED_KEYS:
        problems.append(f"Swift extraction found {len(extracted)} keys, expected {EXPECTED_KEYS}")
    if extracted != manifest:
        for key in sorted(extracted - manifest):
            problems.append(f"key in Swift but missing from manifest: {key}")
        for key in sorted(manifest - extracted):
            problems.append(f"key in manifest but not in Swift: {key}")
    if len(manifest) != EXPECTED_KEYS:
        problems.append(f"manifest has {len(manifest)} keys, expected {EXPECTED_KEYS}")
    for entry in PREFERENCES:
        if entry.swift not in SWIFT:
            problems.append(f"Swift evidence no longer found for {entry.key}: {entry.swift}")
        if entry.status not in ("done", "skip", "todo"):
            problems.append(f"bad status for {entry.key}: {entry.status}")
        if entry.status in ("done", "todo") and not entry.evidence:
            problems.append(f"status {entry.status} without evidence: {entry.key}")
        if entry.status == "skip" and not entry.evidence:
            problems.append(f"status 本版不做 without reason: {entry.key}")
        if entry.allowed is not None:
            if entry.default is not None and entry.default not in entry.allowed:
                problems.append(f"default {entry.default!r} not in allowed values for {entry.key}")
    if problems:
        print("settings parity verification FAILED", file=sys.stderr)
        for problem in problems:
            print("  -", problem, file=sys.stderr)
        raise SystemExit(1)
    done_count = sum(1 for entry in PREFERENCES if entry.status == "done")
    skip_count = sum(1 for entry in PREFERENCES if entry.status == "skip")
    todo_count = sum(1 for entry in PREFERENCES if entry.status == "todo")
    print(f"settings parity OK: {len(PREFERENCES)}/{EXPECTED_KEYS} keys, "
          f"{done_count} done, {skip_count} skip, {todo_count} todo")


# --- C# registry generation ----------------------------------------------
def cs_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def cs_value(value) -> str:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return f"(long){value}"
    if isinstance(value, float):
        text = repr(value)
        if "e" in text or "." not in text:
            text = f"{value:.10f}".rstrip("0").rstrip(".")
        return text if "." in text else text + ".0"
    if isinstance(value, str):
        return cs_string(value)
    if isinstance(value, list):
        return "new List<object?> { " + ", ".join(cs_value(item) for item in value) + " }"
    if isinstance(value, dict):
        return "new Dictionary<string, object?> { " + ", ".join(
            f"[{cs_string(str(key))}] = {cs_value(item)}" for key, item in value.items()) + " }"
    raise TypeError(f"cannot generate C# for {value!r}")


def write_csharp() -> Path:
    target = ROOT / "Windows/src/NextThought.Core/MacPreferences.g.cs"
    entries = [entry for entry in PREFERENCES if entry.status != "todo"]
    lines = [
        "// <auto-generated>",
        "// Generated by Windows/scripts/settings_parity.py from Sources/ (the Mac Swift app).",
        "// Recompute with: python Windows/scripts/settings_parity.py --write",
        "// Do not edit by hand.",
        "// </auto-generated>",
        "#nullable enable",
        "namespace NextThought.Core;",
        "",
        f"/// <summary>G5: the {len(entries)} Mac preference keys with their Mac type, default value, view and range.</summary>",
        "public static class MacPreferencesData",
        "{",
        "    public static readonly MacPreference[] Entries =",
        "    [",
    ]
    for entry in entries:
        allowed = "null" if entry.allowed is None else "new[] { " + ", ".join(cs_string(v) for v in entry.allowed) + " }"
        minimum = "null" if entry.minimum is None else cs_value(float(entry.minimum))
        maximum = "null" if entry.maximum is None else cs_value(float(entry.maximum))
        lines.append(
            "        new(" + ", ".join([
                cs_string(entry.key),
                KIND_CS[entry.kind],
                cs_value(entry.default),
                cs_string(entry.view.split("（")[0].split("(")[0].strip()),
                cs_string(entry.swift),
                allowed,
                minimum,
                maximum,
            ]) + "),")
    lines += ["    ];", "}", ""]
    target.write_text("\n".join(lines), encoding="utf-8")
    return target


# --- Markdown table generation -------------------------------------------
def md_code(text: str) -> str:
    if not text:
        return "—"
    return text.replace("|", "\\|")


def write_markdown() -> Path:
    target = ROOT / "Windows/SETTINGS_PARITY.md"
    done_count = sum(1 for entry in PREFERENCES if entry.status == "done")
    skip_count = sum(1 for entry in PREFERENCES if entry.status == "skip")
    todo_count = sum(1 for entry in PREFERENCES if entry.status == "todo")
    lines = [
        "# 设置对照（SETTINGS_PARITY）",
        "",
        f"Mac 六页设置共 **{EXPECTED_KEYS}** 个偏好键。本表由 `Windows/scripts/settings_parity.py` 从 Swift 源码"
        "（UserDefaults/AppStorage/SceneSettings key/`Sources/NextThoughtCore/Backup.swift` 的 exportedKeys 并集）提取，"
        f"当前 **{len(PREFERENCES)} / {EXPECTED_KEYS}**；其中「已实现、未验证」**{done_count}** 项、"
        f"「未做」**{todo_count}** 项、「本版不做」**{skip_count}** 项。",
        "",
        "复算命令（在仓库根目录运行）：",
        "",
        "```bash",
        "python Windows/scripts/settings_parity.py          # 校验键集、默认值与 Swift 证据；打印当前数字",
        "python Windows/scripts/settings_parity.py --write  # 重新生成 MacPreferences.g.cs 与本表",
        "```",
        "",
        "测试锁定这些数字：`Windows/tests/NextThought.Core.Tests/SettingsParityDefaultsTests.cs` 逐个断言类型、默认值与取值范围；"
        "`Windows/tests/NextThought.Tests/SettingsParityTableTests.cs` 断言本表行数与注册表一致，并断言 `SettingsStore` "
        "写出的 settings.json 使用 Mac 键名且默认值逐个相同。",
        "",
        "| 键名 | 默认类型 | 默认值 | Mac 设置视图 | Windows 状态 |",
        "|---|---|---|---|---|",
    ]
    status_labels = {"done": "已实现、未验证", "skip": "本版不做", "todo": "未做"}
    for entry in PREFERENCES:
        status = status_labels[entry.status]
        evidence = md_code(entry.evidence)
        if entry.status == "done" and entry.key in WIRING_TASK:
            evidence += "；接入任务：" + md_code(WIRING_TASK[entry.key])
        note = f"；{md_code(entry.note)}" if entry.note else ""
        lines.append(
            f"| `{entry.key}` | {KIND_LABEL[entry.kind]} | {md_code(entry.default_text)} | "
            f"{md_code(entry.view)} | {status}｜{evidence}{note} |")
    lines.append("")
    target.write_text("\n".join(lines), encoding="utf-8")
    return target


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="regenerate MacPreferences.g.cs and SETTINGS_PARITY.md")
    args = parser.parse_args()
    verify()
    if args.write:
        csharp = write_csharp()
        table = write_markdown()
        print(f"wrote {csharp.relative_to(ROOT)}")
        print(f"wrote {table.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
