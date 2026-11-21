namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunWorkspaceDeletionCheckAsync(string path)
    {
        await Task.Delay(100);
        var directory = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), "deletion");
        Directory.CreateDirectory(Path.Combine(directory, "data"));
        var file = Path.Combine(directory, "data", "a.txt");
        await File.WriteAllTextAsync(file, "A");
        var staging = Path.Combine(directory, "staging");
        var moves = NextThought.Core.WorkspaceDeletionFileStaging.Manifest([file], staging);
        NextThought.Core.WorkspaceDeletionFileStaging.Stage(moves);
        var staged = !File.Exists(file) && File.Exists(moves[0].Staged);
        NextThought.Core.WorkspaceDeletionFileStaging.Rollback(moves);
        var restored = File.Exists(file) && File.ReadAllText(file) == "A" && !Directory.Exists(staging);
        var steps = new List<string>();
        var ran = await NextThought.Core.WorkspaceDeletionTransaction.Execute(
            prepare: () => { steps.Add("prepare"); return Task.FromResult("plan"); },
            isAuthorized: _ => { steps.Add("authorize"); return true; },
            rollback: _ => { steps.Add("rollback"); return Task.CompletedTask; },
            commit: _ => { steps.Add("commit"); return Task.CompletedTask; },
            stage: _ => { steps.Add("stage"); return Task.CompletedTask; });
        var passed = staged && restored && ran && string.Join(",", steps) == "prepare,authorize,stage,authorize,commit";
        var report = new { passed, staged, restored, ran, steps };
        try
        {
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "workspace-deletion-check: passed" : "workspace-deletion-check: failed");
        Close();
    }

    /// <summary>Creates a document job and checks stage titles, progress and terminal states.</summary>
}
