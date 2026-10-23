using CounterpartyRegistry.Application.Common;
using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Application.Founders;
using CounterpartyRegistry.Domain.Exceptions;

namespace CounterpartyRegistry.UnitTests.Application;

public class FounderServiceTests
{
    [Fact]
    public async Task Create_NormalizesData_AndSavesOnce()
    {
        var context = new ServiceTestContext();
        var response = await context.FounderService.CreateAsync(new(" 123456789047 ", " Иванов Иван "));
        Assert.Equal("123456789047", response.Inn);
        Assert.Equal("Иванов Иван", response.FullName);
        Assert.Equal(context.Clock.Now, response.CreatedAt);
        Assert.Equal(response.Id, Assert.Single(context.Founders).Key);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task Create_WithDuplicateNormalizedInn_DoesNotAddOrSave()
    {
        var context = new ServiceTestContext();
        context.SeedFounder();
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.FounderService.CreateAsync(new(" 123456789047 ", "Иванов Иван")));
        Assert.Equal("founder.inn_conflict", exception.Code);
        Assert.Single(context.Founders);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Create_WithInvalidData_DoesNotAccessStorage()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            context.FounderService.CreateAsync(new("123456789048", "Иванов Иван")));
        Assert.Empty(context.ReceivedTokens);
    }

    [Fact]
    public async Task Get_ReturnsSnapshotWithoutSaving()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        var response = await context.FounderService.GetByIdAsync(founder.Id);
        founder.Update(founder.Inn, "Петров Иван", context.Clock.Now);
        Assert.Equal("Иванов Иван", response.FullName);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Update_AllowsOwnInn_AndUpdatesSharedData()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        var client = context.SeedClient();
        client.AddFounder(founder, context.Clock.Now);
        var createdAt = founder.CreatedAt;
        context.Clock.Now = context.Clock.Now.AddHours(1);
        var response = await context.FounderService.UpdateAsync(founder.Id,
            new(" 123456789047 ", " Петров Иван "));
        Assert.Equal("Петров Иван", response.FullName);
        Assert.Equal("Петров Иван", Assert.Single(client.FounderLinks).Founder.FullName);
        Assert.Equal(createdAt, response.CreatedAt);
        Assert.Equal(createdAt, client.UpdatedAt);
        Assert.Equal(context.Clock.Now, response.UpdatedAt);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task Update_WithOccupiedInn_DoesNotMutateOrSave()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        context.SeedFounder("100010000002");
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.FounderService.UpdateAsync(founder.Id, new("100010000002", "Другое имя")));
        Assert.Equal("founder.inn_conflict", exception.Code);
        Assert.Equal("123456789047", founder.Inn);
        Assert.Equal("Иванов Иван", founder.FullName);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Update_WithInvalidName_DoesNotMutateOrSave()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            context.FounderService.UpdateAsync(founder.Id, new("100010000002", " ")));
        Assert.Equal("123456789047", founder.Inn);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Delete_LinkedFounder_IsRejected()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        context.SeedClient().AddFounder(founder, context.Clock.Now);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            context.FounderService.DeleteAsync(founder.Id));
        Assert.Equal("founder.has_clients", exception.Code);
        Assert.Single(context.Founders);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Delete_AfterLastLinkRemoved_IsAllowed()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        var client = context.SeedClient();
        client.AddFounder(founder, context.Clock.Now);
        client.RemoveFounder(founder.Id, context.Clock.Now);
        await context.FounderService.DeleteAsync(founder.Id);
        Assert.Empty(context.Founders);
        Assert.Equal(1, context.SaveCalls);
    }

    [Fact]
    public async Task Delete_MissingFounder_ReportsNotFoundWithoutSaving()
    {
        var context = new ServiceTestContext();
        var id = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            context.FounderService.DeleteAsync(id));
        Assert.Equal("Founder", exception.EntityName);
        Assert.Equal(id, exception.EntityId);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task EmptyId_IsRejectedBeforeStorageAccess()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAsync<ArgumentException>(() => context.FounderService.GetByIdAsync(Guid.Empty));
        Assert.Empty(context.ReceivedTokens);
    }

    [Fact]
    public async Task List_ForwardsPageWithoutSaving()
    {
        var context = new ServiceTestContext();
        var page = new PageRequest(3, 5);
        context.FounderPage = new(Array.Empty<FounderResponse>(), 12, 3, 5);
        var response = await context.FounderService.GetPageAsync(page);
        Assert.Same(context.FounderPage, response);
        Assert.Same(page, context.LastPage);
        Assert.Equal(0, context.SaveCalls);
    }

    [Fact]
    public async Task Cancellation_IsPropagatedToAllStorageOperations()
    {
        var context = new ServiceTestContext();
        var founder = context.SeedFounder();
        using var source = new CancellationTokenSource();
        await context.FounderService.DeleteAsync(founder.Id, source.Token);
        Assert.Equal(3, context.ReceivedTokens.Count);
        Assert.All(context.ReceivedTokens, token => Assert.Equal(source.Token, token));
    }

    [Fact]
    public async Task AlreadyCancelledRequest_DoesNotAccessStorage()
    {
        var context = new ServiceTestContext();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.FounderService.GetPageAsync(new(), new CancellationToken(true)));
        Assert.Empty(context.ReceivedTokens);
    }
}
