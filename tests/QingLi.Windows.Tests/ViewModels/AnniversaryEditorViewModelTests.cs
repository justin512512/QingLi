using QingLi.Core.Anniversaries;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class AnniversaryEditorViewModelTests
{
    [Fact]
    public void EmptyTitleHasClearValidationMessage()
    {
        var vm = Create(title: "");

        Assert.Contains("请输入纪念日名称", vm.Validate());
    }

    [Theory]
    [InlineData("13", "1", AnniversaryCalendarKind.Lunar, "月份应在 1 到 12 之间")]
    [InlineData("2", "30", AnniversaryCalendarKind.Gregorian, "日期超出范围")]
    [InlineData("8", "31", AnniversaryCalendarKind.Lunar, "农历日期应在 1 到 30 之间")]
    public void InvalidDateHasClearValidationMessage(
        string month,
        string day,
        AnniversaryCalendarKind kind,
        string expected)
    {
        var vm = Create(monthText: month, dayText: day, kind: kind);

        Assert.Contains(expected, vm.Validate());
    }

    [Fact]
    public async Task SavePersistsValidAnniversaryAndRaisesSavedEvent()
    {
        var repository = new RecordingRepository();
        var vm = new AnniversaryEditorViewModel(
            repository,
            (_, _, _, _) => true,
            defaultDate: new DateOnly(2026, 7, 15))
        {
            Title = "结婚纪念日"
        };
        Anniversary? eventValue = null;
        vm.Saved += value => eventValue = value;

        await vm.SaveCommand.ExecuteAsync();

        var saved = Assert.Single(repository.Saved);
        Assert.Equal("结婚纪念日", saved.Title);
        Assert.Equal(2026, saved.StartYear);
        Assert.Equal(7, saved.Month);
        Assert.Equal(15, saved.Day);
        Assert.Equal(saved, eventValue);
    }

    [Fact]
    public async Task EditPreservesExistingId()
    {
        var repository = new RecordingRepository();
        var existing = new Anniversary(
            Guid.NewGuid(), "旧名称", AnniversaryCalendarKind.Gregorian,
            2020, 5, 20, false, 3, new TimeOnly(9, 0), null, true);
        var vm = new AnniversaryEditorViewModel(repository, (_, _, _, _) => true, existing)
        {
            Title = "新名称"
        };

        await vm.SaveCommand.ExecuteAsync();

        Assert.Equal(existing.Id, Assert.Single(repository.Saved).Id);
    }

    [Theory]
    [InlineData("-1", "提前天数应在 0 到 365 之间")]
    [InlineData("366", "提前天数应在 0 到 365 之间")]
    [InlineData("3", "")]
    public void ReminderFieldsAreValidated(string days, string time)
    {
        var vm = Create(reminderDays: days, reminderTime: time);

        var errors = vm.Validate();

        if (string.IsNullOrEmpty(time))
        {
            Assert.Contains("提醒时间格式无效", errors);
        }
        else
        {
            Assert.Contains("提前天数应在 0 到 365 之间", errors);
        }
    }

    private static AnniversaryEditorViewModel Create(
        string title = "结婚纪念日",
        string monthText = "5",
        string dayText = "20",
        AnniversaryCalendarKind kind = AnniversaryCalendarKind.Gregorian,
        string reminderDays = "3",
        string reminderTime = "09:00",
        RecordingRepository? repository = null,
        DateOnly? defaultDate = null) =>
        new AnniversaryEditorViewModel(
            repository ?? new RecordingRepository(),
            (_, _, _, _) => true,
            defaultDate: defaultDate)
        {
            Title = title,
            CalendarKind = kind,
            MonthText = monthText,
            DayText = dayText,
            ReminderDaysBeforeText = reminderDays,
            ReminderTimeText = reminderTime
        };

    private sealed class RecordingRepository : IAnniversaryRepository
    {
        public List<Anniversary> Saved { get; } = [];
        public Task<IReadOnlyList<Anniversary>> ListAsync(string? titleFilter, DateOnly today, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Anniversary>>([]);
        public Task<Anniversary?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Anniversary?>(null);
        public Task SaveAsync(Anniversary anniversary, CancellationToken cancellationToken)
        {
            Saved.Add(anniversary);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
