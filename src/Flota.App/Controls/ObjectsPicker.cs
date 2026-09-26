using Flota.App.Services;
using Flota.Core.Contracts;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Flota.App.Controls;

/// <summary>Wybór wielu obiektów z filtrem tekstowym (odpowiednik listy „Wybierz obiekty...”).</summary>
public sealed partial class ObjectsPicker : UserControl
{
    readonly DropDownButton _button = new() { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
    readonly TextBlock _buttonText = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBox _filter = new();
    readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Multiple, MaxHeight = 360, MinWidth = 280 };
    readonly CheckBox _all = new();
    IReadOnlyList<FleetObject> _objects = [];
    readonly HashSet<int> _selected = [];
    bool _updating;

    public ObjectsPicker()
    {
        _button.Content = _buttonText;
        _filter.PlaceholderText = Loc.Get("TableFilterPlaceholder");
        _filter.TextChanged += (_, _) => Refill();
        _all.Content = Loc.Get("SelectAll");
        _all.Checked += (_, _) => SetAll(true);
        _all.Unchecked += (_, _) => SetAll(false);
        _list.DisplayMemberPath = nameof(PickerItem.Text);
        _list.SelectionChanged += OnListSelectionChanged;

        var panel = new StackPanel { Spacing = 6, Padding = new Thickness(4) };
        panel.Children.Add(_filter);
        panel.Children.Add(_all);
        panel.Children.Add(_list);
        _button.Flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        AutomationProperties.SetName(_button, Loc.Get("PickObjects"));
        Content = _button;
        UpdateText();
    }

    public event EventHandler? SelectionChanged;

    public IReadOnlyList<int> SelectedIds => _objects.Where(o => _selected.Contains(o.Id)).Select(o => o.Id).ToList();

    public void SetObjects(IReadOnlyList<FleetObject> objects)
    {
        _objects = objects.Where(o => o.IsActive).OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        _selected.IntersectWith(_objects.Select(o => o.Id));
        Refill();
        UpdateText();
    }

    public void SetSelected(IEnumerable<int> ids)
    {
        _selected.Clear();
        _selected.UnionWith(ids);
        Refill();
        UpdateText();
    }

    void Refill()
    {
        var f = _filter.Text?.Trim();
        var items = _objects.Where(o => string.IsNullOrEmpty(f) || o.Name.Contains(f, StringComparison.CurrentCultureIgnoreCase)
                                        || o.RegistrationNumber.Contains(f, StringComparison.CurrentCultureIgnoreCase))
            .Select(o => new PickerItem(o.Id, string.IsNullOrEmpty(o.RegistrationNumber) ? o.Name : $"{o.Name} ({o.RegistrationNumber})")).ToList();
        _updating = true;
        _list.ItemsSource = items;
        foreach (var i in items.Where(i => _selected.Contains(i.Id))) _list.SelectedItems.Add(i);
        _all.IsChecked = items.Count > 0 && items.All(i => _selected.Contains(i.Id));
        _updating = false;
    }

    void SetAll(bool value)
    {
        if (_updating) return;
        foreach (var i in (_list.ItemsSource as IEnumerable<PickerItem>) ?? [])
            if (value) _selected.Add(i.Id);
            else _selected.Remove(i.Id);
        Refill();
        UpdateText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;
        foreach (var i in e.AddedItems.OfType<PickerItem>()) _selected.Add(i.Id);
        foreach (var i in e.RemovedItems.OfType<PickerItem>()) _selected.Remove(i.Id);
        UpdateText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    void UpdateText()
    {
        var n = _selected.Count;
        _buttonText.Text = n switch
        {
            0 => Loc.Get("PickObjects"),
            1 => _objects.FirstOrDefault(o => _selected.Contains(o.Id))?.Name ?? Loc.Get("PickObjects"),
            _ => Loc.Format("PickedObjects", n),
        };
    }

    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed partial class PickerItem(int id, string text)
    {
        public int Id { get; } = id;
        public string Text { get; } = text;
        public override string ToString() => Text;
    }
}
