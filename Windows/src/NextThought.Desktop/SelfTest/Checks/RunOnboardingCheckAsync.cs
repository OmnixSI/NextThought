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

            for (var step = 1; step < 4; step++)
            {
                state.NextOnboardingStepCommand.Execute(null);
                await PumpAsync();
                var expected = (NextThought.Core.OnboardingStep)step;
                if (state.GuideStep != expected) failures.Add("next did not reach " + expected);
            }
            if (!state.OnboardingIsStart || !state.OnboardingIsLast) failures.Add("last page flags wrong");
            state.PreviousOnboardingStepCommand.Execute(null);
            await PumpAsync();
            if (state.GuideStep != NextThought.Core.OnboardingStep.Answers) failures.Add("previous did not go back one step");

            state.GoToOnboardingStepCommand.Execute("Sound");
            await PumpAsync();
            if (!state.OnboardingIsSound) failures.Add("go-to sound step failed");

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
                ["hasSceneSheet"] = state.OnboardingSceneMoments.Count > 0
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
