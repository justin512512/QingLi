using QingLi.Core.Birthdays;

namespace QingLi.Infrastructure.Tests.Support;

internal static class BirthdaySamples
{
    public static Birthday Lunar(string name = "小夏") =>
        new(Guid.NewGuid(), name, BirthdayCalendarKind.Lunar, 1990, 4, 30, true, 3,
            new TimeOnly(9, 15), "家人", true);

    public static Birthday Gregorian(string name, int month = 8, int day = 18) =>
        new(Guid.NewGuid(), name, BirthdayCalendarKind.Gregorian, 1990, month, day, false, 1,
            new TimeOnly(8, 30), null, true);
}
