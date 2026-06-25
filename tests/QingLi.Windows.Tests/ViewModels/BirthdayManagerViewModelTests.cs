using QingLi.Core.Birthdays;
using QingLi.Windows.ViewModels;

namespace QingLi.Windows.Tests.ViewModels;

public sealed class BirthdayManagerViewModelTests
{
    [Fact]
    public async Task Load_command_loads_asynchronously_and_sorts_by_next_occurrence()
    {
        var repository = new SequencedBirthdayRepository();
        var vm = new BirthdayManagerViewModel(
            repository,
            new BirthdayOccurrenceService(),
            () => new DateOnly(2026, 6, 24));

        var loadTask = vm.LoadCommand.ExecuteAsync();
        Assert.False(loadTask.IsCompleted);

        repository.Release([
            CreateBirthday("年末", 12, 2),
            CreateBirthday("最近", 6, 30),
            CreateBirthday("已过", 1, 8)
        ]);

        await loadTask;

        Assert.Equal(["最近", "年末", "已过"], vm.Birthdays.Select(item => item.Name));
    }

    [Fact]
    public async Task Search_command_uses_name_filter()
    {
        var repository = new SequencedBirthdayRepository();
        var vm = new BirthdayManagerViewModel(
            repository,
            new BirthdayOccurrenceService(),
            () => new DateOnly(2026, 6, 24))
        {
            SearchText = "林"
        };

        var loadTask = vm.SearchCommand.ExecuteAsync();
        repository.Release([CreateBirthday("小林", 8, 18)]);
        await loadTask;

        Assert.Equal("林", repository.LastNameFilter);
        Assert.Single(vm.Birthdays);
    }

    [Fact]
    public async Task Delete_selected_command_calls_repository_after_selection()
    {
        var repository = new SequencedBirthdayRepository();
        var selected = CreateBirthday("小林", 8, 18);
        var vm = new BirthdayManagerViewModel(
            repository,
            new BirthdayOccurrenceService(),
            () => new DateOnly(2026, 6, 24));

        repository.Release([selected, CreateBirthday("小周", 9, 1)]);
        await vm.LoadCommand.ExecuteAsync();
        vm.SelectedBirthday = vm.Birthdays.Single(item => item.Id == selected.Id);

        await vm.DeleteSelectedCommand.ExecuteAsync();

        Assert.Equal(selected.Id, Assert.Single(repository.DeletedIds));
        Assert.DoesNotContain(vm.Birthdays, item => item.Id == selected.Id);
    }

    private static Birthday CreateBirthday(string name, int month, int day) =>
        new(
            Guid.NewGuid(),
            name,
            BirthdayCalendarKind.Gregorian,
            1990,
            month,
            day,
            false,
            3,
            new TimeOnly(9, 0),
            null,
            true);

    private sealed class SequencedBirthdayRepository : IBirthdayRepository
    {
        private readonly Queue<IReadOnlyList<Birthday>> _queuedResponses = [];
        private TaskCompletionSource<IReadOnlyList<Birthday>>? _response;

        public string? LastNameFilter { get; private set; }

        public List<Guid> DeletedIds { get; } = [];

        public Task<IReadOnlyList<Birthday>> ListAsync(
            string? nameFilter,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            LastNameFilter = nameFilter;

            if (_queuedResponses.Count > 0)
            {
                return Task.FromResult(_queuedResponses.Dequeue());
            }

            _response ??= new TaskCompletionSource<IReadOnlyList<Birthday>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return _response.Task;
        }

        public Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Birthday?>(null);

        public Task SaveAsync(Birthday birthday, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            DeletedIds.Add(id);
            return Task.CompletedTask;
        }

        public void Release(IReadOnlyList<Birthday> birthdays)
        {
            if (_response is not null)
            {
                _response.TrySetResult(birthdays);
                _response = null;
                return;
            }

            _queuedResponses.Enqueue(birthdays);
        }
    }
}
