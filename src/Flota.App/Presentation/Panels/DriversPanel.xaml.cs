using Flota.App.Controls;
using Flota.App.Services;
using Flota.Client;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Kierowcy”: wczytywany przy pierwszym otwarciu (plan, 6.1 — słowniki leniwie).</summary>
public sealed partial class DriversPanel : UserControl
{
    readonly IReadOnlyList<TableColumn> _columns =
    [
        new() { Header = Loc.Get("ColLastName"), Text = o => ((DriverInfo)o).LastName, Width = 140 },
        new() { Header = Loc.Get("ColFirstName"), Text = o => ((DriverInfo)o).FirstName, Width = 120 },
        new() { Header = Loc.Get("ColActive"), Text = o => ((DriverInfo)o).IsActive ? "✓" : string.Empty, Width = 70, Alignment = TextAlignment.Center,
                ExportValue = o => ((DriverInfo)o).IsActive ? "true" : "false", ExportKind = ExportColumnKind.Boolean },
    ];
    bool _loaded;

    public DriversPanel()
    {
        InitializeComponent();
        Table.SetData(_columns, Array.Empty<object>());
        RegisterPropertyChangedCallback(VisibilityProperty, async (_, _) =>
        {
            if (Visibility == Visibility.Visible && !_loaded) await LoadAsync();
        });
    }

    async Task LoadAsync()
    {
        _loaded = true;
        Busy.IsActive = true;
        try
        {
            var drivers = await App.Services.GetRequiredService<FlotaApiClient>().GetDriversAsync(CancellationToken.None);
            Table.SetData(_columns, drivers);
        }
        catch (Exception ex) when (ex is not SessionExpiredException)
        {
            _loaded = false;
            App.Services.GetRequiredService<Notifier>().Error(ex);
        }
        finally
        {
            Busy.IsActive = false;
        }
    }
}
