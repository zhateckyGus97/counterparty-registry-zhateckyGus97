using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Exceptions;
using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.UnitTests.Clients;

public class ClientBehaviorTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public void Create_WithUnknownType_Throws(int type)
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Client.Create("7701234560", "Клиент", (ClientType)type, Start));
        Assert.Equal(nameof(Client.Type), exception.FieldName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_Throws(string? name)
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Client.Create("7701234560", name!, ClientType.LegalEntity, Start));
        Assert.Equal(nameof(Client.Name), exception.FieldName);
    }

    [Fact]
    public void Create_NormalizesNameAndDates_AndStartsWithoutFounders()
    {
        var client = Client.Create("7701234560", "  Альфа  ", ClientType.LegalEntity, Start);

        Assert.Equal("Альфа", client.Name);
        Assert.NotEqual(Guid.Empty, client.Id);
        Assert.Equal(Start.ToUniversalTime(), client.CreatedAt);
        Assert.Equal(TimeSpan.Zero, client.CreatedAt.Offset);
        Assert.Equal(client.CreatedAt, client.UpdatedAt);
        Assert.Empty(client.FounderLinks);
    }

    [Fact]
    public void Create_EnforcesNameLengthBoundary()
    {
        var name = new string('А', Client.MaxNameLength);
        Assert.Equal(name, Client.Create("7701234560", name, ClientType.LegalEntity, Start).Name);
        Assert.Throws<DomainValidationException>(() =>
            Client.Create("7701234560", name + "А", ClientType.LegalEntity, Start));
    }

    [Fact]
    public void Update_ChangesDetailsAndUpdatedAt_PreservingIdentityAndCreatedAt()
    {
        var client = CreateClient();
        var id = client.Id;

        client.Update("1000000010", "  Бета  ", ClientType.LegalEntity, Start.AddHours(1));

        Assert.Equal(id, client.Id);
        Assert.Equal("1000000010", client.Inn);
        Assert.Equal("Бета", client.Name);
        Assert.Equal(Start.ToUniversalTime(), client.CreatedAt);
        Assert.Equal(Start.AddHours(1).ToUniversalTime(), client.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, client.UpdatedAt.Offset);
    }

    [Fact]
    public void Update_WithInvalidInput_DoesNotPartiallyChangeClient()
    {
        var client = CreateClient();

        Assert.Throws<DomainValidationException>(() =>
            client.Update("7701234561", "Новое имя", ClientType.LegalEntity, Start.AddHours(1)));

        Assert.Equal("7701234560", client.Inn);
        Assert.Equal("Альфа", client.Name);
        Assert.Equal(Start.ToUniversalTime(), client.UpdatedAt);
    }

    [Fact]
    public void Update_WithEquivalentNormalizedValues_DoesNotAdvanceTimestamp()
    {
        var client = CreateClient();
        client.Update(" 7701234560 ", " Альфа ", ClientType.LegalEntity, Start.AddHours(1));
        Assert.Equal(Start.ToUniversalTime(), client.UpdatedAt);
    }

    [Fact]
    public void AddFounder_ToEntrepreneur_IsRejectedWithoutMutation()
    {
        var client = Client.Create("123456789047", "ИП", ClientType.IndividualEntrepreneur, Start);
        var exception = Assert.Throws<DomainRuleException>(() =>
            client.AddFounder(CreateFounder(), Start.AddHours(1)));

        Assert.Equal("client.founders_not_allowed", exception.Code);
        Assert.Empty(client.FounderLinks);
        Assert.Equal(Start.ToUniversalTime(), client.UpdatedAt);
    }

    [Fact]
    public void AddFounder_Twice_IsRejectedWithoutChangingExistingLink()
    {
        var client = CreateClient();
        var founder = CreateFounder();
        var link = client.AddFounder(founder, Start.AddHours(1));

        var exception = Assert.Throws<DomainRuleException>(() =>
            client.AddFounder(founder, Start.AddHours(2)));

        Assert.Equal("client.founder_already_linked", exception.Code);
        Assert.Same(link, Assert.Single(client.FounderLinks));
        Assert.Equal(client.Id, link.ClientId);
        Assert.Equal(founder.Id, link.FounderId);
        Assert.Equal(Start.AddHours(1).ToUniversalTime(), link.CreatedAt);
        Assert.Equal(TimeSpan.Zero, link.CreatedAt.Offset);
        Assert.Equal(link.CreatedAt, client.UpdatedAt);
    }

    [Fact]
    public void Client_CanHaveSeveralFounders_AndRemoveOnlyOne()
    {
        var client = CreateClient();
        var first = CreateFounder();
        var second = Founder.Create("100010000002", "Петров Пётр", Start);
        client.AddFounder(first, Start);
        client.AddFounder(second, Start);
        Assert.Equal(2, client.FounderLinks.Count);

        client.RemoveFounder(first.Id, Start.AddHours(1));

        Assert.Same(second, Assert.Single(client.FounderLinks).Founder);
    }

    [Fact]
    public void RemovingLink_DoesNotDeleteSharedFounderOrOtherClientsLink()
    {
        var first = CreateClient();
        var second = Client.Create("1000000010", "Бета", ClientType.LegalEntity, Start);
        var founder = CreateFounder();
        first.AddFounder(founder, Start);
        second.AddFounder(founder, Start);

        first.RemoveFounder(founder.Id, Start.AddHours(1));

        Assert.Empty(first.FounderLinks);
        Assert.Same(founder, Assert.Single(second.FounderLinks).Founder);
        Assert.Equal(Start.AddHours(1).ToUniversalTime(), first.UpdatedAt);
        Assert.Equal(Start.ToUniversalTime(), second.UpdatedAt);
    }

    [Fact]
    public void ChangeToEntrepreneur_RequiresRemovingAllFounderLinks()
    {
        var client = CreateClient();
        var founder = CreateFounder();
        client.AddFounder(founder, Start);

        var exception = Assert.Throws<DomainRuleException>(() => client.Update(
            "123456789047", "ИП", ClientType.IndividualEntrepreneur, Start.AddHours(1)));

        Assert.Equal("client.has_founders", exception.Code);
        Assert.Equal(ClientType.LegalEntity, client.Type);
        Assert.Equal("7701234560", client.Inn);
        Assert.Equal("Альфа", client.Name);
        Assert.Equal(Start.ToUniversalTime(), client.UpdatedAt);

        client.RemoveFounder(founder.Id, Start.AddHours(1));
        client.Update("123456789047", "ИП", ClientType.IndividualEntrepreneur, Start.AddHours(2));

        Assert.Equal(ClientType.IndividualEntrepreneur, client.Type);
        Assert.Equal("123456789047", client.Inn);
        Assert.Empty(client.FounderLinks);
    }

    [Fact]
    public void Entrepreneur_CanBecomeLegalEntityAndThenAddFounder()
    {
        var client = Client.Create("123456789047", "ИП", ClientType.IndividualEntrepreneur, Start);
        client.Update("7701234560", "Альфа", ClientType.LegalEntity, Start.AddHours(1));
        client.AddFounder(CreateFounder(), Start.AddHours(2));
        Assert.Single(client.FounderLinks);
    }

    [Fact]
    public void RemoveFounder_WithMissingLink_IsRejectedWithoutMutation()
    {
        var client = CreateClient();
        client.AddFounder(CreateFounder(), Start);
        var exception = Assert.Throws<DomainRuleException>(() =>
            client.RemoveFounder(Guid.NewGuid(), Start.AddHours(1)));

        Assert.Equal("client.founder_not_linked", exception.Code);
        Assert.Single(client.FounderLinks);
        Assert.Equal(Start.ToUniversalTime(), client.UpdatedAt);
    }

    [Fact]
    public void LinkOperations_RejectMissingArguments()
    {
        var client = CreateClient();
        Assert.Throws<ArgumentNullException>(() => client.AddFounder(null!, Start));
        Assert.Throws<ArgumentException>(() => client.RemoveFounder(Guid.Empty, Start));
    }

    [Fact]
    public void FounderLinks_CannotBeMutatedThroughExposedCollection()
    {
        var client = CreateClient();
        client.AddFounder(CreateFounder(), Start);
        var collection = Assert.IsAssignableFrom<ICollection<ClientFounder>>(client.FounderLinks);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Single(client.FounderLinks);
    }

    private static Client CreateClient() =>
        Client.Create("7701234560", "Альфа", ClientType.LegalEntity, Start);

    private static Founder CreateFounder() => Founder.Create("123456789047", "Иванов Иван", Start);
}
