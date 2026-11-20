using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using NextThought.Desktop.Views;

namespace NextThought.Desktop;

public partial class App : Application
{
    private Mutex? instance;
    private bool ownsInstance;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log("ui", e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("domain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log("task", e.Exception); e.SetObserved(); };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var state = new AppState();
            var appearance = "system";
            try { appearance = state.SettingsStore.Load().Appearance; } catch { }
            RequestedThemeVariant = appearance switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
            var root = state.Paths.Root.ToUpperInvariant();
            var name = "Local\\NextThought." + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..24];
            instance = new Mutex(true, name, out ownsInstance);
            if (!ownsInstance)
            {
                Log("single-instance", new InvalidOperationException("此数据目录已有 NextThought 窗口运行。"));
                desktop.Shutdown();
                return;
            }
            desktop.MainWindow = new MainWindow(state);
            desktop.ShutdownRequested += async (_, _) => await state.ShutdownAsync();
            desktop.Exit += (_, _) =>
            {
                if (ownsInstance) instance?.ReleaseMutex();
                instance?.Dispose();
                instance = null;
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void Log(string area, Exception? error)
    {
        if (error is null) return;
        try
        {
            var root = Environment.GetEnvironmentVariable("NEXTTHOUGHT_DATA_DIR") is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought");
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            var file = Path.Combine(root, "Logs", "desktop.log");
            File.AppendAllText(file, $"{DateTimeOffset.Now:O} [{area}] {error}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            Debug.WriteLine(error);
        }
    }
}
