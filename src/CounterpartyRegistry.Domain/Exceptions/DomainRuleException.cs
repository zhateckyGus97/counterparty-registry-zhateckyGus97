namespace CounterpartyRegistry.Domain.Exceptions;

public sealed class DomainRuleException : Exception
{
    public string Code { get; }

    public DomainRuleException(string code, string message) : base(message)
    {
        Code = code;
    }
}
