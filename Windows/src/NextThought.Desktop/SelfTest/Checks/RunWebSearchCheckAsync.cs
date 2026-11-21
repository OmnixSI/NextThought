using Avalonia.Controls;
using Avalonia.VisualTree;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// E14: drives the web-verification surfaces on an isolated data directory — key save/clear,
    /// status echo, provider resolution and the verification cache (hit, expiry, invalidation on a
    /// new key, manual clear). Real Tavily calls are not made here (no network in the self-check).
    /// </summary>
    private async Task RunWebSearchCheckAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var steps = new List<object>();
        var passed = false;
        string? error = null;
        void Step(string name, bool ok, string detail) => steps.Add(new { step = name, passed = ok, detail = detail });
        try
        {
            // 1) 密钥状态回显：没有保存密钥时识别为未配置。
            state.SaveTavilyKeyCommand.Execute("");
            Step("未保存密钥时显示未配置", !state.IsWebSearchConfigured && !state.HasStoredWebSearchKey,
                state.WebSearchAvailabilityText);
            Step("状态行回显到设置", state.Settings.TavilyKeyStatus.Length > 0, state.Settings.TavilyKeyStatus);

            // 2) 保存密钥：加密落盘、解析出 Tavily provider、状态行更新。
            state.SaveTavilyKeyCommand.Execute("tvly-selftest-not-a-real-key");
            Step("保存密钥（当前用户加密）并解析 provider",
                state.HasStoredWebSearchKey && state.IsWebSearchConfigured
                && state.WebSearchProviderLabel == "tavily",
                "provider=" + state.WebSearchProviderLabel + "；" + state.WebSearchAvailabilityText);

            // 3) 查证缓存：命中、按问题+provider 区分、过期失效。
            var now = DateTimeOffset.UtcNow;
            var results = new[]
            {
                new WebResult("Kubernetes 1.33 发布说明", "https://kubernetes.io/blog", "摘要一", "2026-01-01", now.ToString("O")),
                new WebResult("版本支持周期", "https://example.com/support", "摘要二")
            };
            state.VerificationCacheForCheck.Store(new WebVerificationEntry(
                "Kubernetes 最新版本？", "tavily", "Kubernetes 最新版本？", results, now));
            var hit = state.VerificationCacheForCheck.TryGet("kubernetes  最新版本？", "tavily", now.AddMinutes(5), out var cached);
            var wrongProvider = state.VerificationCacheForCheck.TryGet("Kubernetes 最新版本？", "codex", now, out _);
            var expired = state.VerificationCacheForCheck.TryGet("Kubernetes 最新版本？", "tavily", now.AddMinutes(31), out _);
            Step("查证缓存：命中/区分 provider/过期失效", hit && cached.Results.Count == 2 && !wrongProvider && !expired,
                "缓存 " + state.WebVerificationCacheCount + " 条；年龄=" + cached.AgeLabel(now.AddMinutes(5)));

            // 4) 保存新密钥会让缓存失效（旧密钥可能属于另一个账号）。
            state.VerificationCacheForCheck.Store(new WebVerificationEntry(
                "另一个问题", "tavily", "另一个问题", results, now));
            state.SaveTavilyKeyCommand.Execute("tvly-selftest-rotated-key");
            Step("保存新密钥后缓存失效", state.WebVerificationCacheCount == 0, "缓存 " + state.WebVerificationCacheCount + " 条");

            // 5) 手动清空缓存命令。
            state.VerificationCacheForCheck.Store(new WebVerificationEntry("清空前", "tavily", "清空前", results, now));
            state.ClearWebVerificationCacheCommand.Execute(null);
            Step("手动清空查证缓存", state.WebVerificationCacheCount == 0, state.Status);

            // 6) 设置页「联网」区块截图（真实网络调用不在此执行）。
            state.Section = AppSection.Settings;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(700);
            var anchor = this.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Name == "AnchorAnswer3");
            anchor?.BringIntoView();
            await Task.Delay(500);
            CaptureVisual(this, Path.Combine(directory, "web-search-settings-light.png"));

            // 7) 清空密钥后恢复未配置状态。
            state.SaveTavilyKeyCommand.Execute("");
            Step("清除密钥后回到未配置", !state.IsWebSearchConfigured && !state.HasStoredWebSearchKey,
                state.WebSearchAvailabilityText);
            Step("真实联网未执行（自动化环境无有效密钥/网络）", true, "Tavily 调用需用户密钥，未在自检中发出请求");
            passed = steps.All(step => (bool)step.GetType().GetProperty("passed")!.GetValue(step)!);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var report = new { passed, error, steps };
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "web-search-check.json"),
                System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch { }
        Console.WriteLine(passed ? "web-search-check: passed" : "web-search-check: failed " + (error ?? ""));
        var idle = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < idle && state.Working) await Task.Delay(100);
        Close();
    }
}
