namespace QingLi.Core.Anniversaries;

public interface IAnniversaryRepository
{
    Task<IReadOnlyList<Anniversary>> ListAsync(
        string? titleFilter,
        DateOnly today,
        CancellationToken cancellationToken);

    Task<Anniversary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(Anniversary anniversary, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
