using QingLi.Core.Birthdays;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class BirthdayEditorViewModelTests
{
    [Theory]
    [InlineData("", 8, 18, "请输入姓名")]
    [InlineData("小林", 13, 18, "月份应在 1 到 12 之间")]
    [InlineData("小林", 8, 31, "日期超出范围")]
    public void Invalid_input_has_clear_message(
        string name,
        int month,
        int day,
        string expected)
    {
        var vm = BirthdayEditorFixture.Create(
            name,
            month,
            day,
            calendarKind: BirthdayCalendarKind.Lunar);

        Assert.Contains(expected, vm.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Reminder_days_must_be_between_0_and_365(int daysBefore)
    {
        var vm = BirthdayEditorFixture.Create(
            "小林",
            8,
            18,
            reminderDaysBefore: daysBefore);

        Assert.Contains("提前天数应在 0 到 365 之间", vm.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("09:75")]
    public void Reminder_time_must_be_valid(string reminderTimeText)
    {
        var vm = BirthdayEditorFixture.Create(
            "小林",
            8,
            18,
            reminderTimeText: reminderTimeText);

        Assert.Contains("提醒时间格式无效", vm.Validate());
    }

    [Fact]
    public void Gregorian_date_uses_birth_year_for_validation()
    {
        var vm = BirthdayEditorFixture.Create(
            "小林",
            2,
            29,
            calendarKind: BirthdayCalendarKind.Gregorian,
            birthYear: 2027);

        Assert.Contains("日期超出范围", vm.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Lunar_day_must_be_between_1_and_30(int day)
    {
        var vm = BirthdayEditorFixture.Create(
            "小林",
            8,
            day,
            calendarKind: BirthdayCalendarKind.Lunar);

        Assert.Contains("农历日期应在 1 到 30 之间", vm.Validate());
    }

    [Fact]
    public void Lunar_date_must_be_confirmed_by_lunar_service()
    {
        var vm = BirthdayEditorFixture.Create(
            "小林",
            5,
            12,
            calendarKind: BirthdayCalendarKind.Lunar,
            isLeapMonth: true,
            lunarDateValidator: (_, _, _, _) => false);

        Assert.Contains("农历生日无效", vm.Validate());
    }

    [Fact]
    public async Task Save_command_does_not_write_repository_when_validation_fails()
    {
        var repository = new RecordingBirthdayRepository();
        var vm = BirthdayEditorFixture.Create(
            "",
            8,
            18,
            repository: repository);

        await vm.SaveCommand.ExecuteAsync();

        Assert.Empty(repository.SavedBirthdays);
        Assert.Contains("请输入姓名", vm.ValidationErrors);
    }

    [Fact]
    public async Task Save_command_persists_valid_birthday()
    {
        var repository = new RecordingBirthdayRepository();
        var vm = BirthdayEditorFixture.Create(
            "小林",
            8,
            18,
            calendarKind: BirthdayCalendarKind.Gregorian,
            birthYear: 1992,
            reminderDaysBefore: 3,
            reminderTimeText: "09:15",
            notes: "同学",
            repository: repository);

        await vm.SaveCommand.ExecuteAsync();

        var saved = Assert.Single(repository.SavedBirthdays);
        Assert.Equal("小林", saved.Name);
        Assert.Equal(1992, saved.BirthYear);
        Assert.Equal(8, saved.Month);
        Assert.Equal(18, saved.Day);
        Assert.Equal(3, saved.ReminderDaysBefore);
        Assert.Equal(new TimeOnly(9, 15), saved.ReminderTime);
        Assert.Equal("同学", saved.Notes);
    }

    private static class BirthdayEditorFixture
    {
        public static BirthdayEditorViewModel Create(
            string name,
            int month,
            int day,
            BirthdayCalendarKind calendarKind = BirthdayCalendarKind.Gregorian,
            int birthYear = 1990,
            int reminderDaysBefore = 0,
            string reminderTimeText = "09:00",
            bool isLeapMonth = false,
            string? notes = null,
            RecordingBirthdayRepository? repository = null,
            Func<int, int, int, bool, bool>? lunarDateValidator = null)
        {
            return new BirthdayEditorViewModel(
                repository ?? new RecordingBirthdayRepository(),
                lunarDateValidator ?? ((_, _, _, _) => true))
            {
                Name = name,
                CalendarKind = calendarKind,
                BirthYear = birthYear,
                Month = month,
                Day = day,
                IsLeapMonth = isLeapMonth,
                ReminderDaysBefore = reminderDaysBefore,
                ReminderTimeText = reminderTimeText,
                Notes = notes
            };
        }
    }

    private sealed class RecordingBirthdayRepository : IBirthdayRepository
    {
        public List<Birthday> SavedBirthdays { get; } = [];

        public Task<IReadOnlyList<Birthday>> ListAsync(
            string? nameFilter,
            DateOnly today,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Birthday>>([]);

        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Birthday?>(null);

        public Task SaveAsync(Birthday birthday, CancellationToken cancellationToken)
        {
            SavedBirthdays.Add(birthday);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
