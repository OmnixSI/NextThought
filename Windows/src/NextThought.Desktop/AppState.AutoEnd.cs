namespace NextThought.Desktop;

public partial class AppState
{

    [ObservableProperty] private string autoEndNotice = "";
    public bool HasAutoEndNotice => AutoEndNotice.Length > 0;

    partial void OnAutoEndNoticeChanged(string value) => OnPropertyChanged(nameof(HasAutoEndNotice));

    private int autoEndTicks;
}
