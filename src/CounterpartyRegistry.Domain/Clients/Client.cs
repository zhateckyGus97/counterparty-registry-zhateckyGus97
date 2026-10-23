using CounterpartyRegistry.Domain.Exceptions;
using CounterpartyRegistry.Domain.Founders;
using CounterpartyRegistry.Domain.Validation;

namespace CounterpartyRegistry.Domain.Clients;

public class Client
{
    public const int MaxNameLength = 200;

    private readonly List<ClientFounder> _founderLinks = new();

    public Guid Id { get; private set; }
    public string Inn { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public ClientType Type { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<ClientFounder> FounderLinks => _founderLinks.AsReadOnly();

    private Client()
    {
    }

    public static Client Create(string inn, string name, ClientType type, DateTimeOffset now)
    {
        var values = ValidateAndNormalize(inn, name, type);
        var utcNow = now.ToUniversalTime();

        return new Client
        {
            Id = Guid.NewGuid(),
            Inn = values.Inn,
            Name = values.Name,
            Type = type,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void Update(string inn, string name, ClientType type, DateTimeOffset now)
    {
        var values = ValidateAndNormalize(inn, name, type);

        if (type == ClientType.IndividualEntrepreneur && _founderLinks.Count > 0)
        {
            throw new DomainRuleException("client.has_founders",
                "Нельзя изменить тип клиента на ИП, пока у него есть учредители.");
        }

        if (Inn == values.Inn && Name == values.Name && Type == type)
        {
            return;
        }

        Inn = values.Inn;
        Name = values.Name;
        Type = type;
        UpdatedAt = now.ToUniversalTime();
    }

    public ClientFounder AddFounder(Founder founder, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(founder);

        if (Type != ClientType.LegalEntity)
        {
            throw new DomainRuleException("client.founders_not_allowed",
                "Учредителей можно добавлять только юридическим лицам.");
        }

        if (_founderLinks.Any(link => link.FounderId == founder.Id))
        {
            throw new DomainRuleException("client.founder_already_linked",
                "Этот учредитель уже связан с клиентом.");
        }

        var link = new ClientFounder(Id, founder, now);
        _founderLinks.Add(link);
        UpdatedAt = now.ToUniversalTime();
        return link;
    }

    public void RemoveFounder(Guid founderId, DateTimeOffset now)
    {
        if (founderId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор учредителя не должен быть пустым.",
                nameof(founderId));
        }

        var link = _founderLinks.SingleOrDefault(link => link.FounderId == founderId);

        if (link is null)
        {
            throw new DomainRuleException("client.founder_not_linked",
                "Учредитель не связан с этим клиентом.");
        }

        _founderLinks.Remove(link);
        UpdatedAt = now.ToUniversalTime();
    }

    private static (string Inn, string Name) ValidateAndNormalize(
        string inn, string name, ClientType type)
    {
        if (type != ClientType.LegalEntity && type != ClientType.IndividualEntrepreneur)
        {
            throw new DomainValidationException(nameof(Type), "Неизвестный тип клиента.");
        }

        if (string.IsNullOrWhiteSpace(inn))
            throw new DomainValidationException(nameof(Inn), "Укажите ИНН.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException(nameof(Name), "Укажите наименование клиента.");
        inn = inn.Trim();
        name = name.Trim();

        if (name.Length > MaxNameLength)
        {
            throw new DomainValidationException(nameof(Name),
                $"Наименование не должно превышать {MaxNameLength} символов.");
        }

        var expectedInnLength = type == ClientType.LegalEntity ? 10 : 12;

        if (inn.Length != expectedInnLength || !InnValidator.IsValid(inn))
        {
            throw new DomainValidationException(nameof(Inn),
                $"Некорректный ИНН. Для этого типа клиента требуется " +
                $"{expectedInnLength} цифр с верными контрольными цифрами.");
        }

        return (inn, name);
    }
}
