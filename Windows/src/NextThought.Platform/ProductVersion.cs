using System.Reflection;

namespace NextThought.Platform;

/// <summary>F21: the version reported to external clients is the running app's version.</summary>
public static class ProductVersion
{
    public static string Current
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(ProductVersion).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }
            return assembly.GetName().Version is { } version
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : "0.0.0";
        }
    }
}
