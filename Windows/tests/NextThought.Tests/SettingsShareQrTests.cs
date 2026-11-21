using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E13 / §6.7: the 共享可见性测试 row belongs under the screen-sharing toggle on the presenter page
/// (Mac PresenterSettings 屏幕共享 section) and opens the share-test window; the companion pairing
/// card uses the shared QRCodeView control at the Mac size instead of a raw Image.
/// </summary>
public sealed class SettingsShareQrTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    [Fact]
    public void TheShareVisibilityTestOpensTheShareTestWindow()
    {
        var view = Read("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml");
        Assert.Contains("{loc:Tr 共享可见性测试}", view);
        Assert.Contains("Click=\"ShareVisibilityTest_Click\"", view);
        // The row sits directly under the screen-sharing exclusion toggle (Coach page, Mac presenter form).
        var toggle = view.IndexOf("尽力从屏幕共享中排除本应用窗口", StringComparison.Ordinal);
        Assert.True(toggle > 0);
        var button = view.IndexOf("共享可见性测试", toggle, StringComparison.Ordinal);
        Assert.True(button > toggle, "共享可见性测试 must follow the screen-sharing toggle");
        Assert.True(button - toggle < 400, "共享可见性测试 must be the next row");
        var code = Read("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs");
        Assert.Contains("RunShareVisibilityTestAsync()", code);
        Assert.Contains("new ShareTestWindow", code);
        Assert.Contains("ShowDialog(owner)", code);
    }

    [Fact]
    public void TheCompanionPairingCardUsesTheSharedQrCodeView()
    {
        var view = Read("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml");
        Assert.Contains("xmlns:controls=\"using:NextThought.Desktop.Views.Controls\"", view);
        Assert.Contains("<controls:QRCodeView", view);
        Assert.Contains("Text=\"{Binding CompanionLink}\"", view);
        Assert.Contains("Width=\"168\"", view);
        Assert.Contains("Height=\"168\"", view);
        Assert.DoesNotContain("<Image Source=\"{Binding CompanionQr}\"", view);
        var control = Read("Windows", "src", "NextThought.Desktop", "Views", "Controls", "QrCodeView.cs");
        Assert.Contains("class QRCodeView", control);
        Assert.Contains("using QRCoder;", control);
    }
}
