using System.ComponentModel.DataAnnotations;
using CounterpartyRegistry.Application.Common;

namespace CounterpartyRegistry.Api.Models;

public class PageQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, PageRequest.MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (((long)Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("Номер страницы слишком большой.", new[] { nameof(Page) });
    }
}
