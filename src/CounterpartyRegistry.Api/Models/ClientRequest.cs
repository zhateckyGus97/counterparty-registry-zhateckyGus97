using System.ComponentModel.DataAnnotations;
using CounterpartyRegistry.Domain.Clients;

namespace CounterpartyRegistry.Api.Models;

public sealed class ClientRequest
{
    [Required]
    public string? Inn { get; init; }

    [Required]
    public string? Name { get; init; }

    [Required]
    [EnumDataType(typeof(ClientType))]
    public ClientType? Type { get; init; }
}
