using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NextThought.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NextThought.Platform;

/// <summary>Local, bounded readers. Office is parsed as data; macros and external links are never executed.</summary>
public sealed class DocumentReader(EnhancedOcrService? enhancedOcr = null)
{
    public static readonly string[] Extensions = [".pdf", ".docx", ".pptx", ".xlsx", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".txt", ".md"];
    private const int MaxBlocks = 40000;
    public async Task<(List<DocumentBlock> Blocks, List<string> Warnings)> ReadAsync(string path, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (info.Length > 200L * 1024 * 1024) throw new InvalidDataException("资料超过 200 MB，请拆分后导入。");
        var ext = info.Extension.ToLowerInvariant();
        if (!Extensions.Contains(ext)) throw new NotSupportedException("支持 PDF、DOCX、PPTX、XLSX、图片、TXT 和 Markdown；旧版 Office 请先另存为新版格式。");
        var blocks = new List<DocumentBlock>(); var warnings = new List<string>();
        if (ext == ".pdf")
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(path);
            if (pdf.NumberOfPages > 1500) throw new InvalidDataException("PDF 超过 1500 页，请拆分导入。");
            if (enhancedOcr != null && pdf.NumberOfPages > 10) throw new InvalidDataException("增强 OCR 的 CPU 模式每次最多 10 页，请拆分 PDF，或取消增强选项后使用系统 OCR。");
            global::Windows.Data.Pdf.PdfDocument? rendered = null;
            var pageTexts = new List<string>(pdf.NumberOfPages);
            for (var page = 1; page <= pdf.NumberOfPages; page++) pageTexts.Add(ContentOrderTextExtractor.GetText(pdf.GetPage(page)));
            var furniture = NextThought.Core.PDFTextLayout.Furniture(pageTexts);
            var prose = NextThought.Core.PDFTextLayout.IsProse(pageTexts);
            for (var i = 1; i <= pdf.NumberOfPages; i++)
            {
                ct.ThrowIfCancellationRequested();
                var text = NextThought.Core.PDFTextLayout.Markdown(pageTexts[i - 1], furniture, prose).Trim();
                if (text.Length < 10)
                {
                    rendered ??= await global::Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(path).AsTask(ct)).AsTask(ct);
                    using var page = rendered.GetPage((uint)i - 1);
                    using var stream = new InMemoryRandomAccessStream();
                    var scale = Math.Min(2.5, 2400 / Math.Max(page.Size.Width, page.Size.Height));
                    await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = (uint)(page.Size.Width * scale), DestinationHeight = (uint)(page.Size.Height * scale) }).AsTask(ct);
                    if (enhancedOcr != null)
                    {
                        var temporary = Path.Combine(Path.GetTempPath(), "nt-ocr-page-" + Guid.NewGuid().ToString("N") + ".png");
                        try
                        {
                            stream.Seek(0); await using (var file = File.Create(temporary)) await stream.AsStreamForRead().CopyToAsync(file, ct);
                            blocks.AddRange(await enhancedOcr.ReadAsync(temporary, i, ct));
                            warnings.Add($"第 {i} 页使用 PaddleOCR-VL CPU 增强识别；结果可能有误，请对照原页。");
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                    else { text = await OcrAsync(stream, ct); warnings.Add($"第 {i} 页使用系统 OCR；请对照原页核对。"); Add($"第 {i} 页", text, i, "ocr"); }
                }
                else Add($"第 {i} 页", text, i);
            }
        }
        else if (ext is ".docx" or ".pptx" or ".xlsx")
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Count > 20000 || zip.Entries.Sum(e => e.Length) > 512L * 1024 * 1024) throw new InvalidDataException("Office 解压内容过大。");
            XDocument Xml(string name)
            {
                var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"Office 缺少 {name}");
                if (entry.Length > 32L * 1024 * 1024) throw new InvalidDataException("Office XML 内容过大。");
                using var input = entry.Open();
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32L * 1024 * 1024 });
                return XDocument.Load(reader);
            }
            if (ext == ".docx")
            {
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var document = Xml("word/document.xml"); int paragraph = 0, table = 0;
                foreach (var element in document.Root!.Element(w + "body")!.Elements())
                {
                    ct.ThrowIfCancellationRequested();
                    if (element.Name == w + "p") Add($"段落 {++paragraph}", string.Concat(element.Descendants(w + "t").Select(t => t.Value)));
                    else if (element.Name == w + "tbl")
                    {
                        table++; var row = 0;
                        foreach (var tr in element.Elements(w + "tr"))
                        {
                            row++; var col = 0;
                            foreach (var cell in tr.Elements(w + "tc")) Add($"表 {table} 行 {row} 列 {++col}", string.Join("\n", cell.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)))));
                        }
                    }
                }
                warnings.Add("Word 按正文段落和表格定位；未计算分页，不读取批注、修订删除、嵌入对象或文本框布局。");
            }
            else if (ext == ".pptx")
            {
                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var relationships = Xml("ppt/_rels/presentation.xml.rels").Root!.Elements().ToDictionary(x => x.Attribute("Id")!.Value, x => x.Attribute("Target")!.Value);
                int slide = 0;
                foreach (var id in Xml("ppt/presentation.xml").Descendants().Where(e => e.Name.LocalName == "sldId"))
                {
                    ct.ThrowIfCancellationRequested(); slide++;
                    var target = ResolvePart("ppt/presentation.xml", relationships[id.Attribute(r + "id")!.Value]);
                    Add($"幻灯片 {slide}", string.Join("\n", Xml(target).Descendants(a + "p").Select(p => string.Concat(p.Descendants(a + "t").Select(t => t.Value)))), slide);
                }
                warnings.Add("PPT 按幻灯片定位；提取文字和表格，不解析图表、SmartArt、备注或图片中的文字。");
            }
            else
            {
                XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(s + "si").Select(x => string.Concat(x.Descendants(s + "t").Select(t => t.Value))).ToArray();
                var rels = Xml("xl/_rels/workbook.xml.rels").Root!.Elements().Where(x => (string?)x.Attribute("TargetMode") != "External").ToDictionary(x => x.Attribute("Id")!.Value, x => x.Attribute("Target")!.Value);
                foreach (var sheet in Xml("xl/workbook.xml").Descendants(s + "sheet"))
                {
                    var name = sheet.Attribute("name")!.Value;
                    foreach (var cell in Xml(ResolvePart("xl/workbook.xml", rels[sheet.Attribute(r + "id")!.Value])).Descendants(s + "c"))
                    {
                        ct.ThrowIfCancellationRequested();
                        var value = (string?)cell.Element(s + "v") ?? "";
                        var type = (string?)cell.Attribute("t");
                        if (type == "s") value = int.TryParse(value, out var index) && index >= 0 && index < shared.Length ? shared[index] : "";
                        else if (type == "inlineStr") value = string.Concat(cell.Descendants(s + "t").Select(t => t.Value));
                        if (cell.Element(s + "f") is not null) value += "（公式缓存值）";
                        Add($"工作表「{name}」!{(string?)cell.Attribute("r")}", value);
                    }
                }
                warnings.Add("Excel 按工作表和单元格定位；公式使用文件缓存结果，不重算。日期保留底层数值，图片及外部链接不读取。");
            }
        }
        else if (ext is ".txt" or ".md")
        {
            var text = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), ct);
            int i = 0; foreach (var line in text.Split('\n')) Add($"行 {++i}", line.TrimEnd('\r'));
        }
        else
        {
            if (enhancedOcr != null) { blocks.AddRange(await enhancedOcr.ReadAsync(path, 1, ct)); warnings.Add("PaddleOCR-VL CPU 增强识别，仅处理首帧；表格保留 HTML、公式保留 LaTeX，结果需对照原图。"); }
            else { using var input = await (await StorageFile.GetFileFromPathAsync(path).AsTask(ct)).OpenReadAsync().AsTask(ct);
                Add("图片 1", await OcrAsync(input, ct), 1, "ocr"); warnings.Add("使用 Windows 系统 OCR，复杂版面、手写或低清图片需人工核对；多页 TIFF 仅读取首帧。"); }
        }
        if (blocks.Count == 0) warnings.Add("未提取到可读文字；可能是空文件、缺少 OCR 语言包或受保护内容。");
        return (blocks, warnings);

        void Add(string location, string text, int? page = null, string kind = "text")
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (blocks.Count >= MaxBlocks) throw new InvalidDataException("资料段落过多，请拆分导入。");
            blocks.Add(new(location, text.Trim(), page, kind));
        }
    }

    private static string ResolvePart(string source, string target)
    {
        var uri = new Uri(new Uri("http://package/" + source), target);
        if (uri.Host != "package") throw new InvalidDataException("不读取 Office 外部链接。");
        return uri.AbsolutePath.TrimStart('/');
    }

    private static async Task<string> OcrAsync(IRandomAccessStream stream, CancellationToken ct)
    {
        var engine = OcrEngine.TryCreateFromLanguage(new Language("zh-Hans")) ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null) throw new NotSupportedException("系统未安装可用的 Windows OCR 语言。请在 Windows 语言选项中安装中文或英文 OCR。");
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        var scale = Math.Min(1, (double)OcrEngine.MaxImageDimension / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)) },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(ct);
        var result = await engine.RecognizeAsync(bitmap).AsTask(ct);
        return string.Join("\n", result.Lines.Select(x => x.Text));
    }
}
