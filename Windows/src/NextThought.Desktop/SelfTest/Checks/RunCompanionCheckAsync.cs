namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunCompanionCheckAsync(string path)
    {
        await Task.Delay(300);
        var actions = new List<string>();
        var error = "";
        var passed = false;
        var unauthorized = 0;
        var duplicate = false;
        var address = "";
        CompanionServer? server = null;
        try
        {
            // Bind the LAN interface when one exists: this exercises the address a phone would use.
            var lan = NextThought.Platform.CompanionServer.Addresses().FirstOrDefault();
            var bind = lan?.Address ?? System.Net.IPAddress.Loopback;
            address = bind.ToString();
            state.SeedCompanionHistoryForCheck();
            server = new CompanionServer(bind, action =>
            {
                lock (actions) actions.Add(action);
                return state.HandleCompanionActionForCheckAsync(action);
            });
            server.Publish(new CompanionSnapshot(Status: "自检中", Question: "问题", Answer: "回答"));
            var pairing = server.PairingUrl[(server.PairingUrl.IndexOf("#p=", StringComparison.Ordinal) + 3)..];
            var padded = pairing.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            using var pairingJson = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(padded));
            var token = pairingJson.RootElement.GetProperty("token").GetString()!;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var baseUri = server.BaseUri.ToString().TrimEnd('/');
            using var snapshot = await client.GetAsync($"{baseUri}/snapshot?t={token}");
            var snapshotBody = await snapshot.Content.ReadAsStringAsync();
            var unauthorizedResponse = await client.GetAsync($"{baseUri}/snapshot");
            unauthorized = (int)unauthorizedResponse.StatusCode;
            var id = Guid.NewGuid().ToString("D");
            using var first = await client.PostAsync($"{baseUri}/action?id={id}&a=previous&t={token}", new StringContent(""));
            using var second = await client.PostAsync($"{baseUri}/action?id={id}&a=previous&t={token}", new StringContent(""));
            duplicate = second.StatusCode == System.Net.HttpStatusCode.NoContent && actions.Count == 1;
            using var snapshotJson = System.Text.Json.JsonDocument.Parse(snapshotBody);
            var snapshotOk = snapshotJson.RootElement.TryGetProperty("snapshot", out var stateElement)
                             && stateElement.TryGetProperty("status", out var statusElement)
                             && statusElement.GetString() == "自检中";
            passed = snapshot.IsSuccessStatusCode && snapshotOk
                     && first.StatusCode == System.Net.HttpStatusCode.NoContent && duplicate && unauthorized == 403
                     && state.DetectedQuestion == "自检问题" && state.AnswerText == "自检回答";
        }
        catch (Exception exception)
        {
            error = exception.ToString();
        }
        finally
        {
            if (server is not null) await server.DisposeAsync();
        }
        var report = new { passed, address, actions, duplicate, unauthorized, error };
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
        Console.WriteLine(passed ? "companion-check: passed" : "companion-check: failed");
        Close();
    }


    /// <summary>Shows the floating cue island and verifies the OS excludes it from capture.</summary>
    /// <summary>Hardware check for #10: switch the system endpoint while capture is running.</summary>
    /// <summary>QA: click-through shows a separate strip that can turn it off again.</summary>
}
