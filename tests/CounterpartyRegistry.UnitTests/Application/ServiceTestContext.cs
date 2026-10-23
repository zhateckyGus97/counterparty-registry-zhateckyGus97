using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Founders;
using CounterpartyRegistry.Application.Services;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.UnitTests.Application;

// These doubles verify orchestration only, not database transactions or EF behavior.
internal sealed class ServiceTestContext : IClientRepository, IFounderRepository, IUnitOfWork
{
    public Dictionary<Guid, Client> Clients { get; } = new();
    public Dictionary<Guid, Founder> Founders { get; } = new();
    public List<CancellationToken> ReceivedTokens { get; } = new();
    public int SaveCalls { get; private set; }
    public Exception? SaveException { get; set; }
    public PageRequest? LastPage { get; private set; }
    public ClientType? LastType { get; private set; }
    public PagedResult<ClientSummaryResponse> ClientPage { get; set; } = new(
        Array.Empty<ClientSummaryResponse>(), 0, 1, 20);
    public PagedResult<FounderResponse> FounderPage { get; set; } = new(
        Array.Empty<FounderResponse>(), 0, 1, 20);

    public TestTimeProvider Clock { get; } = new();
    public IClientService ClientService => new ClientService(this, this, this, Clock);
    public IFounderService FounderService => new FounderService(this, this, Clock);

    public Client SeedClient(string inn = "7701234560", ClientType type = ClientType.LegalEntity)
    {
        var client = Client.Create(inn, "Альфа", type, Clock.GetUtcNow());
        Clients.Add(client.Id, client);
        return client;
    }

    public Founder SeedFounder(string inn = "123456789047")
    {
        var founder = Founder.Create(inn, "Иванов Иван", Clock.GetUtcNow());
        Founders.Add(founder.Id, founder);
        return founder;
    }

    Task<Client?> IClientRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Clients.GetValueOrDefault(id));
    }

    Task<Founder?> IFounderRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Founders.GetValueOrDefault(id));
    }

    Task<bool> IClientRepository.ExistsByInnAsync(string inn, Guid? exceptId, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Clients.Values.Any(client => client.Inn == inn && client.Id != exceptId));
    }

    Task<bool> IFounderRepository.ExistsByInnAsync(string inn, Guid? exceptId, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Founders.Values.Any(founder => founder.Inn == inn && founder.Id != exceptId));
    }

    public Task<bool> HasClientLinksAsync(Guid founderId, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        return Task.FromResult(Clients.Values.Any(client =>
            client.FounderLinks.Any(link => link.FounderId == founderId)));
    }

    public Task<PagedResult<ClientSummaryResponse>> GetPageAsync(PageRequest page, ClientType? type,
        CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        LastPage = page;
        LastType = type;
        return Task.FromResult(ClientPage);
    }

    public Task<PagedResult<FounderResponse>> GetPageAsync(PageRequest page, CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        LastPage = page;
        return Task.FromResult(FounderPage);
    }

    public void Add(Client client) => Clients.Add(client.Id, client);
    public void Remove(Client client) => Clients.Remove(client.Id);
    public void Add(Founder founder) => Founders.Add(founder.Id, founder);
    public void Remove(Founder founder) => Founders.Remove(founder.Id);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        Observe(cancellationToken);
        SaveCalls++;
        return SaveException is null ? Task.FromResult(1) : Task.FromException<int>(SaveException);
    }

    private void Observe(CancellationToken cancellationToken)
    {
        ReceivedTokens.Add(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

internal sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
