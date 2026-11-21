namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunTeleprompterHandleCheckAsync(string path)
    {
        await Task.Delay(300);
        var passed = false; var error = ""; var visible = false; var restored = false;
        try
        {
            ToggleTeleprompter();
            await Task.Delay(400);
            teleprompter?.ApplyClickThrough(true);
            await Task.Delay(400);
            visible = teleprompter?.ClickThrough == true && teleprompter.ClickThroughHandleVisible;
            teleprompter?.DisableClickThrough();
            await Task.Delay(400);
            restored = teleprompter?.ClickThrough == false && teleprompter.ClickThroughHandleVisible == false;
            passed = visible && restored;
        }
        catch (Exception exception) { error = exception.ToString(); }
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(
                new { passed, visible, restored, error }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        Console.WriteLine(passed ? "teleprompter-handle-check: passed" : "teleprompter-handle-check: failed");
        Close();
    }
}
