using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class VehicleInputResolverTests
{
    [Fact]
    public void ExactMatchSearchesAllCandidatesNotOnlyVisibleOptions()
    {
        var candidates = Enumerable.Range(1, 60).Select(i => new VehicleInputOption(i.ToString(), $"Marca {i}")).ToArray();
        var result = VehicleInputResolver.Resolve("marca 55", candidates);
        Assert.Equal(VehicleInputResultKind.Exact, result.Kind);
        Assert.Equal("55", Assert.Single(result.Options).Id);
    }

    [Fact]
    public void MisspellingRequiresConfirmation()
    {
        var result = VehicleInputResolver.Resolve("Corola", new[] { new VehicleInputOption("1", "Corolla") });
        Assert.Equal(VehicleInputResultKind.Suggestions, result.Kind);
        Assert.Equal("Corolla", Assert.Single(result.Options).Name);
    }

    [Fact]
    public void UnknownNameAllowsExplicitManualChoice()
    {
        var result = VehicleInputResolver.Resolve("Otro modelo", new[] { new VehicleInputOption("1", "Corolla") });
        Assert.Equal(VehicleInputResultKind.Manual, result.Kind);
        Assert.Empty(result.Options);
    }

    [Fact]
    public void EqualNormalizedNamesRemainAmbiguous()
    {
        var result = VehicleInputResolver.Resolve("citroen", new[] { new VehicleInputOption("1", "Citroën"), new VehicleInputOption("2", "Citroen") });
        Assert.Equal(VehicleInputResultKind.Suggestions, result.Kind);
        Assert.Equal(2, result.Options.Count);
    }

    [Theory]
    [InlineData("manual_si", true)]
    [InlineData("manual_no", false)]
    [InlineData("si", null)]
    [InlineData("cualquier texto", null)]
    public void ManualEntryRequiresAnExplicitButtonChoice(string input, bool? expected)
    {
        Assert.Equal(expected, VehicleInputResolver.ParseManualConfirmation(input));
    }
}
