using QingLi.Core.Birthdays;
using QingLi.Infrastructure.Birthdays;
using QingLi.Infrastructure.Tests.Support;

namespace QingLi.Infrastructure.Tests.Birthdays;

public sealed class SqliteBirthdayRepositoryTests
{
    [Fact]
    public async Task Saves_and_reads_birthday()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteBirthdayRepository(database.Factory);
        var birthday = BirthdaySamples.Lunar();

        await repository.SaveAsync(birthday, CancellationToken.None);
        var actual = await repository.GetAsync(birthday.Id, CancellationToken.None);

        Assert.Equal(birthday, actual);
    }

    [Fact]
    public async Task Delete_removes_only_selected_birthday()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteBirthdayRepository(database.Factory);
        var first = BirthdaySamples.Gregorian("甲");
        var second = BirthdaySamples.Gregorian("乙");
        await repository.SaveAsync(first, default);
        await repository.SaveAsync(second, default);

        await repository.DeleteAsync(first.Id, default);

        Assert.Null(await repository.GetAsync(first.Id, default));
        Assert.NotNull(await repository.GetAsync(second.Id, default));
    }

    [Fact]
    public async Task List_filters_by_name_and_orders_by_next_occurrence()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteBirthdayRepository(database.Factory);
        var later = BirthdaySamples.Gregorian("小林", 12, 1);
        var sooner = BirthdaySamples.Gregorian("林阿姨", 7, 1);
        var unrelated = BirthdaySamples.Gregorian("小周", 1, 1);
        await repository.SaveAsync(later, default);
        await repository.SaveAsync(sooner, default);
        await repository.SaveAsync(unrelated, default);

        var actual = await repository.ListAsync("林", new DateOnly(2026, 6, 24), default);

        Assert.Equal([sooner.Id, later.Id], actual.Select(x => x.Id));
    }
}
