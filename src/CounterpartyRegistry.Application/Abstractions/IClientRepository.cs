using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Application.Abstractions;

public interface IClientRepository
{
    // Returns a tracked client with ALL founder links and their Founder navigation loaded.
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByInnAsync(string inn, Guid? exceptId = null,
        CancellationToken cancellationToken = default);

    // Read-only projection, sorted by CreatedAt descending, then Id descending.
    Task<PagedResult<ClientSummaryResponse>> GetPageAsync(PageRequest page, ClientType? type = null,
        CancellationToken cancellationToken = default);

    // Stage changes in the same context used by IUnitOfWork; do not save here.
    void Add(Client client);
    void Remove(Client client);
}
