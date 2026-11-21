using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E25: the settings overlay keeps the Mac six-page structure (scene / answers / presenter / notes /
/// general / advanced), every card is tagged with exactly one Mac page, and the search matches
/// across all pages and shows the page name like the Mac SettingsSearch.
/// </summary>
public sealed class SettingsStructureTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "MainWindow.axaml.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative));

    private static string Xaml() => Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml"));

    [Fact]
    public void TheSixMacPagesAreTheSidebarButtons()
    {
        var xaml = Xaml();
        var expected = new[]
        {
            ("场景", "Scene"), ("回答与资料", "Answer"), ("提词与教练", "Coach"),
            ("纪要", "Notes"), ("通用", "General"), ("高级", "Advanced")
        };
        var scan = 0;
        foreach (var (title, tag) in expected)
        {
            var marker = $"Content=\"{{loc:Tr {title}}}\" Tag=\"{tag}\"";
            var at = xaml.IndexOf(marker, scan, StringComparison.Ordinal);
            Assert.True(at > 0, "sidebar 顺序缺失：" + title);
            scan = at;
        }
    }

    [Fact]
    public void EveryCardBelongsToExactlyOneMacPage()
    {
        var xaml = Xaml();
        var pages = new HashSet<string> { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" };
        var tags = System.Text.RegularExpressions.Regex.Matches(xaml, "<Border Classes=\"card\"[^>]*?Tag=\"(\\w+)\"")
            .Select(match => match.Groups[1].Value).ToList();
        Assert.NotEmpty(tags);
        Assert.All(tags, tag => Assert.Contains(tag, pages));
        // Mac 场景页只有「各场景开会时的设置」; 回答与资料 包含 回答服务/联网/资料/技能.
        Assert.Contains("Tag=\"Answer\"", xaml);
        Assert.Contains("Tag=\"Scene\"", xaml);
    }

    [Fact]
    public void GroupingsMatchTheMacSettingsPages()
    {
        var xaml = Xaml();
        string Card(string heading)
        {
            var at = xaml.IndexOf("Text=\"{loc:Tr " + heading + "}\"", StringComparison.Ordinal);
            Assert.True(at > 0, "heading 未找到：" + heading);
            var start = xaml.LastIndexOf("<Border", at, StringComparison.Ordinal);
            var tagAt = xaml.IndexOf("Tag=\"", start, StringComparison.Ordinal);
            var tag = xaml[(tagAt + 5)..xaml.IndexOf('"', tagAt + 5)];
            var end = xaml.IndexOf("</Border>", start, StringComparison.Ordinal);
            return tag + "|" + xaml[start..end];
        }
        // Mac GeneralSettings: 语言与外观 / 备份 / 权限 / 更新.
        Assert.StartsWith("General|", Card("界面语言 / Language"));
        Assert.Contains("{loc:Tr 外观}", Card("界面语言 / Language"));
        Assert.StartsWith("General|", Card("权限"));
        Assert.StartsWith("General|", Card("更新"));
        Assert.Contains("{loc:Tr 启动后自动检查更新（每 6 小时）}", Card("更新"));
        // Mac PresenterSettings: 提词 / 双屏演讲 / 字幕条.
        Assert.Contains("{loc:Tr 显示方式}", Card("提词"));
        Assert.StartsWith("Coach|", Card("双屏演讲"));
        // Mac SetupChecklist lives in 高级 → 数据与维护.
        // E3: Mac puts SetupChecklist on the 通用 page (SettingsGeneralData).
        Assert.StartsWith("General|", Card("设置清单"));
        // Mac AnswerSettings: 回答服务 / 联网 / 资料 / 技能.
        Assert.StartsWith("Answer|", Card("技能"));
        Assert.StartsWith("Answer|", Card("资料"));
        // Mac NotesSettingsView: 会议自动收尾 / 自定义模板.
        Assert.StartsWith("Notes|", Card("会议自动收尾"));
        Assert.StartsWith("Notes|", Card("自定义纪要模板"));
    }

    [Fact]
    public void TheSearchMatchesAcrossAllPagesLikeTheMac()
    {
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("SettingsSearchResults", code);
        Assert.Contains("PageTitle(page)", code);
        Assert.Contains("\"回答与资料\"", code);
        Assert.Contains("\"提词与教练\"", code);
        // Results are built from every card, not only the current page.
        Assert.Contains("foreach (var card in cards)", code);
        Assert.DoesNotContain("card.Tag as string == currentPage && text.Contains", code);
        var xaml = Xaml();
        Assert.Contains("Name=\"SettingsSearchResults\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{loc:Tr 设置搜索结果}\"", xaml);
    }
}
