using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Application.Clients;

public sealed record CreateClientRequest(string Inn, string Name, ClientType Type);

public sealed record UpdateClientRequest(string Inn, string Name, ClientType Type);
