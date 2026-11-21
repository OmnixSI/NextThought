using NextThought.Core;
using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E10: the committed face landmarker assets must load through ONNX Runtime and run a whole frame
/// (detector + FaceMesh) with the layout the conversion evidence describes. A flat frame is not a
/// face, so the only hard expectations are that the models load and that when a detection does come
/// back it carries the 478 landmarks and a gaze frame; the real three-state check runs against the
/// camera in the E10 self-test.
/// </summary>
public sealed class FaceLandmarkerOnnxTests
{
    [Fact]
    public void TheCommittedModelsLoadAndAnalyseAFlatFrame()
    {
        var directory = FaceLandmarker.FindAssetDirectory();
        Assert.NotNull(directory);
        using var landmarker = new FaceLandmarker(directory!);

        var rgb = new byte[640 * 480 * 3];
        Array.Fill(rgb, (byte)128);
        var analysis = landmarker.Analyze(rgb, 640, 480, time: 1.25);
        if (analysis is not null)
        {
            Assert.Equal(FaceGeometry.LandmarkCount, analysis.Landmarks.Count);
            Assert.NotNull(analysis.Gaze);
            Assert.InRange(analysis.FacePresenceScore, 0, 1);
        }
    }

    [Fact]
    public void ShortFramesAreRejected()
    {
        using var landmarker = new FaceLandmarker(FaceLandmarker.FindAssetDirectory()!);
        Assert.Throws<ArgumentException>(() => landmarker.Analyze(new byte[10], 640, 480, 0));
    }
}
