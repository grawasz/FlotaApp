using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Flota.App.Tests;

/// <summary>Pliki źródłowe aplikacji: XAML, zasoby tekstowe, plan migracji.</summary>
static partial class AppSources
{
    public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static string Root { get; } = typeof(AppSources).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "RepositoryRoot").Value!;

    public static string App => Path.Combine(Root, "src", "Flota.App");

    /// <summary>Widoki: strony, panele i kontrolki (bez stylów).</summary>
    public static IEnumerable<string> ViewFiles() =>
        Directory.EnumerateFiles(Path.Combine(App, "Presentation"), "*.xaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(App, "Controls"), "*.xaml", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal);

    public static IEnumerable<string> CodeFiles() =>
        Directory.EnumerateFiles(App, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "src", "Flota.Presentation"), "*.cs", SearchOption.AllDirectories))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    /// <summary>Zasoby tekstowe <c>Strings/pl-PL/Resources.resw</c>: klucz → wartość.</summary>
    public static IReadOnlyDictionary<string, string> Strings { get; } =
        XDocument.Load(Path.Combine(App, "Strings", "pl-PL", "Resources.resw")).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    /// <summary>Kolory palety motywu (<c>Light</c> / <c>Dark</c>): klucz → <c>#RRGGBB</c>.</summary>
    public static IReadOnlyDictionary<string, string> Palette(string theme)
    {
        var doc = XDocument.Load(Path.Combine(App, "Styles", "ColorPaletteOverride.xaml"));
        return doc.Descendants().Single(e => e.Name.LocalName == "ResourceDictionary" && (string?)e.Attribute(X + "Key") == theme)
            .Elements().Where(e => e.Name.LocalName == "Color")
            .ToDictionary(e => (string)e.Attribute(X + "Key")!, e => e.Value.Trim().ToUpperInvariant(), StringComparer.Ordinal);
    }

    /// <summary>Pędzle stylów: klucz pędzla → klucz koloru (<c>Color="{ThemeResource …}"</c>).</summary>
    public static IReadOnlyDictionary<string, string> BrushColors()
    {
        var doc = XDocument.Load(Path.Combine(App, "Styles", "FlotaStyles.xaml"));
        return doc.Descendants().Where(e => e.Name.LocalName == "SolidColorBrush" && e.Attribute(X + "Key") is not null)
            .ToDictionary(e => (string)e.Attribute(X + "Key")!, e => ResourceRef().Match((string?)e.Attribute("Color") ?? string.Empty).Groups[1].Value, StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\{(?:ThemeResource|StaticResource)\s+(\w+)\}")]
    public static partial Regex ResourceRef();
}
