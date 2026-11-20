namespace NextThought.Desktop.Views;

public partial class MainWindow
{
    private void ShowSection()
    {
        var section = state.Section;
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
                AppSection.Library => new LibraryView(),
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
