using System.ComponentModel.DataAnnotations;

namespace CounterpartyRegistry.Api.Validation;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("Идентификатор не должен быть пустым.")
    {
    }

    public override bool IsValid(object? value) => value is Guid id && id != Guid.Empty;
}
