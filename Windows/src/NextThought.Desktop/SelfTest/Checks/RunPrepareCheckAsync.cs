using Avalonia.Threading;
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
                ["startEnabled"] = state.StartRecordingCommand.CanExecute(null)
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
