using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Founders;
using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.Application.Abstractions;

public interface IFounderRepository
{
    Task<Founder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByInnAsync(string inn, Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasClientLinksAsync(Guid founderId, CancellationToken cancellationToken = default);

    // Read-only projection, sorted by CreatedAt descending, then Id descending.
    Task<PagedResult<FounderResponse>> GetPageAsync(PageRequest page,
        CancellationToken cancellationToken = default);

    void Add(Founder founder);
    void Remove(Founder founder);
}
