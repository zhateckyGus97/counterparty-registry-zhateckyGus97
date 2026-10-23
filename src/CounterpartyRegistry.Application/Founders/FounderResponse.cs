using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.Application.Founders;

public sealed record FounderResponse(
    Guid Id, string Inn, string FullName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    internal static FounderResponse From(Founder founder) => new(
        founder.Id, founder.Inn, founder.FullName, founder.CreatedAt, founder.UpdatedAt);
}
