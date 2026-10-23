namespace CounterpartyRegistry.Domain.Validation;

public static class InnValidator
{
    private static readonly int[] LegalEntityWeights = { 2, 4, 10, 3, 5, 9, 4, 6, 8 };
    private static readonly int[] FirstIndividualWeights = { 7, 2, 4, 10, 3, 5, 9, 4, 6, 8 };
    private static readonly int[] SecondIndividualWeights = { 3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8 };

    public static bool IsValid(string? inn)
    {
        if (inn is null || (inn.Length != 10 && inn.Length != 12))
        {
            return false;
        }

        if (inn.Any(c => c < '0' || c > '9') || inn.All(c => c == '0'))
        {
            return false;
        }

        if (inn.Length == 10)
        {
            return CalculateCheckDigit(inn, LegalEntityWeights) == inn[9] - '0';
        }

        return CalculateCheckDigit(inn, FirstIndividualWeights) == inn[10] - '0'
            && CalculateCheckDigit(inn, SecondIndividualWeights) == inn[11] - '0';
    }

    private static int CalculateCheckDigit(string inn, int[] weights)
    {
        var sum = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            sum += (inn[i] - '0') * weights[i];
        }

        return sum % 11 % 10;
    }
}
