namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunPresenterCheckAsync(string path)
    {
        await Task.Delay(300);
        var steps = new List<PresenterCheckStep>();
        var real = BuildDisplays().ToList();
        var summary = "";
        try
        {
            var primary = real.FirstOrDefault(display => display.IsPrimary) ?? real.FirstOrDefault()
                          ?? new DisplayInfo("display:0", "主屏", 0, 0, 1920, 1080, true, true);
            var fake = new List<DisplayInfo>(real.Count > 0 ? real : [primary])
            {
                new("fake:external", "外接显示器（自检）", primary.X + primary.Width, primary.Y, 1920, 1080, false, false)
            };
            DisplayCatalog.Refresh(fake);
            state.Settings.PresenterMode = true;
            var plan = CurrentPresenterPlan();
            summary = plan.Summary;
            steps.Add(new PresenterCheckStep("plan-with-second-display", plan.CanActivate && plan.SlidesIsSeparate, plan.Summary));

            ApplyPresenterModeForRecording();
            await Task.Delay(200);
            steps.Add(new PresenterCheckStep("page-steps-aside",
                presenterActive && !IsVisible && island is { IsVisible: true },
                $"active={presenterActive} mainVisible={IsVisible} island={island?.IsVisible}"));

            ExitPresenterMode();
            await Task.Delay(200);
            steps.Add(new PresenterCheckStep("restored", !presenterActive && IsVisible, $"mainVisible={IsVisible}"));

            // Always feed one display: with a real second screen attached, `real` would
            // legitimately activate and the refusal branch would never be exercised.
            var single = PresenterLayout.Resolve([primary], null);
            steps.Add(new PresenterCheckStep("single-display-refuses", !single.CanActivate, single.Summary));
        }
        catch (Exception ex)
        {
            steps.Add(new PresenterCheckStep("exception", false, ex.Message));
        }
        finally
        {
            ExitPresenterMode();
            DisplayCatalog.Refresh(real);
            state.Settings.PresenterMode = false;
        }
        var report = new PresenterCheckReport
        {
            Passed = steps.Count > 0 && steps.All(step => step.Passed),
            Scope = real.Count > 1
                ? "real multi-display topology; window visibility and island, not projector pixels"
                : "synthetic second display; window visibility and island, not real projector output",
            Steps = steps,
            RealScreens = string.Join(" | ", real.Select(display => $"{display.Name} {display.Bounds}")),
            PlanSummary = summary
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch
        {
            // The console line still reports the result if the report file cannot be written.
        }
        Console.WriteLine(report.Passed ? "presenter-check: passed" : "presenter-check: failed");
        Close();
    }

    /// <summary>
    /// End-to-end relay check against a local relay: create a room, publish encrypted state,
    /// wait for the simulated phone's encrypted command, acknowledge it. The phone script
    /// reads the pairing file this writes.
    /// </summary>
}
