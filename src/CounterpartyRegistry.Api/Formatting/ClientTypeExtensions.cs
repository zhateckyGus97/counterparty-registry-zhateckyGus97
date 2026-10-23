using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Api.Formatting;

public static class ClientTypeExtensions
{
    public static string ToDisplayName(this ClientType type)
    {
        return type switch
        {
            ClientType.LegalEntity => "Юридическое лицо",
            ClientType.IndividualEntrepreneur =>
                "Индивидуальный предприниматель",

            _ => throw new ArgumentOutOfRangeException(
                nameof(type), type, "Неизвестный тип клиента.")
        };
    }
}