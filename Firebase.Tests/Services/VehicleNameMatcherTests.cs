using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class VehicleNameMatcherTests
{
    [Theory]
    [InlineData("citroen", "CITROËN")]
    [InlineData("mercedes benz", "Mercedes-Benz")]
    public void NormalizedExactMatchesIgnoreAccentsAndSeparators(string input, string expected)
    {
        Assert.True(VehicleNameMatcher.IsExact(input, expected));
    }

    [Theory]
    [InlineData("volkswgen", "VOLKSWAGEN")]
    [InlineData("corola", "Corolla")]
    [InlineData("volkswgaen", "VOLKSWAGEN")]
    public void MisspellingsProduceSuggestions(string input, string expected)
    {
        Assert.Contains(expected, VehicleNameMatcher.Suggest(input, new[] { expected }));
    }

    [Theory]
    [InlineData("208", "2008")]
    [InlineData("A3", "A4")]
    public void DifferentDigitsNeverProduceSuggestions(string input, string other)
    {
        Assert.Empty(VehicleNameMatcher.Suggest(input, new[] { other }));
    }
}
