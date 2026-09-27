namespace Flota.UniAvlSim;

/// <summary>Zachowanie obiektu w symulacji.</summary>
internal enum SimBehavior
{
    /// <summary>Cykl: jazda po pętli, następnie postój z wyłączoną stacyjką.</summary>
    Driving,
    /// <summary>Postój w stałym miejscu; urządzenie nadaje.</summary>
    Parked,
    /// <summary>Urządzenie nadaje bez poprawnej pozycji GPS.</summary>
    NoGps,
    /// <summary>Brak wiadomości od kilku godzin (status „Nieaktualny”).</summary>
    Offline,
}

/// <summary>Obiekt floty symulacji (dane fikcyjne).</summary>
internal sealed record SimObject(
    int Id,
    string Name,
    string Registration,
    int Type,
    string Manufacturer,
    string Model,
    string? SenderId,
    string? DeviceModel,
    double Latitude,
    double Longitude,
    double RadiusKm,
    double SpeedKmh,
    int? TankCapacity,
    int DriverId,
    SimBehavior Behavior,
    int PhaseMinutes,
    bool IsActive = true,
    bool HasLogbook = false,
    bool HasCan = true);

/// <summary>Kierowca symulacji.</summary>
internal sealed record SimDriver(int Id, string Login, string FirstName, string LastName, bool IsActive = true);

/// <summary>Obszar symulacji: prostokąt wokół punktu.</summary>
internal sealed record SimArea(int Id, string Name, double Latitude, double Longitude, double HalfSizeKm, int FillColor, int OutlineColor);

/// <summary>Flota demonstracyjna: nazwy, numery rejestracyjne i osoby są fikcyjne.</summary>
internal static class SimFleet
{
    public const int UserId = 900;
    public const string CompanyName = "Firma demonstracyjna";

    public static readonly IReadOnlyList<SimDriver> Drivers =
    [
        new(901, "akowalska", "Anna", "Kowalska"),
        new(902, "bnowak", "Bartosz", "Nowak"),
        new(903, "cwisniewski", "Cezary", "Wiśniewski"),
        new(904, "dwojcik", "Dorota", "Wójcik"),
        new(905, "ekaminski", "Emil", "Kamiński"),
        new(906, "flewandowska", "Filomena", "Lewandowska"),
        new(907, "gzielinski", "Grzegorz", "Zieliński"),
        new(908, "hszymanska", "Halina", "Szymańska", IsActive: false),
    ];

    public static readonly IReadOnlyList<SimObject> Objects =
    [
        new(101, "Renault Trafic", "WX 12345", 1, "Renault", "Trafic", "SIM-101", "Skaut 2", 52.2297, 21.0122, 4.0, 42, 80, 901, SimBehavior.Driving, 0, HasLogbook: true),
        new(102, "Skoda Octavia", "WX 20231", 0, "Skoda", "Octavia", "SIM-102", "Skaut 2", 52.2550, 20.9800, 3.0, 38, 55, 902, SimBehavior.Driving, 17, HasLogbook: true),
        new(103, "Volvo FH 500", "WX 30442", 3, "Volvo", "FH 500", "SIM-103", "Lincor L350", 52.1500, 21.0500, 12.0, 68, 600, 903, SimBehavior.Driving, 31),
        new(104, "MAN TGS", "WX 40553", 2, "MAN", "TGS", "SIM-104", "Lincor L350", 52.1900, 20.9500, 0, 0, 400, 904, SimBehavior.Parked, 0),
        new(105, "Iveco Daily", "KR 51234", 1, "Iveco", "Daily", "SIM-105", "Skaut 2", 50.0614, 19.9366, 3.5, 35, 90, 905, SimBehavior.Driving, 9),
        new(106, "Solaris Urbino 12", "KR 62345", 4, "Solaris", "Urbino 12", "SIM-106", "Lincor L350", 50.0800, 19.9500, 5.0, 28, 300, 906, SimBehavior.Driving, 23),
        new(107, "Ford Transit", "GD 71234", 1, "Ford", "Transit", "SIM-107", "Skaut 2", 54.3520, 18.6466, 0, 0, 80, 907, SimBehavior.NoGps, 0),
        new(108, "Toyota Corolla", "GD 82345", 0, "Toyota", "Corolla", "SIM-108", "Skaut 2", 54.3800, 18.6000, 0, 0, 50, 902, SimBehavior.Offline, 0),
        new(109, "Koparka CAT 320", "WX 93456", 22, "Caterpillar", "320", "SIM-109", "GT06", 52.3000, 21.1000, 0, 0, 350, 903, SimBehavior.Parked, 0, HasCan: false),
        new(110, "Radiotelefon 1", "", 19, "", "", "SIM-110", "Radiotelefon", 52.2400, 21.0300, 0.8, 5, null, 905, SimBehavior.Driving, 5, HasCan: false),
        new(111, "Mercedes Sprinter", "PO 11234", 5, "Mercedes-Benz", "Sprinter", "SIM-111", "Skaut 2", 52.4064, 16.9252, 6.0, 55, 75, 906, SimBehavior.Driving, 41),
        new(112, "Honda CB500", "WX 12121", 12, "Honda", "CB500", null, null, 52.2200, 21.0000, 0, 0, 17, 907, SimBehavior.Parked, 0, IsActive: false, HasCan: false),
    ];

    public static readonly IReadOnlyList<SimArea> Areas =
    [
        new(1, "Baza Warszawa", 52.2297, 21.0122, 0.6, 0x0000FF, 0x0000A0),
        new(2, "Magazyn Kraków", 50.0614, 19.9366, 0.5, 0x00A5FF, 0x005A8C),
        new(3, "Parking Gdańsk", 54.3520, 18.6466, 0.4, 0x00C000, 0x006000),
    ];

    public static SimObject? Find(int id) => Objects.FirstOrDefault(o => o.Id == id);

    public static SimDriver DriverOf(SimObject o) => Drivers.First(d => d.Id == o.DriverId);
}
