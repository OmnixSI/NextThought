using System.Windows;
using System.Security.Cryptography;
using System.Text;
using NextThought.Platform;

namespace NextThought.Windows;

public partial class App : System.Windows.Application
{
    private Mutex? instance;
    private bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        var root = new AppPaths().Root.ToUpperInvariant();
        var name = "Local\\NextThought." + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..24];
        instance = new Mutex(true, name, out ownsInstance);
        if (!ownsInstance) { MessageBox.Show("此数据目录已有 NextThought 窗口运行，请使用已有窗口。", "NextThought"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "NextThought", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsInstance) instance?.ReleaseMutex();
        instance?.Dispose(); base.OnExit(e);
    }
}
