using QingLi.Core.Calendars;

namespace QingLi.Core.Tests.Calendars;

public sealed class SolarTermServiceTests
{
    [Theory]
    [InlineData(2026, 2, 4, "立春")]
    [InlineData(2026, 6, 21, "夏至")]
    public void Returns_known_solar_terms(int year, int month, int day, string name)
    {
        var actual = new SolarTermService().GetName(new DateOnly(year, month, day));

        Assert.Equal(name, actual);
    }
}
