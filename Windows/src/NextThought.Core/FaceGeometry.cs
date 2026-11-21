namespace NextThought.Core;

/// <summary>One SSDA anchor as MediaPipe's SsdAnchorsCalculator emits it (normalized to the tensor).</summary>
public readonly record struct FaceAnchor(double XCenter, double YCenter, double Width, double Height);

/// <summary>One detected face in image pixels (x right, y down).</summary>
public sealed record FaceDetection(
    double Score,
    double Left,
    double Top,
    double Right,
    double Bottom,
    IReadOnlyList<GazePoint> Keypoints)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
    public double CenterX => (Left + Right) / 2;
    public double CenterY => (Top + Bottom) / 2;
}

/// <summary>
/// The square, rotated crop of the camera frame that the landmark model reads. Center and side
/// live in image pixels; <see cref="Angle"/> is the roll of the face (eye line in radians, positive
/// clockwise on screen). Crop pixel (u, v) in <c>[0,1]²</c> maps to image
/// <c>center + Rotate((u - 0.5) * side, (v - 0.5) * side)</c>.
/// </summary>
public sealed record FaceRoi(double CenterX, double CenterY, double Side, double Angle);

/// <summary>One FaceMesh landmark in the image (x right, y down, both in pixels; z is relative).</summary>
public readonly record struct FaceLandmark(double X, double Y, double Z);

/// <summary>
/// Pure geometry of the Windows face landmarker: the BlazeFace short-range anchors and decode
/// (MediaPipe SsdAnchorsCalculator / TensorsToDetections), greedy NMS, the rotated 1.5x ROI the
/// FaceMesh model reads, and the MediaPipe FaceMesh landmark groups mapped into the
/// <see cref="GazeEstimator"/> input (eye outline + pupil). No I/O, no ONNX here, so it is testable
/// without models or a camera.
///
/// The landmark indices and the anchor recipe are MediaPipe's (Apache-2.0); see
/// Windows/assets/face-landmarker/README.md and Windows/validation/e10/conversion.md.
/// </summary>
public static class FaceGeometry
{
    /// <summary>The face detector tensor is 128×128; its model emits 896 anchors.</summary>
    public const int DetectorInputSize = 128;

    /// <summary>The landmark model tensor is 256×256.</summary>
    public const int LandmarkInputSize = 256;

    public const int DetectorAnchorCount = 896;
    public const int LandmarkCount = 478;

    /// <summary>MediaPipe FaceMesh V2 eye outlines (six points per eye; side does not matter for ratios).</summary>
    public static readonly int[] LeftEyeContour = [362, 385, 387, 263, 373, 380];
    public static readonly int[] RightEyeContour = [33, 160, 158, 133, 153, 144];

    /// <summary>MediaPipe FaceMesh V2 iris rings; their mean is the pupil point.</summary>
    public static readonly int[] LeftIris = [468, 469, 470, 471, 472];
    public static readonly int[] RightIris = [473, 474, 475, 476, 477];

    // MediaPipe face_detection_short_range defaults (face_detector_graph.cc): num_layers 4,
    // strides 8,16,16,16, aspect ratio 1.0, interpolated 1.0, fixed_anchor_size true.
    private static readonly int[] Strides = [8, 16, 16, 16];

    private static readonly FaceAnchor[] AnchorTable = GenerateAnchors();

    /// <summary>The 896 anchors in model output order.</summary>
    public static IReadOnlyList<FaceAnchor> Anchors => AnchorTable;

    private static FaceAnchor[] GenerateAnchors()
    {
        // Mirrors SsdAnchorsCalculator::GenerateAnchors for the options in
        // mediapipe/tasks/cc/vision/face_detector/face_detector_graph.cc: layers with the same
        // stride are merged, each layer contributes the aspect-ratio-1 anchor and the interpolated
        // anchor, and fixed_anchor_size makes every anchor a unit square at a grid centre. Layer 0
        // (stride 8) therefore has 2 anchors per 16x16 cell and layers 1-3 (stride 16) share one
        // 8x8 grid with 6 anchors per cell: 512 + 384 = 896.
        var anchors = new List<FaceAnchor>(DetectorAnchorCount);
        var layer = 0;
        while (layer < Strides.Length)
        {
            var dimensions = (int)Math.Ceiling(DetectorInputSize / (double)Strides[layer]);
            var lastSameStride = layer;
            var perCell = 0;
            while (lastSameStride < Strides.Length && Strides[lastSameStride] == Strides[layer])
            {
                perCell += 2; // the 1.0 aspect-ratio anchor plus the interpolated one
                lastSameStride++;
            }
            for (var y = 0; y < dimensions; y++)
            for (var x = 0; x < dimensions; x++)
            for (var anchor = 0; anchor < perCell; anchor++)
                anchors.Add(new FaceAnchor((x + 0.5) / dimensions, (y + 0.5) / dimensions, 1.0, 1.0));
            layer = lastSameStride;
        }
        if (anchors.Count != DetectorAnchorCount)
            throw new InvalidOperationException($"anchor generation produced {anchors.Count}, expected {DetectorAnchorCount}");
        return [.. anchors];
    }

