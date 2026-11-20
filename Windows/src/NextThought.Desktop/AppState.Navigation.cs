namespace NextThought.Desktop;

public partial class AppState
{

    public bool OnHome => Section == AppSection.Home;

    public bool OnLive => Section == AppSection.Live;

    public bool OnNotes => Section == AppSection.Notes;

    public bool OnSettings => Section == AppSection.Settings;
}
