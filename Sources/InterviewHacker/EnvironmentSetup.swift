import AppKit
import Foundation
import InterviewHackerCore

/// One-click environment configuration and rebuild.
///
/// Getting the app to work by hand means walking through the Codex CLI, the
/// answer model, the speech model, the audio source and the material index — five
/// places that can each be half-configured. These flows run that whole checklist
/// in order and report every step, so a broken setup shows up as a named failed
/// step instead of as "it just does not answer".
@MainActor
enum EnvironmentSetup {
    struct Step: Identifiable, Equatable {
        enum Status: String, Equatable {
            case pending, running, done, failed, skipped

            var title: String {
                switch self {
                case .pending: "等待中"
                case .running: "进行中"
                case .done: "完成"
                case .failed: "失败"
                case .skipped: "已跳过"
                }
            }

            var symbol: String {
                switch self {
                case .pending: "circle"
                case .running: "arrow.triangle.2.circlepath"
                case .done: "checkmark.circle.fill"
                case .failed: "exclamationmark.triangle.fill"
                case .skipped: "minus.circle"
                }
            }
        }

        let id = UUID()
        var title: String
        var detail: String = ""
        var status: Status = .pending
    }

    /// Configures the environment for the first use: checks the answer engine,
    /// picks a model, makes sure the speech model is there, finds the audio
    /// source and indexes the materials.
    static func quickSetup(state: AppState, onStep: @escaping (Step) -> Void) async {
        // 1. The answer engine has to exist before anything else can work.
        var step = Step(title: "检查回答引擎", status: .running)
        onStep(step)
        await state.refreshCodexStatus()
        if state.answerEngineReady {
            step.status = .done
            step.detail = state.answerBackend == .openAICompatible
                ? "OpenAI 兼容接口：\(state.apiModel)"
                : "Codex 已就绪：\(state.currentCodexModel?.name ?? state.codexModelID)"
        } else {
            step.status = .failed
            step.detail = state.answerBackend == .openAICompatible
                ? "请填写接口地址、模型名和 API Key。"
                : "没有检测到已登录的 Codex。请先在 ChatGPT / Codex 客户端登录。"
        }
        onStep(step)

        // 2. A model has to be selected, not just installed.
        step = Step(title: "选择回答模型", status: .running)
        onStep(step)
        await state.refreshCodexModels()
        if state.answerBackend == .openAICompatible {
            step.status = state.apiModel.trimmingCharacters(in: .whitespaces).isEmpty ? .failed : .done
            step.detail = step.status == .done ? "使用 \(state.apiModel)" : "还没有填写模型名。"
        } else if state.codexModelID.isEmpty, let first = state.codexModels.first {
            state.codexModelID = first.id
            state.updateSettings()
            step.status = .done
            step.detail = "已选择 \(first.name)"
        } else if state.codexModelID.isEmpty {
            step.status = .failed
            step.detail = state.codexModels.isEmpty ? "没有取到模型列表，请稍后重试。" : "请在“回答”里选一个模型。"
        } else {
            step.status = .done
            step.detail = "使用 \(state.currentCodexModel?.name ?? state.codexModelID)"
        }
        onStep(step)

        // 3. The speech model: a missing local model is downloaded in the
        //    background, the app stays usable while it runs.
        step = Step(title: "准备语音识别", status: .running)
        onStep(step)
        if state.recognitionReady {
            step.status = .done
            step.detail = state.transcriptionBackend == "apple" ? "系统语音识别已就绪" : "识别引擎已就绪"
        } else if state.transcriptionBackend == "zipformer", !state.isModelInstalled {
            state.downloadModel()
            step.status = .running
            step.detail = "开始下载本地模型，完成后自动可用。"
        } else {
            step.status = .failed
            step.detail = state.transcriptionBackend == "cloud" ? "请填写云端识别密钥。" : "本地模型不可用，请重新下载。"
        }
        onStep(step)

        // 4. The audio source: without it only the microphone is captured.
        step = Step(title: "刷新音频来源", status: .running)
        onStep(step)
        await state.refreshTargets()
        if state.captureTargets.isEmpty {
            step.status = .failed
            step.detail = "没有找到会议窗口。请在“系统设置 → 隐私与安全性 → 屏幕录制”里允许本应用。"
        } else {
            step.status = .done
            step.detail = "找到 \(state.captureTargets.count) 个可采集的窗口"
        }
        onStep(step)

        // 5. The material index: what the model actually reads.
        step = Step(title: "重建资料索引", status: .running)
        onStep(step)
        state.rebuildContextIndex()
        let count = state.documents.filter(\.enabled).count
        step.status = .done
        step.detail = count == 0 ? "还没有资料，场景里的背景信息仍会生效。" : "已索引 \(count) 份资料"
        onStep(step)

        // 6. Warm the recognizer so the first question is not slow.
        step = Step(title: "预热识别器", status: .running)
        onStep(step)
        await state.prewarmRecognizer()
        step.status = .done
        step.detail = state.keepRecognizerWarm ? "已预热" : "已跳过（关闭了常驻预热）"
        onStep(step)

        // 7. The answer template of the current mode.
        step = Step(title: "应用回答模板", status: .running)
        onStep(step)
        state.applyTemplatePreferences()
        step.status = .done
        step.detail = "已套用\(state.sessionMode.title)的回答要求"
        onStep(step)
    }

