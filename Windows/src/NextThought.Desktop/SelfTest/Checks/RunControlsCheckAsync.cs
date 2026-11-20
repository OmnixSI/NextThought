using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    /// <summary>
    /// D8 smoke test: builds the focusable controls and the focus rings for real. The D8 focus
    /// adorner templates are only validated when a control with the style is instantiated, so a
    /// build or a pure unit test alone cannot catch a wrong template type (that mistake crashed
    /// the whole shell at startup). This check creates a window with a CheckBox, a chip, a nav row
    /// and a ListBox row, shows it, moves keyboard focus through them and reports the adorner state.
    /// </summary>
    private async Task RunControlsCheckAsync(string outputPath)
    {
        var failures = new List<string>();
        var check = new CheckBox { Content = "D8 focus ring · checkbox" };
        var chip = new RadioButton { Content = "D8 focus ring · chip", GroupName = "d8-chip" };
        chip.Classes.Add("chip");
        var nav = new RadioButton { Content = "D8 focus ring · nav", GroupName = "d8-nav" };
        nav.Classes.Add("nav");
        var text = new TextBox { Text = "D8 focus ring · text" };
        var rows = new ListBox
        {
            ItemsSource = new[] { "第一行", "第二行" }
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            Children = { check, chip, nav, text, rows }
        };
        var window = new Window { Content = panel, Width = 420, Height = 360 };
        var adorners = new Dictionary<string, string?>();
        try
        {
            window.Show();
            await Task.Delay(250);
            window.UpdateLayout();
            await PumpAsync();
            foreach (var control in new Control[] { check, chip, nav, text })
            {
                control.Focus();
                await Task.Delay(60);
                window.UpdateLayout();
                await PumpAsync();
            }
            foreach (var (name, control) in new (string, Control)[]
                     {
                         ("checkbox", check), ("chip", chip), ("navigation row", nav), ("text box", text)
                     })
            {
                var adorner = control.FocusAdorner;
                adorners[name] = adorner?.GetType().Name;
                // TextBox draws its ring on the control border (D8), the others use a focus adorner.
                if (name != "text box" && adorner is null) failures.Add(name + " has no focus adorner (style not applied)");
            }
            if (rows.ContainerFromIndex(0) is ListBoxItem row)
            {
                row.Focus();
                await Task.Delay(40);
                window.UpdateLayout();
                await PumpAsync();
                var adorner = row.FocusAdorner;
                adorners["list row"] = adorner?.GetType().Name;
                if (adorner is null) failures.Add("list row has no focus adorner (style not applied)");
            }
        }
        catch (Exception error)
        {
            failures.Add(error.GetType().Name + ": " + error.Message);
        }
        finally
        {
            try { window.Close(); } catch { }
        }
        var report = new Dictionary<string, object?>
        {
            ["passed"] = failures.Count == 0,
            ["failures"] = failures,
            ["focusAdorners"] = adorners
        };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine(failures.Count == 0 ? "controls-check: passed" : "controls-check: failed " + string.Join("; ", failures));
    }
}
