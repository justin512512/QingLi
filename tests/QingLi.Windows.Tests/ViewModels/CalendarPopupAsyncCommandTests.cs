using QingLi.Core.Birthdays;
using QingLi.Core.Calendars;
using QingLi.Core.Holidays;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class CalendarPopupAsyncCommandTests
{
    [Fact]
    public async Task Next_month_command_completes_asynchronously_after_repository_releases()
    {
        var repository = new SequencedBirthdayRepository();
        var vm = CreateViewModel(repository);

        repository.ReleaseInitial([]);
        await vm.InitializeAsync();

        var commandTask = vm.NextMonthCommand.ExecuteAsync();
        Assert.False(commandTask.IsCompleted);

        repository.ReleaseNext([]);
        await commandTask;

        Assert.Equal(7, vm.DisplayMonth.Month);
        Assert.Equal(42, vm.Days.Count);
    }

    private static CalendarPopupViewModel CreateViewModel(SequencedBirthdayRepository repository)
    {
        var calendarMonthService = new CalendarMonthService(
            new LunarCalendarService(),
            new SolarTermService(),
            new HolidayService([]));

        return new CalendarPopupViewModel(
            calendarMonthService,
            repository,
            new BirthdayOccurrenceService(),
            new DateOnly(2026, 6, 24),
            DayOfWeek.Monday);
    }

    private sealed class SequencedBirthdayRepository : IBirthdayRepository
    {
        private readonly TaskCompletionSource<IReadOnlyList<Birthday>> _initial = NewResponse();
        private readonly TaskCompletionSource<IReadOnlyList<Birthday>> _next = NewResponse();
        private int _callCount;

        public Task<IReadOnlyList<Birthday>> ListAsync(
            string? nameFilter,
            DateOnly today,
            CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _callCount) == 1 ? _initial.Task : _next.Task;

        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Birthday?>(null);

        public Task SaveAsync(Birthday birthday, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ReleaseInitial(IReadOnlyList<Birthday> birthdays) =>
            _initial.TrySetResult(birthdays);

        public void ReleaseNext(IReadOnlyList<Birthday> birthdays) =>
            _next.TrySetResult(birthdays);

        private static TaskCompletionSource<IReadOnlyList<Birthday>> NewResponse() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
