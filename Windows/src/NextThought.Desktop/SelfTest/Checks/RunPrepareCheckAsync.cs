using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E2: drives the prepare page the way a click would. The scene chips go through the same
    /// command, the carry switches through the same properties the view binds, and the start path
    /// goes through BeginPreparedMeeting exactly like StartRecordingAsync does.
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
            foreach (var mode in Enum.GetValues<MeetingMode>())
            {
                if (!state.SetSceneCommand.CanExecute(mode)) { failures.Add("scene command disabled for " + mode); continue; }
                state.SetSceneCommand.Execute(mode);
                if (state.SelectedMode != mode) failures.Add("scene chip did not switch to " + mode);
            }
            if (state.SelectedScene is not { } scene) failures.Add("selected scene missing after chips");
            else if (scene.Title.Length == 0) failures.Add("scene title empty");

            var materials = state.CarryMaterials;
            state.CarryMaterials = !materials;
            if (state.CarryPlan.Materials == materials) failures.Add("带入资料 switch did not update MeetingCarry");
            state.CarryMaterials = materials;
            var review = state.CarryReview;
            state.CarryReview = !review;
            if (state.CarryPlan.Review == review) failures.Add("带入题库 switch did not update MeetingCarry");
            state.CarryReview = review;
            if (state.PrepareCarrySummary.Length == 0) failures.Add("carry summary empty");

            var series = state.SelectedSeries is { } row
                ? state.WorkspaceSnapshot.Series.FirstOrDefault(item => item.Id == row.Id)
                : null;
            state.BeginPreparedMeeting(series);
            if (state.ActivePlan is null) failures.Add("BeginPreparedMeeting produced no MeetingPlan");
            else if (state.ActivePlan.Carry.Materials != materials) failures.Add("plan carry does not match the switches");

            await PumpAsync();
            var report = new Dictionary<string, object?>
            {
                ["passed"] = failures.Count == 0,
                ["failures"] = failures,
                ["scene"] = state.SelectedScene?.Title ?? "",
                ["carrySummary"] = state.PrepareCarrySummary,
                ["preparedMaterials"] = state.PreparedMaterialCount,
                ["planMode"] = state.ActivePlan?.Mode.ToString() ?? "",
                ["startEnabled"] = state.StartRecordingCommand.CanExecute(null)
            };
            await WriteReportAsync(path, report);
            Console.WriteLine(failures.Count == 0
                ? "prepare-check: passed"
                : "prepare-check: failed " + string.Join("; ", failures));
            state.CancelDraftCommand.Execute(null);
            if (state.Section != AppSection.Home) Console.WriteLine("prepare-check: cancel did not return home");
        }
        finally
        {
            Close();
        }
    }
}
