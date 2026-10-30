using System.Globalization;
using System.Text;

namespace Firebase.Services;

/// <summary>Finds suggestions only; callers must obtain explicit confirmation.</summary>
public static class VehicleNameMatcher
{
    public static bool IsExact(string input, string candidate) =>
        Normalize(input) == Normalize(candidate);

    public static IReadOnlyList<string> Suggest(string input, IEnumerable<string> candidates)
    {
        var normalized = Normalize(input);
        if (normalized.Length < 3) return Array.Empty<string>();
        var digits = Digits(normalized);

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => new { Name = candidate, Normalized = Normalize(candidate) })
            .Where(candidate => candidate.Normalized.Length >= 3 && Digits(candidate.Normalized) == digits)
            .Select(candidate => new { candidate.Name, Distance = Distance(normalized, candidate.Normalized), Length = Math.Max(normalized.Length, candidate.Normalized.Length) })
            .Where(candidate => candidate.Distance > 0 && candidate.Distance <= Math.Min(3, candidate.Length / 4))
            .OrderBy(candidate => (double)candidate.Distance / candidate.Length)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            .Select(char.ToUpperInvariant).ToArray());
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    private static int Distance(string left, string right)
    {
        var distance = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) distance[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) distance[0, j] = j;
        for (var i = 1; i <= left.Length; i++)
        for (var j = 1; j <= right.Length; j++)
        {
            var cost = left[i - 1] == right[j - 1] ? 0 : 1;
            distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
            if (i > 1 && j > 1 && left[i - 1] == right[j - 2] && left[i - 2] == right[j - 1])
                distance[i, j] = Math.Min(distance[i, j], distance[i - 2, j - 2] + 1);
        }
        return distance[left.Length, right.Length];
    }
}
