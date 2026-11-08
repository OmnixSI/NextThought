using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NextThought.Platform;
using QRCoder;

namespace NextThought.Windows;

public partial class MainWindow
{
    private CompanionServer? companion;
    private readonly DispatcherTimer companionTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private void RefreshCompanion_Click(object sender, RoutedEventArgs e)
    {
        var addresses = CompanionServer.Addresses(); CompanionAddressBox.ItemsSource = addresses; CompanionAddressBox.SelectedIndex = 0;
        CompanionToggle.IsEnabled = companion != null || addresses.Count > 0;
        if (companion == null) CompanionInfo.Text = addresses.Count == 0 ? "未检测到可用局域网地址，请连接 Wi-Fi/以太网后刷新。" : "请选择手机能访问的 Wi-Fi/以太网地址；校园网 100.64–100.127 网段已支持。";
    }
    private async void CompanionToggle_Click(object sender, RoutedEventArgs e)
    {
        if (companion != null) { await StopCompanionAsync(); return; }
        if (CompanionAddressBox.SelectedItem is not CompanionAddress selected) { SetStatus("请先选择局域网地址。"); return; }
        if (System.Net.IPAddress.IsLoopback(selected.Address)) { SetStatus("本机地址无法供手机访问，请选择 Wi-Fi/以太网地址。", true); return; }
        try
        {
            companion = new(selected.Address, action => Dispatcher.InvokeAsync(() => CompanionAction(action)).Task);
            using var generator = new QRCodeGenerator(); using var data = generator.CreateQrCode(companion.PairingUrl, QRCodeGenerator.ECCLevel.M);
            using var png = new PngByteQRCode(data); using var bytes = new MemoryStream(png.GetGraphic(5));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = bytes; bitmap.EndInit(); bitmap.Freeze(); CompanionQr.Source = bitmap;
            CompanionLink.Text = companion.PairingUrl; CompanionToggle.Content = "关闭并撤销配对"; CompanionAddressBox.IsEnabled = false;
            CompanionInfo.Text = $"已在 {companion.BaseUri.Authority} 开启，8 小时后失效；关闭、重启或换网后重新配对。";
            companionTimer.Tick -= CompanionTick; companionTimer.Tick += CompanionTick; companionTimer.Start(); PublishCompanion();
        }
        catch (Exception ex) { await StopCompanionAsync(); SetStatus("手机伴侣未开启：" + ex.Message, true); }
    }
    private void CompanionTick(object? sender, EventArgs e)
    {
        if (companion?.Running != true) { _ = StopCompanionAsync(); return; } PublishCompanion();
    }
    private void PublishCompanion() => companion?.Publish(new(recording ? "正在记录" : currentSession != null ? "已归档" : "待机",
        DetectedQuestion.Text, AnswerText.Text, answering, QuestionInput.Text, currentSession?.Title ?? "", currentSession?.Id.ToString(),
        SourcesBox.ItemsSource is IEnumerable<NextThought.Core.SourceCitation> sources ? string.Join(" · ", sources.Select(s => $"[{s.Label}] {s.DocumentName} {s.Location}")) : ""));
    private bool CompanionAction(string action)
    {
        if (closing || busy || stopping) return false;
        if (action == "cancel") { CancelRequest(); UpdateState(); return true; }
        if (answering || summarizing) return false;
        var question = QuestionInput.Text.Trim(); if (question.Length == 0) question = DetectedQuestion.Text.Trim();
        if (action is "previous" or "next") return false; // No answer-history navigation in the current Windows UI.
        if (question.Length == 0 || question is "可以继续对这场会议提问。") return false;
        var instruction = action switch { "shorter" => "请更简短地回答：", "example" => "请用一个具体例子回答：", "english" => "Please answer in English: ", "chinese" => "请用中文回答：", _ => "" };
        _ = AnswerAsync(instruction + question, false); return true;
    }
    private async Task StopCompanionAsync()
    {
        companionTimer.Stop(); var previous = companion; companion = null;
        if (previous != null) await previous.DisposeAsync();
        CompanionLink.Text = ""; CompanionQr.Source = null; CompanionToggle.Content = "开启局域网手机伴侣"; CompanionAddressBox.IsEnabled = true; CompanionInfo.Text = "已关闭，配对链接已失效。";
    }
    private void CopyCompanion_Click(object sender, RoutedEventArgs e)
    { if (companion is { Running: true }) { Clipboard.SetText(companion.PairingUrl); SetStatus("已复制临时配对链接，仅供自己的手机使用。"); } }
}
