using System.Globalization;

namespace Pos.Web.Helpers;

/// <summary>Money formatting shared across components (money is integer cents end to end).</summary>
public static class Money
{
    /// <summary>Formats integer cents as a currency string in the active culture, e.g. 130 → "€1.30".</summary>
    public static string FormatEuro(int cents) => (cents / 100m).ToString("C", CultureInfo.CurrentCulture);
}
