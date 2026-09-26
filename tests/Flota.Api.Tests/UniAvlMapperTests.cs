using Flota.Api.Infrastructure.UniAvl;
using Flota.Core.Contracts;

namespace Flota.Api.Tests;

public class UniAvlMapperTests
{
    [Fact]
    public void Deserialization_ToleratesReferenceMetadata()
    {
        var objects = Samples.TrackingObjects();
        Assert.Equal(2, objects.Count);
        Assert.Equal("S-101", objects[0].Device?.SenderId);
        // Powtórzenie w postaci {"$ref": …} — obiekt pusty, bez wyjątku
        Assert.NotNull(objects[1].Company);
        Assert.Null(objects[1].Company!.CompanyName);
    }

    [Fact]
    public void ToFleetObject_MapsDeviceAndDriver()
    {
        var o = UniAvlMapper.ToFleetObject(Samples.TrackingObjects()[0], null);
        Assert.Equal(101, o.Id);
        Assert.Equal("WX 12345", o.Name);
        Assert.Equal("GPS CAN", o.DeviceName);
        Assert.Equal("Jan Kowalski", o.DriverName);
        Assert.Equal(80, o.TankCapacity);
    }

    [Fact]
    public void ToFleetObject_WithoutDevice_HasEmptyDeviceName()
    {
        var o = UniAvlMapper.ToFleetObject(Samples.TrackingObjects()[1], null);
        Assert.Equal(string.Empty, o.DeviceName);
        Assert.Equal(string.Empty, o.DriverName);
        Assert.Null(o.SenderId);
    }

    [Fact]
    public void MatchMessages_BySenderTakesLatest_ThenBySensor()
    {
        var match = UniAvlMapper.MatchMessages(Samples.TrackingObjects(), Samples.LastMessages());
        Assert.Equal(1001, match[101].TrackingMessageId);
        Assert.Equal(2000, match[102].TrackingMessageId);
    }

    [Fact]
    public void ToPosition_MapsFieldsAndFuelLevel()
    {
        var m = Samples.LastMessages()[0];
        var p = UniAvlMapper.ToPosition(m, tankCapacity: 80);
        Assert.Equal(52.2297, p.Latitude);
        Assert.Equal(21.0122, p.Longitude);
        Assert.True(p.ValidGps);
        Assert.Equal(-90, p.Heading);
        Assert.Equal(40.0, p.FuelLevel);
        var door = Assert.Single(p.Sensors);
        Assert.Equal("Drzwi", door.Name);
        Assert.True(door.BoolValue);
    }

    [Fact]
    public void ToSensor_NumericValue_IsText()
    {
        var s = UniAvlMapper.ToSensor(Samples.LastMessages()[2].Sensors![0]);
        Assert.Null(s.BoolValue);
        Assert.False(string.IsNullOrEmpty(s.Value));
    }

    [Fact]
    public void ToRouteReport_SummaryAndSensorActivity()
    {
        var r = UniAvlMapper.ToRouteReport(101, Samples.Route(), null);
        Assert.Equal("WX 12345", r.ObjectName);
        Assert.Equal(80, r.TankCapacity);
        Assert.Equal(TimeSpan.FromHours(5.5), r.RideTime);
        Assert.True(r.HasFuelData);
        Assert.Equal(8.0, r.FuelConsumption!.Value, 1);

        var names = r.SensorActivity.Select(x => x.Name).ToList();
        Assert.DoesNotContain(names, n => n.StartsWith('$'));
        Assert.DoesNotContain(names, n => n.Contains("Stacyjka", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Hamulec", StringComparison.Ordinal)); // 00:00:00 pomijane
        Assert.Contains(r.SensorActivity, x => x.Value == "00:10:00");
        Assert.Contains(r.SensorActivity, x => x.Value == "12,3");
    }

    [Fact]
    public void ToRouteReport_StageColumnsFromFirstStages()
    {
        var r = UniAvlMapper.ToRouteReport(101, Samples.Route(), null);
        var keys = r.StageColumns.Select(c => c.Name).ToList();
        Assert.Contains("TotalDistanceCAN", keys);
        Assert.Contains("FuelUsed", keys);
        Assert.Contains("FuelUsage", keys);
        Assert.Contains(keys, k => k.StartsWith("S:Drzwi", StringComparison.Ordinal));
        Assert.DoesNotContain(keys, k => k.Contains('$', StringComparison.Ordinal));

        Assert.Equal(2, r.Stages.Count);
        Assert.Equal(StageType.Ride, r.Stages[0].Type);
        Assert.Equal(8.0, r.Stages[0].FuelUsage!.Value, 1);
        var second = r.Stages[1];
        Assert.All(second.Values, v => Assert.Equal(string.Empty, v.Value));
    }

    [Fact]
    public void ToFleetRow_ReadsSensorActivityByTrimmedName()
    {
        var report = Samples.FleetReport();
        var day = report.DailyReports![0];
        var row = UniAvlMapper.ToFleetRow(101, "WX 12345", "Renault Trafic", day.DateTime,
            day.DailyReportRouteDetail, day.DailyReportFuelDetail, day.DailyReportSensorDetails?.Values);
        Assert.Equal("00:05:00", row.Doors);
        Assert.Equal(100.25, row.Distance);
        Assert.Equal(8, row.FuelConsumption);

        var total = UniAvlMapper.ToFleetRow(101, "WX 12345", "Renault Trafic", null,
            report.DailyReportRouteDetail, report.DailyReportFuelDetail, report.DailyReportSensorDetails);
        Assert.Equal("00:30:00", total.Brake);
        Assert.Equal("45:00:00", total.StopTime);
    }
}
