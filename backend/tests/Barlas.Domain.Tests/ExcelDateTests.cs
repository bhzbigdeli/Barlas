using Barlas.Infrastructure.Orders;

namespace Barlas.Domain.Tests;

public class ExcelDateTests
{
    [Theory]
    [InlineData("1405/09/03", 2026, 11, 24)]
    [InlineData("۱۴۰۵/۰۹/۰۳", 2026, 11, 24)]
    [InlineData("2026-11-24", 2026, 11, 24)]
    public void Parses_jalali_and_gregorian_dates(string raw, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), ExcelOrderSource.ParseDateText(raw));

    [Theory]
    [InlineData("نامعتبر")]
    [InlineData("1405/13/01")]
    public void Rejects_invalid_dates(string raw) => Assert.Null(ExcelOrderSource.ParseDateText(raw));
}
