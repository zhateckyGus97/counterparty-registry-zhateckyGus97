namespace CounterpartyRegistry.Application.Exceptions;

public sealed class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }
}
