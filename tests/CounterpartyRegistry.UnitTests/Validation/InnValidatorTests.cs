using CounterpartyRegistry.Domain.Validation;

namespace CounterpartyRegistry.UnitTests.Validation;

public class InnValidatorTests
{
    [Theory]
    [InlineData("7701234560")]
    [InlineData("123456789047")]
    // Both cases produce a remainder of 10 before the final modulo operation.
    [InlineData("1000000010")]
    [InlineData("100010000002")]
    public void IsValid_WithCorrectCheckDigits_ReturnsTrue(string inn)
    {
        Assert.True(InnValidator.IsValid(inn));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("          ")]
    [InlineData("770123456")]
    [InlineData("77012345600")]
    [InlineData("1234567890470")]
    [InlineData("770123456A")]
    [InlineData("７７０１２３４５６０")]
    [InlineData(" 7701234560 ")]
    [InlineData("0000000000")]
    [InlineData("000000000000")]
    [InlineData("7701234561")]
    [InlineData("123456789048")]
    // The last digit matches the changed 11th digit; the first checksum must still fail.
    [InlineData("123456789054")]
    public void IsValid_WithInvalidInput_ReturnsFalse(string? inn)
    {
        Assert.False(InnValidator.IsValid(inn));
    }
}
