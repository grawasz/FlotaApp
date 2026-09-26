using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Flota.App.Controls;

/// <summary>
/// Panel zwijany (odpowiednik <c>RadzenPanel</c> aplikacji źródłowej): ikona 16×16, tytuł pogrubiony w kolorze akcentu,
/// opcjonalny pasek narzędzi, treść.
/// </summary>
[TemplatePart(Name = "PART_Toggle", Type = typeof(ButtonBase))]
public sealed partial class PanelFrame : ContentControl
{
    public PanelFrame()
    {
        DefaultStyleKey = typeof(PanelFrame);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PanelFrame), new PropertyMetadata(null));

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(PanelFrame), new PropertyMetadata(null, (d, e) =>
            ((PanelFrame)d).IconSource = e.NewValue is string s && s.Length > 0 ? new BitmapImage(new Uri($"ms-appx:///Assets/Images/{s}")) : null));

    /// <summary>Nazwa pliku ikony w <c>Assets/Images</c>.</summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(
        nameof(IconSource), typeof(ImageSource), typeof(PanelFrame), new PropertyMetadata(null));

    public ImageSource? IconSource
    {
        get => (ImageSource?)GetValue(IconSourceProperty);
        private set => SetValue(IconSourceProperty, value);
    }

    public static readonly DependencyProperty ToolbarProperty = DependencyProperty.Register(
        nameof(Toolbar), typeof(object), typeof(PanelFrame), new PropertyMetadata(null, (d, e) =>
            ((PanelFrame)d).ToolbarVisibility = e.NewValue is null ? Visibility.Collapsed : Visibility.Visible));

    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    public static readonly DependencyProperty ToolbarVisibilityProperty = DependencyProperty.Register(
        nameof(ToolbarVisibility), typeof(Visibility), typeof(PanelFrame), new PropertyMetadata(Visibility.Collapsed));

    public Visibility ToolbarVisibility
    {
        get => (Visibility)GetValue(ToolbarVisibilityProperty);
        private set => SetValue(ToolbarVisibilityProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(PanelFrame), new PropertyMetadata(true, (d, e) => ((PanelFrame)d).UpdateExpanded()));

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public static readonly DependencyProperty BodyVisibilityProperty = DependencyProperty.Register(
        nameof(BodyVisibility), typeof(Visibility), typeof(PanelFrame), new PropertyMetadata(Visibility.Visible));

    public Visibility BodyVisibility
    {
        get => (Visibility)GetValue(BodyVisibilityProperty);
        private set => SetValue(BodyVisibilityProperty, value);
    }

    public static readonly DependencyProperty ChevronGlyphProperty = DependencyProperty.Register(
        nameof(ChevronGlyph), typeof(string), typeof(PanelFrame), new PropertyMetadata(""));

    public string ChevronGlyph
    {
        get => (string)GetValue(ChevronGlyphProperty);
        private set => SetValue(ChevronGlyphProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Toggle") is ButtonBase toggle)
        {
            toggle.Click -= OnToggle;
            toggle.Click += OnToggle;
            AutomationProperties.SetName(toggle, Services.Loc.Get("PanelToggle"));
        }
        UpdateExpanded();
    }

    void OnToggle(object sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    void UpdateExpanded()
    {
        BodyVisibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        ChevronGlyph = IsExpanded ? "" : "";
        if (GetTemplateChild("PART_Chevron") is FontIcon chevron) chevron.Glyph = ChevronGlyph;
    }
}
