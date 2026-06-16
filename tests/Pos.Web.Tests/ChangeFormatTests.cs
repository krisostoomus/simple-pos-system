using System.Globalization;
using Pos.Web.Components;

namespace Pos.Web.Tests;

public class ChangeFormatTests
{
    [Fact]
    public void FormatEuro_FormatsCentsAsCurrency()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-IE"); // euro, dot decimal
            Assert.Equal("€1.30", ProductCard.FormatEuro(130));
            Assert.Equal("€0.00", ProductCard.FormatEuro(0));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
