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
            CultureInfo.CurrentCulture = new CultureInfo("en-IE"); // euro, dot decimal
            Assert.Equal("€1.30", Money.FormatEuro(130));
            Assert.Equal("€0.00", Money.FormatEuro(0));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
