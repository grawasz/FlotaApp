namespace Flota.App.Presentation;

/// <summary>Widoczność paneli strony głównej (przełączniki paska nagłówka).</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    [ObservableProperty] public partial bool ShowObjects { get; set; } = true;
    [ObservableProperty] public partial bool ShowDetails { get; set; } = true;
    [ObservableProperty] public partial bool ShowMap { get; set; } = true;
    [ObservableProperty] public partial bool ShowAreas { get; set; }
    [ObservableProperty] public partial bool ShowDrivers { get; set; }
    [ObservableProperty] public partial bool ShowEvents { get; set; }
    [ObservableProperty] public partial bool ShowMessages { get; set; }
    [ObservableProperty] public partial bool ShowRouteReport { get; set; }
    [ObservableProperty] public partial bool ShowFleetReport { get; set; }
    [ObservableProperty] public partial bool ShowEcoDriving { get; set; }
    [ObservableProperty] public partial bool ShowLogbooks { get; set; }
    [ObservableProperty] public partial bool ShowEquipment { get; set; }

    /// <summary>Tryb kompaktowy: ekran wąski lub orientacja pionowa (odpowiednik <c>isMobile</c>).</summary>
    [ObservableProperty] public partial bool IsCompact { get; set; }

    public bool AnyLeft => ShowObjects || ShowDetails || ShowAreas || ShowDrivers;

    public bool AnyRight => ShowEvents || ShowMessages || ShowRouteReport || ShowFleetReport || ShowEcoDriving || ShowLogbooks || ShowEquipment;

    /// <summary>Stan początkowy jak w aplikacji źródłowej: w orientacji pionowej tylko mapa.</summary>
    public void Initialize(bool portrait)
    {
        ShowObjects = !portrait;
        ShowDetails = !portrait;
        ShowMap = true;
    }
}