    /// <summary>
    /// Decodes the BlazeFace outputs. <paramref name="regressors"/> is 896×16 row-major,
    /// <paramref name="scores"/> 896 raw logits. Raw order is MediaPipe's reverse output order
    /// (y_center, x_center, h, w, then six keypoints), scores are sigmoid-ed and thresholded.
    /// </summary>
    public static List<FaceDetection> Decode(
        IReadOnlyList<float> regressors,
        IReadOnlyList<float> scores,
        int imageWidth,
        int imageHeight,
        double minScore = 0.5)
    {
        if (regressors.Count < DetectorAnchorCount * 16)
            throw new ArgumentException("regressors must hold 896x16 values", nameof(regressors));
        if (scores.Count < DetectorAnchorCount)
            throw new ArgumentException("scores must hold 896 values", nameof(scores));

        var detections = new List<FaceDetection>();
        for (var box = 0; box < DetectorAnchorCount; box++)
        {
            var logit = Math.Clamp(scores[box], -100.0, 100.0);
            var score = 1.0 / (1.0 + Math.Exp(-logit));
            if (score < minScore) continue;
            var anchor = AnchorTable[box];
            var offset = box * 16;
            double Raw(int index) => regressors[offset + index] / DetectorInputSize;
            // reverse_output_order: the model emits (y_center, x_center, h, w).
            var centerY = Raw(0) * anchor.Height + anchor.YCenter;
            var centerX = Raw(1) * anchor.Width + anchor.XCenter;
            var height = Raw(2) * anchor.Height;
            var width = Raw(3) * anchor.Width;
            var keypoints = new GazePoint[6];
            for (var point = 0; point < 6; point++)
            {
                var keypointY = regressors[offset + 4 + point * 2] / DetectorInputSize * anchor.Height + anchor.YCenter;
                var keypointX = regressors[offset + 5 + point * 2] / DetectorInputSize * anchor.Width + anchor.XCenter;
                keypoints[point] = new GazePoint(keypointX * imageWidth, keypointY * imageHeight);
            }
            detections.Add(new FaceDetection(
                score,
                (centerX - width / 2) * imageWidth,
                (centerY - height / 2) * imageHeight,
                (centerX + width / 2) * imageWidth,
                (centerY + height / 2) * imageHeight,
                keypoints));
        }
        return detections;
    }

    /// <summary>Greedy NMS at MediaPipe's 0.3 suppression threshold; largest/highest score first.</summary>
    public static List<FaceDetection> NonMaximumSuppression(
        IEnumerable<FaceDetection> detections,
        double intersectionOverUnion = 0.3)
    {
        var order = detections.OrderByDescending(detection => detection.Score).ToList();
        var kept = new List<FaceDetection>();
        foreach (var candidate in order)
        {
            if (kept.All(other => IntersectionOverUnion(candidate, other) <= intersectionOverUnion))
                kept.Add(candidate);
        }
        return kept;
    }

    public static double IntersectionOverUnion(FaceDetection left, FaceDetection right)
    {
        var x = Math.Max(0, Math.Min(left.Right, right.Right) - Math.Max(left.Left, right.Left));
        var y = Math.Max(0, Math.Min(left.Bottom, right.Bottom) - Math.Max(left.Top, right.Top));
        var intersection = x * y;
        var union = left.Width * left.Height + right.Width * right.Height - intersection;
        return union <= 1e-9 ? 0 : intersection / union;
    }

    /// <summary>
    /// The rotated square crop the landmark model reads: 1.5× the detected face, centred on the
    /// detection and rolled so the eye line is horizontal. MediaPipe's face detector names
    /// keypoint 0 the left eye and keypoint 1 the right eye (observer's point of view), and its
    /// DetectionsToRectsCalculator rotates that vector onto angle 0.
    /// </summary>
    public static FaceRoi Roi(FaceDetection detection, double scale = 1.5)
    {
        var leftEye = detection.Keypoints[0];
        var rightEye = detection.Keypoints[1];
        var angle = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X);
        var side = Math.Max(1e-3, scale * Math.Max(detection.Width, detection.Height));
        return new FaceRoi(detection.CenterX, detection.CenterY, side, angle);
    }

    /// <summary>Maps crop-normalized landmark coordinates produced by the FaceMesh model to image pixels.</summary>
    public static FaceLandmark[] ProjectLandmarks(IReadOnlyList<FaceLandmark> cropLandmarks, FaceRoi roi)
    {
        var cos = Math.Cos(roi.Angle);
        var sin = Math.Sin(roi.Angle);
        var projected = new FaceLandmark[cropLandmarks.Count];
        for (var index = 0; index < cropLandmarks.Count; index++)
        {
            var landmark = cropLandmarks[index];
            var x = (landmark.X - 0.5) * roi.Side;
            var y = (landmark.Y - 0.5) * roi.Side;
            projected[index] = new FaceLandmark(
                roi.CenterX + x * cos - y * sin,
                roi.CenterY + x * sin + y * cos,
                landmark.Z * roi.Side);
        }
        return projected;
    }

    /// <summary>Builds the <see cref="GazeEstimator"/> frame from 478 image-space landmarks.</summary>
    public static GazeFrame? GazeFrame(IReadOnlyList<FaceLandmark> landmarks, double time)
    {
        if (landmarks.Count < LandmarkCount) return null;
        var left = Eye(landmarks, RightEyeContour, RightIris);
        var right = Eye(landmarks, LeftEyeContour, LeftIris);
        if (left is null || right is null) return null;
        // MediaPipe gives no head pose; the eye ratios carry the gaze signal, and the calibration
        // learns the user's own resting pose. yaw/pitch therefore stay null.
        return new GazeFrame(left, right, null, null, time);
    }

    private static EyeGeometry? Eye(IReadOnlyList<FaceLandmark> landmarks, int[] contour, int[] iris)
    {
        var points = new GazePoint[contour.Length];
        for (var index = 0; index < contour.Length; index++)
        {
            var landmark = landmarks[contour[index]];
            points[index] = new GazePoint(landmark.X, landmark.Y);
        }
        double x = 0, y = 0;
        foreach (var index in iris)
        {
            x += landmarks[index].X;
            y += landmarks[index].Y;
        }
        return new EyeGeometry(points, new GazePoint(x / iris.Length, y / iris.Length));
    }
}
