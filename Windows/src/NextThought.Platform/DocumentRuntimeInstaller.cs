using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using NextThought.Core;

namespace NextThought.Platform;

/// <summary>
/// E23: the optional on-device recognition runtime (Mac DocumentRuntimeInstaller). Windows does not
/// ship a Python: the installer creates an isolated venv under the app data directory from a local
/// 64-bit Python 3.12, installs the pinned PaddleOCR requirements and verifies that the worker
/// imports. Nothing is installed into the system Python, PATH or the registry.
/// </summary>
public sealed class DocumentRuntimeInstaller(AppPaths paths)
{
    public string Root => Path.Combine(paths.Root, "runtimes", "paddleocr-vl-cpu");
    public string Python => Path.Combine(Root, "Scripts", "python.exe");
    public string Marker => Path.Combine(Root, "installed.json");

    public bool Installed => File.Exists(Python) && File.Exists(Marker);

    /// <summary>64-bit Python 3.12 found through the py launcher, python3.12 or python.</summary>
    public static string? FindSystemPython()
    {
        foreach (var candidate in new[] { ("py", new[] { "-3.12" }), ("python3.12", Array.Empty<string>()), ("python", Array.Empty<string>()) })
        {
            try
            {
                var start = new ProcessStartInfo(candidate.Item1) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in candidate.Item2) start.ArgumentList.Add(argument);
                foreach (var argument in new[] { "-c", "import sys,struct; assert sys.version_info[:2]==(3,12) and struct.calcsize('P')*8==64; print(sys.executable)" }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start);
                if (process is null) continue;
                var output = process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                if (process.WaitForExit(15000) && process.ExitCode == 0 && output.Trim().Length > 0) return output.Trim();
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
            }
        }
        return null;
    }

    /// <summary>Creates the venv, installs the pinned requirements and checks the worker imports.</summary>
    public async Task InstallAsync(IProgress<string>? status = null, CancellationToken ct = default)
    {
        var system = FindSystemPython() ?? throw new InvalidOperationException(
            "需要 64 位 Python 3.12：请先安装 Python 3.12，或改用系统识别。");
        Directory.CreateDirectory(Path.GetDirectoryName(Root)!);
        status?.Report("正在创建隔离环境…");
        await RunAsync(system, ["-m", "venv", Root], status, TimeSpan.FromMinutes(5), ct);
        status?.Report("正在安装 PaddleOCR 依赖（数 GB，需联网）…");
        var requirements = WriteRequirementsLock();
        try
        {
            await RunAsync(Python, ["-m", "pip", "install", "--disable-pip-version-check", "--no-compile",
                "--no-cache-dir", "--timeout", "120", "--retries", "5", "-r", requirements], status, TimeSpan.FromMinutes(90), ct);
        }
        finally { try { File.Delete(requirements); } catch (IOException) { } }
        status?.Report("正在检查文档环境…");
        await RunAsync(Python, ["-c", "import paddle, paddlex; print('paddle-ok')"], status, TimeSpan.FromMinutes(10), ct);
        await File.WriteAllTextAsync(Marker, JsonSerializer.Serialize(new
        {
            systemPython = system,
            installedAt = DateTimeOffset.UtcNow,
            requirementsSha256 = RequirementsSha256()
        }, JsonDefaults.Options), ct);
        status?.Report("文档环境已就绪；无需把依赖装进系统 Python。");
    }

    /// <summary>Re-runs the worker import check (校验／修复 on the settings page).</summary>
    public async Task VerifyAsync(IProgress<string>? status = null, CancellationToken ct = default)
    {
        if (!Installed) throw new FileNotFoundException("尚未安装文档环境。");
        await RunAsync(Python, ["-c", "import paddle, paddlex; print('paddle-ok')"], status, TimeSpan.FromMinutes(5), ct);
    }

    public void Delete()
    {
        var root = Path.GetFullPath(Root);
        if (!root.StartsWith(Path.GetFullPath(paths.Root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("文档环境路径无效。");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static string RequirementsSha256()
    {
        using var stream = Resource();
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string WriteRequirementsLock()
    {
        var path = Path.Combine(Path.GetTempPath(), "nextthought-ocr-requirements-" + Guid.NewGuid().ToString("N") + ".txt");
        using var stream = Resource();
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    private static Stream Resource() =>
        typeof(DocumentRuntimeInstaller).Assembly.GetManifestResourceStream("NextThought.Platform.Resources.ocr-requirements-lock.txt")
        ?? throw new InvalidDataException("缺少 OCR 依赖清单。");

    private static async Task RunAsync(string file, string[] arguments, IProgress<string>? status, TimeSpan timeout, CancellationToken ct)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("无法启动 " + Path.GetFileName(file) + "。");
        using var registration = ct.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } });
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) lines.Enqueue(line); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) lines.Enqueue(line); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var timeoutTask = Task.Delay(timeout, ct);
        var exit = process.WaitForExitAsync(ct);
        while (true)
        {
            var finished = await Task.WhenAny(exit, timeoutTask).ConfigureAwait(false);
            while (lines.TryDequeue(out var line))
            {
                if (line.Length > 400) line = line[..400];
                status?.Report(line);
            }
            if (finished == exit) break;
            try { process.Kill(true); } catch (InvalidOperationException) { }
            throw new TimeoutException("文档环境准备超时，已停止。");
        }
        while (lines.TryDequeue(out var line)) status?.Report(line);
        if (process.ExitCode != 0) throw new InvalidOperationException("文档环境准备失败（退出码 " + process.ExitCode + "）。");
    }
}
