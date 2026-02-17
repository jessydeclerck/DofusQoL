using System.Windows.Media.Imaging;

namespace DofusManager.UI.Helpers;

/// <summary>
/// Mapping entre les noms de classe Dofus (titre de fenêtre) et les breed IDs (fichiers icônes).
/// Format titre fenêtre : "NomPersonnage - Classe - Version - Release"
/// </summary>
public static class DofusClassHelper
{
    private static readonly Dictionary<string, int> ClassToBreedId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Feca"] = 1, ["Féca"] = 1,
        ["Osamodas"] = 2,
        ["Enutrof"] = 3,
        ["Sram"] = 4,
        ["Xelor"] = 5, ["Xélor"] = 5,
        ["Ecaflip"] = 6, ["Écaflip"] = 6,
        ["Eniripsa"] = 7,
        ["Iop"] = 8,
        ["Cra"] = 9, ["Crâ"] = 9,
        ["Sadida"] = 10,
        ["Sacrieur"] = 11,
        ["Pandawa"] = 12,
        ["Roublard"] = 13,
        ["Zobal"] = 14,
        ["Steamer"] = 15,
        ["Eliotrope"] = 16, ["Éliotrope"] = 16,
        ["Huppermage"] = 17,
        ["Ouginak"] = 18,
        ["Forgelance"] = 20,
    };

    private static readonly Dictionary<int, BitmapImage> IconCache = new();

    /// <summary>
    /// Extrait le nom de classe depuis le titre de fenêtre Dofus.
    /// "Cuckoolo - Pandawa - 3.4.18.19 - Release" → "Pandawa"
    /// </summary>
    public static string? ExtractClassName(string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle)) return null;

        var parts = windowTitle.Split(" - ", StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[1] : null;
    }

    /// <summary>
    /// Retourne le breed ID pour un nom de classe, ou null si non reconnu.
    /// </summary>
    public static int? GetBreedId(string? className)
    {
        if (className is null) return null;
        return ClassToBreedId.TryGetValue(className, out var id) ? id : null;
    }

    /// <summary>
    /// Retourne l'icône BitmapImage pour un titre de fenêtre Dofus.
    /// </summary>
    public static BitmapImage? GetClassIcon(string? windowTitle)
    {
        var className = ExtractClassName(windowTitle);
        var breedId = GetBreedId(className);
        if (breedId is null) return null;

        if (IconCache.TryGetValue(breedId.Value, out var cached))
            return cached;

        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/ClassIcons/{breedId.Value}.jpg", UriKind.Absolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 36;
            bitmap.EndInit();
            bitmap.Freeze();
            IconCache[breedId.Value] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
