using NextThought.Core;

namespace NextThought.Platform;

/// <summary>A downloaded installer that passed SHA-256 verification.</summary>
public sealed record PreparedInstaller(string Path, string Sha256);

/// <summary>
/// F16: downloads an update installer and verifies its SHA-256 before anything is started
/// (port of Updater.downloadUpdate's download + checksum step). Works for https releases and
/// for the file paths a QA feed points at, so the whole discover → download → verify flow can
/// run against a local fake release directory.
/// </summary>
public static class UpdateInstaller
{
    public static async Task<PreparedInstaller> PrepareAsync(
        string setupUrl, string setupName, string checksumsUrl, string directory,
        IProgress<double>? progress = null, string? token = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setupUrl)) throw new InvalidOperationException("发布里没有安装包下载地址。");
        if (string.IsNullOrWhiteSpace(setupName)) throw new InvalidOperationException("发布里没有安装包文件名。");
        if (string.IsNullOrWhiteSpace(checksumsUrl)) throw new InvalidOperationException("发布里没有校验值文件。");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Path.GetFileName(setupName));
        try
        {
            await DownloadAsync(setupUrl, target, progress, token, cancellationToken);
            return new PreparedInstaller(target, await VerifyAsync(target, checksumsUrl, token, cancellationToken));
        }
        catch
        {
            try { if (File.Exists(target)) File.Delete(target); } catch { }
            throw;
        }
    }

    /// <summary>Re-checks a previously downloaded installer against the release's checksum file.</summary>
    public static async Task<string> VerifyAsync(string installerPath, string checksumsUrl, string? token = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(installerPath)) throw new InvalidOperationException("安装包不存在，请重新下载。");
        var checksums = await UpdateFeedPolicy.ReadAsync(checksumsUrl, token, cancellationToken);
        var expected = UpdateChecksums.Find(checksums, Path.GetFileName(installerPath))
                       ?? throw new InvalidOperationException("发布里没有这个安装包的校验值。");
        if (!UpdateChecksums.Verify(installerPath, expected))
        {
            try { File.Delete(installerPath); } catch { }
            throw new InvalidOperationException("安装包校验失败，已删除下载文件。");
        }
        return expected.ToLowerInvariant();
    }

    /// <summary>HTTP(S) streaming download, or a byte copy when the QA feed points at a local file.</summary>
    private static async Task DownloadAsync(string url, string target, IProgress<double>? progress, string? token, CancellationToken ct)
    {
        var partial = target + ".part";
        if (File.Exists(partial)) File.Delete(partial);
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NextThought-Windows/0.5");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim());
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(partial);
            await CopyAsync(source, file, total, progress, ct);
        }
        else
        {
            var origin = url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(url).LocalPath
                : url;
            await using var source = File.OpenRead(origin);
            var total = source.Length;
            await using var file = File.Create(partial);
            await CopyAsync(source, file, total, progress, ct);
        }
        File.Move(partial, target, overwrite: true);
    }

    private static async Task CopyAsync(Stream source, Stream target, long total, IProgress<double>? progress, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            if (total > 0) progress?.Report(Math.Round((double)done / total, 3));
        }
        progress?.Report(1);
    }
}
