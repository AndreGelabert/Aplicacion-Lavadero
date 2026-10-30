namespace Firebase.Services;

public sealed record VehicleInputOption(string Id, string Name);

public enum VehicleInputResultKind { Exact, Suggestions, Manual }

public sealed record VehicleInputResult(VehicleInputResultKind Kind, IReadOnlyList<VehicleInputOption> Options);

/// <summary>Resolves a complete catalogue without treating a suggestion as a selection.</summary>
public static class VehicleInputResolver
{
    public static bool? ParseManualConfirmation(string input) => input switch
    {
        "manual_si" => true,
        "manual_no" => false,
        _ => null
    };

    public static VehicleInputResult Resolve(string input, IEnumerable<VehicleInputOption> candidates)
    {
        var options = candidates.Where(option => !string.IsNullOrWhiteSpace(option.Name)).ToArray();
        var exact = options.Where(option => VehicleNameMatcher.IsExact(input, option.Name)).ToArray();
        if (exact.Length == 1) return new(VehicleInputResultKind.Exact, exact);
        if (exact.Length > 1) return new(VehicleInputResultKind.Suggestions, exact.Take(5).ToArray());

        var names = VehicleNameMatcher.Suggest(input, options.Select(option => option.Name));
        var suggestions = names.SelectMany(name => options.Where(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Take(5).ToArray();
        return suggestions.Length == 0
            ? new(VehicleInputResultKind.Manual, Array.Empty<VehicleInputOption>())
            : new(VehicleInputResultKind.Suggestions, suggestions);
    }
}
