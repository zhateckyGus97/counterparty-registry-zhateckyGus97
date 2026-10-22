using CounterpartyRegistry.Domain.Founders;

namespace CounterpartyRegistry.Domain.Clients;

public class ClientFounder
{
    public Guid ClientId { get; private set; }
    public Guid FounderId { get; private set; }
    public Founder Founder { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private ClientFounder()
    {
    }

    // Links are created through Client.AddFounder, which enforces client rules.
    internal ClientFounder(Guid clientId, Founder founder, DateTimeOffset now)
    {
        ClientId = clientId;
        FounderId = founder.Id;
        Founder = founder;
        CreatedAt = now.ToUniversalTime();
    }
}
