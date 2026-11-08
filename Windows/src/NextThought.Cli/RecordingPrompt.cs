using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NextThought.Core;

internal sealed class RecordingPrompt
{
    private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource stop = new();
    private readonly List<string> finalLines = [];
    private readonly Dictionary<AudioSource, string> partial = [];
    private Window? window;
    private TextBlock? status;
    private TextBox? transcript;
    private Button? finish;
    private DispatcherTimer? timer;
    private bool recording, completed;
    public CancellationToken StopToken => stop.Token;
    public Task<bool> WaitForStartAsync(CancellationToken ct) => started.Task.WaitAsync(ct);
    public Task WaitForCloseAsync(CancellationToken ct) => closed.Task.WaitAsync(ct);
    public static async Task<RecordingPrompt> ShowAsync(double seconds, string device, bool playback, CancellationToken ct, bool disconnect = false)
    {
        var result = new TaskCompletionSource<RecordingPrompt>(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompt = new RecordingPrompt();
        var thread = new Thread(() =>
        {
            try { prompt.Show(seconds, device, playback, disconnect, () => result.TrySetResult(prompt)); }
            catch (Exception ex) { result.TrySetException(ex); prompt.started.TrySetException(ex); prompt.closed.TrySetException(ex); }
        }) { IsBackground = true, Name = "NextThought acceptance prompt" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return await result.Task.WaitAsync(ct);
    }
    private void Show(double seconds, string device, bool playback, bool disconnect, Action ready)
    {
        window = new Window { Title = "NextThought · HECATE 采音验收", Width = 700, Height = 590,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize,
            Topmost = true, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 15, Background = Brushes.White };
        var content = new StackPanel { Margin = new Thickness(26) };
        status = new TextBlock { Text = disconnect ? "设备断开测试，请确认开始" : "模型已加载，请确认开始测试", FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) };
        content.Children.Add(status);
        content.Children.Add(new TextBlock { Text = disconnect ? $"设备：HECATE\n点击开始后等待 2 秒，再拔出 HECATE 底座的 USB 连接。最长等待 {seconds:0} 秒。" : $"麦克风：{device}\n点击“开始采音”后立即录音 {seconds:0} 秒，窗口保留并显示倒计时。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        content.Children.Add(new TextBlock { Text = disconnect ? "本轮无需朗读。程序应检测设备断开，停止记录并保留已有转写。看到完成结果后，关闭本窗口并重新插上 HECATE 底座。" : "这是 HECATE 麦克风测试。今天讨论 Windows 移植，先验证录音，再生成会议纪要。",
            TextWrapping = TextWrapping.Wrap, FontSize = 19, Foreground = Brushes.MidnightBlue, Margin = new Thickness(0, 0, 0, 14) });
        content.Children.Add(new TextBlock { Text = disconnect ? "只需拔插本次使用的 HECATE USB 底座，无需调整系统设置。" : playback ? "本轮耳机还会播放公开样例，用于双路验证。测试音频不保存，转写保存在本机。" : "请暂时暂停其他播放。测试音频不保存，转写保存在本机。",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) });
        transcript = new TextBox { Height = 150, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = "等待开始…", Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 16) };
        content.Children.Add(transcript);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        finish = new Button { Content = "稍后测试", Padding = new Thickness(18, 9, 18, 9), Margin = new Thickness(0, 0, 12, 0) };
        var begin = new Button { Content = "开始采音", Padding = new Thickness(24, 9, 24, 9), IsDefault = true };
        begin.Click += (_, _) =>
        {
            recording = true; begin.IsEnabled = false; finish.Content = "提前结束"; transcript.Text = "正在收音，请朗读测试句…";
            var watch = Stopwatch.StartNew();
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (_, _) => status.Text = $"正在采音 · 剩余 {Math.Max(0, Math.Ceiling(seconds - watch.Elapsed.TotalSeconds)):0} 秒";
            status.Text = $"正在采音 · 剩余 {seconds:0} 秒"; timer.Start(); started.TrySetResult(true);
        };
        finish.Click += (_, _) =>
        {
            if (!recording) { started.TrySetResult(false); window.Close(); }
            else if (completed) window.Close();
            else { stop.Cancel(); finish.IsEnabled = false; status.Text = "正在结束并保存最后一句…"; }
        };
        window.Closing += (_, e) =>
        {
            if (recording && !completed) { e.Cancel = true; stop.Cancel(); status.Text = "正在结束并保存最后一句…"; }
        };
        window.Closed += (_, _) => { timer?.Stop(); started.TrySetResult(false); closed.TrySetResult(); };
        buttons.Children.Add(finish); buttons.Children.Add(begin); content.Children.Add(buttons); window.Content = content;
        window.Loaded += (_, _) => ready(); window.ShowDialog();
    }
    public void Update(TranscriptUpdate update)
    {
        window?.Dispatcher.BeginInvoke(() =>
        {
            var source = update.Source == AudioSource.System ? "系统" : "麦克风";
            if (update.IsFinal) { finalLines.Add(source + "：" + update.Text); partial.Remove(update.Source); }
            else partial[update.Source] = source + "（识别中）：" + update.Text;
            transcript!.Text = string.Join("\n", finalLines.Concat(partial.Values)); transcript.ScrollToEnd();
        });
    }
    public void Complete(string message)
    {
        window?.Dispatcher.BeginInvoke(() =>
        {
            completed = true; timer?.Stop(); status!.Text = message; finish!.Content = "关闭"; finish.IsEnabled = true;
            if (finalLines.Count == 0) transcript!.Text = "本轮未识别到完整语句。请检查麦克风位置、静音或增益。";
        });
    }
}
