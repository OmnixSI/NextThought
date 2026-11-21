using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// E10: the calibrated eye-contact pose. The Mac keeps coachGazeBaseline in UserDefaults (four
/// doubles: x, y, yaw, pitch); on Windows it lives in a small WindowsState sidecar so it is never
/// written into the Mac settings.json key space and never travels in a backup. The pose describes
/// the user's own seat and camera, so restoring it on the next run is the Mac behaviour.
/// </summary>
public sealed class GazeBaselineStore(AppPaths paths)
{
    private string FilePath => Path.Combine(paths.Root, "WindowsState", "coach-gaze-baseline.json");

    public GazeSignature? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var values = JsonSerializer.Deserialize<double[]>(File.ReadAllText(FilePath), JsonDefaults.ArchiveOptions);
            if (values is not { Length: 4 }) return null;
            if (values.Any(value => !double.IsFinite(value))) return null;
            return new GazeSignature(values[0], values[1], values[2], values[3]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(GazeSignature baseline)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(new[] { baseline.X, baseline.Y, baseline.Yaw, baseline.Pitch }, JsonDefaults.ArchiveOptions));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A read-only data directory must not break the meeting; the coach simply recalibrates next time.
        }
    }
}
