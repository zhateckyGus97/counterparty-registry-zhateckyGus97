using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Founders;

namespace CounterpartyRegistry.Application.Abstractions;

public interface IFounderService
{
    Task<FounderResponse> CreateAsync(CreateFounderRequest request,
        CancellationToken cancellationToken = default);
    Task<FounderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<FounderResponse>> GetPageAsync(PageRequest page,
        CancellationToken cancellationToken = default);
    Task<FounderResponse> UpdateAsync(Guid id, UpdateFounderRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
