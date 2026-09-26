using System.Collections.Concurrent;
using Flota.Api.Auth;
using Flota.Api.Infrastructure.Geocoding;
using Flota.Api.Infrastructure.UniAvl;
using Flota.Api.Infrastructure.UniAvl.Dto;
using Flota.Core.Contracts;
using Flota.Core.Geo;
using Flota.Core.Series;
using Microsoft.Extensions.Caching.Memory;

namespace Flota.Api.Services;

/// <summary>Konfiguracja współbieżności wywołań UniAVL.</summary>
public sealed class ReportOptions
{
    public const string Section = "Reports";

    /// <summary>Liczba równoczesnych żądań do UniAVL w obrębie jednego raportu.</summary>
    public int MaxConcurrency { get; set; } = 6;
    /// <summary>Maksymalna liczba punktów serii wykresu po redukcji (LTTB).</summary>
    public int ChartPoints { get; set; } = 1000;
}

/// <summary>Raporty obiektu i raporty zbiorcze (P5: agregacja i równoległość po stronie serwera).</summary>
public sealed class ReportService(
    UniAvlClient avl,
    FleetService fleet,
    GeocodingService geocoding,
    IMemoryCache cache,
    Microsoft.Extensions.Options.IOptions<ReportOptions> options)
{
    readonly ReportOptions _options = options.Value;

    public async Task<RouteReport?> RouteAsync(ActiveSession s, int objectId, RouteQuery q, CancellationToken ct)
    {
        var route = await avl.GetRouteAsync(s.AvlToken, objectId, q.From, q.To,
            q.StopTime ?? TimeSpan.Zero, q.IgnitionOffTime ?? TimeSpan.Zero, q.StopTime is null ? 0 : q.MinSpeed, q.StageSubtypeBySensor, ct);
        if (route is null) return null;
        var o = await fleet.FindObjectAsync(s, objectId, ct);
        return UniAvlMapper.ToRouteReport(objectId, route, o);
    }

    /// <summary>Punkty trasy wybranych kursów z adresami i kolumnami czujników (odpowiednik <c>GetMessages</c> + <c>GetWaypoints</c>).</summary>
    public async Task<WaypointsResponse> WaypointsAsync(ActiveSession s, int objectId, IReadOnlyList<StageWindow> stages, CancellationToken ct)
    {
        var o = await fleet.FindObjectAsync(s, objectId, ct);
        var parts = new AvlTrackingMessage[stages.Count][];
        await Parallel.ForEachAsync(Enumerable.Range(0, stages.Count), Limit(ct), async (i, token) =>
        {
            var w = stages[i];
            parts[i] = [.. await avl.GetMessagesAsync(s.AvlToken, objectId, w.BeginDate, w.EndDate, (int)w.Type, token) ?? []];
        });

        var messages = parts.SelectMany(p => p).Where(m => m.ValidGPS == true).ToList();
        var positions = messages.Select(m => UniAvlMapper.ToPosition(m, o?.TankCapacity)).ToList();
        positions = [.. await geocoding.WithAddressesAsync(positions, ct)];

        var columns = positions.FirstOrDefault()?.Sensors.Where(x => x.Visible).OrderBy(x => x.Order).Select(x => x.Name).Distinct().ToList() ?? [];
        var points = positions.Select((p, i) => new Waypoint
        {
            Id = i,
            Position = p,
            SensorValues = columns.Select(c => p.Sensors.FirstOrDefault(x => x.Name == c)).ToList(),
        }).ToList();
        return new WaypointsResponse(columns, points);
    }

    /// <summary>Ślad obiektu w okresie (tryb „Śledź”; odpowiednik <c>DrawTrace</c> — wiadomości od ostatniej pozycji − 5 min).</summary>
    public async Task<IReadOnlyList<ObjectPosition>> TraceAsync(ActiveSession s, int objectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var o = await fleet.FindObjectAsync(s, objectId, ct);
        var messages = await avl.GetMessagesAsync(s.AvlToken, objectId, from, to, null, ct) ?? [];
        var positions = messages.Where(m => m.ValidGPS == true).OrderBy(m => m.Timedate)
            .Select(m => UniAvlMapper.ToPosition(m, o?.TankCapacity)).ToList();
        return await geocoding.WithAddressesAsync(positions, ct);
    }

    public async Task<FuelReport?> FuelAsync(ActiveSession s, int objectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var f = await avl.GetFuelReportAsync(s.AvlToken, objectId, from, to, ct);
        if (f is null) return null;
        var addresses = await geocoding.ResolveAsync((f.Events ?? []).Where(e => e.Location is not null)
            .Select(e => new GeoPoint(e.Location!.Latitude, e.Location.Longitude)), ct);
        var report = UniAvlMapper.ToFuelReport(f, addresses);
        return report with
        {
            TimeSeries = Lttb.Downsample(report.TimeSeries, _options.ChartPoints, p => p.Time.Ticks, p => p.Value),
            DistanceSeries = Lttb.Downsample(report.DistanceSeries, _options.ChartPoints, p => p.Kilometers, p => p.Value),
        };
    }

    public async Task<IReadOnlyList<ObjectEvent>> EventsAsync(ActiveSession s, int objectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var events = await avl.GetEventsAsync(s.AvlToken, objectId, from, to, ct) ?? [];
        var addresses = await geocoding.ResolveAsync(events.Where(e => e.Location is not null)
            .Select(e => new GeoPoint(e.Location!.Latitude, e.Location.Longitude)), ct);
        return events.OrderByDescending(e => e.EventDate).Select(e => UniAvlMapper.ToEvent(e, addresses)).ToList();
    }

    public async Task<DrivingStyle?> DrivingStyleAsync(ActiveSession s, int objectId, DrivingStyleQuery q, CancellationToken ct)
    {
        var r = q.Route;
        var d = await avl.GetDrivingStyleAsync(s.AvlToken, objectId, r.From, r.To,
            r.StopTime ?? TimeSpan.Zero, r.IgnitionOffTime ?? TimeSpan.Zero, r.StopTime is null ? 0 : r.MinSpeed,
            q.Speeding, q.ExceedRpm, q.SuddenBraking, q.QuickAcceleration, ct);
        return d is null ? null : UniAvlMapper.ToDrivingStyle(d);
    }

    /// <summary>Podsumowanie kursów: adres postojów z pierwszej wiadomości kursu (odpowiednik <c>GetStgSum</c>).</summary>
    public async Task<IReadOnlyList<RouteStage>> StageSummaryAsync(ActiveSession s, int objectId, RouteQuery q, CancellationToken ct)
    {
        var report = await RouteAsync(s, objectId, q, ct);
        if (report is null) return [];
        var stops = report.Stages.Where(x => x.Type == StageType.Stop).ToList();
        var first = new ConcurrentDictionary<int, AvlTrackingMessage>();
        await Parallel.ForEachAsync(stops, Limit(ct), async (stage, token) =>
        {
            var msgs = await avl.GetMessagesAsync(s.AvlToken, objectId, stage.BeginDate, stage.EndDate, (int)StageType.Stop, token) ?? [];
            var m = msgs.Where(x => x.ValidGPS == true && x.Location is not null).OrderBy(x => x.Timedate).FirstOrDefault();
            if (m is not null) first[stage.Index] = m;
        });
        var addresses = await geocoding.ResolveAsync(first.Values.Select(m => new GeoPoint(m.Location!.Latitude, m.Location.Longitude)), ct);
        return report.Stages.Select(st => first.TryGetValue(st.Index, out var m)
            ? st with { Address = UniAvlMapper.AddressFor(m.Location, addresses) }
            : st).ToList();
    }

    /// <summary>Raport floty: podsumowanie na obiekt i szczegóły dzienne wszystkich obiektów.</summary>
    public async Task<FleetReportResponse> FleetReportAsync(ActiveSession s, FleetReportRequest r, CancellationToken ct)
    {
        var objects = (await fleet.GetObjectsAsync(s, refresh: false, ct)).ToDictionary(o => o.TrackingObjectId);
        var results = new ConcurrentDictionary<int, AvlFleetReport>();
        await Parallel.ForEachAsync(r.ObjectIds.Distinct(), Limit(ct), async (id, token) =>
        {
            var report = await avl.GetFleetReportAsync(s.AvlToken, id, r.From, r.To, token);
            if (report is not null) results[id] = report;
        });

        var summary = new List<FleetReportRow>();
        var details = new List<FleetReportRow>();
        foreach (var id in r.ObjectIds.Distinct())
        {
            if (!results.TryGetValue(id, out var f)) continue;
            objects.TryGetValue(id, out var o);
            var name = o?.TrackingObjectName ?? f.DailyReports?.FirstOrDefault()?.TrackingObject?.TrackingObjectName ?? string.Empty;
            var make = o?.MakeModel ?? string.Empty;
            summary.Add(UniAvlMapper.ToFleetRow(id, name, make, null, f.DailyReportRouteDetail, f.DailyReportFuelDetail, f.DailyReportSensorDetails));
            foreach (var d in f.DailyReports ?? [])
                details.Add(UniAvlMapper.ToFleetRow(id, name, make, d.DateTime, d.DailyReportRouteDetail, d.DailyReportFuelDetail, d.DailyReportSensorDetails?.Values));
        }
        return new FleetReportResponse(summary, details.OrderBy(d => d.Date).ThenBy(d => d.ObjectName, StringComparer.CurrentCulture).ToList());
    }

    /// <summary>Ranking Eco-Driving: średnia ocen cech stylu jazdy (0–100), malejąco.</summary>
    public async Task<IReadOnlyList<EcoDrivingRow>> EcoDrivingAsync(ActiveSession s, EcoDrivingRequest r, CancellationToken ct)
    {
        var objects = (await fleet.GetObjectsAsync(s, refresh: false, ct)).ToDictionary(o => o.TrackingObjectId);
        var rows = new ConcurrentBag<EcoDrivingRow>();
        await Parallel.ForEachAsync(r.ObjectIds.Distinct(), Limit(ct), async (id, token) =>
        {
            var d = await avl.GetDrivingStyleAsync(s.AvlToken, id, r.From, r.To, TimeSpan.Zero, TimeSpan.FromMinutes(1), 0, 50, 2000, 1, 1, token);
            if (d is null) return;
            var style = UniAvlMapper.ToDrivingStyle(d);
            double? F(int type) => style.Features.FirstOrDefault(f => f.Type == type)?.Value;
            var values = style.Features.Where(f => f.Value is not null).Select(f => f.Value!.Value).ToList();
            objects.TryGetValue(id, out var o);
            rows.Add(new EcoDrivingRow
            {
                ObjectId = id,
                ObjectName = o?.TrackingObjectName ?? string.Empty,
                RegistrationNumber = o?.RegistrationNumber ?? string.Empty,
                Score = values.Count > 0 ? Math.Round(values.Average(), 0) : null,
                Speeding = F(0),
                ExceedRpm = F(1),
                SuddenBraking = F(2),
                QuickAcceleration = F(3),
                SpeedingTime = style.SpeedingTime,
                ExceedRpmTime = style.ExceedRpmTime,
                SuddenBrakings = style.SuddenBrakings,
                QuickAccelerations = style.QuickAccelerations,
            });
        });
        return rows.OrderByDescending(x => x.Score ?? -1).ThenBy(x => x.ObjectName, StringComparer.CurrentCulture)
            .Select((x, i) => x with { Position = i + 1 }).ToList();
    }

    public async Task<IReadOnlyList<LogbookInfo>> LogbooksAsync(ActiveSession s, DateTime from, DateTime to, CancellationToken ct) =>
        (await avl.GetLogbooksAsync(s.AvlToken, from, to, ct) ?? []).Select(UniAvlMapper.ToLogbook).OrderByDescending(l => l.Date).ToList();

    public async Task<IReadOnlyList<LogbookEntry>> LogbookEntriesAsync(ActiveSession s, int logbookId, CancellationToken ct) =>
        (await avl.GetLogbookDetailsAsync(s.AvlToken, logbookId, ct) ?? []).Select(UniAvlMapper.ToLogbookEntry).OrderBy(e => e.BeginTime).ToList();

    /// <summary>Słowniki z pamięcią podręczną 10 min na sesję.</summary>
    public Task<IReadOnlyList<DriverInfo>> DriversAsync(ActiveSession s, CancellationToken ct) =>
        Cached($"drivers:{s.Id}", async () => (IReadOnlyList<DriverInfo>)(await avl.GetDriversAsync(s.AvlToken, ct) ?? [])
            .Select(UniAvlMapper.ToDriver).OrderBy(d => d.LastName, StringComparer.CurrentCulture).ThenBy(d => d.FirstName, StringComparer.CurrentCulture).ToList());

    public Task<IReadOnlyList<EquipmentInfo>> EquipmentAsync(ActiveSession s, bool refresh, CancellationToken ct)
    {
        if (refresh) cache.Remove($"equipment:{s.Id}");
        return Cached($"equipment:{s.Id}", async () => (IReadOnlyList<EquipmentInfo>)(await avl.GetEquipmentAsync(s.AvlToken, ct) ?? [])
            .Select(UniAvlMapper.ToEquipment).OrderBy(e => e.ObjectName, StringComparer.CurrentCulture).ToList());
    }

    public async Task<IReadOnlyList<EventTemplateInfo>> EventTemplatesAsync(ActiveSession s, IReadOnlyList<int> objectIds, CancellationToken ct)
    {
        var objects = (await fleet.GetObjectsAsync(s, refresh: false, ct)).ToDictionary(o => o.TrackingObjectId);
        var result = new ConcurrentBag<EventTemplateInfo>();
        await Parallel.ForEachAsync(objectIds.Distinct(), Limit(ct), async (id, token) =>
        {
            var name = objects.TryGetValue(id, out var o) ? o.TrackingObjectName ?? string.Empty : string.Empty;
            foreach (var t in await avl.GetEventTemplatesAsync(s.AvlToken, id, token) ?? [])
                result.Add(UniAvlMapper.ToEventTemplate(t, id, name));
        });
        return result.OrderBy(t => t.ObjectName, StringComparer.CurrentCulture).ThenBy(t => t.Name, StringComparer.CurrentCulture).ToList();
    }

    async Task<T> Cached<T>(string key, Func<Task<T>> factory)
    {
        if (cache.TryGetValue(key, out T? value) && value is not null) return value;
        value = await factory();
        cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), Size = 1 });
        return value;
    }

    ParallelOptions Limit(CancellationToken ct) => new() { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency) };
}
