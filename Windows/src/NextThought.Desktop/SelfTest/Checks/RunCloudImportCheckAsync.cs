namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private async Task RunCloudImportCheckAsync(string path)
    {
        var directory = Path.Combine(Path.GetTempPath(), "nt-cloud-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        System.Net.HttpListener? listener = null;
        var passed = false;
        var lineCount = 0;
        var error = "";
        try
        {
            var wav = Path.Combine(directory, "cloud.wav");
            var pcm = new byte[3200];
            using (var stream = File.Create(wav))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(16000);
                writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
            }
            var portProbe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            portProbe.Start();
            var port = ((System.Net.IPEndPoint)portProbe.LocalEndpoint).Port;
            portProbe.Stop();
            listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var server = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                var buffer = new byte[16 * 1024];
                while (true)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) break;
                }
                var final = """{"tokens":[{"text":"cloud hello","is_final":true,"start_ms":0,"end_ms":500},{"text":"<end>","is_final":true}],"finished":true}""";
                await socket.SendAsync(Encoding.UTF8.GetBytes(final), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
                try { await socket.CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
            });
            state.Settings.SelectedCloudStt = state.Settings.CloudSttChoices.First(choice => choice.Value == "soniox");
            state.Settings.CloudSttModel = "stt-rt-v5";
            state.Settings.CloudSttEndpointValue = $"ws://127.0.0.1:{port}/stt";
            state.SettingsStore.SaveCloudSttKey("test-cloud-key");
            await state.ImportWaveFileAsync(wav);
            await server.WaitAsync(TimeSpan.FromSeconds(5));
            lineCount = state.CurrentLines.Count;
            passed = state.CurrentLines.Any(line => line.Text.Contains("cloud hello", StringComparison.Ordinal));
        }
        catch (Exception ex) { error = ex.Message; }
        finally
        {
            try { listener?.Stop(); } catch { }
            try { Directory.Delete(directory, true); } catch { }
        }
        var report = new { passed, lineCount, error };
        try
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
        }
        catch { }
        Console.WriteLine(passed ? "cloud-import-check: passed" : "cloud-import-check: failed");
        Close();
    }

    /// <summary>QA: cloud provider choice, model and DPAPI key round-trip.</summary>
}
