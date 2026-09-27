using CommunityToolkit.Mvvm.ComponentModel;
using Flota.Client;

namespace Flota.Presentation;

/// <summary>Model widoku z operacją w toku i komunikatem błędu dla użytkownika.</summary>
public abstract partial class BusyViewModel(Notifier notifier) : ObservableObject
{
    protected Notifier Notifier { get; } = notifier;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Wykonanie operacji ze wskaźnikiem zajętości; błąd zgłaszany użytkownikowi (wygaśnięcie sesji obsługuje powłoka).
    /// Zwraca <c>true</c> po powodzeniu.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not SessionExpiredException)
        {
            Notifier.Error(ex);
            return false;
        }
        catch (SessionExpiredException)
        {
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Wymagany wybór co najmniej jednego obiektu.</summary>
    protected bool RequireObjects(IReadOnlyCollection<int> ids)
    {
        if (ids.Count > 0) return true;
        Notifier.Info(Loc.Get("PickObjectsFirst"));
        return false;
    }
}
