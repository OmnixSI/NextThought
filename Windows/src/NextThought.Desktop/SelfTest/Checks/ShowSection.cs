namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private void ShowSection()
    {
        var section = state.Section;
        // D1: demo mode shows each page with the Mac demo section data, like the Mac screenshots
        // script that re-launches the app per NEXTTHOUGHT_DEMO_SECTION.
        if (StartupOptions.Demo) state.ApplyDemoSection(section switch
        {
            AppSection.Home => "home",
            AppSection.Prepare => "prepare",
            AppSection.Live => "live",
            AppSection.Notes => "notes",
            AppSection.History => "history",
            AppSection.Series => "series",
            _ => ""
        });
        if (!pages.TryGetValue(section, out var page))
        {
            page = section switch
            {
                AppSection.Home => new HomeView(),
                AppSection.Prepare => new PrepareView(),
                AppSection.Live => new LiveView(),
                AppSection.Notes => new NotesView(),
                AppSection.History => new HistoryView(),
                AppSection.Series => new SeriesView(),
                // §3.5: Mac 3.4 has no separate library page; materials live in the series detail.
                AppSection.Library => new SeriesView(),
                AppSection.Settings => new SettingsView(),
                _ => new HomeView()
            };
            pages[section] = page;
        }
        if (!ReferenceEquals(PageHost.Content, page))
        {
            PageHost.Content = page;
            page.Opacity = 0;
            _ = FadeInAsync(page);
        }
    }

    /// <summary>Measures the lazy page host: pages built at startup, then working set after visiting all.</summary>
}
