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

    /// <summary>
    /// Préférence "Toujours visible" de la fenêtre principale.
    /// </summary>
    public bool IsTopmost { get; set; }

    /// <summary>
    /// Si true, l'overlay de rappel "Capture d'âme" est affiché en permanence.
    /// </summary>
    public bool ShowCaptureAmeReminder { get; set; }

    /// <summary>
    /// Taille de police de l'overlay Capture d'âme (12–40). Default = 20.
    /// </summary>
    public double CaptureAmeFontSize { get; set; } = 20;

    /// <summary>
    /// Durée d'un cycle de clignotement en millisecondes (200–5000). Default = 1500.
    /// </summary>
    public int CaptureAmeBlinkMs { get; set; } = 1500;

    /// <summary>
    /// Si true, le panneau d'actions overlay est affiché.
    /// </summary>
    public bool ShowActionOverlay { get; set; }

    /// <summary>
    /// Position X (écran) du panneau d'actions overlay. Null = position par défaut.
    /// </summary>
    public double? ActionOverlayLeft { get; set; }

    /// <summary>
    /// Position Y (écran) du panneau d'actions overlay. Null = position par défaut.
    /// </summary>
    public double? ActionOverlayTop { get; set; }

    // Backward compat : ignoré au chargement si SessionSnapshot est présent
    public GlobalHotkeyConfig? LastHotkeyConfig { get; set; }
}
