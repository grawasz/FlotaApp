using Flota.App.Services;
using Flota.Client;

namespace Flota.App.Presentation;

/// <summary>Logowanie (odpowiednik <c>Pages/Login.razor</c>); hasło nie jest zapisywane.</summary>
public sealed partial class LoginPage : Page
{
    public LoginPage()
    {
        InitializeComponent();
        Loaded += (_, _) => UserBox.Focus(FocusState.Programmatic);
    }

    async void OnLoginClick(object sender, RoutedEventArgs e) => await LoginAsync();

    async void OnFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        if (sender == UserBox) PasswordBox.Focus(FocusState.Programmatic);
        else await LoginAsync();
    }

    async Task LoginAsync()
    {
        var login = UserBox.Text?.Trim() ?? string.Empty;
        var password = PasswordBox.Password ?? string.Empty;
        UserError.Visibility = login.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PasswordError.Visibility = password.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorBar.IsOpen = false;
        if (login.Length == 0 || password.Length == 0) return;

        SetBusy(true);
        try
        {
            var api = App.Services.GetRequiredService<FlotaApiClient>();
            if (api.BaseAddress is null)
            {
                ShowError(Loc.Get("ErrorNoServerAddress"));
                return;
            }
            var result = await api.LoginAsync(login, password, CancellationToken.None);
            if (result is null)
            {
                ShowError(Loc.Get("LoginFailed"));
                return;
            }
            PasswordBox.Password = string.Empty;
            App.Current.ShowShell();
        }
        catch (Exception ex) when (ex is FlotaApiException or HttpRequestException or TaskCanceledException)
        {
            ShowError(ex is FlotaApiException api ? api.Message : Loc.Get("ErrorNoConnection"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    void ShowError(string message)
    {
        ErrorBar.Title = Loc.Get("NoticeError");
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        UserBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
}