    /// Rebuilds the runtime environment without changing the user's choices:
    /// re-reads the configuration, re-indexes the materials and re-creates the
    /// folders the app writes into.
    static func rebuild(state: AppState, onStep: @escaping (Step) -> Void) async {
        var step = Step(title: "重建资料索引", status: .running)
        onStep(step)
        state.rebuildContextIndex()
        step.status = .done
        step.detail = "已索引 \(state.documents.filter(\.enabled).count) 份资料"
        onStep(step)

        step = Step(title: "刷新 Codex 状态", status: .running)
        onStep(step)
        await state.refreshCodexStatus()
        if state.answerBackend == .openAICompatible, !state.codexInstalled {
            // An API key needs no Codex: not finding it is not a failure.
            step.status = .skipped
            step.detail = "当前使用 API Key，不需要 Codex。"
        } else {
            step.status = state.codexInstalled ? .done : .failed
            step.detail = state.codexInstalled ? state.codexLoginStatus : "没有找到 Codex CLI。"
        }
        onStep(step)

        step = Step(title: "刷新回答模型", status: .running)
        onStep(step)
        await state.refreshCodexModels()
        step.status = state.codexModels.isEmpty ? .skipped : .done
        step.detail = state.codexModels.isEmpty ? "没有取到模型列表" : "共 \(state.codexModels.count) 个可用模型"
        onStep(step)

        step = Step(title: "重建音频来源", status: .running)
        onStep(step)
        await state.refreshTargets()
        step.status = state.captureTargets.isEmpty ? .failed : .done
        step.detail = state.captureTargets.isEmpty ? "没有找到可采集的窗口" : "找到 \(state.captureTargets.count) 个窗口"
        onStep(step)

        step = Step(title: "重建设置与外观", status: .running)
        onStep(step)
        state.updateSettings()
        step.status = .done
        step.detail = "已重新应用全部设置"
        onStep(step)

        step = Step(title: "校验数据目录", status: .running)
        onStep(step)
        do {
            try FileManager.default.createDirectory(at: AppPaths.root, withIntermediateDirectories: true)
            step.status = .done
            step.detail = AppPaths.root.path
        } catch {
            step.status = .failed
            step.detail = error.localizedDescription
        }
        onStep(step)

        step = Step(title: "预热识别器", status: .running)
        onStep(step)
        await state.prewarmRecognizer()
        step.status = .done
        step.detail = "完成"
        onStep(step)
    }
}
