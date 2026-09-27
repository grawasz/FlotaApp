using Flota.Core.Status;

namespace Flota.App.Services;

/// <summary>Pędzel koloru statusu obiektu z zasobów palety (<c>Styles/ColorPaletteOverride.xaml</c>).</summary>
public static class StatusBrushes
{
    public static Brush For(TrackingObjectStatus status) => (Brush)Application.Current.Resources[status switch
    {
        TrackingObjectStatus.Move => "StatusMovingBrush",
        TrackingObjectStatus.Stop => "StatusStoppedBrush",
        TrackingObjectStatus.InvalidGps => "StatusNoGpsBrush",
        TrackingObjectStatus.Outdated => "StatusOutdatedBrush",
        _ => "StatusUnknownBrush",
    }];
}
