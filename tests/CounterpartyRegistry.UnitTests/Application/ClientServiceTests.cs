using CounterpartyRegistry.Application.Clients;
using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Exceptions;

namespace CounterpartyRegistry.UnitTests.Application;

public class ClientServiceTests
{
    [Fact]
    public async Task Create_NormalizesValues_UsesInjectedTime_AndSavesOnce()
    {
        var context = new ServiceTestContext();
        var response = await context.ClientService.CreateAsync(
            new(" 7701234560 ", " Альфа ", ClientType.LegalEntity));

        Assert.Equal("7701234560", response.Inn);
        Assert.Equal("Альфа", response.Name);
        Assert.Equal(context.Clock.Now, response.CreatedAt);
        Assert.Equal(response.CreatedAt, response.UpdatedAt);
        Assert.Equal(response.Id, Assert.Single(context.Clients).Key);
        Assert.Empty(response.Founders);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task Create_WithDuplicateNormalizedInn_DoesNotAddOrSave()
    {
        var context = new ServiceTestContext();
        context.SeedClient();
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.ClientService.CreateAsync(new(" 7701234560 ", "Бета", ClientType.LegalEntity)));

        Assert.Equal("client.inn_conflict", exception.Code);
        Assert.Single(context.Clients);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Create_WithInvalidInn_DoesNotAccessStorage()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAsync<DomainValidationException>(() => context.ClientService.CreateAsync(
            new("7701234561", "Альфа", ClientType.LegalEntity)));
        Assert.Empty(context.ReceivedTokens);
        Assert.Empty(context.Clients);
    }

    [Fact]
    public async Task Get_ReturnsSnapshotWithSharedFounderData_WithoutSaving()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        var founder = context.SeedFounder();
        client.AddFounder(founder, context.Clock.Now);
        var response = await context.ClientService.GetByIdAsync(client.Id);

        founder.Update(founder.Inn, "Петров Иван", context.Clock.Now.AddHours(1));

