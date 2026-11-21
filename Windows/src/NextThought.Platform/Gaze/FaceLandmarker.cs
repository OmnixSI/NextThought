using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>One analysed camera frame: where the face is, its 478 landmarks and the gaze signal.</summary>
public sealed record FaceAnalysis(
    FaceDetection Detection,
    IReadOnlyList<FaceLandmark> Landmarks,
    GazeFrame? Gaze,
    double FacePresenceScore);

/// <summary>
/// E10: runs the converted MediaPipe Face Landmarker models (BlazeFace short range + FaceMesh V2,
/// Apache-2.0) through ONNX Runtime, the Windows equivalent of the Mac's Vision request behind
/// LiveCoach's GazeCamera. The two stages and their constants follow
/// mediapipe/tasks/cc/vision/face_detector/face_detector_graph.cc: letterboxed 128x128 detector
/// input, MediaPipe SSD decode, greedy NMS at 0.3, then the rotated 1.5x ROI is warped to 256x256
/// for FaceMesh. Frames are only ever held in memory as RGB bytes; nothing is written to disk.
/// </summary>
public sealed class FaceLandmarker : IDisposable
{
    public const string DetectorModelName = "face_detector.onnx";
    public const string LandmarksModelName = "face_landmarks_detector.onnx";

    private readonly InferenceSession detector;
    private readonly InferenceSession landmarks;
    private readonly string detectorInput;
    private readonly string landmarksInput;
    private bool disposed;

    public FaceLandmarker(string assetDirectory)
    {
        var detectorPath = Path.Combine(assetDirectory, DetectorModelName);
        var landmarksPath = Path.Combine(assetDirectory, LandmarksModelName);
        if (!File.Exists(detectorPath) || !File.Exists(landmarksPath))
            throw new FileNotFoundException($"face landmarker models missing in {assetDirectory}");
        detector = new InferenceSession(detectorPath);
        landmarks = new InferenceSession(landmarksPath);
        detectorInput = detector.InputMetadata.Keys.First();
        landmarksInput = landmarks.InputMetadata.Keys.First();
    }

