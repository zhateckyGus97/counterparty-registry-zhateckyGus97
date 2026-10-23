using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Application.Founders;
using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.Application.Services;

public sealed class FounderService : IFounderService
{
    private readonly IFounderRepository _founders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public FounderService(IFounderRepository founders, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(founders);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _founders = founders;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<FounderResponse> CreateAsync(CreateFounderRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var founder = Founder.Create(request.Inn, request.FullName, _timeProvider.GetUtcNow());
        await EnsureInnAvailableAsync(founder.Inn, null, cancellationToken);

        _founders.Add(founder);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return FounderResponse.From(founder);
    }

    public async Task<FounderResponse> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var founder = await RequireFounderAsync(id, cancellationToken);
        return FounderResponse.From(founder);
    }

    public Task<PagedResult<FounderResponse>> GetPageAsync(PageRequest page,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(page);
        return _founders.GetPageAsync(page, cancellationToken);
    }

    public async Task<FounderResponse> UpdateAsync(Guid id, UpdateFounderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var founder = await RequireFounderAsync(id, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Inn);
        var inn = request.Inn.Trim();

        await EnsureInnAvailableAsync(inn, id, cancellationToken);
        founder.Update(inn, request.FullName, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return FounderResponse.From(founder);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var founder = await RequireFounderAsync(id, cancellationToken);

        if (await _founders.HasClientLinksAsync(id, cancellationToken))
        {
            throw new ConflictException("founder.has_clients",
                "Нельзя удалить учредителя, пока он связан с клиентами.");
        }

        _founders.Remove(founder);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Founder> RequireFounderAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestValidation.EnsureId(id, nameof(id));
        return await _founders.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Founder), id);
    }

    private async Task EnsureInnAvailableAsync(string inn, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        if (await _founders.ExistsByInnAsync(inn, exceptId, cancellationToken))
        {
            throw new ConflictException("founder.inn_conflict", "Учредитель с таким ИНН уже существует.");
        }
    }
}
