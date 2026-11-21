using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E2: drives the prepare page the way a click would. The scene chips go through the draft command,
    /// the carry switches through the same properties the view binds, the venue picker through the
    /// draft, and the start path builds the ported MeetingPlan the same way startPreparedMeeting does.
    /// </summary>
    private async Task RunPrepareCheckAsync(string path)
    {
        await Task.Delay(400);
        var failures = new List<string>();
        var state = this.state;
        try
        {
            state.Section = AppSection.Prepare;
            await PumpAsync();
            state.EnsureDraft();
            if (state.Draft is null) failures.Add("prepare page did not open a MeetingDraft");

            // Drive the scene chips the page shows.
            foreach (var choice in state.Scenes)
            {
                state.SetDraftType(choice.Mode);
                if (state.SelectedMode != choice.Mode) failures.Add("scene chip did not switch to " + choice.Mode);
                if (state.Draft?.Mode != choice.Mode) failures.Add("draft mode did not follow " + choice.Mode);
                if (state.Draft?.TypeChosen != true) failures.Add("draft type was not marked chosen");
            }
            if (state.DraftSummary.Length == 0) failures.Add("scene summary empty");
            if (!state.DraftSummary.Contains(state.SelectedScene?.Title ?? "x", StringComparison.Ordinal))
                failures.Add("scene summary does not name the scene: " + state.DraftSummary);

            // J2/§6.7: the reworked page's collection blocks must be bound to non-empty state, and the
            // visual tree must actually contain the chips/moments (a silent binding failure would
            // otherwise only show up in a screenshot).
            await PumpAsync();
            var chipsState = state.DraftSceneChips.Count;
            var momentsState = state.DraftMomentRows.Count;
            var venuesState = state.VenueOptions.Count;
            if (chipsState != state.Scenes.Count) failures.Add("scene chips state count " + chipsState);
            if (momentsState == 0) failures.Add("moment rows state empty");
            if (venuesState != 2) failures.Add("venue options state " + venuesState);
            if (PageHost.Content is Control preparePage)
            {
                var buttons = preparePage.GetVisualDescendants().OfType<Button>().ToList();
                var chipButtons = buttons.Count(button => button.Classes.Contains("sceneChip"));
                if (chipButtons != state.Scenes.Count + 1) failures.Add("scene chip buttons in the tree: " + chipButtons);
                var momentDots = preparePage.GetVisualDescendants().OfType<ItemsControl>()
                    .Count(control => control.Name == "MomentList");
                if (momentDots == 0) failures.Add("moment list not in the visual tree");
                if (preparePage.FindControl<ItemsControl>("SceneChipsList") is not { } chipList) failures.Add("SceneChipsList missing");
                else if (chipList.ItemCount != state.Scenes.Count) failures.Add("SceneChipsList items: " + chipList.ItemCount);
                if (preparePage.FindControl<Control>("VenuePicker") is { } venue && venue.Bounds.Width <= 0) failures.Add("VenuePicker has no width");
                var firstChip = buttons.FirstOrDefault(button => button.Classes.Contains("sceneChip"));
                if (firstChip is not null)
                {
                    var text = firstChip.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
                    Console.WriteLine($"chip bounds={firstChip.Bounds} visible={firstChip.IsVisible}/{firstChip.IsEffectivelyVisible} opacity={firstChip.Opacity} bg={firstChip.Background} fg={firstChip.Foreground}");
                    if (text is not null) Console.WriteLine($"chip text='{text.Text}' bounds={text.Bounds} fg={text.Foreground} size={text.FontSize}");
                    if (firstChip.Bounds.Width <= 0 || firstChip.Bounds.Height <= 0) failures.Add("first chip button has no size");
                    if (firstChip.GetVisualDescendants().OfType<TextBlock>().All(candidate => candidate.Bounds.Width <= 0))
                        failures.Add("chip label has no size");
                }
                if (preparePage.FindControl<Control>("VenuePicker") is { } venueControl)
                    Console.WriteLine($"venue bounds={venueControl.Bounds} visible={venueControl.IsEffectivelyVisible} items={(venueControl as ItemsControl)?.ItemCount}");
            }

            // Custom type: the Mac 自定义… chip and its base picker.
            state.SetDraftCustomTypeCommand.Execute(null);
            state.SetDraftCustomName("导师一对一");
            if (!state.DraftIsCustom) failures.Add("custom type chip did not switch the draft");
            if (!state.DraftSummary.Contains("导师一对一", StringComparison.Ordinal)) failures.Add("custom name missing from the summary");
            state.SetDraftCustomBase(MeetingMode.Interview);
            if (state.Draft?.CustomBaseMode != MeetingMode.Interview) failures.Add("custom base did not apply");

            // Venue: 线下 means microphone only (Mac MeetingVenue.offline).
            var wasOnline = state.DraftOnline;
            state.DraftOffline = true;
            await PumpAsync();
            Console.WriteLine($"venue offline: captureSystem={state.Settings.CaptureSystem} draftOffline={state.DraftOffline} index={state.DraftVenueIndex} wasOnline={wasOnline}");
            if (state.Settings.CaptureSystem) failures.Add("线下 did not switch capture to the microphone only");
            state.DraftOnline = wasOnline;
            await PumpAsync();
            if (state.Settings.CaptureSystem != wasOnline) failures.Add("venue picker did not restore");

            // Microphone choice: 只记录对方 then 重新打开麦克风.
            state.ContinueWithoutMicrophoneCommand.Execute(null);
            await PumpAsync();
            if (state.Settings.CaptureMicrophone) failures.Add("只记录对方 did not switch the microphone off");
            state.UseMicrophoneAgainCommand.Execute(null);
            await PumpAsync();
            if (!state.Settings.CaptureMicrophone) failures.Add("重新打开麦克风 did not switch it back on");

            var materials = state.CarryMaterials;
            state.CarryMaterials = !materials;
            if (state.CarryPlan.Materials == materials) failures.Add("带入资料 switch did not update MeetingCarry");
            if (state.Draft?.Carry.Materials == materials) failures.Add("draft carry did not follow 带入资料");
            state.CarryMaterials = materials;
            if (state.PrepareCarrySummary.Length == 0) failures.Add("carry summary empty");
            if (state.PrepareMaterialsRows.Count == 0) failures.Add("materials file rows empty");

            // The draft becomes the ported MeetingPlan (title/type carry through).
            state.DraftTitle = "第二轮面试";
            var plan = state.DraftMeetingPlan;
            if (plan is null) failures.Add("draftMeetingPlan is null");
            else
            {
                if (plan.Title != "第二轮面试") failures.Add("plan title did not come from the draft");
                if (plan.Mode != state.Draft!.Mode) failures.Add("plan mode did not come from the draft");
            }
            var series = state.SelectedSeries is { } row
                ? state.WorkspaceSnapshot.Series.FirstOrDefault(item => item.Id == row.Id)
                : null;
            state.BeginPreparedMeeting(series);
            if (state.ActivePlan is null) failures.Add("BeginPreparedMeeting produced no MeetingPlan");
            else
            {
                if (state.ActivePlan.Carry.Materials != materials) failures.Add("plan carry does not match the switches");
                if (state.ActivePlan.Title != "第二轮面试") failures.Add("plan did not keep the draft title");
            }
            if (!state.PrepareAudioReady && state.PrepareAudioProblem.Length == 0) failures.Add("missing audio problem text");
            if (state.PrepareAnswerLine.Length == 0) failures.Add("answer line empty");
            if (state.PrepareEngineVerification.Length == 0) failures.Add("engine verification line empty");

            // J2: 这场用什么 — the four switches are remembered on the series and applied at start.
            if (!state.MeetingFeaturesAreDefault) failures.Add("feature picker did not open on the scene's defaults");
            var standardFeatures = state.StandardMeetingFeatures;
            if (state.DraftMeetingFeatures != standardFeatures) failures.Add("draft features are not the standard ones");
            if (state.MeetingFeatureAnswersDetail.Length == 0 || state.MeetingFeatureLiveNotesDetail.Length == 0
                || state.MeetingFeatureTranslationDetail.Length == 0 || state.MeetingFeatureSpeakersDetail.Length == 0)
                failures.Add("a feature row has no explanation line");
            var featureSeriesID = state.Draft?.SeriesId;
            state.MeetingFeatureAnswers = !standardFeatures.Answers;
            if (state.MeetingFeatureAnswers == standardFeatures.Answers) failures.Add("自动回答 switch did not flip");
            if (state.MeetingFeaturesAreDefault) failures.Add("switch did not mark the choices as changed");
            if (state.DraftMeetingFeatures.Answers == standardFeatures.Answers) failures.Add("自动回答 switch did not persist");
            state.MeetingFeatureLiveNotes = !standardFeatures.LiveNotes;
            state.MeetingFeatureTranslation = !standardFeatures.Translation;
            state.MeetingFeatureSpeakers = !standardFeatures.Speakers;
            var changed = state.DraftMeetingFeatures;
            if (!string.IsNullOrEmpty(featureSeriesID))
            {
                // A different series must not inherit this series' switches (Mac featuresKey).
                if (state.OtherSeriesRows.Count > 0)
                {
                    state.ContinueDraftSeries(state.OtherSeriesRows[0].Id);
                    if (!state.MeetingFeaturesAreDefault) failures.Add("another series inherited the switches");
                    state.SelectedSeries = state.SeriesRows.FirstOrDefault(item => item.Id == featureSeriesID);
                    state.ContinueDraftSeries(featureSeriesID!);
                    if (state.DraftMeetingFeatures != changed) failures.Add("the series did not keep its switches");
                }
            }
            state.ResetMeetingFeaturesCommand.Execute(null);
            if (!state.MeetingFeaturesAreDefault) failures.Add("恢复这个场景的默认 did not reset the switches");
            // Applied at start: answers need a ready engine; notes and interpretation follow the switches.
            state.MeetingFeatureAnswers = false;
            state.MeetingFeatureLiveNotes = false;
            state.MeetingFeatureTranslation = false;
            state.ApplyActiveMeetingFeatures();
            if (state.Settings.AutomaticAnswers != (state.AnswerEngineReady && false)) failures.Add("answers not applied at start");
            if (state.LiveNotesEnabled) failures.Add("实时纪要 ran although the switch was off");
            if (state.LiveTranslationEnabled) failures.Add("同声传译 ran although the switch was off");
            state.ResetMeetingFeaturesCommand.Execute(null);
            state.MeetingFeatureAnswers = false;
            state.MeetingFeatureLiveNotes = false;
            state.MeetingFeatureTranslation = false;
            state.MeetingFeatureSpeakers = false;
            state.ApplyActiveMeetingFeatures();
            if (state.ActiveMeetingFeatures.Answers || state.ActiveMeetingFeatures.LiveNotes
                || state.ActiveMeetingFeatures.Translation || state.ActiveMeetingFeatures.Speakers)
                failures.Add("the started meeting did not use the four switches");
            state.ResetMeetingFeaturesCommand.Execute(null);

            await PumpAsync();
            var report = new Dictionary<string, object?>
            {
                ["passed"] = failures.Count == 0,
                ["failures"] = failures,
                ["draftScene"] = state.Draft?.TypeDisplayName ?? "",
                ["draftSummary"] = state.DraftSummary,
                ["customBase"] = state.Draft?.CustomBaseMode.ToString() ?? "",
                ["venueOnline"] = state.DraftOnline,
                ["carrySummary"] = state.PrepareCarrySummary,
                ["materialRows"] = state.PrepareMaterialsRows.Count,
                ["preparedMaterials"] = state.PreparedMaterialCount,
                ["planTitle"] = state.ActivePlan?.Title ?? "",
                ["planMode"] = state.ActivePlan?.Mode.ToString() ?? "",
                ["startEnabled"] = state.StartRecordingCommand.CanExecute(null),
                ["standardFeatures"] = standardFeatures.ToString(),
                ["featureSeries"] = featureSeriesID ?? ""
            };
            await WriteReportAsync(path, report);
            Console.WriteLine(failures.Count == 0
                ? "prepare-check: passed"
                : "prepare-check: failed " + string.Join("; ", failures));
            state.CancelDraftCommand.Execute(null);
            if (state.Section != AppSection.Home) Console.WriteLine("prepare-check: cancel did not return home");
            if (state.Draft is not null) Console.WriteLine("prepare-check: cancel kept the draft");
        }
        finally
        {
            Close();
        }
    }
}
