using CounterpartyRegistry.Application.Common;

namespace CounterpartyRegistry.UnitTests.Application;

public class PageRequestTests
{
    [Fact]
    public void Defaults_UseFirstPageWithTwentyItems()
    {
        var request = new PageRequest();
        Assert.Equal(1, request.PageNumber);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(0, request.Offset);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void InvalidOrOverflowingPage_IsRejected(int pageNumber, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(pageNumber, pageSize));
    }

    [Fact]
    public void Offset_IsCalculatedAtMaximumAllowedPageSize()
    {
        var request = new PageRequest(3, 100);
        Assert.Equal(200, request.Offset);
    }
}
