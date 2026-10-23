using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Founders;
using CounterpartyRegistry.Domain.Founders;
using CounterpartyRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CounterpartyRegistry.Infrastructure.Repositories;

public sealed class FounderRepository : IFounderRepository
{
    private readonly RegistryDbContext _dbContext;

    public FounderRepository(RegistryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Founder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Founders.SingleOrDefaultAsync(founder => founder.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByInnAsync(string inn, Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Founders.AnyAsync(founder => founder.Inn == inn &&
            (!exceptId.HasValue || founder.Id != exceptId.Value), cancellationToken);
    }

    public Task<bool> HasClientLinksAsync(Guid founderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ClientFounders.AnyAsync(link => link.FounderId == founderId, cancellationToken);
    }

    public async Task<PagedResult<FounderResponse>> GetPageAsync(PageRequest page,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Founders.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var founders = await query.OrderByDescending(founder => founder.CreatedAt)
            .ThenByDescending(founder => founder.Id)
            .Skip(page.Offset).Take(page.PageSize)
            .Select(founder => new FounderResponse(founder.Id, founder.Inn, founder.FullName,
                founder.CreatedAt, founder.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<FounderResponse>(founders, totalCount, page.PageNumber, page.PageSize);
    }

    public void Add(Founder founder) => _dbContext.Founders.Add(founder);
    public void Remove(Founder founder) => _dbContext.Founders.Remove(founder);
}
