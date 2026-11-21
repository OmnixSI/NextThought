using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace NextThought.Platform;

/// <summary>
/// E10: decodes a JPEG/PNG file into the RGB24 frame layout the face landmarker reads. Used by the
/// self-test to feed recorded pictures through the same in-memory path as the camera, and by the
/// camera pipeline tests. Frames stay in memory; nothing is written.
/// </summary>
public static class GazeImage
{
    public sealed record RgbFrame(byte[] Rgb, int Width, int Height);

    public static async Task<RgbFrame> LoadAsync(string path, CancellationToken ct = default)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var random = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(random);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var buffer = new Windows.Storage.Streams.Buffer((uint)(width * height * 4));
        bitmap.CopyToBuffer(buffer);
        using var reader = DataReader.FromBuffer(buffer);
        var bgra = new byte[buffer.Length];
        reader.ReadBytes(bgra);
        var rgb = new byte[width * height * 3];
        for (var source = 0; source < bgra.Length; source += 4)
        {
            var target = source / 4 * 3;
            rgb[target] = bgra[source + 2];
            rgb[target + 1] = bgra[source + 1];
            rgb[target + 2] = bgra[source];
        }
        return new RgbFrame(rgb, width, height);
    }
}
