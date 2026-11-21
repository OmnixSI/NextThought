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
            // §3.6 G2: the Mac sidebar rows are icon + label buttons (settings-nav).
            var marker = $"Classes=\"quiet settings-nav\" HorizontalContentAlignment=\"Left\" Tag=\"{tag}\"";
            var at = xaml.IndexOf(marker, scan, StringComparison.Ordinal);
            Assert.True(at > 0, "sidebar 顺序缺失：" + title);
            var block = xaml[at..Math.Min(xaml.Length, at + 500)];
            Assert.Contains($"{{loc:Tr {title}}}", block);
            Assert.Contains("FontFamily=\"{StaticResource IconFont}\"", block);
            scan = at;
        }
    }

    [Fact]
    public void EveryCardBelongsToExactlyOneMacPage()
    {
        var xaml = Xaml();
        var pages = new HashSet<string> { "Scene", "Answer", "Coach", "Notes", "General", "Advanced" };
        var tags = System.Text.RegularExpressions.Regex.Matches(xaml, "<Border Classes=\"card settings-section\"[^>]*?Tag=\"(\\w+)\"")
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
        // §3.6: Mac SetupChecklist / data about live in 高级 → 数据与维护.
        Assert.StartsWith("Advanced|", Card("设置清单"));
        Assert.StartsWith("Advanced|", Card("数据与关于"));
        // Mac AnswerSettings: 回答服务 / 联网 / 资料 / 技能.
        Assert.StartsWith("Answer|", Card("技能"));
        Assert.StartsWith("Answer|", Card("资料"));
        // Mac NotesSettingsView: 会议自动收尾 / 自定义模板.
        Assert.StartsWith("Notes|", Card("会议自动收尾"));
        Assert.StartsWith("Notes|", Card("自定义纪要模板"));
        // §3.6: 区分说话人（声纹）and 术语和人名 live on 通用; 说话人与翻译 moved to 通用 as well;
        // 字幕条 stays on 提词与教练 and 联网搜索 moves to its own 回答与资料 section (E27).
        Assert.StartsWith("General|", Card("区分说话人"));
        Assert.StartsWith("General|", Card("术语和人名"));
        Assert.StartsWith("General|", Card("同声传译"));
        Assert.StartsWith("Coach|", Card("字幕条"));
        Assert.StartsWith("Answer|", Card("回答服务"));
        // §3.6 A1: Mac AnswerSettings API / Codex segmented backend.
        var answer = Card("回答服务");
        Assert.Contains("InkSegmented", answer);
        Assert.Contains("{Binding Settings.AnswerBackendIndex", answer);
        // §3.6 A5: 联网搜索 is its own Answer section with the Mac tri-state picker.
        var web = Card("联网搜索");
        Assert.StartsWith("Answer|", web);
        Assert.Contains("{Binding Settings.WebSearchModeChoices}", web);
        Assert.Contains("{Binding Settings.SelectedWebSearchMode}", web);
        Assert.Contains("{loc:Tr 联网}", web);
        // §3.6 E3: 术语和人名 opens the glossary window (Mac 管理…) instead of inline editing.
        var glossary = Card("术语和人名");
        Assert.Contains("{loc:Tr 管理…}", glossary);
        Assert.DoesNotContain("GlossaryText", glossary);
        // §3.6 N3: 纪要高级 on the notes page carries the notes model.
        var notesAdvanced = Card("纪要高级");
        Assert.StartsWith("Notes|", notesAdvanced);
        Assert.Contains("{Binding Settings.NotesModel}", notesAdvanced);
        // §3.6 P1: the presenter page has the Mac 字号 slider.
        Assert.Contains("{Binding Settings.TeleprompterFontSize}", Card("提词"));
        // §3.6 A3: Mac 模型 row — overline + 模型名 field + the fetched list + 获取列表 + status.
        Assert.Contains("{loc:Tr 模型}", answer);
        Assert.Contains("{loc:Tr 模型名}", answer);
        Assert.Contains("{loc:Tr 请选择模型}", answer);
        Assert.Contains("Settings.ApiModelChoices", answer);
        Assert.Contains("{loc:Tr 获取列表}", answer);
        Assert.Contains("Settings.ApiModelsStatus", answer);
        // §3.6 V4: 高级 → 数据与维护 has the Mac toggles.
        var about = Card("数据与关于");
        Assert.StartsWith("Advanced|", about);
        Assert.Contains("{loc:Tr 减少视觉特效}", about);
        Assert.Contains("{Binding Settings.ReducedEffects}", about);
        Assert.Contains("{loc:Tr 工作台置顶}", about);
        Assert.Contains("{Binding Settings.KeepOnTop}", about);
        // Mac 高级 order: 手机伴侣 sits before 更新（高级）; data/maintenance stay last.
        var companionAt = xaml.IndexOf("{loc:Tr 手机伴侣}", StringComparison.Ordinal);
        var updateAdvancedAt = xaml.IndexOf("{loc:Tr 更新（高级）}", StringComparison.Ordinal);
        Assert.True(companionAt > 0 && updateAdvancedAt > companionAt, "高级 ordering");
        // §3.6 settings-structure: recordSessions gets its Mac B BGeneral card.
        var record = Card("记录每次会议");
        Assert.StartsWith("General|", record);
        Assert.Contains("IsChecked=\"{Binding Settings.RecordSessions}\"", record);
        Assert.Contains("{loc:Tr 保存转录和会中回答，不保存原始音频。生成纪要时会把本次转录发送给当前回答服务。}", record);
        // The update card shows the Mac version/build line and no longer owns the channel picker.
        Assert.Contains("{Binding Settings.VersionLine}", Card("更新"));
        Assert.Contains("{Binding Settings.BuildLine}", Card("更新"));
        Assert.DoesNotContain("{Binding UpdateChannels}", Card("更新"));
        // Semantic retrieval appears once, in the 高级 → 资料组件 control (not on 回答与资料).
        Assert.DoesNotContain("Settings.UseSemanticRetrieval", xaml);
        var advanced = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "DocumentComponentsAdvanced.axaml"));
        Assert.Equal(1, advanced.Split("{Binding Settings.UseSemanticRetrieval}").Length - 1);
    }

    [Fact]
    public void TheSearchMatchesAcrossAllPagesLikeTheMac()
    {
        var code = Read(Path.Combine("Windows", "src", "NextThought.Desktop", "Views", "SettingsView.axaml.cs"));
        Assert.Contains("SettingsSearchResults", code);
        Assert.Contains("PageTitle(page)", code);
        Assert.Contains("\"回答与资料\"", code);
        Assert.Contains("\"提词与教练\"", code);
        // §3.6: results come from the Mac SettingsSearchItem keyword table, not only visible card text.
        Assert.Contains("NextThought.Core.SettingsSearchItem.Matching(query)", code);
        Assert.Contains("ScrollToSection(item.Anchor)", code);
        var index = Read(Path.Combine("Windows", "src", "NextThought.Core", "SettingsSearchIndex.cs"));
        foreach (var term in new[] { "刘海", "钉住", "notch", "同声传译", "记录 保存 会议记录" })
            Assert.Contains(term, index);
        var xaml = Xaml();
        Assert.Contains("Name=\"SettingsSearchResults\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{loc:Tr 设置搜索结果}\"", xaml);
    }
}
