using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NextThought.Desktop.Views.Island;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// §6.6 / C5–C9: end-to-end check of the cue island. Drives the six Mac presentations plus the
/// three the Mac did not photograph (收起/展开/讲稿), asserts every element the task book names and
/// the interaction contract (non-activating, click-through while put away, 0.22 s open / 0.2 s
/// close, morph spring, reduced-motion snap). Writes a JSON report next to the other self-tests.
/// </summary>
public partial class MainWindow
{
    private sealed record IslandCheckStep(string Name, bool Passed, string Detail);

    private async Task RunIslandCheckAsync(string path)
    {
        await Task.Delay(200);
        var steps = new List<IslandCheckStep>();
        var island = new CueIslandWindow(state);
        try
        {
            island.Show();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(350);

            void Check(string name, bool passed, string detail) => steps.Add(new IslandCheckStep(name, passed, detail));

            // ---- the six states (plus 收起/展开/讲稿) ------------------------------------
            var expectations = new (string State, CueIslandPhase Phase)[]
            {
                ("cue", CueIslandPhase.Cue),
                ("hearing", CueIslandPhase.Hearing),
                ("listening", CueIslandPhase.Listening),
                ("deck", CueIslandPhase.Deck),
                ("launcher", CueIslandPhase.Launcher),
                ("pinned", CueIslandPhase.Cue),
                ("autoend", CueIslandPhase.AutoEnd),
                ("compact", CueIslandPhase.Compact),
                ("open", CueIslandPhase.Cue),
                ("slide", CueIslandPhase.Slide),
                ("bilingual", CueIslandPhase.Cue)
            };
            foreach (var (stateName, expected) in expectations)
            {
                island.ApplyStateForCheck(stateName);
                await Task.Delay(120);
                Check("phase-" + stateName, island.Model.Phase == expected,
                    $"expected {expected}, got {island.Model.Phase}");
            }

            // ---- the deck (C6): copy, the four pill actions, the shape --------------------
            island.ApplyStateForCheck("deck");
            await Task.Delay(150);
            string Text(string name) => island.FindControl<TextBlock>(name)?.Text ?? "<missing>";
            bool Visible(string name) => island.FindControl<Control>(name)?.IsEffectivelyVisible == true;
            var title = Text("DeckTitleText");
            var sceneTag = Text("DeckSceneTag");
            var mode = Text("DeckModeText");
            var speaker = Text("DeckHeardSpeaker");
            var heard = Text("DeckHeardText");
            Check("deck-title", title == "字节后端二面", title);
            Check("deck-scene-tag", sceneTag == "面试", sceneTag);
            Check("deck-mode", mode == "自动回答", mode);
            Check("deck-heard", speaker == "对方" && heard == DemoScript.LivePartialTranscript, speaker + " " + heard);
            Check("deck-warning-hidden", !Visible("DeckWarningDot"), "answer engine ready in demo");
            var force = island.FindControl<Button>("DeckForceAnswerButton");
            var recordOnly = island.FindControl<Button>("DeckRecordOnlyButton");
            var pause = island.FindControl<Button>("DeckPauseButton");
            var end = island.FindControl<Button>("DeckEndButton");
            Check("deck-four-buttons",
                force is { IsEffectivelyVisible: true } && recordOnly is { IsEffectivelyVisible: true }
                && pause is { IsEffectivelyVisible: true } && end is { IsEffectivelyVisible: true },
                "立即回答/改为只记录/暂停/结束");
            Check("deck-commands",
                ReferenceEquals(force?.Command, state.ForceAnswerCommand)
                && ReferenceEquals(pause?.Command, state.TogglePauseListeningCommand)
                && ReferenceEquals(end?.Command, state.StopRecordingCommand)
                && recordOnly?.Command is not null,
                "force/pause/end bound to the Mac actions, 改为只记录 switches the live mode");
            Check("deck-shape", Math.Abs(island.IslandFlare - 14) < 0.01
                                && Math.Abs(island.IslandCornerRadius - 24) < 0.01
                                && island.IslandContentWidth >= 470
                                && island.Width > island.IslandFrameWidth,
                $"flare={island.IslandFlare:0.##} radius={island.IslandCornerRadius:0.##} width={island.IslandContentWidth:0}");

            // ---- the answer (C5): question, lines and the hover controls -----------------
            island.ApplyStateForCheck("cue");
            await Task.Delay(120);
            var question = Text("CueQuestionText");
            var cueRows = island.FindControl<ItemsControl>("CueRowsHost")?.ItemCount ?? 0;
            Check("cue", question.Contains("数据库和缓存") && cueRows > 0,
                $"question='{question}' rows={cueRows}");
            Check("cue-width", Math.Abs(island.IslandContentWidth - 540) < 0.5, $"{island.IslandContentWidth:0}");
            Check("cue-tint", island.Model.TintColor == Color.Parse("#93aee0"), island.Model.TintColor.ToString());

            // ---- 对方在说 (C5): their words, with the interpretation ---------------------
            island.ApplyStateForCheck("listening");
            await Task.Delay(120);
            var listening = Text("ListeningText");
            var translated = Text("ListeningTranslationText");
            Check("listening", listening.Contains("physics informed") && translated.Length > 0
                                && Visible("ListeningTranslationText"),
                $"text='{listening[..Math.Min(40, listening.Length)]}…' translation='{translated}'");

            // ---- 启动器 (C5) ------------------------------------------------------------
            island.ApplyStateForCheck("launcher");
            await Task.Delay(120);
            var scenes = island.FindControl<ItemsControl>("LauncherScenesHost")?.ItemCount ?? 0;
            Check("launcher", island.Model.Phase == CueIslandPhase.Launcher && scenes >= 9,
                $"scenes={scenes}");
            Check("launcher-copy", island.FindControl<Button>("LauncherOpenWindowButton") is { IsEffectivelyVisible: true },
                "打开主窗口");

            // ---- 自动结束提醒 (C7) ------------------------------------------------------
            island.ApplyStateForCheck("autoend");
            await Task.Delay(120);
            var autoEnd = Text("AutoEndText");
            Check("autoend", autoEnd == "好像没人说话了，42 秒后结束记录", autoEnd);
            Check("autoend-actions",
                island.FindControl<Button>("KeepRecordingButton")?.Command is not null
                && island.FindControl<Button>("EndNowButton")?.Command is not null
                && Visible("KeepRecordingButton") && Visible("EndNowButton"),
                "继续记录 / 现在结束");

            // ---- 讲稿 (C7) --------------------------------------------------------------
            island.ApplyStateForCheck("slide");
            await Task.Delay(150);
            var slideHeader = Text("SlideHeaderText");
            var slideRows = island.FindControl<ItemsControl>("SlidePointsHost")?.ItemCount ?? 0;
            Check("slide", slideHeader.Contains("第 4 页") && slideRows == 4,
                $"header='{slideHeader}' rows={slideRows}");
            Check("slide-page-ear", Text("SlidePageText") == "4/12", Text("SlidePageText"));

            // ---- 双语 (C8) --------------------------------------------------------------
            island.ApplyStateForCheck("bilingual");
            await Task.Delay(120);
            var asked = Text("CueAskedText");
            Check("bilingual", asked.Length > 0 && Visible("CueAskedText"),
                $"asked='{asked[..Math.Min(30, asked.Length)]}…'");
            island.ApplyStateForCheck("pinned");
            await Task.Delay(120);
            Check("pinned", island.FindControl<ItemsControl>("CueRowsHost")?.ItemCount > 0
                            && Visible("CueAskedText"),
                "kept open: the last answer in full, with the meaning under each sentence");

            // ---- C9: scene pigment edge, morph spring, reduced motion --------------------
            island.ApplyStateForCheck("cue");
            await Task.Delay(120);
            var edge = island.Model.EdgeBrush;
            Check("c9-edge", edge is Avalonia.Media.LinearGradientBrush
                             && Math.Abs(island.Model.EdgeWidth - 1.2) < 0.001,
                edge?.GetType().Name ?? "null");
            island.SetReducedMotionForCheck(true);
            island.SetHoveringForCheck(true);
            Check("c9-reduced-motion-snap",
                island.Model.Phase == CueIslandPhase.Deck
                && Math.Abs(island.IslandContentWidth - 470) < 0.5,
                $"phase={island.Model.Phase} width={island.IslandContentWidth:0}");
            island.SetHoveringForCheck(false);
            Check("c9-reduced-motion-collapse",
                island.Model.Phase == CueIslandPhase.Compact
                && Math.Abs(island.IslandContentWidth - IslandLayout.EarsWidth(IslandMetrics.Notch)) < 0.5,
                $"phase={island.Model.Phase} width={island.IslandContentWidth:0}");
            island.SetReducedMotionForCheck(false);
            var morph = NextThought.Platform.MotionTokens.Morph;
            Check("c9-morph-token",
                Math.Abs(morph.Response - 0.5) < 1e-9 && Math.Abs(morph.DampingFraction - 0.82) < 1e-9,
                $"response={morph.Response} damping={morph.DampingFraction}");
            Check("c2-dwell",
                Math.Abs(IslandLayout.HoverOpenSeconds - 0.22) < 1e-9
                && Math.Abs(IslandLayout.HoverCloseSeconds - 0.2) < 1e-9,
                "0.22 s open / 0.2 s close");
            Check("c1-non-activating", island.IsNonActivating && island.ShowActivated == false,
                $"style=0x{island.ExtendedStyle:X} showActivated={island.ShowActivated}");
            Check("c2-click-through", island.ClickThrough,
                "collapsed island lets clicks through until the pointer rests on a control");
        }
        catch (Exception error)
        {
            steps.Add(new IslandCheckStep("exception", false, error.ToString()));
        }
        finally
        {
            try
            {
                island.SetReducedMotionForCheck(false);
                island.ClearStateForCheck();
                island.AllowClose = true;
                island.Close();
            }
            catch { }
            state.Recording = false;
            state.PresenterLayoutActive = false;
            state.Settings.CueAlwaysOpen = false;
            state.Settings.PresenterSlideNotes = true;
            state.Settings.AutomaticAnswers = true;
        }
        var report = new
        {
            passed = steps.Count > 0 && steps.All(step => step.Passed),
            scope = "island states, copy, deck actions, geometry, dwell and reduced-motion; not real pointer motion",
            steps
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(report.passed ? "island-check: passed" : "island-check: failed");
        Close();
    }
}
