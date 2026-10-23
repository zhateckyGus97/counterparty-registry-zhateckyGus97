namespace CounterpartyRegistry.Application.Exceptions;

public sealed class EntityNotFoundException : Exception
{
    public string EntityName { get; }
    public Guid EntityId { get; }

    public EntityNotFoundException(string entityName, Guid entityId)
        : base($"Объект {entityName} с идентификатором {entityId} не найден.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}
