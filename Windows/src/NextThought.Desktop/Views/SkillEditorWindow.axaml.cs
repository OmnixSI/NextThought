using Avalonia.Controls;
using Avalonia.Interactivity;
using NextThought.Core;

namespace NextThought.Desktop.Views;

/// <summary>
/// E28: Mac <c>SkillEditor</c> sheet. Shows the four Skill fields, keeps the Mac character-count
/// hint live and returns the edited skill on 保存 (Cancel returns null). The settings page never
/// edits skills inline any more.
/// </summary>
public partial class SkillEditorWindow : Window
{
    private readonly Skill skill;

    public SkillEditorWindow() : this(new Skill()) { }

    public SkillEditorWindow(Skill skill)
    {
        this.skill = skill;
        InitializeComponent();
        NameBox.Text = skill.Name;
        SummaryBox.Text = skill.Summary;
        ContentBox.Text = skill.Content;
        GlobalBox.IsChecked = skill.Global;
        UpdateLength();
        ContentBox.TextChanged += (_, _) => UpdateLength();
    }

    public static async Task<Skill?> EditAsync(Window owner, Skill skill)
    {
        var dialog = new SkillEditorWindow(skill);
        return await dialog.ShowDialog<Skill?>(owner);
    }

    private void UpdateLength() =>
        LengthText.Text = UiText.Format("%lld 字 · 单个技能最多使用约 6000 字", ContentBox.Text?.Length ?? 0);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void Save_Click(object? sender, RoutedEventArgs e) =>
        Close(skill with
        {
            Name = NameBox.Text ?? "",
            Summary = SummaryBox.Text ?? "",
            Content = ContentBox.Text ?? "",
            Global = GlobalBox.IsChecked == true
        });
}
