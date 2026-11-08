using System.Windows;
using System.Security.Cryptography;
using System.Text;
using NextThought.Platform;

namespace NextThought.Windows;

public partial class App : System.Windows.Application
{
    private Mutex? instance;
    private Mutex? installerPresence;
    private bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        var checkIndex = Array.IndexOf(e.Args, "--startup-check");
        var checkReport = checkIndex >= 0 && checkIndex + 1 < e.Args.Length ? e.Args[checkIndex + 1] : null;
        var root = new AppPaths().Root.ToUpperInvariant();
        var name = "Local\\NextThought." + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..24];
        instance = new Mutex(true, name, out ownsInstance);
        if (!ownsInstance) { MessageBox.Show("此数据目录已有 NextThought 窗口运行，请使用已有窗口。", "NextThought"); Shutdown(); return; }
        installerPresence = new Mutex(false, "Local\\NextThought.Windows.Preview.Running");
        DispatcherUnhandledException += (_, args) =>
        {
            if (checkIndex >= 0) { args.Handled = true; Environment.ExitCode = 2; Shutdown(2); return; }
            MessageBox.Show(args.Exception.Message, "NextThought", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
        if (checkIndex >= 0)
        {
            if (checkReport is null) Shutdown(2); else StartupCheck.Begin(this, checkReport);
        }
        else { MainWindow = new MainWindow(); MainWindow.Show(); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsInstance) instance?.ReleaseMutex();
        instance?.Dispose(); installerPresence?.Dispose(); base.OnExit(e);
    }
}
