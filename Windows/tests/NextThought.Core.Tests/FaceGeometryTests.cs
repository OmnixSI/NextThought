using System.Numerics;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// E10: pure geometry of the MediaPipe face landmarker (MediaPipe Apache-2.0). The anchor recipe
/// and the eye/iris index groups are pinned here so the ONNX runner can only feed the model in the
/// layout the conversion evidence describes.
/// </summary>
public sealed class FaceGeometryTests
{
    private const double Epsilon = 1e-9;

    [Fact]
    public void TheAnchorTableHasMediaPipes896BoxesInLayerOrder()
    {
        var anchors = FaceGeometry.Anchors;
        Assert.Equal(896, anchors.Count);

        // fixed_anchor_size: every anchor is a unit square at a feature-map cell centre.
        var first = anchors[0];
        Assert.Equal(0.03125, first.XCenter, 12); // (0 + 0.5) / 16
        Assert.Equal(0.03125, first.YCenter, 12);
        Assert.Equal(1.0, first.Width, 12);
        Assert.Equal(1.0, first.Height, 12);

        // The stride-16 group starts at 16*16*2 = 512 and gives 6 anchors per 8x8 cell.
        var stride16 = anchors[512];
        Assert.Equal(0.0625, stride16.XCenter, 12); // (0 + 0.5) / 8
        Assert.Equal(1.0, stride16.Width, 12);
        Assert.Equal(1.0, anchors[517].Width, 12);
        // The first layer ends at the last 16x16 cell, so the stride changes exactly at 512.
        Assert.Equal(15.5 / 16, anchors[511].XCenter, 12);
        Assert.Equal(15.5 / 16, anchors[511].YCenter, 12);
        Assert.Equal(7.5 / 8, anchors[^1].XCenter, 12);
        Assert.Equal(7.5 / 8, anchors[^1].YCenter, 12);
    }

    private static (List<float> Regressors, List<float> Scores) BlankOutputs()
    {
        var regressors = Enumerable.Repeat(0f, 896 * 16).ToList();
        var scores = Enumerable.Repeat(-100f, 896).ToList();
        return (regressors, scores);
    }

    [Fact]
    public void DecodeUsesTheReverseOutputOrderAndSigmoidScore()
    {
        var (regressors, scores) = BlankOutputs();
        scores[0] = (float)Math.Log(9 / 1.0); // sigmoid = 0.9
        // reverse order: y_center, x_center, h, w offsets (all zero -> box collapses to anchor centre)
        var detections = FaceGeometry.Decode(regressors, scores, imageWidth: 640, imageHeight: 480);

        var detection = Assert.Single(detections);
        Assert.Equal(0.9, detection.Score, 6);
        Assert.Equal(0.03125 * 640, detection.CenterX, 6);
        Assert.Equal(0.03125 * 480, detection.CenterY, 6);
        Assert.Equal(0, detection.Width, 9);
        Assert.Equal(0, detection.Height, 9);
        Assert.All(detection.Keypoints, point =>
        {
            Assert.Equal(0.03125 * 640, point.X, 6);
            Assert.Equal(0.03125 * 480, point.Y, 6);
        });
    }

    [Fact]
    public void DecodeConvertsRawOffsetsIntoPixels()
    {
        var (regressors, scores) = BlankOutputs();
        scores[0] = 10;
        var anchor = FaceGeometry.Anchors[0];
        var offset = 0;
        regressors[offset + 0] = 64;  // y_center offset -> +0.5 tensor pixels (unit anchor)
        regressors[offset + 1] = 32;  // x_center offset -> +0.25 tensor pixels
        regressors[offset + 2] = 64;  // h -> 0.5 tensor pixels
        regressors[offset + 3] = 32;  // w -> 0.25 tensor pixels

        // The detector tensor is 128x128, so one tensor pixel is one pixel here.
        var detection = Assert.Single(FaceGeometry.Decode(regressors, scores, 128, 128));
        Assert.Equal(anchor.XCenter * 128 + 32, detection.CenterX, 6);
        Assert.Equal(anchor.YCenter * 128 + 64, detection.CenterY, 6);
        Assert.Equal(32, detection.Width, 6);
        Assert.Equal(64, detection.Height, 6);

        // Keypoints use the same anchor mapping; keypoint 1 is one tensor pixel down and right.
        regressors[offset + 4] = 128;
        regressors[offset + 5] = 128;
        var withKeypoint = Assert.Single(FaceGeometry.Decode(regressors, scores, 128, 128));
        Assert.Equal(anchor.YCenter * 128 + 128, withKeypoint.Keypoints[0].Y, 6);
        Assert.Equal(anchor.XCenter * 128 + 128, withKeypoint.Keypoints[0].X, 6);
    }

    [Fact]
    public void DecodeDropsScoresBelowTheThreshold()
    {
        var (regressors, scores) = BlankOutputs();
        scores[0] = (float)Math.Log(0.49 / 0.51); // sigmoid < 0.5
        scores[1] = (float)Math.Log(0.51 / 0.49);
        var detections = FaceGeometry.Decode(regressors, scores, 640, 480);
        Assert.Single(detections);
        Assert.Equal(0.51, detections[0].Score, 3);
    }

