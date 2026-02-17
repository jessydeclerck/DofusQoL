using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de broadcast d'inputs (touche ou clic) vers plusieurs fenêtres Dofus.
/// </summary>
public interface IBroadcastService
{
    /// <summary>
    /// Exécute un broadcast d'input vers les fenêtres cibles selon le preset.
    /// </summary>
    Task<BroadcastResult> ExecuteBroadcastAsync(
        BroadcastPreset preset,
        IReadOnlyList<DofusWindow> allWindows,
        nint? leaderHandle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure le délai anti-rafale en millisecondes.
    /// </summary>
    int CooldownMs { get; set; }

    /// <summary>
    /// Indique si le cooldown anti-rafale est actif (broadcast récent).
    /// </summary>
    bool IsOnCooldown { get; }

    /// <summary>
    /// Pause globale : si true, aucun broadcast n'est envoyé.
    /// </summary>
    bool IsPaused { get; set; }
}
