using System.Text.RegularExpressions;

namespace Flota.App.Tests;

/// <summary>Paleta kolorów (T1.4): wartości motywu jasnego zgodne z tabelą 7.1 planu; motyw ciemny kompletny.</summary>
public sealed partial class PaletteTests
{
    /// <summary>Wiersze tabeli 7.1 planu: zasób → wartość (zasoby i wartości w apostrofach wstecznych, w tej samej kolejności).</summary>
    public static TheoryData<string, string> PlanTable()
    {
        var plan = File.ReadAllLines(Path.Combine(AppSources.Root, "docs", "Plan-migracji-Uno.md"));
        var start = Array.FindIndex(plan, l => l.StartsWith("### 7.1.", StringComparison.Ordinal));
        var data = new TheoryData<string, string>();
        foreach (var line in plan.Skip(start + 1).TakeWhile(l => !l.StartsWith("###", StringComparison.Ordinal)))
        {
            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            if (cells.Length < 4 || !cells[2].Contains('#', StringComparison.Ordinal)) continue;
            var names = Code().Matches(cells[1]).Select(m => m.Groups[1].Value).ToList();
            var values = Code().Matches(cells[2]).Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(names.Count, values.Count);
            for (var i = 0; i < names.Count; i++) data.Add(names[i], values[i]);
        }
        return data;
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Code();

    [Theory]
    [MemberData(nameof(PlanTable))]
    public void LightTheme_MatchesPlanTable(string resource, string value)
    {
        var palette = AppSources.Palette("Light");
        var colorKey = resource.EndsWith("Brush", StringComparison.Ordinal) ? AppSources.BrushColors()[resource] : resource;
        Assert.True(palette.ContainsKey(colorKey), $"Brak zasobu {colorKey} w motywie jasnym");
        Assert.Equal(value.ToUpperInvariant(), palette[colorKey]);
    }

    [Fact]
    public void PlanTable_CoversStatusBrushes()
    {
        var resources = PlanTable().Select(r => r.Data.Item1).ToList();
        Assert.Contains("PrimaryColor", resources);
        Assert.Contains("StatusOutdatedBrush", resources);
        Assert.Equal(13, resources.Count);
    }

    [Fact]
    public void DarkTheme_DefinesAllLightResources()
    {
        var light = AppSources.Palette("Light");
        var dark = AppSources.Palette("Dark");
        Assert.Empty(light.Keys.Except(dark.Keys));
        Assert.Empty(dark.Keys.Except(light.Keys));
    }

    [Fact]
    public void DarkTheme_StatusColorsUnchanged()
    {
        // Plan 7.1: kolory statusów bez zmian w motywie ciemnym
        var light = AppSources.Palette("Light");
        var dark = AppSources.Palette("Dark");
        foreach (var key in light.Keys.Where(k => k.StartsWith("Status", StringComparison.Ordinal)))
            Assert.Equal(light[key], dark[key]);
    }

    [Fact]
    public void Brushes_ReferToPaletteColors()
    {
        var light = AppSources.Palette("Light");
        foreach (var (brush, color) in AppSources.BrushColors())
        {
            if (color.Length == 0) continue;
            Assert.True(light.ContainsKey(color) || color.StartsWith("SystemColor", StringComparison.Ordinal), $"{brush}: nieznany kolor {color}");
        }
    }
}
