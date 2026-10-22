using CounterpartyRegistry.Domain.Validation;

namespace CounterpartyRegistry.Domain.Founders;

public class Founder
{
    public const int MaxFullNameLength = 200;

    public Guid Id { get; private set; }
    public string Inn { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Founder()
    {
    }

    public static Founder Create(string inn, string fullName, DateTimeOffset now)
    {
        var values = ValidateAndNormalize(inn, fullName);
        var utcNow = now.ToUniversalTime();

        return new Founder
        {
            Id = Guid.NewGuid(),
            Inn = values.Inn,
            FullName = values.FullName,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void Update(string inn, string fullName, DateTimeOffset now)
    {
        var values = ValidateAndNormalize(inn, fullName);

        if (Inn == values.Inn && FullName == values.FullName)
        {
            return;
        }

        Inn = values.Inn;
        FullName = values.FullName;
        UpdatedAt = now.ToUniversalTime();
    }

    private static (string Inn, string FullName) ValidateAndNormalize(string inn, string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inn);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        inn = inn.Trim();
        fullName = fullName.Trim();

        if (inn.Length != 12 || !InnValidator.IsValid(inn))
        {
            throw new ArgumentException(
                "ИНН учредителя должен содержать 12 цифр с верными контрольными цифрами.",
                nameof(inn));
        }

        if (fullName.Length > MaxFullNameLength)
        {
            throw new ArgumentException(
                $"ФИО не должно превышать {MaxFullNameLength} символов.", nameof(fullName));
        }

        return (inn, fullName);
    }
}
