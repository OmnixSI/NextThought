using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace NextThought.Desktop;

/// <summary>
/// Global shortcuts (plan §7, the Windows counterpart of Mac's Carbon RegisterHotKey).
/// The registrations belong to a dedicated message-pump thread, which receives WM_HOTKEY
/// and posts the action back to the UI thread. Windows defaults use Ctrl+Alt because
/// Ctrl+Shift collides with browsers and the shell (plan §7 table).
/// </summary>
internal sealed class GlobalHotkeys : IDisposable
{
    public sealed record Binding(string Name, uint VirtualKey, Action Action);

    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0400 + 1;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;

    private readonly Thread thread;
    private readonly IReadOnlyList<Binding> bindings;
    private readonly ManualResetEventSlim ready = new();
    private readonly List<string> unregistered = [];
    private uint threadId;
    private volatile bool running = true;

    public GlobalHotkeys(IReadOnlyList<Binding> bindings)
    {
        this.bindings = bindings;
        thread = new Thread(Pump) { IsBackground = true, Name = "NextThought.Hotkeys" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(3));
    }

    public IReadOnlyList<string> Unregistered => unregistered;

    private void Pump()
    {
        threadId = GetCurrentThreadId();
        // A thread message queue must exist before RegisterHotKey can attach to it.
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        for (var index = 0; index < bindings.Count; index++)
        {
            var binding = bindings[index];
            var id = index + 1;
            var ok = RegisterHotKey(IntPtr.Zero, id, ModControl | ModAlt | ModNoRepeat, binding.VirtualKey);
            Log($"register {binding.Name} vk=0x{binding.VirtualKey:X2} ok={ok} err={(ok ? 0 : Marshal.GetLastWin32Error())}");
            if (!ok) unregistered.Add(binding.Name);
        }
        ready.Set();
        while (running)
        {
            var result = GetMessage(out var message, IntPtr.Zero, 0, 0);
            if (result == 0 || result == -1) break;
            if (message.message == WmHotkey)
            {
                var index = (int)message.wParam - 1;
                if (index >= 0 && index < bindings.Count) Dispatcher.UIThread.Post(bindings[index].Action);
            }
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
        for (var index = 0; index < bindings.Count; index++) UnregisterHotKey(IntPtr.Zero, index + 1);
    }

    internal static void Log(string message)
    {
        try
        {
            var root = Environment.GetEnvironmentVariable("NEXTTHOUGHT_DATA_DIR") is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextThought");
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            File.AppendAllText(Path.Combine(root, "Logs", "hotkeys.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics only.
        }
    }

    public void Dispose()
    {
        running = false;
        if (threadId != 0) PostThreadMessage(threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        if (thread.IsAlive) thread.Join(TimeSpan.FromSeconds(2));
        ready.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out Msg message, IntPtr window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Msg message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Msg message);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Msg message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
}