        var link = Assert.Single(response.Founders);
        Assert.Equal(founder.Id, link.Founder.Id);
        Assert.Equal("Иванов Иван", link.Founder.FullName);
        Assert.Equal(context.Clock.Now, link.CreatedAt);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task MissingClient_IsReportedWithItsId()
    {
        var context = new ServiceTestContext();
        var id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            context.ClientService.GetByIdAsync(id));
        Assert.Equal(id, exception.EntityId);
        Assert.Equal("Client", exception.EntityName);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task EmptyId_IsRejectedBeforeStorageAccess()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAsync<ArgumentException>(() => context.ClientService.DeleteAsync(Guid.Empty));
        Assert.Empty(context.ReceivedTokens);
    }

    [Fact]
    public async Task Update_KeepsOwnInn_AndSavesChangedDetails()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        var createdAt = client.CreatedAt;
        context.Clock.Now = context.Clock.Now.AddHours(1);
        var response = await context.ClientService.UpdateAsync(client.Id,
            new(" 7701234560 ", " Бета ", ClientType.LegalEntity));

        Assert.Equal("Бета", response.Name);
        Assert.Equal(createdAt, response.CreatedAt);
        Assert.Equal(context.Clock.Now, response.UpdatedAt);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task Update_WithAnotherClientsInn_DoesNotMutateOrSave()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        context.SeedClient("1000000010");
        await Assert.ThrowsAsync<ConflictException>(() => context.ClientService.UpdateAsync(
            client.Id, new("1000000010", "Бета", ClientType.LegalEntity)));

        Assert.Equal("7701234560", client.Inn);
        Assert.Equal("Альфа", client.Name);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Update_RespectsDomainRestrictionOnTypeChange()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        client.AddFounder(context.SeedFounder(), context.Clock.Now);
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            context.ClientService.UpdateAsync(client.Id,
                new("123456789047", "ИП", ClientType.IndividualEntrepreneur)));

        Assert.Equal("client.has_founders", exception.Code);
        Assert.Equal(ClientType.LegalEntity, client.Type);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task AddFounder_ReusesExistingFounderAcrossClients_AndSavesOncePerOperation()
    {
        var context = new ServiceTestContext();
        var first = context.SeedClient();
        var second = context.SeedClient("1000000010");
        var founder = context.SeedFounder();
        await context.ClientService.AddFounderAsync(first.Id, founder.Id);
        var response = await context.ClientService.AddFounderAsync(second.Id, founder.Id);

        Assert.Equal(founder.Id, Assert.Single(response.Founders).Founder.Id);
        Assert.Same(founder, Assert.Single(first.FounderLinks).Founder);
        Assert.Same(founder, Assert.Single(second.FounderLinks).Founder);
        Assert.Single(context.Founders);
        Assert.Equal(2, context.SaveCalls);
    }

    [Fact]
    public async Task AddMissingFounder_DoesNotCreateLinkOrSave()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        var id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            context.ClientService.AddFounderAsync(client.Id, id));
        Assert.Equal("Founder", exception.EntityName);
        Assert.Empty(client.FounderLinks);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task DuplicateLink_IsNotSavedAgain()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        var founder = context.SeedFounder();
        client.AddFounder(founder, context.Clock.Now);
        await Assert.ThrowsAsync<DomainRuleException>(() =>
            context.ClientService.AddFounderAsync(client.Id, founder.Id));
        Assert.Single(client.FounderLinks);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task RemoveFounder_RemovesOnlyLink_AndKeepsSharedRecord()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        var founder = context.SeedFounder();
        client.AddFounder(founder, context.Clock.Now);
        var response = await context.ClientService.RemoveFounderAsync(client.Id, founder.Id);
        Assert.Empty(response.Founders);
        Assert.Single(context.Founders);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task Delete_StagesClientRemoval_AndSavesOnce()
    {
        var context = new ServiceTestContext();
        var client = context.SeedClient();
        var founder = context.SeedFounder();
        client.AddFounder(founder, context.Clock.Now);
        await context.ClientService.DeleteAsync(client.Id);
        Assert.Empty(context.Clients);
        Assert.Single(context.Founders);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task List_ForwardsPageAndFilter_ReturnsProjectionWithoutSaving()
    {
        var context = new ServiceTestContext();
        var page = new PageRequest(2, 10);
        context.ClientPage = new(Array.Empty<ClientSummaryResponse>(), 12, 2, 10);
        var result = await context.ClientService.GetPageAsync(page, ClientType.LegalEntity);
        Assert.Same(context.ClientPage, result);
        Assert.Same(page, context.LastPage);
        Assert.Equal(ClientType.LegalEntity, context.LastType);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task List_RejectsUnknownType()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            context.ClientService.GetPageAsync(new(), (ClientType)99));
        Assert.Empty(context.ReceivedTokens);
    }

    [Fact]
    public async Task CancellationToken_IsPassedToEveryStorageOperation()
    {
        var context = new ServiceTestContext();
        using var source = new CancellationTokenSource();
        await context.ClientService.CreateAsync(new("7701234560", "Альфа", ClientType.LegalEntity), source.Token);
        Assert.Equal(2, context.ReceivedTokens.Count);
        Assert.All(context.ReceivedTokens, token => Assert.Equal(source.Token, token));
    }

    [Fact]
    public async Task AlreadyCancelledRequest_DoesNotAccessStorage()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.ClientService.CreateAsync(
            new("7701234560", "Альфа", ClientType.LegalEntity), new CancellationToken(true)));
        Assert.Empty(context.ReceivedTokens);
    }

    [Fact]
    public async Task SaveFailure_IsPropagatedInsteadOfReturningSuccess()
    {
        var context = new ServiceTestContext { SaveException = new InvalidOperationException("Storage failure") };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.ClientService.CreateAsync(new("7701234560", "Альфа", ClientType.LegalEntity)));
        Assert.Same(context.SaveException, exception);
    }
}
