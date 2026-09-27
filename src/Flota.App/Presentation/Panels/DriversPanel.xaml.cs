using Flota.App.Controls;
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
    readonly DriversViewModel _vm = App.Services.GetRequiredService<DriversViewModel>();

    public DriversPanel()
    {
        InitializeComponent();
        Table.SetData(_columns, Array.Empty<object>());
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DriversViewModel.Drivers)) Table.SetData(_columns, _vm.Drivers);
            else if (e.PropertyName == nameof(DriversViewModel.IsBusy)) Busy.IsActive = _vm.IsBusy;
        };
        RegisterPropertyChangedCallback(VisibilityProperty, async (_, _) =>
        {
            if (Visibility == Visibility.Visible) await _vm.EnsureLoadedAsync();
        });
    }
}
