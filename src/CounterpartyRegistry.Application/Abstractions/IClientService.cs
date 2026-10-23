using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Application.Abstractions;

public interface IClientService
{
    Task<ClientResponse> CreateAsync(CreateClientRequest request,
        CancellationToken cancellationToken = default);
    Task<ClientResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ClientSummaryResponse>> GetPageAsync(PageRequest page, ClientType? type = null,
        CancellationToken cancellationToken = default);
    Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClientResponse> AddFounderAsync(Guid clientId, Guid founderId,
        CancellationToken cancellationToken = default);
    Task<ClientResponse> RemoveFounderAsync(Guid clientId, Guid founderId,
        CancellationToken cancellationToken = default);
}
