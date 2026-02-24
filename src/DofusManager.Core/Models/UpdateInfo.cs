namespace DofusManager.Core.Models;

/// <summary>
/// Informations sur une mise à jour disponible.
/// </summary>
public class UpdateInfo
{
    /// <summary>Version tag de la release (ex: "v1.2.0").</summary>
    public required string TagName { get; init; }

    /// <summary>Version parsée sans le préfixe "v".</summary>
    public required Version Version { get; init; }

    /// <summary>URL de téléchargement du zip.</summary>
    public required string DownloadUrl { get; init; }

    /// <summary>Notes de release (Markdown).</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>Date de publication.</summary>
    public DateTime PublishedAt { get; init; }

    /// <summary>Taille du fichier en octets (0 si inconnue).</summary>
    public long SizeBytes { get; init; }
}

/// <summary>
/// Résultat d'une vérification de mise à jour.
/// </summary>
public record UpdateCheckResult(bool IsUpdateAvailable, UpdateInfo? Update = null, string? ErrorMessage = null)
{
    public static UpdateCheckResult UpToDate() => new(false);
    public static UpdateCheckResult Available(UpdateInfo update) => new(true, update);
    public static UpdateCheckResult Error(string message) => new(false, ErrorMessage: message);
}
