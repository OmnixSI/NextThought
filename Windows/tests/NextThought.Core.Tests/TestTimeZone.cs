using System.Runtime.CompilerServices;
using NextThought.Core;

namespace NextThought.Core.Tests;

/// <summary>
/// B24: Windows cannot change <c>TimeZoneInfo.Local</c> for a process without administrator rights
/// (the TZ environment variable is only honoured on Unix). The verification script therefore runs
/// the Core suite a second time with NEXTTHOUGHT_TEST_TIMEZONE=UTC and this bootstrap points the
/// production-local-time seam at UTC, so every test that still reads local time executes in a UTC
/// process without touching the machine's clock settings. The variable is read only by this test
/// assembly; the app itself never looks at it.
/// </summary>
internal static class TestTimeZone
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var requested = Environment.GetEnvironmentVariable("NEXTTHOUGHT_TEST_TIMEZONE");
        if (string.IsNullOrWhiteSpace(requested)) return;
        try
        {
            LocalTime.Zone = TimeZoneInfo.FindSystemTimeZoneById(requested);
        }
        catch (TimeZoneNotFoundException)
        {
            LocalTime.Zone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            LocalTime.Zone = TimeZoneInfo.Utc;
        }
        Console.WriteLine("B24: Core tests run with NEXTTHOUGHT_TEST_TIMEZONE=" + LocalTime.Zone.Id);
    }
}
