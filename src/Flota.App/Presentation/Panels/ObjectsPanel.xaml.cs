using System.ComponentModel;
using Flota.App.Services;

namespace Flota.App.Presentation.Panels;

/// <summary>Panel „Obiekty”: lista obiektów z widocznością na mapie, statusem i lokalizacją.</summary>
public sealed partial class ObjectsPanel : UserControl
{
    readonly FleetViewModel _fleet;
    bool _syncing;

    public ObjectsPanel()
    {
        InitializeComponent();
        _fleet = App.Services.GetRequiredService<FleetViewModel>();
        List.ItemsSource = _fleet.Objects;
        _fleet.PropertyChanged += OnFleetChanged;
        _fleet.Objects.CollectionChanged += (_, _) => UpdateCount();
        _fleet.ObjectsReset += (_, _) => UpdateAllVisible();
        UpdateSortIcons();
        UpdateCount();
        // Panel tworzony przy pierwszym wyświetleniu — stan zaznaczenia, śledzenia i wczytywania z modelu
        foreach (var property in new[] { nameof(FleetViewModel.SelectedObject), nameof(FleetViewModel.IsTracking), nameof(FleetViewModel.IsLoading) })
            OnFleetChanged(this, new PropertyChangedEventArgs(property));
    }

    void OnFleetChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FleetViewModel.SelectedObject):
                _syncing = true;
                List.SelectedItem = _fleet.SelectedObject;
                if (_fleet.SelectedObject is not null) List.ScrollIntoView(_fleet.SelectedObject);
                _syncing = false;
                break;
            case nameof(FleetViewModel.IsTracking):
                TraceButton.IsChecked = _fleet.IsTracking;
                break;
            case nameof(FleetViewModel.IsLoading):
                RefreshButton.IsEnabled = !_fleet.IsLoading;
                break;
        }
    }

    async void OnRefreshClick(object sender, RoutedEventArgs e) => await _fleet.RefreshAsync();

    async void OnTraceClick(object sender, RoutedEventArgs e) => await _fleet.ToggleTrackingAsync();

    void OnFilterChanged(object sender, TextChangedEventArgs e) => _fleet.FilterText = FilterBox.Text;

    void OnSortClick(object sender, RoutedEventArgs e)
    {
        _fleet.SetSort(Enum.Parse<ObjectSort>((string)((Button)sender).Tag));
        UpdateSortIcons();
    }

    void UpdateSortIcons()
    {
        string Glyph(ObjectSort s) => _fleet.Sort == s ? (_fleet.SortDescending ? "" : "") : string.Empty;
        SortName.Glyph = Glyph(ObjectSort.Name);
        SortStatus.Glyph = Glyph(ObjectSort.Status);
        SortLocation.Glyph = Glyph(ObjectSort.Location);
    }

    void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || List.SelectedItem is not ObjectItem item || item == _fleet.SelectedObject) return;
        _fleet.Select(item);
    }

    void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (List.SelectedItem is ObjectItem item) _fleet.ZoomTo(item);
    }

    void OnStatusClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ObjectItem item) _fleet.Select(item);
    }

    void OnVisibleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ObjectItem item) _fleet.OnVisibilityChanged(item);
        UpdateAllVisible();
    }

    void OnAllVisibleClick(object sender, RoutedEventArgs e) => _fleet.SetAllVisible(AllVisibleBox.IsChecked == true);

    void UpdateAllVisible() =>
        AllVisibleBox.IsChecked = _fleet.AllObjects.Where(o => o.Enabled).All(o => o.IsVisibleOnMap);

    void UpdateCount()
    {
        var all = _fleet.AllObjects.Count;
        CountText.Text = Loc.Format("ObjectsCount", _fleet.Objects.Count, all);
        UpdateAllVisible();
    }
}
