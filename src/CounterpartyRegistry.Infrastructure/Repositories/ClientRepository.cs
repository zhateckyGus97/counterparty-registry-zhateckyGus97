using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CounterpartyRegistry.Infrastructure.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly RegistryDbContext _dbContext;

    public ClientRepository(RegistryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Clients.Include(client => client.FounderLinks)
            .ThenInclude(link => link.Founder)
            .SingleOrDefaultAsync(client => client.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByInnAsync(string inn, Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Clients.AnyAsync(client => client.Inn == inn &&
            (!exceptId.HasValue || client.Id != exceptId.Value), cancellationToken);
    }

    public async Task<PagedResult<ClientSummaryResponse>> GetPageAsync(PageRequest page,
        ClientType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Clients.AsNoTracking();
        if (type.HasValue)
        {
            query = query.Where(client => client.Type == type.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var clients = await query.OrderByDescending(client => client.CreatedAt)
            .ThenByDescending(client => client.Id)
            .Skip(page.Offset).Take(page.PageSize)
            .Select(client => new ClientSummaryResponse(client.Id, client.Inn, client.Name,
                client.Type, client.CreatedAt, client.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientSummaryResponse>(clients, totalCount, page.PageNumber, page.PageSize);
    }

    public void Add(Client client) => _dbContext.Clients.Add(client);
    public void Remove(Client client) => _dbContext.Clients.Remove(client);
}
