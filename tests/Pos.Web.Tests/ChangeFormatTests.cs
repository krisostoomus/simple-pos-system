using System.Globalization;
using Pos.Web.Helpers;

namespace Pos.Web.Tests;

public class ChangeFormatTests
{
    [Fact]
    public void FormatEuro_FormatsCentsAsCurrency()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            // Neutral "en" is what the app actually runs under; its own currency symbol is "¤",
            // so this guards that FormatEuro still forces "€".
            CultureInfo.CurrentCulture = new CultureInfo("en");
            Assert.Equal("1.30 €", Money.FormatEuro(130));
            Assert.Equal("0.00 €", Money.FormatEuro(0));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
