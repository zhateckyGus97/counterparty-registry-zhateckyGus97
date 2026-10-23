namespace CounterpartyRegistry.Application.Common;

internal static class RequestValidation
{
    public static void EnsureId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор не должен быть пустым.", parameterName);
        }
    }
}
