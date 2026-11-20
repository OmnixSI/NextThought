using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using NextThought.Core;

namespace NextThought.Desktop.Views.Controls;

/// <summary>
/// E1: the little scene dot on the home page rows, tinted with the scene's pigment for the
/// current appearance (SceneTint light/dark).
/// </summary>
public sealed class ModeTintConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var mode = value switch
        {
            MeetingMode meetingMode => meetingMode,
            SessionMode sessionMode => sessionMode.ToMeetingMode(),
            string text => SessionModes.Parse(text).ToMeetingMode(),
            _ => MeetingMode.General
        };
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        return new SolidColorBrush(Color.Parse(SceneTheme.For(mode).Tint(dark)));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
