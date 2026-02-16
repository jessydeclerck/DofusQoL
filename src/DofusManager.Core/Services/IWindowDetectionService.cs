using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

public interface IWindowDetectionService : IDisposable
{
    /// <summary>
    /// Liste des fenêtres Dofus actuellement détectées.
    /// </summary>
    IReadOnlyList<DofusWindow> DetectedWindows { get; }

    /// <summary>
    /// Déclenché quand la liste des fenêtres change (ajout ou suppression).
    /// </summary>
    event EventHandler<WindowsChangedEventArgs>? WindowsChanged;

    /// <summary>
    /// Démarre le polling périodique.
    /// </summary>
    void StartPolling(int intervalMs = 500);

    /// <summary>
    /// Arrête le polling périodique.
    /// </summary>
    void StopPolling();

    /// <summary>
    /// Effectue un scan ponctuel des fenêtres Dofus.
    /// </summary>
    IReadOnlyList<DofusWindow> DetectOnce();

    /// <summary>
    /// Indique si le polling est actif.
    /// </summary>
    bool IsPolling { get; }
}
