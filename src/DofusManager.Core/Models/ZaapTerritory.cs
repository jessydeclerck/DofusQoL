namespace DofusManager.Core.Models;

public class ZaapTerritory
{
    public required string Name { get; init; }
    public required string Region { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public string DisplayName => $"{Name} ({Region})";
    public string Coordinates => $"[{X},{Y}]";
}

public static class ZaapTerritories
{
    public static IReadOnlyList<ZaapTerritory> All { get; } = new List<ZaapTerritory>
    {
        new() { Name = "Arche de Vili", Region = "Profondeurs d'Astrub", X = 15, Y = -20 },
        new() { Name = "Bord de la forêt maléfique", Region = "Amakna", X = -1, Y = 13 },
        new() { Name = "Champs de Cania", Region = "Plaines de Cania", X = -27, Y = -36 },
        new() { Name = "Château d'Amakna", Region = "Amakna", X = 3, Y = -5 },
        new() { Name = "Cimetière", Region = "Incarnam", X = 3, Y = 0 },
        new() { Name = "Cimetière primitif", Region = "Montagne des Koalaks", X = -12, Y = 19 },
        new() { Name = "Cité d'Astrub", Region = "Astrub", X = 5, Y = -18 },
        new() { Name = "Cœur immaculé", Region = "Bonta", X = -31, Y = -56 },
        new() { Name = "Coin des Bouftous", Region = "Amakna", X = 5, Y = 71 },
        new() { Name = "Crocuzko", Region = "Archipel des Écailles", X = -83, Y = -15 },
        new() { Name = "Dunes des ossements", Region = "Saharach", X = 15, Y = -58 },
        new() { Name = "Entrée du château de Harebourg", Region = "Île de Frigost", X = -67, Y = -77 },
        new() { Name = "Foire du Trool", Region = "Foire du Trool", X = -11, Y = -36 },
        new() { Name = "Futaie enneigée", Region = "Archipel de Valonia", X = 39, Y = -82 },
        new() { Name = "Île de la Cawotte", Region = "Île des Wabbits", X = 25, Y = -4 },
        new() { Name = "La Bourgade", Region = "Île de Frigost", X = -78, Y = -41 },
        new() { Name = "La Cuirasse", Region = "Brâkmar", X = -26, Y = 37 },
        new() { Name = "Laboratoires abandonnés", Region = "Île des Wabbits", X = 27, Y = -14 },
        new() { Name = "Lac de Cania", Region = "Plaines de Cania", X = -3, Y = -42 },
        new() { Name = "Massif de Cania", Region = "Plaines de Cania", X = -13, Y = -28 },
        new() { Name = "Mont des Tombeaux", Region = "Île de Grobe", X = 40, Y = -44 },
        new() { Name = "Montagne des Craqueleurs", Region = "Amakna", X = -5, Y = -8 },
        new() { Name = "Nimotopia", Region = "Nimotopia", X = -67, Y = 29 },
        new() { Name = "Pâturages", Region = "Incarnam", X = 2, Y = -5 },
        new() { Name = "Plage de la Tortue", Region = "Île de Moon", X = 35, Y = 12 },
        new() { Name = "Plaine des Porkass", Region = "Plaines de Cania", X = -5, Y = -23 },
        new() { Name = "Plaine des Scarafeuilles", Region = "Amakna", X = -1, Y = 24 },
        new() { Name = "Plaines Rocheuses", Region = "Plaines de Cania", X = -17, Y = -47 },
        new() { Name = "Port de Madrestam", Region = "Amakna", X = 7, Y = -4 },
        new() { Name = "Rivage sufokien", Region = "Baie de Sufokia", X = 10, Y = 22 },
        new() { Name = "Route des âmes", Region = "Incarnam", X = -1, Y = -3 },
        new() { Name = "Route des Roulottes", Region = "Landes de Sidimote", X = -25, Y = 12 },
        new() { Name = "Routes Rocailleuses", Region = "Plaines de Cania", X = -20, Y = -20 },
        new() { Name = "Sufokia", Region = "Baie de Sufokia", X = 13, Y = 26 },
        new() { Name = "Tainéla", Region = "Astrub", X = 1, Y = -32 },
        new() { Name = "Temple des alliances", Region = "Baie de Sufokia", X = 13, Y = 35 },
        new() { Name = "Terres Désacrées", Region = "Landes de Sidimote", X = -15, Y = 25 },
        new() { Name = "Village côtier", Region = "Île d'Otomaï", X = -46, Y = 18 },
        new() { Name = "Village d'Amakna", Region = "Amakna", X = -2, Y = 0 },
        new() { Name = "Village de la Canopée", Region = "Île d'Otomaï", X = -54, Y = 16 },
        new() { Name = "Village de Pandala", Region = "Île de Pandala", X = 20, Y = -29 },
        new() { Name = "Village des Brigandins", Region = "Plaines de Cania", X = -16, Y = -24 },
        new() { Name = "Village des Dopeuls", Region = "Plaines de Cania", X = -34, Y = -8 },
        new() { Name = "Village des Eleveurs", Region = "Montagne des Koalaks", X = -16, Y = 1 },
        new() { Name = "Village des Kanigs", Region = "Plaines de Cania", X = 0, Y = -56 },
        new() { Name = "Village des Zoths", Region = "Île d'Otomaï", X = -53, Y = 18 },
        new() { Name = "Village enseveli", Region = "Île de Frigost", X = -77, Y = -73 },
    }.OrderBy(z => z.Name, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
}
