using Flota.App.Controls;
using Flota.App.Services;
using Flota.Core.Contracts;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Zdarzenia”: szablony zdarzeń i terminy (przypomnienia) wybranych obiektów.</summary>
public sealed partial class EventsPanel : UserControl
{
    static readonly IReadOnlyList<TableColumn> TemplateColumns =
    [
        new() { Header = Loc.Get("ColObject"), Text = o => ((EventTemplateInfo)o).ObjectName, Width = 140 },
        new() { Header = Loc.Get("ColName"), Text = o => ((EventTemplateInfo)o).Name, Width = 180 },
        new() { Header = Loc.Get("ColDuration"), Text = o => ((EventTemplateInfo)o).Duration, Width = 100 },
        new() { Header = Loc.Get("ColValidFrom"), Text = o => ((EventTemplateInfo)o).BeginHour, Width = 100 },
        new() { Header = Loc.Get("ColValidTo"), Text = o => ((EventTemplateInfo)o).EndHour, Width = 100 },
    ];

    static readonly IReadOnlyList<TableColumn> ReminderColumns =
    [
        new() { Header = Loc.Get("ColObject"), Text = o => ((EventTemplateInfo)o).ObjectName, Width = 140 },
        new() { Header = Loc.Get("ColName"), Text = o => ((EventTemplateInfo)o).Name, Width = 180 },
        new() { Header = Loc.Get("ColDue"), Text = o => Loc.DateTimeShort(((EventTemplateInfo)o).DateTimeValue), Width = 130,
                SortKey = o => ((EventTemplateInfo)o).DateTimeValue, ExportValue = o => Loc.Raw(((EventTemplateInfo)o).DateTimeValue), ExportKind = ExportColumnKind.DateTime },
        new() { Header = Loc.Get("ColNotification"), Text = o => Loc.DateTimeShort(((EventTemplateInfo)o).DateTimeValue), Width = 130,
                SortKey = o => ((EventTemplateInfo)o).DateTimeValue, ExportValue = o => Loc.Raw(((EventTemplateInfo)o).DateTimeValue), ExportKind = ExportColumnKind.DateTime },
    ];

    readonly FleetViewModel _fleet;
    readonly EventsViewModel _vm = App.Services.GetRequiredService<EventsViewModel>();

    public EventsPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(EventsViewModel.Templates):
                    TemplatesTable.SetData(TemplateColumns, _vm.Templates);
                    break;
                case nameof(EventsViewModel.Reminders):
                    RemindersTable.SetData(ReminderColumns, _vm.Reminders);
                    break;
                case nameof(EventsViewModel.IsBusy):
                    Busy.IsActive = _vm.IsBusy;
                    RefreshButton.IsEnabled = !_vm.IsBusy;
                    break;
            }
        };
        TemplatesTable.FooterText = Loc.Get("TotalFormat");
        RemindersTable.FooterText = Loc.Get("TotalFormat");
        TemplatesTable.SetData(TemplateColumns, Array.Empty<object>());
        RemindersTable.SetData(ReminderColumns, Array.Empty<object>());
        _fleet.ObjectsReset += (_, _) => Picker.SetObjects(_fleet.FleetObjects);
        _fleet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FleetViewModel.SelectedObject) && _fleet.SelectedObject is { } s && Picker.SelectedIds.Count == 0)
                Picker.SetSelected([s.Id]);
        };
        Picker.SetObjects(_fleet.FleetObjects);
    }

    void OnTabClick(object sender, RoutedEventArgs e)
    {
        var reminders = (string)((FrameworkElement)sender).Tag == "1";
        TabTemplates.IsChecked = !reminders;
        TabReminders.IsChecked = reminders;
        TemplatesTable.Visibility = reminders ? Visibility.Collapsed : Visibility.Visible;
        RemindersTable.Visibility = reminders ? Visibility.Visible : Visibility.Collapsed;
    }

    async void OnRefreshClick(object sender, RoutedEventArgs e) => await _vm.LoadAsync(Picker.SelectedIds);
}
