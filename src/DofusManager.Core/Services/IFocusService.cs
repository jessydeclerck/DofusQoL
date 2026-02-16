using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de navigation et focus entre les fenêtres Dofus.
/// </summary>
public interface IFocusService
{
    /// <summary>Focus la fenêtre au slot donné.</summary>
    FocusResult FocusSlot(int slotIndex);

    /// <summary>Focus la fenêtre suivante dans l'ordre.</summary>
    FocusResult FocusNext();

    /// <summary>Focus la fenêtre précédente dans l'ordre.</summary>
    FocusResult FocusPrevious();

    /// <summary>Focus la dernière fenêtre utilisée.</summary>
    FocusResult FocusLast();

    /// <summary>Focus la fenêtre du leader.</summary>
    FocusResult FocusLeader();

    /// <summary>Désigne une fenêtre comme leader.</summary>
    void SetLeader(nint windowHandle);

    /// <summary>Met à jour les slots depuis la liste de fenêtres détectées.</summary>
    void UpdateSlots(IReadOnlyList<DofusWindow> windows);

    /// <summary>Le leader actuel, ou null.</summary>
    DofusWindow? CurrentLeader { get; }

    /// <summary>Index du slot actuellement focusé, ou null.</summary>
    int? CurrentSlotIndex { get; }

    /// <summary>Liste ordonnée des fenêtres dans les slots.</summary>
    IReadOnlyList<DofusWindow> Slots { get; }
}