    [Fact]
    public void NonMaximumSuppressionKeepsTheBestOfOverlappingBoxes()
    {
        static FaceDetection Box(double score, double left) =>
            new(score, left, 10, left + 100, 110, []);
        var kept = FaceGeometry.NonMaximumSuppression(
        [
            Box(0.6, 0),
            Box(0.9, 50), // overlaps the first one heavily
            Box(0.7, 400)
        ]);
        Assert.Equal(2, kept.Count);
        Assert.Equal(0.9, kept[0].Score);
        Assert.Equal(0.7, kept[1].Score);
    }

    [Fact]
    public void RoiCentresOnTheDetectionAndRollsTheEyeLineHorizontal()
    {
        var detection = new FaceDetection(0.9, 100, 100, 200, 220,
        [
            new GazePoint(120, 150), // right eye
            new GazePoint(180, 150), // left eye (same height -> level)
            new GazePoint(150, 170), new GazePoint(150, 190),
            new GazePoint(110, 160), new GazePoint(190, 160)
        ]);
        var roi = FaceGeometry.Roi(detection);
        Assert.Equal(150, roi.CenterX, 9);
        Assert.Equal(160, roi.CenterY, 9);
        Assert.Equal(1.5 * 120, roi.Side, 9);
        Assert.Equal(0, roi.Angle, 9);

        var tilted = detection with
        {
            Keypoints =
            [
                new GazePoint(120, 150), // left eye
                new GazePoint(180, 174), // right eye 24 px lower over 60 px
                new GazePoint(150, 170), new GazePoint(150, 190),
                new GazePoint(110, 160), new GazePoint(190, 160)
            ]
        };
        Assert.Equal(Math.Atan2(24, 60), FaceGeometry.Roi(tilted).Angle, 9);
    }

    [Fact]
    public void ProjectLandmarksUnrotatesTheCropAroundItsCentre()
    {
        var roi = new FaceRoi(300, 200, 120, 0);
        // (0.75, 0.5) is a quarter crop to the right of the crop centre: (0.75-0.5)*120 = +30 px.
        var projected = FaceGeometry.ProjectLandmarks([new FaceLandmark(0.75, 0.5, 0)], roi);
        Assert.Equal(330, projected[0].X, 9);
        Assert.Equal(200, projected[0].Y, 9);

        var rotated = new FaceRoi(300, 200, 120, Math.PI / 2);
        var rolled = FaceGeometry.ProjectLandmarks([new FaceLandmark(0.75, 0.5, 0)], rotated);
        // Positive roll turns "right" into "down" on screen.
        Assert.Equal(300, rolled[0].X, 9);
        Assert.Equal(230, rolled[0].Y, 9);
    }

    private static FaceLandmark[] EyeLandmarks(
        (double Left, double Top, double Right, double Bottom) rightEye,
        GazePoint rightPupil,
        (double Left, double Top, double Right, double Bottom) leftEye,
        GazePoint leftPupil)
    {
        var landmarks = new FaceLandmark[FaceGeometry.LandmarkCount];
        for (var index = 0; index < landmarks.Length; index++) landmarks[index] = new FaceLandmark(0, 0, 0);

        void Fill(int[] contour, (double Left, double Top, double Right, double Bottom) box)
        {
            // A diamond whose leftmost/rightmost points are unique (GazeEstimator picks the two
            // horizontal extremes, so tied ties would make the expected ratio hard to read), plus
            // two interior points that do not extend the span.
            var midX = (box.Left + box.Right) / 2;
            var midY = (box.Top + box.Bottom) / 2;
            var corners = new[]
            {
                new GazePoint(box.Left, midY), new GazePoint(midX, box.Top),
                new GazePoint(box.Right, midY), new GazePoint(midX, box.Bottom),
                new GazePoint(midX, midY - 1), new GazePoint(midX, midY + 1)
            };
            for (var index = 0; index < contour.Length; index++)
                landmarks[contour[index]] = new FaceLandmark(corners[index].X, corners[index].Y, 0);
        }

        void Pupil(int[] iris, GazePoint point)
        {
            foreach (var index in iris) landmarks[index] = new FaceLandmark(point.X, point.Y, 0);
        }

        Fill(FaceGeometry.RightEyeContour, rightEye);
        Pupil(FaceGeometry.RightIris, rightPupil);
        Fill(FaceGeometry.LeftEyeContour, leftEye);
        Pupil(FaceGeometry.LeftIris, leftPupil);
        return landmarks;
    }

    [Fact]
    public void GazeFrameReadsThePupilPositionInsideEachEye()
    {
        var landmarks = EyeLandmarks(
            (100, 100, 140, 120), new GazePoint(125, 110),
            (200, 100, 240, 120), new GazePoint(220, 110));
        var frame = FaceGeometry.GazeFrame(landmarks, time: 3.5);
        Assert.NotNull(frame);
        Assert.Equal(3.5, frame!.Time);
        var signature = GazeEstimator.Signature(frame);
        Assert.NotNull(signature);
        // Right eye pupil is 5 px right of its centre inside a 40 px wide eye (+0.25 half-widths);
        // the left eye pupil is centred, and Signature() averages the two open eyes.
        Assert.Equal(0.125, signature!.X, 6);
        Assert.Equal(0, signature.Y, 6);
        Assert.Null(frame.Yaw);
        Assert.Null(frame.Pitch);
    }

    [Fact]
    public void GazeFrameIgnoresIncompleteLandmarkSets()
    {
        Assert.Null(FaceGeometry.GazeFrame([new FaceLandmark(0, 0, 0)], 0));
    }
}
