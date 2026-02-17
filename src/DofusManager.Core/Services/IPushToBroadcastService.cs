using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de broadcast "hold to broadcast" : maintenir une touche pour
/// broadcaster automatiquement chaque clic vers toutes les fenêtres Dofus.
/// </summary>
public interface IPushToBroadcastService : IDisposable
{
    /// <summary>
    /// Arme le mode push-to-broadcast : installe le hook souris et commence la capture.
    /// </summary>
    void Arm(IReadOnlyList<DofusWindow> dofusWindows);

    /// <summary>
    /// Désarme le mode : supprime le hook souris et arrête la capture.
    /// </summary>
    void Disarm();

    /// <summary>
    /// Indique si le mode est actuellement armé.
    /// </summary>
    bool IsArmed { get; }

    /// <summary>
    /// Événement déclenché après chaque broadcast de clic réussi.
    /// Le paramètre indique le nombre de fenêtres atteintes.
    /// </summary>
    event EventHandler<int>? BroadcastPerformed;
}
