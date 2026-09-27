using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Flota.App.Tests;

/// <summary>
/// Reguły widoków: nazwa dostępna każdej kontrolki interaktywnej (T7.4), teksty interfejsu w zasobach <c>.resw</c>,
/// kolory wyłącznie z palety (zasady pracy — konwencje kodu).
/// </summary>
public sealed partial class ViewRulesTests
{
    const string AutomationNameKey = "[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name";

    /// <summary>Kontrolki, dla których czytnik ekranu wymaga nazwy.</summary>
    static readonly HashSet<string> Interactive = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "CheckBox", "RadioButton", "TextBox", "PasswordBox", "ComboBox", "NumberBox",
        "CalendarDatePicker", "TimePicker", "DatePicker", "Slider", "ToggleSwitch", "HyperlinkButton", "AutoSuggestBox",
    };

    static IEnumerable<(string File, XElement Element)> Elements() =>
        AppSources.ViewFiles().SelectMany(f => XDocument.Load(f, LoadOptions.SetLineInfo).Descendants().Select(e => (f, e)));

    static string? Attr(XElement e, string localName) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName == localName && a.Name.Namespace != AppSources.X)?.Value;

    static bool HasUidText(XElement e, params string[] properties) =>
        e.Attribute(AppSources.X + "Uid")?.Value is { } uid && properties.Any(p => AppSources.Strings.ContainsKey($"{uid}.{p}"));

    static string Where(string file, XElement e) => $"{AppSources.Relative(file)}:{((System.Xml.IXmlLineInfo)e).LineNumber} <{e.Name.LocalName}>";

    /// <summary>Nazwa dostępna: treść tekstowa, nagłówek, nazwa automatyzacji lub etykieta wskazana przez <c>LabeledBy</c>.</summary>
    static bool HasAccessibleName(XElement e)
    {
        if (e.Name.LocalName == "ToolButton")
            return !string.IsNullOrWhiteSpace(Attr(e, "Label")) || HasUidText(e, "Label");
        var name = e.Attributes().FirstOrDefault(a => a.Name.LocalName is "AutomationProperties.Name" or "AutomationProperties.LabeledBy")?.Value;
        return !string.IsNullOrWhiteSpace(name)
               || !string.IsNullOrWhiteSpace(Attr(e, "Content"))
               || !string.IsNullOrWhiteSpace(Attr(e, "Header"))
               || HasUidText(e, "Content", "Header", AutomationNameKey);
    }

    [Fact]
    public void InteractiveControls_HaveAccessibleName()
    {
        var missing = Elements()
            .Where(x => Interactive.Contains(x.Element.Name.LocalName) || x.Element.Name.LocalName == "ToolButton")
            .Where(x => !HasAccessibleName(x.Element))
            .Select(x => Where(x.File, x.Element))
            .ToList();
        Assert.True(missing.Count == 0, "Kontrolki bez nazwy dostępnej:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void Uids_HaveResourceEntries()
    {
        var missing = Elements()
            .Where(x => x.Element.Attribute(AppSources.X + "Uid") is not null)
            .Where(x => !AppSources.Strings.Keys.Any(k => k.StartsWith(x.Element.Attribute(AppSources.X + "Uid")!.Value + ".", StringComparison.Ordinal)))
            .Select(x => $"{Where(x.File, x.Element)} x:Uid=\"{x.Element.Attribute(AppSources.X + "Uid")!.Value}\"")
            .ToList();
        Assert.True(missing.Count == 0, "x:Uid bez wpisu w Resources.resw:\n" + string.Join('\n', missing));
    }

    [GeneratedRegex("""Loc\.(?:Get|Format)\("([A-Za-z0-9_]+)"[,)]""")]
    private static partial Regex LocKey();

    [Fact]
    public void CodeTextKeys_ExistInResources()
    {
        var missing = AppSources.CodeFiles()
            .SelectMany(f => LocKey().Matches(File.ReadAllText(f)).Select(m => (File: f, Key: m.Groups[1].Value)))
            .Where(x => !AppSources.Strings.ContainsKey(x.Key))
            .Select(x => $"{AppSources.Relative(x.File)}: {x.Key}")
            .Distinct()
            .ToList();
        Assert.True(missing.Count == 0, "Klucze tekstów bez wpisu w Resources.resw:\n" + string.Join('\n', missing));
    }

    [GeneratedRegex("^(#[0-9A-Fa-f]{3,8}|Red|Green|Blue|White|Black|Gray|Grey|Orange|Yellow|Transparent|DarkGray|LightGray)$")]
    private static partial Regex LiteralColor();

    static readonly HashSet<string> ColorProperties = new(StringComparer.Ordinal)
    {
        "Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "PlaceholderForeground",
    };

    [Fact]
    public void Views_UsePaletteResourcesOnly()
    {
        var literal = Elements()
            .SelectMany(x => x.Element.Attributes().Select(a => (x.File, x.Element, Attribute: a)))
            .Where(x => ColorProperties.Contains(x.Attribute.Name.LocalName) && LiteralColor().IsMatch(x.Attribute.Value.Trim()))
            .Select(x => $"{Where(x.File, x.Element)} {x.Attribute.Name.LocalName}=\"{x.Attribute.Value}\"")
            .ToList();
        Assert.True(literal.Count == 0, "Kolory poza paletą:\n" + string.Join('\n', literal));
    }
}
