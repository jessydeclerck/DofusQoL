namespace DofusManager.Core.Models;

public class ZaapTerritory
{
    public required string Name { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public string Coordinates => $"[{X},{Y}]";
}

public static class ZaapTerritories
{
    public static IReadOnlyList<ZaapTerritory> All { get; } = new List<ZaapTerritory>
    {
        new() { Name = "Arche de Vili", X = 15, Y = -20 },
        new() { Name = "Bord de la forêt maléfique", X = -1, Y = 13 },
        new() { Name = "Champs de Cania", X = -27, Y = -36 },
        new() { Name = "Château d'Amakna", X = 3, Y = -5 },
        new() { Name = "Cimetière", X = 3, Y = 0 },
        new() { Name = "Cimetière primitif", X = -12, Y = 19 },
        new() { Name = "Cité d'Astrub", X = 5, Y = -18 },
        new() { Name = "Cœur immaculé", X = -31, Y = -56 },
        new() { Name = "Coin des Bouftous", X = 5, Y = 71 },
        new() { Name = "Crocuzko", X = -83, Y = -15 },
        new() { Name = "Dunes des ossements", X = 15, Y = -58 },
        new() { Name = "Entrée du château de Harebourg", X = -67, Y = -77 },
        new() { Name = "Foire de Trool", X = -11, Y = -36 },
        new() { Name = "Futaie enneigée", X = 39, Y = -82 },
        new() { Name = "Île de la Cawotte", X = 25, Y = -4 },
        new() { Name = "La Bourgade", X = -78, Y = -41 },
        new() { Name = "La Cuirasse", X = -26, Y = 37 },
        new() { Name = "Laboratoires abandonnés", X = 27, Y = -14 },
        new() { Name = "Lac de Cania", X = -3, Y = -42 },
        new() { Name = "Massif de Cania", X = -13, Y = -28 },
        new() { Name = "Mont des Tombeaux", X = 40, Y = -44 },
        new() { Name = "Montagne des Craqueleurs", X = -5, Y = -8 },
        new() { Name = "Nimotopia", X = -67, Y = 29 },
        new() { Name = "Pâturages", X = 2, Y = -5 },
        new() { Name = "Plage de la Tortue", X = 35, Y = 12 },
        new() { Name = "Plaine des Porkass", X = -5, Y = -23 },
        new() { Name = "Plaine des Scarafeuilles", X = -1, Y = 24 },
        new() { Name = "Plaines Rocheuses", X = -17, Y = -47 },
        new() { Name = "Port de Madrestam", X = 7, Y = -4 },
        new() { Name = "Rivage sufokien", X = 10, Y = 22 },
        new() { Name = "Route des âmes", X = -1, Y = -3 },
        new() { Name = "Route des Roulottes", X = -25, Y = 12 },
        new() { Name = "Routes Rocailleuses", X = -20, Y = -20 },
        new() { Name = "Sufokia", X = 13, Y = 26 },
        new() { Name = "Tainéla", X = 1, Y = -32 },
        new() { Name = "Temple des alliances", X = 13, Y = 35 },
        new() { Name = "Terres Désacrées", X = -15, Y = 25 },
        new() { Name = "Village côtier", X = -46, Y = 18 },
        new() { Name = "Village d'Amakna", X = -2, Y = 0 },
        new() { Name = "Village de la Canopée", X = -54, Y = 16 },
        new() { Name = "Village de Pandala", X = 20, Y = -29 },
        new() { Name = "Village des Brigandins", X = -16, Y = -24 },
        new() { Name = "Village des Dopeuls", X = -34, Y = -8 },
        new() { Name = "Village des Eleveurs", X = -16, Y = 1 },
        new() { Name = "Village des Kanigs", X = 0, Y = -56 },
        new() { Name = "Village des Zoths", X = -53, Y = 18 },
        new() { Name = "Village enseveli", X = -77, Y = -73 },
    }.OrderBy(z => z.Name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
}
