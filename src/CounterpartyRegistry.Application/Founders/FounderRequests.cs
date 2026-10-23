namespace CounterpartyRegistry.Application.Founders;

public sealed record CreateFounderRequest(string Inn, string FullName);

public sealed record UpdateFounderRequest(string Inn, string FullName);
