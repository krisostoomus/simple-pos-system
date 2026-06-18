using System.Globalization;

namespace Pos.Web.Helpers;

/// <summary>Money formatting shared across components (money is integer cents end to end).</summary>
public static class Money
{
    /// <summary>Formats integer cents as euros with the symbol trailing the amount, e.g. 130 → "1.30 €"
    /// ("1,30 €" in Estonian). The € symbol is forced because the UI runs under a neutral culture (en/et)
    /// whose own currency symbol is the generic "¤"; the number itself is still formatted per the active
    /// culture. The trailing space follows the Estonian/EU convention.</summary>
    public static string FormatEuro(int cents) => $"{(cents / 100m).ToString("N2", CultureInfo.CurrentCulture)} €";
}
