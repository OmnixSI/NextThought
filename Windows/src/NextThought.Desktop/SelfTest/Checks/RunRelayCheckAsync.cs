using CompanionSnapshot = NextThought.Platform.CompanionSnapshot;
namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunRelayCheckAsync(string path)
    {
        await Task.Delay(300);
        var relayUrl = Environment.GetEnvironmentVariable("NEXTTHOUGHT_RELAY_URL") ?? "http://127.0.0.1:8787";
        var pairingFile = path + ".pairing";
        var handled = new System.Collections.Concurrent.ConcurrentBag<string>();
        CompanionRelayClient? client = null;
        var passed = false;
        var error = "";
        var room = "";
        var status = "";
        try
        {
            client = new CompanionRelayClient(
                () => new CompanionSnapshot("中转自检", "手机上看到的问题", "手机上看到的回答", false, "", "1/3"),
                action => { handled.Add(action); return Task.FromResult(action == "next"); },
                message => Console.WriteLine("relay: " + message));
            var pairing = await client.StartAsync(new Uri(relayUrl), new CompanionPairing(), allowLoopback: true);
            room = pairing.Room;
            var directory = Path.GetDirectoryName(Path.GetFullPath(pairingFile));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(pairingFile, System.Text.Json.JsonSerializer.Serialize(pairing, CompanionCrypto.Json));

            var deadline = DateTime.UtcNow.AddSeconds(20);
            var tick = 0;
            while (DateTime.UtcNow < deadline && !handled.Contains("next"))
            {
                client.Publish(new CompanionSnapshot("中转自检", "第 " + (++tick) + " 次状态", "回答内容", false, "", "1/3"));
                await Task.Delay(400);
            }
            status = client.StatusText;
            passed = handled.Contains("next");
            if (!passed) error = "20 秒内没有收到模拟手机的命令（中转或加密层可能不匹配）。";
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            if (client is not null)
            {
                try { await client.StopAsync(); } catch { }
            }
        }
        var report = new
        {
            passed,
            relay = relayUrl,
            room,
            commandHandled = handled.Contains("next"),
            status,
            error,
            pairingFile
        };
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch { }
        Console.WriteLine(passed ? "relay-check: passed" : "relay-check: failed");
        Close();
    }

    /// <summary>
    /// In-app update check: read the release feed, download the installer, verify its SHA-256,
    /// and record the silent-install command instead of running it (QA switch).
    /// </summary>
}
