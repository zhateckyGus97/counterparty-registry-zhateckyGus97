namespace CounterpartyRegistry.Application.Common;

public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    public int PageNumber { get; }
    public int PageSize { get; }
    public int Offset { get; }

    public PageRequest(int pageNumber = 1, int pageSize = 20)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Номер страницы должен быть положительным.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize),
                $"Размер страницы должен быть от 1 до {MaxPageSize}.");
        }

        var offset = ((long)pageNumber - 1) * pageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Номер страницы слишком большой.");
        }

        PageNumber = pageNumber;
        PageSize = pageSize;
        Offset = (int)offset;
    }
}
