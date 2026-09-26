using System.Text;

namespace Flota.Core.Geo;

/// <summary>Adres punktu z geokodowania odwrotnego (struktura odpowiedzi Targeo).</summary>
public sealed record AddressInfo
{
    public string Country { get; init; } = string.Empty;
    public string Province { get; init; } = string.Empty;
    public string District { get; init; } = string.Empty;
    public string Community { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Quarter { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string House { get; init; } = string.Empty;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(City) && string.IsNullOrWhiteSpace(Province) &&
        string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(District);

    /// <summary>Miejscowość, a przy jej braku województwo (kolumna „Miasto”, „Lokalizacja”).</summary>
    public string CityOrProvince => !string.IsNullOrWhiteSpace(City) ? City : Province;

    /// <summary>Ulica, a przy jej braku powiat (kolumna „Ulica”).</summary>
    public string StreetOrDistrict => !string.IsNullOrWhiteSpace(Street) ? Street : District;

    /// <summary>
    /// Pełny adres: „Miasto (Województwo[ - Powiat poza PL]),⏎Ulica Numer”
    /// (odpowiednik <c>Address.FullAddress</c>).
    /// </summary>
    public string FullAddress
    {
        get
        {
            if (IsEmpty) return string.Empty;
            var sb = new StringBuilder(City);
            if (!string.IsNullOrWhiteSpace(Province))
            {
                sb.Append(" (").Append(Province);
                if (Country != "PL" && !string.IsNullOrWhiteSpace(District)) sb.Append(" - ").Append(District);
                sb.Append(')');
            }
            if (!string.IsNullOrWhiteSpace(Street))
            {
                sb.Append(',').Append('\n').Append(Street);
                if (!string.IsNullOrWhiteSpace(House) && !Street.Contains(House, StringComparison.Ordinal))
                    sb.Append(' ').Append(House);
            }
            return sb.ToString();
        }
    }

    /// <summary>Pełny adres w jednym wierszu (listy, eksport).</summary>
    public string SingleLine => FullAddress.Replace(",\n", ", ", StringComparison.Ordinal);

    public override string ToString() => FullAddress;
}
