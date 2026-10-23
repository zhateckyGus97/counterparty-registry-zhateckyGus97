using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.Application.Services;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _clients;
    private readonly IFounderRepository _founders;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ClientService(IClientRepository clients, IFounderRepository founders,
        IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(founders);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _clients = clients;
        _founders = founders;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var client = Client.Create(request.Inn, request.Name, request.Type, _timeProvider.GetUtcNow());
        await EnsureInnAvailableAsync(client.Inn, null, cancellationToken);

        _clients.Add(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientResponse.From(client);
    }

    public async Task<ClientResponse> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(id, cancellationToken);
        return ClientResponse.From(client);
    }

    public Task<PagedResult<ClientSummaryResponse>> GetPageAsync(PageRequest page,
        ClientType? type = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(page);

        if (type.HasValue && type != ClientType.LegalEntity && type != ClientType.IndividualEntrepreneur)
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Неизвестный тип клиента.");
        }

        return _clients.GetPageAsync(page, type, cancellationToken);
    }

    public async Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = await RequireClientAsync(id, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Inn);
        var inn = request.Inn.Trim();

        // Check before mutating the tracked entity, so a conflict leaves it unchanged.
        await EnsureInnAvailableAsync(inn, id, cancellationToken);
        client.Update(inn, request.Name, request.Type, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientResponse.From(client);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await RequireClientAsync(id, cancellationToken);
        _clients.Remove(client);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ClientResponse> AddFounderAsync(Guid clientId, Guid founderId,
        CancellationToken cancellationToken = default)
    {
        RequestValidation.EnsureId(founderId, nameof(founderId));
        var client = await RequireClientAsync(clientId, cancellationToken);
        var founder = await _founders.GetByIdAsync(founderId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Founder), founderId);

        client.AddFounder(founder, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientResponse.From(client);
    }

    public async Task<ClientResponse> RemoveFounderAsync(Guid clientId, Guid founderId,
        CancellationToken cancellationToken = default)
    {
        RequestValidation.EnsureId(founderId, nameof(founderId));
        var client = await RequireClientAsync(clientId, cancellationToken);
        client.RemoveFounder(founderId, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ClientResponse.From(client);
    }

    private async Task<Client> RequireClientAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestValidation.EnsureId(id, nameof(id));
        return await _clients.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Client), id);
    }

    private async Task EnsureInnAvailableAsync(string inn, Guid? exceptId,
        CancellationToken cancellationToken)
    {
        if (await _clients.ExistsByInnAsync(inn, exceptId, cancellationToken))
        {
            throw new ConflictException("client.inn_conflict", "Клиент с таким ИНН уже существует.");
        }
    }
}
