namespace NextThought.Core;

/// <summary>
/// B24: the one place the Windows build reads the process-local time zone for display strings.
/// On Windows .NET <c>TimeZoneInfo.Local</c> cannot be changed for a process (the TZ environment
/// variable only works on Unix and tzutil needs administrator rights), so tests that must prove
/// time-zone independence pass an explicit <see cref="TimeZoneInfo"/> to the formatter; the test
/// assembly also uses <see cref="Zone"/> to run the whole suite as if the process were in UTC.
/// Production code never overrides this: the default is the machine's local zone.
/// </summary>
public static class LocalTime
{
    private static TimeZoneInfo zone = TimeZoneInfo.Local;

    /// <summary>The zone display strings use. Defaults to <see cref="TimeZoneInfo.Local"/>.</summary>
    public static TimeZoneInfo Zone
    {
        get => zone;
        set => zone = value ?? TimeZoneInfo.Local;
    }

    public static DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, zone);

    public static DateTimeOffset Now() => TimeZoneInfo.ConvertTime(DateTimeOffset.Now, zone);

    public static void Reset() => zone = TimeZoneInfo.Local;
}
