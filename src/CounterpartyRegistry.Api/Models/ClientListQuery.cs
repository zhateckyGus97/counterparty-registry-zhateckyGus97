using System.ComponentModel.DataAnnotations;
using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Api.Models;

public sealed class ClientListQuery : PageQuery
{
    [EnumDataType(typeof(ClientType))]
    public ClientType? Type { get; init; }
}
