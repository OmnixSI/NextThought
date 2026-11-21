using NextThought.Core;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// K18: the document component download in three situations — a real download of the smallest
    /// document model (PP-DocLayoutV3, ~126 MB), a cancellation in the middle of the transfer, and
    /// an interrupted download that is retried successfully. Everything goes into throw-away
    /// AppPaths under %TEMP% so the app's own data directory and models are untouched.
    /// </summary>
    private async Task RunDocumentDownloadCheckAsync(string path)
    {
        await Task.Delay(200);
        var report = new Dictionary<string, object?>
        {
            ["test"] = "K18 文档组件真实下载 / 取消 / 中断重试",
            ["date"] = DateTimeOffset.Now.ToString("s"),
            ["model"] = NextThought.Platform.DocumentAssets.Layout.Id,
            ["modelBytes"] = NextThought.Platform.DocumentAssets.Layout.Bytes
        };
        var failures = new List<string>();
        try
        {
            var layout = NextThought.Platform.DocumentAssets.Layout;
            // 1) real download + SHA-256 verification
            var successRoot = Path.Combine(Path.GetTempPath(), "nt-k18-success-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(successRoot);
            var successPaths = new NextThought.Platform.AppPaths(successRoot);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await NextThought.Platform.DocumentAssets.InstallAsync(successPaths, layout,
                new Progress<ModelProgress>(_ => { }), CancellationToken.None);
            watch.Stop();
            var installed = NextThought.Platform.DocumentAssets.IsInstalled(successPaths, layout);
            report["downloadSeconds"] = Math.Round(watch.Elapsed.TotalSeconds, 1);
            report["installed"] = installed;
            if (!installed) failures.Add("真实下载后 IsInstalled 为 false");
            try
            {
                await NextThought.Platform.DocumentAssets.VerifyAsync(successPaths, layout, CancellationToken.None);
                report["sha256Verified"] = true;
            }
            catch (Exception error)
            {
                report["sha256Verified"] = false;
                report["verifyError"] = error.Message;
                failures.Add("SHA-256 校验失败：" + error.Message);
            }

            // 2) cancelled in the middle of the transfer
            var cancelRoot = Path.Combine(Path.GetTempPath(), "nt-k18-cancel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cancelRoot);
            var cancelPaths = new NextThought.Platform.AppPaths(cancelRoot);
            var cancelled = false;
            string cancelError = "";
            try
            {
                using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(900));
                await NextThought.Platform.DocumentAssets.InstallAsync(cancelPaths, layout, null, source.Token);
                cancelError = "下载在 900 ms 内就完成了，没有触发取消";
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception error)
            {
                cancelError = error.GetType().Name + ": " + error.Message;
            }
            var cancelInstalled = NextThought.Platform.DocumentAssets.IsInstalled(cancelPaths, layout);
            report["cancelRequested"] = true;
            report["cancelObserved"] = cancelled;
            report["cancelInstalled"] = cancelInstalled;
            if (!cancelled) failures.Add("取消没有生效：" + cancelError);
            if (cancelInstalled) failures.Add("取消后组件仍然显示已安装（部分文件被当成完整安装）");

            // 3) interrupted download, then a retry that succeeds (the interrupted state must not
            //    stop the next attempt from installing cleanly)
            var retryRoot = Path.Combine(Path.GetTempPath(), "nt-k18-retry-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(retryRoot);
            var retryPaths = new NextThought.Platform.AppPaths(retryRoot);
            var interrupted = false;
            try
            {
                using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(900));
                await NextThought.Platform.DocumentAssets.InstallAsync(retryPaths, layout, null, source.Token);
            }
            catch (OperationCanceledException)
            {
                interrupted = true;
            }
            var afterInterruptInstalled = NextThought.Platform.DocumentAssets.IsInstalled(retryPaths, layout);
            var retryWatch = System.Diagnostics.Stopwatch.StartNew();
            await NextThought.Platform.DocumentAssets.InstallAsync(retryPaths, layout,
                new Progress<ModelProgress>(_ => { }), CancellationToken.None);
            retryWatch.Stop();
            var retryInstalled = NextThought.Platform.DocumentAssets.IsInstalled(retryPaths, layout);
            report["interruptObserved"] = interrupted;
            report["afterInterruptInstalled"] = afterInterruptInstalled;
            report["retryInstalled"] = retryInstalled;
            report["retrySeconds"] = Math.Round(retryWatch.Elapsed.TotalSeconds, 1);
            if (!interrupted) failures.Add("中断模拟没有触发取消");
            if (afterInterruptInstalled) failures.Add("中断后组件错误地显示已安装");
            if (!retryInstalled) failures.Add("中断后重试没有安装成功");

            foreach (var root in new[] { successRoot, cancelRoot, retryRoot })
            {
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }
        catch (Exception error)
        {
            failures.Add(error.ToString());
        }
        report["passed"] = failures.Count == 0;
        report["failures"] = failures;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(report["passed"] is true ? "document-download-check: passed" : "document-download-check: failed");
        Close();
    }
}
