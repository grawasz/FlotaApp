namespace Flota.App.Presentation;

public partial class MainViewModel : ObservableObject
{
    private IAuthenticationService _authentication;


    public MainViewModel(
        IStringLocalizer localizer,
        IOptions<AppConfig> appInfo,
        IAuthenticationService authentication,
        INavigator navigator)
    {
        _authentication = authentication;
        Title = "Main";
        Title += $" - {localizer["ApplicationName"]}";
        Title += $" - {appInfo?.Value?.Environment}";
        Logout = new AsyncRelayCommand(DoLogout);
    }
    public string? Title { get; }


    public ICommand Logout { get; }

    public async Task DoLogout(CancellationToken token)
    {
        await _authentication.LogoutAsync(token);
    }
}
