namespace DofusManager.Core.Models;

/// <summary>
/// État de la dernière session, persisté entre les lancements.
/// Permet de restaurer la configuration automatiquement au démarrage.
/// </summary>
public class AppState
{
    /// <summary>
    /// Nom du profil actif lors de la dernière session.
    /// Null si aucun profil n'était chargé explicitement.
    /// </summary>
    public string? ActiveProfileName { get; set; }

    /// <summary>
    /// Snapshot de la configuration de la dernière session (raccourcis globaux, touche broadcast).
    /// Utilisé quand aucun profil n'est actif, pour ne rien perdre entre 2 lancements.
    /// </summary>
    public GlobalHotkeyConfig? LastHotkeyConfig { get; set; }
}
