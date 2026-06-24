namespace QingLi.Core.Birthdays;

public interface IBirthdayRepository
{
    Task<IReadOnlyList<Birthday>> ListAsync(
        string? nameFilter,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<Birthday?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(Birthday birthday, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
