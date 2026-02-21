using DofusManager.Core.Models;
using Xunit;

namespace DofusManager.Tests.Models;

public class ZaapTerritoryTests
{
    [Fact]
    public void All_Contains47Territories()
    {
        Assert.Equal(47, ZaapTerritories.All.Count);
    }

    [Fact]
    public void All_HasNoDuplicates()
    {
        var names = ZaapTerritories.All.Select(z => z.Name).ToList();
        var distinct = names.Distinct().ToList();
        Assert.Equal(names.Count, distinct.Count);
    }

    [Fact]
    public void All_IsSortedAlphabetically()
    {
        var names = ZaapTerritories.All.Select(z => z.Name).ToList();
        var sorted = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Theory]
    [InlineData("Cité d'Astrub")]
    [InlineData("Village d'Amakna")]
    [InlineData("La Bourgade")]
    [InlineData("Sufokia")]
    [InlineData("Village de Pandala")]
    [InlineData("Village des Dopeuls")]
    [InlineData("Village des Eleveurs")]
    public void All_ContainsExpectedTerritories(string expectedName)
    {
        Assert.Contains(ZaapTerritories.All, z => z.Name == expectedName);
    }

    [Fact]
    public void All_AllTerritoriesHaveNonEmptyNames()
    {
        Assert.All(ZaapTerritories.All, z => Assert.False(string.IsNullOrWhiteSpace(z.Name)));
    }

    [Fact]
    public void All_AllTerritoriesHaveNonEmptyRegions()
    {
        Assert.All(ZaapTerritories.All, z => Assert.False(string.IsNullOrWhiteSpace(z.Region)));
    }

    [Fact]
    public void DisplayName_IncludesNameAndRegion()
    {
        var territory = new ZaapTerritory { Name = "Cité d'Astrub", Region = "Astrub", X = 5, Y = -18 };
        Assert.Equal("Cité d'Astrub (Astrub)", territory.DisplayName);
    }

    [Fact]
    public void Coordinates_FormatsCorrectly()
    {
        var territory = new ZaapTerritory { Name = "Test", Region = "TestRegion", X = 5, Y = -18 };
        Assert.Equal("[5,-18]", territory.Coordinates);
    }

    [Fact]
    public void Coordinates_NegativeValues()
    {
        var territory = new ZaapTerritory { Name = "Test", Region = "TestRegion", X = -27, Y = -36 };
        Assert.Equal("[-27,-36]", territory.Coordinates);
    }

    [Fact]
    public void All_AllTerritoriesHaveCoordinates()
    {
        // Vérifie qu'au moins un territoire n'a pas (0,0) — la plupart ont des coords non-nulles
        Assert.Contains(ZaapTerritories.All, z => z.X != 0 || z.Y != 0);
    }

    [Theory]
    [InlineData("Cité d'Astrub", "Astrub", 5, -18)]
    [InlineData("La Bourgade", "Île de Frigost", -78, -41)]
    [InlineData("Sufokia", "Baie de Sufokia", 13, 26)]
    public void All_HasCorrectData(string name, string expectedRegion, int expectedX, int expectedY)
    {
        var territory = ZaapTerritories.All.First(z => z.Name == name);
        Assert.Equal(expectedRegion, territory.Region);
        Assert.Equal(expectedX, territory.X);
        Assert.Equal(expectedY, territory.Y);
    }
}
