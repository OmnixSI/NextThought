using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace NextThought.Platform;

/// <summary>
/// E9: one already-captured frame to text. The caller supplies PNG bytes kept in memory;
/// this class never writes the frame to disk or to the material library.
/// </summary>
public static class ScreenOcrReader
{
    public static async Task<string> ReadAsync(byte[] encodedImage, CancellationToken ct = default)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("zh-Hans")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null) throw new NotSupportedException("系统未安装可用的 Windows OCR 语言。");
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(encodedImage.AsBuffer()).AsTask(ct);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        var scale = Math.Min(1, (double)OcrEngine.MaxImageDimension / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)) },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(ct);
        var result = await engine.RecognizeAsync(bitmap).AsTask(ct);
        return string.Join("\n", result.Lines.Select(line => line.Text).Where(text => text.Length > 0));
    }
}
