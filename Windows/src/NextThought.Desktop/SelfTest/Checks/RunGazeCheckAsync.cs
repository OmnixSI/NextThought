namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunGazeCheckAsync(string path)
    {
        await Task.Delay(100);
        static NextThought.Core.EyeGeometry Eye(double pupilX = 5) => new(
            [new NextThought.Core.GazePoint(0, 2.5), new NextThought.Core.GazePoint(5, 0),
             new NextThought.Core.GazePoint(10, 2.5), new NextThought.Core.GazePoint(5, 5)],
            new NextThought.Core.GazePoint(pupilX, 2.5));
        static NextThought.Core.GazeFrame Frame(double pupilX, double time) =>
            new(Eye(pupilX), Eye(pupilX), 0, 0, time);
        var estimator = new NextThought.Core.GazeEstimator();
        for (var index = 0; index < 9; index++) estimator.AddCalibration(Frame(5 + index * 0.1, index));
        var calibrated = estimator.FinishCalibration();
        var onCamera = estimator.Ingest(Frame(5, 10));
        var away = estimator.Ingest(Frame(9, 11));
        var passed = NextThought.Core.GazeEstimator.Signature(Frame(5, 0)) is not null
                     && calibrated && estimator.IsCalibrated
                     && onCamera.State == NextThought.Core.GazeReading.StateValue.OnCamera
                     && away.State == NextThought.Core.GazeReading.StateValue.Away
                     && away.Deviation > 1;
        var report = new { passed, calibrated, baselineX = estimator.Baseline.X, onCamera = onCamera.State.ToString(), away = away.State.ToString(), deviation = Math.Round(away.Deviation, 3) };
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "gaze-check: passed" : "gaze-check: failed");
        Close();
    }

    /// <summary>Builds a tiny tokenizer.json, then encodes and decodes with the ported BPE tokenizer.</summary>
}
