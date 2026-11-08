using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using NextThought.Core;
using NextThought.Platform;

internal static class DeploymentDiagnostics
{
    public static async Task<int> RunAsync(string command, Func<string, string?> option, CancellationToken ct)
    {
        var store = new DeploymentStore(option("--install-root") ?? throw new ArgumentException("Missing --install-root"));
        var version = option("--version") ?? "";
        if (command == "deployment-verify")
        {
            var manifest = await store.VerifyAsync(version, ct);
            Console.WriteLine(JsonSerializer.Serialize(new { verified = true, manifest.Version, manifest.Revision, manifest.Signing }, JsonDefaults.Options)); return 0;
        }
        EnsureStopped(store.Root);
        using var shell = new ShellChanges(store, option("--uninstall-key") ?? "NextThought.Windows.Preview_is1");
        var state = command == "deployment-rollback" ? await store.RollbackAsync(ct, shell.ApplyAsync)
            : await store.ActivateAsync(version, option("--group") ?? "NextThought Windows Preview", option("--desktop") == "true", ct, shell.ApplyAsync);
        shell.Commit(); Console.WriteLine(JsonSerializer.Serialize(new { applied = true, state }, JsonDefaults.Options)); return 0;
    }
    private static void EnsureStopped(string root)
    {
        foreach (var process in Process.GetProcessesByName("NextThought"))
        using (process)
        {
            string? file = null;
            try { file = process.MainModule?.FileName; } catch (System.ComponentModel.Win32Exception) { }
            if (file?.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true)
                throw new InvalidOperationException("请先结束会议并关闭此安装目录中的 NextThought，再升级或回退。");
        }
    }
    private sealed class ShellChanges(DeploymentStore store, string keyName) : IDisposable
    {
        private readonly Dictionary<string, byte[]?> originals = new(StringComparer.OrdinalIgnoreCase);
        private bool committed;
        private string? oldVersion;
        private bool registryChanged;
        public Task ApplyAsync(InstalledRelease state)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(keyName, @"^NextThought\.Windows\.Preview(?:\.QA[A-Za-z0-9_-]+)?_is1$")) throw new InvalidDataException("卸载注册项名称无效。");
            var executable = Path.Combine(store.VersionDirectory(state.CurrentVersion), "NextThought.exe");
            var programs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", state.ShortcutGroup);
            Directory.CreateDirectory(programs);
            WriteLink(Path.Combine(programs, "NextThought.lnk"), executable);
            WriteLink(Path.Combine(programs, "一键自检.lnk"), Path.Combine(store.VersionDirectory(state.CurrentVersion), "Run-Validation.cmd"));
            WriteLink(Path.Combine(programs, "回退上一版.lnk"), Path.Combine(store.Root, "Rollback.cmd"));
            var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), state.ShortcutGroup + ".lnk");
            if (state.DesktopShortcut || File.Exists(desktop)) WriteLink(desktop, executable);
            var launch = Path.Combine(store.Root, "Launch.cmd"); Snapshot(launch);
            File.WriteAllText(launch, "@echo off\r\nstart \"\" \"%~dp0versions\\" + state.CurrentVersion + "\\NextThought.exe\"\r\n", new UTF8Encoding(false));
            var rollback = Path.Combine(store.Root, "Rollback.cmd"); Snapshot(rollback);
            File.WriteAllText(rollback, "@echo off\r\nsetlocal\r\n\"%~dp0versions\\" + state.CurrentVersion + "\\tools\\NextThought.Cli.exe\" deployment-rollback --install-root \"%~dp0.\" --uninstall-key " + keyName + "\r\nset \"rollbackExit=%errorlevel%\"\r\nif not \"%NEXTTHOUGHT_VALIDATION_NO_PAUSE%\"==\"1\" pause\r\nexit /b %rollbackExit%\r\n", new UTF8Encoding(false));
            using var registry = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + keyName, writable: true);
            if (registry is not null)
            {
                var location = registry.GetValue("InstallLocation") as string;
                if (location is null || !string.Equals(Path.GetFullPath(location).TrimEnd('\\'), store.Root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("卸载注册项不属于当前安装目录。");
                oldVersion = registry.GetValue("DisplayVersion") as string; registryChanged = true;
                registry.SetValue("DisplayVersion", state.CurrentVersion);
            }
            return Task.CompletedTask;
        }
        private void Snapshot(string file) { if (!originals.ContainsKey(file)) originals[file] = File.Exists(file) ? File.ReadAllBytes(file) : null; }
        private void WriteLink(string file, string executable)
        {
            Snapshot(file); var temporary = file + "." + Guid.NewGuid().ToString("N") + ".lnk";
            object? shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Windows 快捷方式接口不可用。"));
                dynamic dynamicShell = shell!; link = dynamicShell.CreateShortcut(temporary); dynamic dynamicLink = link;
                dynamicLink.TargetPath = executable; dynamicLink.WorkingDirectory = Path.GetDirectoryName(executable); dynamicLink.Description = "NextThought Windows Preview";
                dynamicLink.Save(); File.Move(temporary, file, overwrite: true);
            }
            finally
            {
                if (link is not null) Marshal.FinalReleaseComObject(link); if (shell is not null) Marshal.FinalReleaseComObject(shell);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        public void Commit() => committed = true;
        public void Dispose()
        {
            if (committed) return;
            foreach (var (file, bytes) in originals)
                if (bytes is null) { if (File.Exists(file)) File.Delete(file); } else File.WriteAllBytes(file, bytes);
            if (registryChanged)
            {
                using var registry = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + keyName, writable: true);
                if (oldVersion is null) registry?.DeleteValue("DisplayVersion", false); else registry?.SetValue("DisplayVersion", oldVersion);
            }
        }
    }
}
