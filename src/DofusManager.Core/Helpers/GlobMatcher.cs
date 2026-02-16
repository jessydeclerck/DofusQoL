using System.Text.RegularExpressions;

namespace DofusManager.Core.Helpers;

/// <summary>
/// Utilitaire pour le matching de patterns glob simples (wildcards * et ?).
/// </summary>
public static class GlobMatcher
{
    /// <summary>
    /// Vérifie si un texte correspond à un pattern glob.
    /// '*' matche 0+ caractères, '?' matche exactement 1 caractère.
    /// La comparaison est insensible à la casse.
    /// </summary>
    public static bool IsMatch(string pattern, string text)
    {
        if (pattern == "*") return true;

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }
}
