using Flota.App.Controls;
using Flota.App.Services;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Obszary”: lista obszarów; wybór przesuwa mapę do obszaru.</summary>
public sealed partial class AreasPanel : UserControl
{
    readonly FleetViewModel _fleet;
    readonly IReadOnlyList<TableColumn> _columns =
    [
        new() { Header = Loc.Get("ColName"), Text = o => ((AreaInfo)o).Name, Width = 260 },
    ];

    public AreasPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        _fleet.AreasChanged += (_, _) => Fill();
        Table.SelectionChanged += (_, items) =>
        {
            if (items.FirstOrDefault() is AreaInfo a) _fleet.CenterOnArea(a);
        };
        Fill();
    }

    void Fill() => Table.SetData(_columns, _fleet.ActiveAreas);
}
