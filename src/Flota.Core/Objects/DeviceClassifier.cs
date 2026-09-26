namespace Flota.Core.Objects;

/// <summary>Klasyfikacja urządzenia (odpowiednik <c>TrackingObject.DeviceName</c>).</summary>
public static class DeviceClassifier
{
    public const string GpsCan = "GPS CAN";
    public const string GpsStandard = "GPS standard";

    static readonly HashSet<string> CanModels = new(StringComparer.Ordinal)
    {
        "Lincor LX Micro", "Lincor L350", "Lincor LX1", "Lincor L35", "Skaut 4", "Skaut 2",
    };

    /// <summary>Nazwa klasy urządzenia; pusta, gdy obiekt nie ma urządzenia.</summary>
    public static string Classify(string? deviceModel, bool hasDevice = true)
    {
        if (!hasDevice) return string.Empty;
        return deviceModel is not null && CanModels.Contains(deviceModel) ? GpsCan : GpsStandard;
    }
}
