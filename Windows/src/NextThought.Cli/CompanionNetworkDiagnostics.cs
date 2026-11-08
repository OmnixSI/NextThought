using System.Net;
using System.Text.Json;
using NextThought.Core;
using NextThought.Platform;

internal static class CompanionNetworkDiagnostics
{
    public static async Task<int> RunAsync(Func<string, string?> option, CancellationToken ct)
    {
        var addresses = CompanionServer.Addresses(); var address = option("--address") is { } value ? IPAddress.Parse(value) : addresses.First().Address;
        if (!addresses.Any(a => a.Address.Equals(address))) throw new ArgumentException("诊断地址必须属于本机已连接的局域网接口。");
        await using var server = new CompanionServer(address, _ => Task.FromResult(true));
        server.Publish(new("public-network-fixture", "公开合成连接检查", "仅用于本机自检"));
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await http.GetAsync(server.BaseUri, ct); var html = await response.Content.ReadAsStringAsync(ct);
        var passed = response.IsSuccessStatusCode && html.Contains("NextThought") && !server.BaseUri.IsLoopback;
        await File.WriteAllTextAsync(option("--json")!, JsonSerializer.Serialize(new { testedAt = DateTimeOffset.Now, passed, boundAddress = address.ToString(), port = server.BaseUri.Port, httpStatus = (int)response.StatusCode,
            availableAddresses = addresses.Select(a => new { a.Adapter, address = a.Address.ToString() }), remotePhoneVerified = false }, JsonDefaults.Options), ct);
        Console.WriteLine("Actual WLAN bind and local HTTP: " + (passed ? "PASS" : "FAIL") + "; does not prove phone reachability."); return passed ? 0 : 2;
    }
}
