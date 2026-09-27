namespace Flota.Presentation;

public enum NoticeSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

/// <summary>Komunikat dla użytkownika (odpowiednik powiadomień Radzen, bez szczegółów technicznych — B4).</summary>
public sealed record Notice(NoticeSeverity Severity, string Title, string Message);

/// <summary>Powiadomienia wyświetlane w powłoce aplikacji.</summary>
public sealed class Notifier
{
    public event EventHandler<Notice>? Raised;

    public void Info(string message) => Raised?.Invoke(this, new Notice(NoticeSeverity.Informational, Loc.Get("NoticeInfo"), message));

    public void Success(string message) => Raised?.Invoke(this, new Notice(NoticeSeverity.Success, string.Empty, message));

    public void Error(string message) => Raised?.Invoke(this, new Notice(NoticeSeverity.Error, Loc.Get("NoticeError"), message));

    /// <summary>Komunikat błędu z wyjątku: treść z Flota.Api albo komunikat ogólny.</summary>
    public void Error(Exception ex) => Error(ex switch
    {
        Flota.Client.FlotaApiException api => api.Message,
        Flota.Client.SessionExpiredException s => s.Message,
        HttpRequestException => Loc.Get("ErrorNoConnection"),
        TaskCanceledException => Loc.Get("ErrorTimeout"),
        _ => Loc.Get("ErrorGeneral"),
    });
}
