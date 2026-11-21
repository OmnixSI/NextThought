using System.Text.RegularExpressions;
using Xunit;

namespace NextThought.Tests;

/// <summary>
/// R1: version and product identity live in one place (the desktop project), the installer is a
/// per-user NextThought.Desktop install under %LOCALAPPDATA%\Programs\NextThought, and the old
/// preview identity (NextThought.Windows.Preview) is detected so its users are told to uninstall
/// before the new installer writes into the same folder. R2: one release command reads that
/// version and produces the §10.3 artifacts.
/// </summary>
public sealed class ReleaseIdentityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "NextThought.Windows.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    [Fact]
    public void TheVersionIsDefinedOnlyInTheDesktopProject()
    {
        var csproj = Read("Windows", "src", "NextThought.Desktop", "NextThought.Desktop.csproj");
        Assert.Contains("<Version>0.9.0</Version>", csproj);
        Assert.Contains("<Product>NextThought</Product>", csproj);
        Assert.Contains("<AssemblyName>NextThought</AssemblyName>", csproj);
        // No other Windows project hardcodes a version; the installer/update/release read the csproj.
        foreach (var file in Directory.GetFiles(Path.Combine(Root(), "Windows", "src"), "*.csproj", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "NextThought.Desktop.csproj") continue;
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("<Version>", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheInstallerUsesTheFixedDesktopIdentityAndUserInstall()
    {
        var iss = Read("Windows", "installer", "NextThought.Desktop.iss");
        Assert.Contains("#define InstallId \"NextThought.Desktop\"", iss);
        Assert.Contains("AppId={#InstallId}", iss);
        Assert.Contains("AppName=NextThought", iss);
        Assert.Equal(1, Directory.GetFiles(Path.Combine(Root(), "Windows", "installer"), "*.iss").Length);
        Assert.Contains("DefaultDirName={localappdata}\\Programs\\NextThought", iss);
        Assert.Contains("PrivilegesRequired=lowest", iss);
        Assert.Contains("UninstallDisplayIcon={app}\\NextThought.exe", iss);
        // The old preview installs into the same folder under a different AppId: the setup must
        // detect it and ask for an uninstall instead of writing a mixed installation over it.
        Assert.Contains("NextThought.Windows.Preview", iss);
        Assert.Contains("InitializeSetup", iss);
    }

    [Fact]
    public void TheBuildScriptsReadTheVersionFromTheProject()
    {
        var desktop = Read("Windows", "scripts", "build_desktop.ps1");
        Assert.Contains("NextThought.Desktop.csproj", desktop);
        Assert.DoesNotContain("0.5.0", desktop);
        var installer = Read("Windows", "scripts", "build_installer.ps1");
        Assert.Contains("NextThought.Desktop.iss", installer);
        Assert.Contains("[string]$InstallId = 'NextThought.Desktop'", installer);
        Assert.Contains("NextThought-' + $Version + '-windows-x64-setup", installer);
    }

    [Fact]
    public void TheReleaseCommandProducesTheSectionTenArtifacts()
    {
        var release = Read("Windows", "release.ps1");
        Assert.Contains("[switch]$Ci", release);
        Assert.Contains("NextThought.Desktop.csproj", release);
        Assert.Contains("windows-v", release);
        Assert.Contains("SHA256SUMS.txt", release);
        Assert.Contains("update.json", release);
        Assert.Contains("RELEASE_NOTES.md", release);
        Assert.Contains("--latest=false", release);
        Assert.Contains("artifacts\\release", release);
        // The fields the update manifests need (tag, version, build, asset, sha256, commit, publishedAt).
        foreach (var field in new[] { "tag", "version", "build", "asset", "sha256", "commit", "publishedAt" })
            Assert.Contains(field, release);
        // The publish is x64 self-contained ReadyToRun and unsigned.
        Assert.Contains("win-x64", release);
        Assert.Contains("PublishReadyToRun=true", release);
        Assert.Contains("Unsigned", release);
    }

    [Fact]
    public void TheBuildNumberRuleIsDocumentedAndMonotonic()
    {
        var release = Read("Windows", "release.ps1");
        Assert.Contains("rev-list --count HEAD", release);
        Assert.Contains("build", release);
    }

    [Fact]
    public void TheVersionBadgeIsGoneFromTheInterface()
    {
        var state = Read("Windows", "src", "NextThought.Desktop", "AppState.cs");
        Assert.DoesNotContain("\"WINDOWS  \"", state);
        var title = Read("Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml");
        Assert.DoesNotContain("VersionText", title);
        // The about section still shows the version (allowed place).
        var settings = Read("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml");
        Assert.Contains("VersionText", settings);
    }
}
