using System.Text.Json;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;
using NextThought.Core;

internal static class HotspotStatusDiagnostics
{
    public static int Run()
    {
        var profile = NetworkInformation.GetConnectionProfiles().FirstOrDefault(p => p.IsWlanConnectionProfile && p.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess);
        if (profile is null) throw new InvalidOperationException("No connected WLAN profile.");
        var manager = NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        Console.WriteLine(JsonSerializer.Serialize(new { readOnly = true, source = profile.ProfileName, state = manager.TetheringOperationalState.ToString(), clients = manager.ClientCount }, JsonDefaults.Options));
        return 0;
    }
}
