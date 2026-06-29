using QingLi.Core.Settings;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class TaskbarClockViewModelTests
{
    [Theory]
    [InlineData(false, "21:05")]
    [InlineData(true, "9:05 PM")]
    public void Formats_time_and_chinese_date(bool useTwelveHour, string expected)
    {
        var vm = new TaskbarClockViewModel();

        vm.Update(
            new DateTimeOffset(2026, 6, 24, 21, 5, 0, TimeSpan.FromHours(8)),
            AppSettings.Default with { UseTwelveHourClock = useTwelveHour });

        Assert.Equal(expected, vm.TimeText);
        Assert.Equal("6月24日 周三", vm.DateText);
    }

    [Fact]
    public void Applies_user_clock_appearance()
    {
        var vm = new TaskbarClockViewModel();

        vm.Update(
            new DateTimeOffset(2026, 6, 24, 21, 5, 0, TimeSpan.FromHours(8)),
            AppSettings.Default with
            {
                DateFormat = "yyyy/MM/dd ddd",
                ClockFontSize = 16,
                ClockTextColor = "#FFF4F4F4"
            });

        Assert.Equal("2026/06/24 周三", vm.DateText);
        Assert.Equal(16, vm.ClockFontSize);
        Assert.Equal("#FFF4F4F4", vm.ClockTextColor);
    }

    [Fact]
    public void Invalid_date_format_falls_back_to_default()
    {
        var vm = new TaskbarClockViewModel();

        vm.Update(
            new DateTimeOffset(2026, 6, 24, 21, 5, 0, TimeSpan.FromHours(8)),
            AppSettings.Default with { DateFormat = "unterminated '" });

        Assert.Equal("6月24日 周三", vm.DateText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(35792)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_font_size_falls_back_to_default(double fontSize)
    {
        var vm = new TaskbarClockViewModel();

        vm.Update(DateTimeOffset.Now, AppSettings.Default with { ClockFontSize = fontSize });

        Assert.Equal(AppSettings.Default.ClockFontSize, vm.ClockFontSize);
    }

    [Fact]
    public void Invalid_color_falls_back_to_default()
    {
        var vm = new TaskbarClockViewModel();

        vm.Update(DateTimeOffset.Now, AppSettings.Default with { ClockTextColor = "not-a-color" });

        Assert.Equal(TaskbarClockViewModel.DefaultClockTextColor, vm.ClockTextColor);
    }
}