    /// <summary>The committed assets next to the app, or the repository copy for tests.</summary>
    public static string? FindAssetDirectory()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "assets", "face-landmarker");
                if (File.Exists(Path.Combine(candidate, DetectorModelName)) &&
                    File.Exists(Path.Combine(candidate, LandmarksModelName)))
                    return candidate;
                var windows = Path.Combine(directory.FullName, "Windows", "assets", "face-landmarker");
                if (File.Exists(Path.Combine(windows, DetectorModelName)) &&
                    File.Exists(Path.Combine(windows, LandmarksModelName)))
                    return windows;
                directory = directory.Parent;
            }
        }
        return null;
    }

    /// <summary>
    /// Analyses one RGB24 frame (row-major, three bytes per pixel). Returns null when the detector
    /// finds no face, which is the "没有人脸" state the coach reports as NoFace.
    /// </summary>
    public FaceAnalysis? Analyze(ReadOnlySpan<byte> rgb, int width, int height, double time)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (rgb.Length < width * height * 3) throw new ArgumentException("frame is shorter than width*height*3", nameof(rgb));

        var detection = Detect(rgb, width, height);
        if (detection is null) return null;
        var roi = FaceGeometry.Roi(detection);
        var crop = WarpRoi(rgb, width, height, roi);
        var (cropLandmarks, presence) = RunLandmarks(crop);
        var imageLandmarks = FaceGeometry.ProjectLandmarks(cropLandmarks, roi);
        var gaze = FaceGeometry.GazeFrame(imageLandmarks, time);
        return new FaceAnalysis(detection, imageLandmarks, gaze, presence);
    }

    private FaceDetection? Detect(ReadOnlySpan<byte> rgb, int width, int height)
    {
        // keep_aspect_ratio + BORDER_ZERO: fit the frame into 128x128 and pad with zeros.
        var scale = Math.Min(FaceGeometry.DetectorInputSize / (double)width, FaceGeometry.DetectorInputSize / (double)height);
        var resizedWidth = Math.Max(1, (int)Math.Round(width * scale));
        var resizedHeight = Math.Max(1, (int)Math.Round(height * scale));
        var padX = (FaceGeometry.DetectorInputSize - resizedWidth) / 2;
        var padY = (FaceGeometry.DetectorInputSize - resizedHeight) / 2;
        var tensor = new DenseTensor<float>([1, 3, FaceGeometry.DetectorInputSize, FaceGeometry.DetectorInputSize]);
        for (var y = 0; y < resizedHeight; y++)
        {
            var sourceY = Math.Min(height - 1, (int)((y + 0.5) / scale));
            for (var x = 0; x < resizedWidth; x++)
            {
                var sourceX = Math.Min(width - 1, (int)((x + 0.5) / scale));
                var offset = (sourceY * width + sourceX) * 3;
                var target = (y + padY) * FaceGeometry.DetectorInputSize + (x + padX);
                tensor[0, 0, target / FaceGeometry.DetectorInputSize, target % FaceGeometry.DetectorInputSize] = rgb[offset] / 255f;
                tensor[0, 1, target / FaceGeometry.DetectorInputSize, target % FaceGeometry.DetectorInputSize] = rgb[offset + 1] / 255f;
                tensor[0, 2, target / FaceGeometry.DetectorInputSize, target % FaceGeometry.DetectorInputSize] = rgb[offset + 2] / 255f;
            }
        }
        using var results = detector.Run([NamedOnnxValue.CreateFromTensor(detectorInput, tensor)]);
        var regressors = results.First(item => item.Name == "regressors").AsEnumerable<float>().ToArray();
        var scores = results.First(item => item.Name == "classificators").AsEnumerable<float>().ToArray();
        var detections = FaceGeometry.Decode(regressors, scores, FaceGeometry.DetectorInputSize, FaceGeometry.DetectorInputSize);
        if (detections.Count == 0) return null;
        var best = FaceGeometry.NonMaximumSuppression(detections).MaxBy(item => item.Width * item.Height)!;
        // Undo the letterbox: tensor pixels -> source pixels.
        FaceDetection Project(FaceDetection item) => item with
        {
            Left = (item.Left - padX) / scale,
            Top = (item.Top - padY) / scale,
            Right = (item.Right - padX) / scale,
            Bottom = (item.Bottom - padY) / scale,
            Keypoints = item.Keypoints.Select(point => new GazePoint((point.X - padX) / scale, (point.Y - padY) / scale)).ToArray()
        };
        return Project(best);
    }

    private static float[] WarpRoi(ReadOnlySpan<byte> rgb, int width, int height, FaceRoi roi)
    {
        var tensor = new DenseTensor<float>([1, 3, FaceGeometry.LandmarkInputSize, FaceGeometry.LandmarkInputSize]);
        var cos = Math.Cos(roi.Angle);
        var sin = Math.Sin(roi.Angle);
        for (var y = 0; y < FaceGeometry.LandmarkInputSize; y++)
        for (var x = 0; x < FaceGeometry.LandmarkInputSize; x++)
        {
            var cropX = ((x + 0.5) / FaceGeometry.LandmarkInputSize - 0.5) * roi.Side;
            var cropY = ((y + 0.5) / FaceGeometry.LandmarkInputSize - 0.5) * roi.Side;
            var sourceX = roi.CenterX + cropX * cos - cropY * sin;
            var sourceY = roi.CenterY + cropX * sin + cropY * cos;
            var clampedX = Math.Clamp(sourceX, 0, width - 1);
            var clampedY = Math.Clamp(sourceY, 0, height - 1);
            var offset = ((int)Math.Round(clampedY) * width + (int)Math.Round(clampedX)) * 3;
            tensor[0, 0, y, x] = rgb[offset] / 255f;
            tensor[0, 1, y, x] = rgb[offset + 1] / 255f;
            tensor[0, 2, y, x] = rgb[offset + 2] / 255f;
        }
        return tensor.ToArray();
    }

    private (FaceLandmark[] Landmarks, double Presence) RunLandmarks(float[] crop)
    {
        var tensor = new DenseTensor<float>(crop, [1, 3, FaceGeometry.LandmarkInputSize, FaceGeometry.LandmarkInputSize]);
        using var results = landmarks.Run([NamedOnnxValue.CreateFromTensor(landmarksInput, tensor)]);
        var raw = results.First().AsEnumerable<float>().ToArray();
        var presence = 1.0 / (1.0 + Math.Exp(-Math.Clamp(results.First(item => item.Name.Contains("Identity_1")).AsEnumerable<float>().First(), -100, 100)));
        var output = new FaceLandmark[FaceGeometry.LandmarkCount];
        for (var index = 0; index < output.Length; index++)
            output[index] = new FaceLandmark(raw[index * 3], raw[index * 3 + 1], raw[index * 3 + 2]);
        return (output, presence);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        detector.Dispose();
        landmarks.Dispose();
    }
}
