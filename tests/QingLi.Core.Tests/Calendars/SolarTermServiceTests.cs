using QingLi.Core.Calendars;

namespace QingLi.Core.Tests.Calendars;

public sealed class SolarTermServiceTests
{
    [Theory]
    [InlineData(2026, 2, 4, "立春")]
    [InlineData(2026, 6, 21, "夏至")]
    [InlineData(1901, 2, 4, "立春")]
    [InlineData(2100, 12, 22, "冬至")]
    public void Returns_known_solar_terms(int year, int month, int day, string name)
    {
        var actual = new SolarTermService().GetName(new DateOnly(year, month, day));

        Assert.Equal(name, actual);
    }

    [Theory]
    [InlineData(1901)]
    [InlineData(2100)]
    public void Boundary_years_expose_24_solar_terms_without_throwing(int year)
    {
        var service = new SolarTermService();
        var terms = new List<string>();

        foreach (var date in EachDateOfYear(year))
        {
            var exception = Record.Exception(() => service.GetName(date));
            Assert.Null(exception);

            var value = service.GetName(date);
            if (value is not null)
            {
                terms.Add(value);
            }
        }

        Assert.Equal(24, terms.Count);
    }

    [Fact]
    public void Declared_supported_range_has_200_years_and_each_year_returns_24_terms()
    {
        Assert.Equal(1901, SolarTermService.MinSupportedYear);
        Assert.Equal(2100, SolarTermService.MaxSupportedYear);
        Assert.Equal(200, SolarTermService.MaxSupportedYear - SolarTermService.MinSupportedYear + 1);

        var service = new SolarTermService();

        for (var year = SolarTermService.MinSupportedYear; year <= SolarTermService.MaxSupportedYear; year++)
        {
            var count = EachDateOfYear(year).Count(date => service.GetName(date) is not null);
            Assert.Equal(24, count);
        }
    }

    private static IEnumerable<DateOnly> EachDateOfYear(int year)
    {
        var first = new DateOnly(year, 1, 1);
        var totalDays = DateTime.IsLeapYear(year) ? 366 : 365;

        return Enumerable.Range(0, totalDays).Select(first.AddDays);
    }
}
