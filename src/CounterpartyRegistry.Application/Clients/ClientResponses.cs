using CounterpartyRegistry.Application.Founders;
using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Application.Clients;

public sealed record ClientSummaryResponse(
    Guid Id, string Inn, string Name, ClientType Type,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record ClientFounderResponse(FounderResponse Founder, DateTimeOffset CreatedAt);

public sealed record ClientResponse(
    Guid Id, string Inn, string Name, ClientType Type,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<ClientFounderResponse> Founders)
{
    internal static ClientResponse From(Client client)
    {
        var founders = client.FounderLinks
            .OrderBy(link => link.CreatedAt).ThenBy(link => link.FounderId)
            .Select(link => new ClientFounderResponse(FounderResponse.From(link.Founder), link.CreatedAt))
            .ToArray();

        return new ClientResponse(
            client.Id, client.Inn, client.Name, client.Type, client.CreatedAt, client.UpdatedAt,
            Array.AsReadOnly(founders));
    }
}
