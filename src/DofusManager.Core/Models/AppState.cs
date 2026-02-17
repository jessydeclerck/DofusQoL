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
    /// Snapshot complet de la dernière session (slots, raccourcis globaux, touche broadcast).
    /// Capture l'état exact incluant les modifications utilisateur (ordre, leader, hotkeys).
    /// </summary>
    public Profile? SessionSnapshot { get; set; }

    // Backward compat : ignoré au chargement si SessionSnapshot est présent
    public GlobalHotkeyConfig? LastHotkeyConfig { get; set; }
}
