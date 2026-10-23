using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Application.Services;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Founders;
using CounterpartyRegistry.Infrastructure;
using CounterpartyRegistry.Infrastructure.Persistence;
using CounterpartyRegistry.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CounterpartyRegistry.IntegrationTests;

public sealed class PersistenceTests : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly SqlServerFixture _database;

    public PersistenceTests(SqlServerFixture database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        // Only the fixture's disposable database is cleared, never the development database.
        await using var context = _database.CreateContext();
        await context.ClientFounders.ExecuteDeleteAsync();
        await context.Clients.ExecuteDeleteAsync();
        await context.Founders.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Services_PersistAndReloadSharedFounders_UsingOneContextPerScope()
    {
        using var provider = CreateServices();
        Guid clientId;
        Guid founderId;
        using (var scope = provider.CreateScope())
        {
            Assert.Same(scope.ServiceProvider.GetRequiredService<RegistryDbContext>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
            var clients = scope.ServiceProvider.GetRequiredService<IClientService>();
            var founders = scope.ServiceProvider.GetRequiredService<IFounderService>();
            var first = await clients.CreateAsync(new("7701234560", "Альфа", ClientType.LegalEntity));
            var second = await clients.CreateAsync(new("1000000010", "Бета", ClientType.LegalEntity));
            var founder = await founders.CreateAsync(new("123456789047", "Иванов Иван"));
            await clients.AddFounderAsync(first.Id, founder.Id);
            await clients.AddFounderAsync(second.Id, founder.Id);
            clientId = first.Id;
            founderId = founder.Id;
        }

        using var readScope = provider.CreateScope();
        var response = await readScope.ServiceProvider.GetRequiredService<IClientService>().GetByIdAsync(clientId);
        Assert.Equal(founderId, Assert.Single(response.Founders).Founder.Id);
        Assert.Equal("Иванов Иван", response.Founders[0].Founder.FullName);
        Assert.Equal(TimeSpan.Zero, response.CreatedAt.Offset);
        var db = readScope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        Assert.Equal(1, await db.Founders.CountAsync());
        Assert.Equal(2, await db.ClientFounders.CountAsync());
    }

    [Fact]
    public async Task Services_UpdateClientAndSharedFounder_AfterReload()
    {
        using var provider = CreateServices();
        Guid clientId;
        Guid founderId;
        using (var scope = provider.CreateScope())
        {
            var clients = scope.ServiceProvider.GetRequiredService<IClientService>();
            var founder = await scope.ServiceProvider.GetRequiredService<IFounderService>()
                .CreateAsync(new("123456789047", "Иванов Иван"));
            var client = await clients.CreateAsync(new("7701234560", "Альфа", ClientType.LegalEntity));
            await clients.AddFounderAsync(client.Id, founder.Id);
            clientId = client.Id;
            founderId = founder.Id;
        }

        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IClientService>()
                .UpdateAsync(clientId, new("1000000010", "Бета", ClientType.LegalEntity));
            await scope.ServiceProvider.GetRequiredService<IFounderService>()
                .UpdateAsync(founderId, new("123456789047", "Петров Иван"));
        }

        using var readScope = provider.CreateScope();
        var response = await readScope.ServiceProvider.GetRequiredService<IClientService>().GetByIdAsync(clientId);
        Assert.Equal("1000000010", response.Inn);
        Assert.Equal("Бета", response.Name);
        Assert.Equal("Петров Иван", Assert.Single(response.Founders).Founder.FullName);
    }

    [Fact]
    public async Task Lists_ApplyFilterPaginationAndProjection_WithoutTracking()
    {
        await using var context = _database.CreateContext();
        context.Clients.AddRange(
            Client.Create("7701234560", "Раньше", ClientType.LegalEntity, Now),
            Client.Create("1000000010", "Позже", ClientType.LegalEntity, Now.AddMinutes(1)),
            Client.Create("123456789047", "ИП", ClientType.IndividualEntrepreneur, Now.AddMinutes(2)));
        context.Founders.Add(Founder.Create("123456789047", "Иванов Иван", Now));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var clients = await new ClientRepository(context).GetPageAsync(new PageRequest(2, 1), ClientType.LegalEntity);
        Assert.Equal(2, clients.TotalCount);
        Assert.Equal("Раньше", Assert.Single(clients.Items).Name);
        var founders = await new FounderRepository(context).GetPageAsync(new PageRequest());
        Assert.Single(founders.Items);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task UniqueInnIndexes_RejectWritesThatBypassServiceChecks(bool client, bool synchronous)
    {
        await using (var first = _database.CreateContext())
        {
            if (client) first.Clients.Add(Client.Create("7701234560", "Первый", ClientType.LegalEntity, Now));
            else first.Founders.Add(Founder.Create("123456789047", "Первый", Now));
            await first.SaveChangesAsync();
        }

        await using var duplicate = _database.CreateContext();
        if (client) duplicate.Clients.Add(Client.Create("7701234560", "Второй", ClientType.LegalEntity, Now));
        else duplicate.Founders.Add(Founder.Create("123456789047", "Второй", Now));
        var error = synchronous
            ? Assert.Throws<ConflictException>(() => duplicate.SaveChanges())
            : await Assert.ThrowsAsync<ConflictException>(() => duplicate.SaveChangesAsync());
        Assert.Equal(client ? "client.inn_conflict" : "founder.inn_conflict", error.Code);
        Assert.IsType<DbUpdateException>(error.InnerException);
    }

    [Fact]
    public async Task RemovingLink_DeletesOnlyParticipation_AfterFreshLoad()
    {
        var (clientId, founderId) = await SeedLinkAsync();
        await using (var context = _database.CreateContext())
        {
            var client = await new ClientRepository(context).GetByIdAsync(clientId);
            client!.RemoveFounder(founderId, Now.AddHours(1));
            await context.SaveChangesAsync();
        }

        await using var check = _database.CreateContext();
        Assert.Equal(0, await check.ClientFounders.CountAsync());
        Assert.Equal(1, await check.Founders.CountAsync());
        Assert.Equal(1, await check.Clients.CountAsync());
    }

    [Fact]
    public async Task DeletingClient_CascadesLinks_ButKeepsFounder()
    {
        var (clientId, _) = await SeedLinkAsync();
        using var provider = CreateServices();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IClientService>().DeleteAsync(clientId);
        }

        await using var check = _database.CreateContext();
        Assert.Equal(0, await check.Clients.CountAsync());
        Assert.Equal(0, await check.ClientFounders.CountAsync());
        Assert.Equal(1, await check.Founders.CountAsync());
    }

    [Fact]
    public async Task ForeignKey_PreventsDeletingLinkedFounder_EvenWithoutServiceCheck()
    {
        var (_, founderId) = await SeedLinkAsync();
        await using var context = _database.CreateContext();
        var founder = await context.Founders.SingleAsync(founder => founder.Id == founderId);
        context.Founders.Remove(founder);
        var error = await Assert.ThrowsAsync<ConflictException>(() => context.SaveChangesAsync());
        Assert.Equal("founder.has_clients", error.Code);
    }

    [Fact]
    public async Task ConcurrentClientUpdates_RejectStaleWrite()
    {
        var (clientId, _) = await SeedLinkAsync();
        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();
        var firstClient = await new ClientRepository(first).GetByIdAsync(clientId);
        var secondClient = await new ClientRepository(second).GetByIdAsync(clientId);
        firstClient!.Update(firstClient.Inn, "Первое изменение", firstClient.Type, Now.AddHours(1));
        secondClient!.Update(secondClient.Inn, "Устаревшее изменение", secondClient.Type, Now.AddHours(2));
        await first.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ConflictException>(() => second.SaveChangesAsync());
        Assert.Equal("concurrency_conflict", error.Code);
        await using var check = _database.CreateContext();
        Assert.Equal("Первое изменение", (await check.Clients.SingleAsync()).Name);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task TypeChangeAndAddingFounder_CannotBothSucceed_EvenWithIdenticalTimestamps(
        bool linkFirst, bool synchronous)
    {
        Guid clientId;
        Guid founderId;
        await using (var seed = _database.CreateContext())
        {
            var client = Client.Create("7701234560", "Альфа", ClientType.LegalEntity, Now);
            var founder = Founder.Create("123456789047", "Иванов Иван", Now);
            seed.AddRange(client, founder);
            await seed.SaveChangesAsync();
            clientId = client.Id;
            founderId = founder.Id;
        }

        await using var linking = _database.CreateContext();
        await using var changingType = _database.CreateContext();
        var clientToLink = await new ClientRepository(linking).GetByIdAsync(clientId);
        var founderToLink = await linking.Founders.SingleAsync(founder => founder.Id == founderId);
        var clientToChange = await new ClientRepository(changingType).GetByIdAsync(clientId);
        clientToLink!.AddFounder(founderToLink, Now);
        clientToChange!.Update("123456789047", "ИП", ClientType.IndividualEntrepreneur, Now);

        var first = linkFirst ? linking : changingType;
        var second = linkFirst ? changingType : linking;
        ConflictException error;
        if (synchronous)
        {
            first.SaveChanges();
            error = Assert.Throws<ConflictException>(() => second.SaveChanges());
        }
        else
        {
            await first.SaveChangesAsync();
            error = await Assert.ThrowsAsync<ConflictException>(() => second.SaveChangesAsync());
        }
        Assert.Equal("concurrency_conflict", error.Code);

        await using var check = _database.CreateContext();
        Assert.Equal(linkFirst ? 1 : 0, await check.ClientFounders.CountAsync());
        Assert.Equal(linkFirst ? ClientType.LegalEntity : ClientType.IndividualEntrepreneur,
            (await check.Clients.SingleAsync()).Type);
    }

    [Fact]
    public async Task FailedSave_RollsBackOtherChangesInSameTransaction()
    {
        await using (var seed = _database.CreateContext())
        {
            seed.Founders.Add(Founder.Create("123456789047", "Иванов Иван", Now));
            await seed.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            context.Clients.Add(Client.Create("7701234560", "Альфа", ClientType.LegalEntity, Now));
            context.Founders.Add(Founder.Create("123456789047", "Повторный", Now));
            await Assert.ThrowsAsync<ConflictException>(() => context.SaveChangesAsync());
        }

        await using var check = _database.CreateContext();
        Assert.Equal(0, await check.Clients.CountAsync());
        Assert.Equal(1, await check.Founders.CountAsync());
    }

    private async Task<(Guid ClientId, Guid FounderId)> SeedLinkAsync()
    {
        await using var context = _database.CreateContext();
        var client = Client.Create("7701234560", "Альфа", ClientType.LegalEntity, Now);
        var founder = Founder.Create("123456789047", "Иванов Иван", Now);
        client.AddFounder(founder, Now);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return (client.Id, founder.Id);
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(_database.ConnectionString);
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IFounderService, FounderService>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
