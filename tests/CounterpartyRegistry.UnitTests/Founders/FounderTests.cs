using CounterpartyRegistry.Domain.Clients;
using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.UnitTests.Founders;

public class FounderTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("7701234560")]
    [InlineData("123456789048")]
    [InlineData("000000000000")]
    [InlineData("12345678904A")]
    public void Create_WithInvalidInn_Throws(string? inn)
    {
        Assert.ThrowsAny<ArgumentException>(() => Founder.Create(inn!, "Иванов Иван", Start));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingFullName_Throws(string? fullName)
    {
        Assert.ThrowsAny<ArgumentException>(() => Founder.Create("123456789047", fullName!, Start));
    }

    [Fact]
    public void Create_NormalizesValuesAndUsesUtc()
    {
        var founder = Founder.Create(" 123456789047 ", " Иванов Иван ", Start);

        Assert.Equal("123456789047", founder.Inn);
        Assert.Equal("Иванов Иван", founder.FullName);
        Assert.NotEqual(Guid.Empty, founder.Id);
        Assert.Equal(Start.ToUniversalTime(), founder.CreatedAt);
        Assert.Equal(TimeSpan.Zero, founder.CreatedAt.Offset);
        Assert.Equal(founder.CreatedAt, founder.UpdatedAt);
    }

    [Fact]
    public void Create_EnforcesFullNameLengthBoundary()
    {
        var fullName = new string('А', Founder.MaxFullNameLength);
        Assert.Equal(fullName, Founder.Create("123456789047", fullName, Start).FullName);
        Assert.Throws<ArgumentException>(() =>
            Founder.Create("123456789047", fullName + "А", Start));
    }

    [Fact]
    public void Update_ChangesDetails_PreservingIdentityAndCreationDate()
    {
        var founder = Founder.Create("123456789047", "Иванов Иван", Start);
        var id = founder.Id;
        founder.Update("100010000002", " Петров Пётр ", Start.AddHours(1));

        Assert.Equal(id, founder.Id);
        Assert.Equal("100010000002", founder.Inn);
        Assert.Equal("Петров Пётр", founder.FullName);
        Assert.Equal(Start.ToUniversalTime(), founder.CreatedAt);
        Assert.Equal(Start.AddHours(1).ToUniversalTime(), founder.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, founder.UpdatedAt.Offset);
    }

    [Fact]
    public void Update_WithInvalidInput_DoesNotPartiallyChangeFounder()
    {
        var founder = Founder.Create("123456789047", "Иванов Иван", Start);
        Assert.Throws<ArgumentException>(() => founder.Update(
            "100010000002", new string('А', Founder.MaxFullNameLength + 1), Start.AddHours(1)));

        Assert.Equal("123456789047", founder.Inn);
        Assert.Equal("Иванов Иван", founder.FullName);
        Assert.Equal(Start.ToUniversalTime(), founder.UpdatedAt);
    }

    [Fact]
    public void Update_WithEquivalentValues_DoesNotAdvanceTimestamp()
    {
        var founder = Founder.Create("123456789047", "Иванов Иван", Start);
        founder.Update(" 123456789047 ", " Иванов Иван ", Start.AddHours(1));
        Assert.Equal(Start.ToUniversalTime(), founder.UpdatedAt);
    }

    [Fact]
    public void UpdatingSharedFounder_IsVisibleThroughBothLinks_WithoutTouchingClientsDates()
    {
        var founder = Founder.Create("123456789047", "Иванов Иван", Start);
        var first = Client.Create("7701234560", "Альфа", ClientType.LegalEntity, Start);
        var second = Client.Create("1000000010", "Бета", ClientType.LegalEntity, Start);
        var firstLink = first.AddFounder(founder, Start);
        var secondLink = second.AddFounder(founder, Start);

        founder.Update(founder.Inn, "Петров Иван", Start.AddHours(1));

        Assert.Equal("Петров Иван", firstLink.Founder.FullName);
        Assert.Equal("Петров Иван", secondLink.Founder.FullName);
        Assert.Equal(Start.ToUniversalTime(), first.UpdatedAt);
        Assert.Equal(Start.ToUniversalTime(), second.UpdatedAt);
        Assert.Equal(Start.ToUniversalTime(), firstLink.CreatedAt);
    }
}
