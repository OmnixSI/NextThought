using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using QRCoder;

namespace NextThought.Desktop.Views.Controls;

/// <summary>
/// E13 / §6.7: QRCodeView (ContentView.swift) — renders a QR code for a text/address on a white
/// rounded card, the same way the companion pairing card shows the address.
/// </summary>
public class QRCodeView : ContentControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<QRCodeView, string?>(nameof(Text));

    public QRCodeView()
    {
        Content = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            Child = new Image { Stretch = Stretch.Uniform }
        };
        PropertyChanged += (_, change) =>
        {
            if (change.Property == TextProperty) Rebuild();
        };
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Rebuild()
    {
        if (Content is not Border { Child: Image image }) return;
        if (string.IsNullOrWhiteSpace(Text))
        {
            image.Source = null;
            return;
        }
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(Text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(6);
        using var stream = new MemoryStream(png);
        image.Source = new Bitmap(stream);
    }
}
