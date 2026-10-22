using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.UnitTests.Clients;

public class ClientCreationTests
{
    [Theory]
    [InlineData("7701234561", ClientType.LegalEntity)]
    [InlineData("123456789048", ClientType.IndividualEntrepreneur)]
    [InlineData("123456789047", ClientType.LegalEntity)]
    [InlineData("7701234560", ClientType.IndividualEntrepreneur)]
    public void Create_WithInvalidInnOrWrongType_ThrowsArgumentException(
        string inn, ClientType type)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Client.Create(inn, "Клиент", type, DateTimeOffset.UtcNow));

        Assert.Equal("inn", exception.ParamName);
    }

    [Theory]
    [InlineData("7701234560", ClientType.LegalEntity)]
    [InlineData("123456789047", ClientType.IndividualEntrepreneur)]
    public void Create_WithSurroundingWhitespace_NormalizesInnBeforeValidation(
        string inn, ClientType type)
    {
        var client = Client.Create($" {inn} ", "Клиент", type, DateTimeOffset.UtcNow);

        Assert.Equal(inn, client.Inn);
    }
}
