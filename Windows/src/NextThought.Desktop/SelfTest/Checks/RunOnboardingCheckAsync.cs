using Avalonia.Threading;
using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E3: walks the four guide steps through the same commands the buttons use, checks that each
    /// page's visibility flag flips, that the sample toggle works and that finishing hides the guide.
    /// </summary>
    private async Task RunOnboardingCheckAsync(string path)
    {
        await Task.Delay(400);
        var failures = new List<string>();
        var state = this.state;
        try
        {
            state.OnboardingVisible = true;
            state.OnboardingStep = 0;
            await PumpAsync();
            if (!state.OnboardingIsScene) failures.Add("guide did not open on the scene page");
            if (state.OnboardingIsFirst is false) failures.Add("first page is not flagged first");
            if (state.OnboardingSceneTitle.Length == 0) failures.Add("scene sheet title empty");
            // E3 §6.7: header progress, the scene sheet's chips/moments and the four page bodies.
            if (state.OnboardingProgressCapsules.Count != 4) failures.Add("progress capsules: " + state.OnboardingProgressCapsules.Count);
            if (state.OnboardingSceneChips.Count != state.Scenes.Count) failures.Add("scene sheet chips: " + state.OnboardingSceneChips.Count);
            if (state.OnboardingSceneMomentRows.Count == 0) failures.Add("scene sheet moments empty");
            if (state.OnboardingScenePromise.Length == 0) failures.Add("scene sheet promise empty");
            if (state.OnboardingStepTime.Length == 0 || state.OnboardingPosition != "1 / 4") failures.Add("header text wrong");

            for (var step = 1; step < 4; step++)
            {
                state.NextOnboardingStepCommand.Execute(null);
                await PumpAsync();
                var expected = (NextThought.Core.OnboardingStep)step;
                if (state.GuideStep != expected) failures.Add("next did not reach " + expected);
                if (state.OnboardingStepTitle.Length == 0 || state.OnboardingStepTime.Length == 0)
                    failures.Add("header empty on step " + step);
                var current = state.OnboardingProgressCapsules[(int)expected];
                if (current.Background is null) failures.Add("progress capsule has no brush");
            }
            if (!state.OnboardingIsStart || !state.OnboardingIsLast) failures.Add("last page flags wrong");
            if (state.OnboardingCueIslandQuestion.Length == 0 || state.OnboardingCueIslandAnswer.Length == 0
                || state.OnboardingCuePoint1.Length == 0 || state.OnboardingCuePoint2.Length == 0)
                failures.Add("cue island mock copy empty");
            if (state.OnboardingReadinessMessage.Length == 0) failures.Add("start page readiness line empty");
            state.PreviousOnboardingStepCommand.Execute(null);
            await PumpAsync();
            if (state.GuideStep != NextThought.Core.OnboardingStep.Answers) failures.Add("previous did not go back one step");
            if (state.OnboardingAnswerProviders.Count < 10) failures.Add("answer provider chips: " + state.OnboardingAnswerProviders.Count);
            if (state.OnboardingAnswerServiceTitle.Length == 0 || state.OnboardingTestAnswerHint.Length == 0)
                failures.Add("answer page copy empty");

            state.GoToOnboardingStepCommand.Execute("Sound");
            await PumpAsync();
            if (!state.OnboardingIsSound) failures.Add("go-to sound step failed");
            if (state.OnboardingVenueDetail.Length == 0 || state.OnboardingSystemAudioDetail.Length == 0
                || state.OnboardingMicrophoneDetail.Length == 0 || state.OnboardingRecognitionDetail.Length == 0
                || state.OnboardingDataRouteSummary.Length == 0)
                failures.Add("sound page row copy empty");
            if (state.OnboardingMicrophoneOptions.Count != 2) failures.Add("microphone choices: " + state.OnboardingMicrophoneOptions.Count);
            // 线下: microphone only (Mac MeetingVenue). Restored right after.
            var wasOnline = state.Settings.CaptureSystem;
            state.OnboardingVenueIndex = 1;
            await PumpAsync();
            if (state.Settings.CaptureSystem || !state.Settings.CaptureMicrophone) failures.Add("guide 线下 did not switch to microphone only");
            state.OnboardingVenueIndex = 0;
            await PumpAsync();
            if (!state.Settings.CaptureSystem) failures.Add("guide venue did not restore 线上");
            if (wasOnline != state.Settings.CaptureSystem) failures.Add("guide venue restore mismatch");

            state.ToggleOnboardingSampleCommand.Execute(null);
            if (!state.OnboardingShowSample) failures.Add("sample toggle did not open");
            state.ToggleOnboardingSampleCommand.Execute(null);
            if (state.OnboardingShowSample) failures.Add("sample toggle did not close");

            var stepCount = OnboardingSteps.Count;
            state.SkipOnboardingCommand.Execute(null);
            await PumpAsync();
            if (state.OnboardingVisible) failures.Add("skip did not hide the guide");

            var report = new Dictionary<string, object?>
            {
                ["passed"] = failures.Count == 0,
                ["failures"] = failures,
                ["steps"] = stepCount,
                ["position"] = state.OnboardingPosition,
                ["stepTitle"] = state.OnboardingStepTitle,
                ["hasSceneSheet"] = state.OnboardingSceneMoments.Count > 0,
                ["progressCapsules"] = state.OnboardingProgressCapsules.Count,
                ["sceneChips"] = state.OnboardingSceneChips.Count,
                ["providers"] = state.OnboardingAnswerProviders.Count
            };
            await WriteReportAsync(path, report);
            Console.WriteLine(failures.Count == 0
                ? "onboarding-check: passed"
                : "onboarding-check: failed " + string.Join("; ", failures));
        }
        finally
        {
            Close();
        }
    }
}
