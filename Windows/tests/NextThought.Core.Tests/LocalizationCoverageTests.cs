using System.Text;
using System.Text.RegularExpressions;
using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// G9: no user-visible sentence may bypass the string table. Every <c>{loc:Tr …}</c> key and every
/// Chinese literal in the shell must have an English translation, otherwise the English screenshot
/// would silently show Chinese. Files owned by other tasks (demo copy, update feed, proofreading)
/// and Mac-original prompt segments are allowlisted with the reason stated.
/// </summary>
public sealed class LocalizationCoverageTests
{
    private static readonly Regex Cjk = new("[\u3000-\u303f\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\uff00-\uffef]", RegexOptions.Compiled);
    private static readonly Regex TrAttribute = new(@"\{loc:Tr\s+(?:'(?<quoted>[^']*)'|(?<plain>[^}]*))\}", RegexOptions.Compiled);
    private static readonly Regex StringLiteral = new("(?<![\\w@$])(?:\\$)?@?\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    /// <summary>Files another task owns; their wording is cleaned up there, not here.</summary>
    private static readonly string[] ForeignFiles =
    [
        "DemoContent.cs", "KnowledgeCheck.cs", "AppState.Proofread.cs", "AppState.Update.cs", "UpdateFeed.cs",
        "AppState.Demo.cs", "DemoScript.cs", "MacPreferences.cs"
    ];

    /// <summary>Demo/user data shown as sample content; identical Mac wording is not required.</summary>
    private static readonly string[] UserDataStrings =
    [
        "温度补偿之后残差还剩多少，和有限元对得上吗？",
        "残差约占 12%，主要集中在支座附近。",
        "Kubernetes | K8s | 库伯奈提斯\n同济大学 | 同技大学\nOpenSees | open sees"
    ];

    /// <summary>Mac-original answer prompt segments: they are sent to the model, never rendered.</summary>
    private static readonly string[] PromptStrings =
    [
        "你是会议中的参考助手。根据已提供的转写回答当前问题，使用简洁中文；问题为英文时可用英文。",
        "区分事实与建议，不编造未提供的文件、数据、决定或经历。参考资料库中的具体事实必须有对应摘录支持并标注引用；",
        "资料和转写均未提供所需事实时，明确说明资料不足，不猜测金额、日期、人员或参数。",
        "请只回复：连接成功",
        "请总结当前讨论", "请总结当前讨论。",
        "请基于当前讨论重新生成一版与上一版不同的回答；",
        "请把上一条回答压缩到三句话以内，只保留结论。",
        "请针对上一条回答给出一个具体例子。",
        "请用英文重写上一条回答，保留事实与结构。",
        "请用中文重写上一条回答，保留事实与结构。",
        "\n\n回答语言：中文。", "\n\n回答语言：English。",
        "\n\n这个系列已经确认的经验（优先遵守）：\n",
        "\n\n用户加载的技能（按适用部分执行）：\n",
        "\n\n英语回答时，在每句下用 Markdown 引用行 `> 译：中文意思` 附中文对照。",
        "\n联网搜索结果（可能过时，引用时标注链接）：\n",
        "\n当前幻灯片（第 {page.Number} 页，{page.DocumentName}）：\n{page.Text}\n",
        "\n刚截取的屏幕 OCR 文本：\n{ScreenContextText[..Math.Min(ScreenContextText.Length, 3000)]}\n",
        "\n这个系列的题库（历次复盘确认过的回答）：\n{bank}\n",
        "- {result.Title}（{result.Url}）：{result.Snippet}",
        "最近的会议转写：\n{transcript}\n\n参考资料：\n{KeywordIndex.Context(sources)}\n{slide}{bankBlock}{webBlock}\n当前问题：{question}",
        "\u3000"
    ];

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "App.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop => Path.Combine(RepoRoot(), "Windows", "src", "NextThought.Desktop");

    [Fact]
    public void EveryMarkupExtensionKeyHasAnEnglishTranslation()
    {
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Desktop, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (Match match in TrAttribute.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
                key = System.Net.WebUtility.HtmlDecode(key);
                if (UserDataStrings.Contains(key, StringComparer.Ordinal)) continue;
                if (!UiText.HasTranslation(key)) missing.Add(key);
            }
        }
        Assert.True(missing.Count == 0,
            "interface strings without English wording:\n" + string.Join("\n", missing.Select(key => "  " + key)));
    }

    [Fact]
    public void EveryChineseLiteralInTheShellHasAnEnglishTranslation()
    {
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Desktop, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (ForeignFiles.Contains(name, StringComparer.Ordinal)) continue;
            if (file.Contains(Path.DirectorySeparatorChar + "SelfTest" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                foreach (Match match in StringLiteral.Matches(line))
                {
                    var literal = Unescape(match.Groups[1].Value);
                    if (!Cjk.IsMatch(literal)) continue;
                    if (PromptStrings.Contains(literal, StringComparer.Ordinal)) continue;
                    if (UserDataStrings.Contains(literal, StringComparer.Ordinal)) continue;
                    if (UiText.HasTranslation(literal)) continue;
                    missing.Add($"{Path.GetFileName(file)}:{index + 1}: {literal}");
                }
            }
        }
        Assert.True(missing.Count == 0,
            "Chinese literals with no English wording (wrap in UiText.T/Format or add to the Windows table):\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void WindowsTableKeysAreChineseSourceSentences()
    {
        // A Windows-only entry must carry a Chinese key (it is the source language) and non-empty wording.
        Assert.All(UiText.WindowsStrings, pair =>
        {
            Assert.True(Cjk.IsMatch(pair.Key), "Windows table key is not Chinese: " + pair.Key);
            Assert.False(string.IsNullOrWhiteSpace(pair.Value), "empty wording for " + pair.Key);
        });
    }

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character != '\\' || index + 1 >= value.Length)
            {
                builder.Append(character);
                continue;
            }
            var next = value[++index];
            switch (next)
            {
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case 'u' when index + 4 < value.Length:
                    builder.Append((char)Convert.ToInt32(value.Substring(index + 1, 4), 16));
                    index += 4;
                    break;
                default: builder.Append('\\').Append(next); break;
            }
        }
        return builder.ToString();
    }
}
