using NextThought.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace NextThought.Platform;

/// <summary>
/// E10: the Windows port of LiveCoach's GazeCamera. Reads face landmarks off the camera (at most
/// six analyses a second, 640x480, CPU memory frames only) and reduces them to a GazeReading.
/// Frames live in memory for one analysis and are dropped; nothing is recorded, saved or uploaded.
/// Calibration is the Mac's: collect frames while the user looks into the lens, then take the
/// median pose so the coach measures deviation from that person's own resting pose.
/// </summary>
public sealed class GazeCamera : IAsyncDisposable
{
    private readonly FaceLandmarker landmarker;
    private readonly GazeEstimator estimator = new();
    private readonly object gate = new();
    private MediaCapture? capture;
    private MediaFrameReader? reader;
    private byte[]? rgb;
    private DateTimeOffset startedAt;
    private DateTimeOffset calibrationEnd = DateTimeOffset.MinValue;
    private TaskCompletionSource<bool>? calibrationWaiter;
    private DateTimeOffset lastAnalysis;
    private volatile bool running;

    /// <summary>One reading per analysed frame (roughly six a second).</summary>
    public event Action<GazeReading, double?>? Reading;

    /// <summary>Starts/stops; the coach mirrors this into its "正在看镜头" state.</summary>
    public event Action<bool>? RunningChanged;

    /// <summary>Raised after a successful calibration so the caller can persist the pose.</summary>
    public event Action<GazeSignature>? CalibrationCompleted;

    /// <summary>True when the OS/device refused the camera (Windows privacy setting or missing device).</summary>
    public bool PermissionDenied { get; private set; }

    /// <summary>Why the last StartAsync failed (diagnostics for the self-check).</summary>
    public string? StartError { get; private set; }

    public bool IsRunning => running;
    public bool IsCalibrated => estimator.IsCalibrated;
    public GazeSignature Baseline => estimator.Baseline;
    public double Tolerance
    {
        get => estimator.Tolerance;
        set => estimator.Tolerance = value;
    }

    public GazeCamera(string assetDirectory)
    {
        landmarker = new FaceLandmarker(assetDirectory);
    }

    /// <summary>Applies the stored pose from a previous run (the Mac keeps it in user defaults).</summary>
    public void RestoreBaseline(GazeSignature? baseline)
    {
        if (baseline is not null) estimator.Restore(baseline);
    }

    public async Task<bool> StartAsync()
    {
        if (running) return true;
        StartError = null;
        try
        {
            capture = new MediaCapture();
            // Exclusive like the Mac's AVCaptureSession; SharedReadOnly is rejected by several
            // built-in laptop cameras with a file-in-use COMException.
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            });
            var source = capture.FrameSources.Values.FirstOrDefault(item => item.Info.SourceKind == MediaFrameSourceKind.Color);
            if (source is null) return false;
            // The Mac asks for VGA; pick the 640x480 recording format when the device offers it.
            foreach (var property in capture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord))
            {
                if (property is VideoEncodingProperties video && video.Width == 640 && video.Height == 480)
                {
                    await capture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, property);
                    break;
                }
            }
            reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            reader.FrameArrived += OnFrameArrived;
            var status = await reader.StartAsync();
            running = status == MediaFrameReaderStartStatus.Success;
            startedAt = DateTimeOffset.Now;
            lastAnalysis = DateTimeOffset.MinValue;
            estimator.ResetSession();
            RunningChanged?.Invoke(running);
            return running;
        }
        catch (UnauthorizedAccessException error)
        {
            PermissionDenied = true;
            StartError = "unauthorized: " + error.Message;
            return false;
        }
        catch (Exception error)
        {
            StartError = error.GetType().Name + ": " + error.Message;
            await StopAsync();
            return false;
        }
    }

    public async Task StopAsync()
    {
        running = false;
        if (reader is not null)
        {
            reader.FrameArrived -= OnFrameArrived;
            try { await reader.StopAsync(); } catch { }
            reader.Dispose();
            reader = null;
        }
        if (capture is not null)
        {
            capture.Dispose();
            capture = null;
        }
        calibrationWaiter?.TrySetResult(false);
        calibrationWaiter = null;
        calibrationEnd = DateTimeOffset.MinValue;
        RunningChanged?.Invoke(false);
    }

    /// <summary>Collects frames for <paramref name="duration"/> and keeps the median pose when enough arrived.</summary>
    public async Task<bool> CalibrateAsync(TimeSpan duration)
    {
        if (!running) return false;
        var waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            estimator.CancelCalibration();
            calibrationWaiter?.TrySetResult(false);
            calibrationWaiter = waiter;
            calibrationEnd = DateTimeOffset.Now + duration;
        }
        return await waiter.Task;
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (!running) return;
        var now = DateTimeOffset.Now;
        if (lastAnalysis != DateTimeOffset.MinValue && (now - lastAnalysis).TotalMilliseconds < 1000.0 / 6.0) return;
        lastAnalysis = now;
        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap is null) return;
            SoftwareBitmap? converted = null;
            try
            {
                if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
                    converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var source = converted ?? bitmap;
                var width = source.PixelWidth;
                var height = source.PixelHeight;
                var size = width * height * 3;
                var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
                source.CopyToBuffer(buffer);
                using var dataReader = DataReader.FromBuffer(buffer);
                var pixels = new byte[buffer.Length];
                dataReader.ReadBytes(pixels);
                if (rgb is null || rgb.Length != size) rgb = new byte[size];
                BgraToRgb(pixels, rgb);
                var analysis = landmarker.Analyze(rgb, width, height, (now - startedAt).TotalSeconds);
                GazeReading reading;
                double? contactRatio;
                lock (gate)
                {
                    reading = estimator.Ingest(analysis?.Gaze);
                    if (calibrationWaiter is not null)
                    {
                        if (analysis?.Gaze is { } calibrationFrame && now < calibrationEnd)
                            estimator.AddCalibration(calibrationFrame);
                        if (now >= calibrationEnd)
                        {
                            var success = estimator.FinishCalibration();
                            var waiter = calibrationWaiter;
                            calibrationWaiter = null;
                            calibrationEnd = DateTimeOffset.MinValue;
                            if (success) CalibrationCompleted?.Invoke(estimator.Baseline);
                            waiter?.TrySetResult(success);
                        }
                    }
                    contactRatio = estimator.ContactRatio;
                }
                Reading?.Invoke(reading, contactRatio);
            }
            finally
            {
                converted?.Dispose();
            }
        }
        catch (Exception)
        {
            // A dropped or malformed frame must not end the meeting; the next frame is retried.
        }
    }

    private static void BgraToRgb(ReadOnlySpan<byte> bgra, Span<byte> rgb)
    {
        for (var source = 0; source < bgra.Length; source += 4)
        {
            var target = source / 4 * 3;
            rgb[target] = bgra[source + 2];
            rgb[target + 1] = bgra[source + 1];
            rgb[target + 2] = bgra[source];
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        landmarker.Dispose();
    }
}
