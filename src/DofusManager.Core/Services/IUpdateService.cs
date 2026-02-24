using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de vérification et téléchargement des mises à jour via GitHub Releases.
/// </summary>
public interface IUpdateService
{
    /// <summary>Vérifie si une mise à jour est disponible.</summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Télécharge le zip de mise à jour dans un dossier temporaire.
    /// Retourne le chemin du fichier zip téléchargé.
    /// </summary>
    Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lance le processus de mise à jour : démarre l'updater, puis quitte l'application.
    /// </summary>
    void LaunchUpdaterAndExit(string zipPath, string installDirectory);
}
