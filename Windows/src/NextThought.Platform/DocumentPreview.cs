using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Data.Pdf;

namespace NextThought.Platform;

public static class DocumentPreview
{
    public static async Task<byte[]?> RenderAsync(string path, int page, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".pdf")
        {
            var document = await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(path).AsTask(ct)).AsTask(ct);
            using var pdfPage = document.GetPage((uint)Math.Clamp(page - 1, 0, (int)document.PageCount - 1));
            using var stream = new InMemoryRandomAccessStream();
            var scale = Math.Min(2, 1600 / Math.Max(pdfPage.Size.Width, pdfPage.Size.Height));
            await pdfPage.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = Math.Max(1, (uint)(pdfPage.Size.Width * scale)), DestinationHeight = Math.Max(1, (uint)(pdfPage.Size.Height * scale)) }).AsTask(ct);
            using var output = new MemoryStream(); await stream.AsStreamForRead().CopyToAsync(output, ct); return output.ToArray();
        }
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" ? await File.ReadAllBytesAsync(path, ct) : null;
    }
}
