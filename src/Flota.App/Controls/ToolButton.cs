using Microsoft.UI.Xaml.Media.Imaging;

namespace Flota.App.Controls;

/// <summary>
/// Przycisk paska narzędzi: ikona 16×16 i etykieta w kolorze akcentu; w trybie kompaktowym tylko ikona z podpowiedzią.
/// Stan <see cref="IsChecked"/> — wygląd wciśnięty (przezroczystość 0,5, obramowanie wklęsłe), jak w aplikacji źródłowej.
/// </summary>
public sealed partial class ToolButton : Button
{
    readonly Image _image = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _label = new() { Margin = new Thickness(5, 0, 0, 0) };
    readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };

    public ToolButton()
    {
        DefaultStyleKey = typeof(ToolButton);
        _panel.Children.Add(_image);
        _panel.Children.Add(_label);
        Content = _panel;
        Loaded += (_, _) =>
        {
            if (Application.Current.Resources.TryGetValue("FlotaLabelTextStyle", out var s) && s is Style style)
                _label.Style = style;
            Update();
        };
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(ToolButton), new PropertyMetadata(null, (d, _) => ((ToolButton)d).Update()));

    /// <summary>Nazwa pliku w <c>Assets/Images</c>, np. <c>refresh_16x16.png</c>.</summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ToolButton), new PropertyMetadata(null, (d, _) => ((ToolButton)d).Update()));

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(ToolButton), new PropertyMetadata(false, (d, _) => ((ToolButton)d).Update()));

    /// <summary>Tylko ikona (ekrany wąskie).</summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(ToolButton), new PropertyMetadata(false, (d, _) => ((ToolButton)d).Update()));

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    void Update()
    {
        _image.Source = string.IsNullOrEmpty(Icon) ? null : new BitmapImage(new Uri($"ms-appx:///Assets/Images/{Icon}"));
        _label.Text = Label ?? string.Empty;
        _label.Visibility = IsCompact || string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(this, string.IsNullOrEmpty(Label) ? null : Label);
        AutomationProperties.SetName(this, Label ?? string.Empty);
        VisualStateManager.GoToState(this, IsChecked ? "Checked" : "Unchecked", true);
    }
}
