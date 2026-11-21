using NextThought.Platform;
using Xunit;

namespace NextThought.Tests;

public sealed class ScreenShareDetectorTests
{
    [Theory]
    [InlineData("Zoom - Screen Sharing", "", true)]
    [InlineData("Sharing your screen - Teams", "", true)]
    [InlineData("腾讯会议 - 正在共享屏幕", "", true)]
    [InlineData("NextThought 提示岛", "ScreenShare", false)]
    [InlineData("普通窗口", "Chrome_WidgetWin_1", false)]
    public void RecognizesSharingTitles(string title, string className, bool expected) =>
        Assert.Equal(expected, ScreenShareDetector.IsSharingTitle(title, className));

    [Theory]
    [InlineData("", "ZPShareWnd", true)]
    [InlineData("", "TeamsShareWindow", true)]
    [InlineData("", "Chrome_WidgetWin_1", false)]
    public void RecognizesSharingWindowClasses(string title, string className, bool expected) =>
        Assert.Equal(expected, ScreenShareDetector.IsSharingTitle(title, className));
}
