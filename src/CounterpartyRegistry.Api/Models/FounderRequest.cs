using System.ComponentModel.DataAnnotations;

namespace CounterpartyRegistry.Api.Models;

public sealed class FounderRequest
{
    [Required]
    public string? Inn { get; init; }

    [Required]
    public string? FullName { get; init; }
}
