using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.Core.Services;

/// <summary>
/// Logique de navigation et focus entre fenêtres Dofus.
/// Gère les slots, le leader, et le tracking de la dernière fenêtre.
/// </summary>
public class FocusService : IFocusService
{
    private static readonly ILogger Logger = Log.ForContext<FocusService>();

    private readonly IWin32WindowHelper _windowHelper;
    private readonly object _lock = new();

    private List<DofusWindow> _slots = [];
    private nint _leaderHandle;
    private int? _currentSlotIndex;
    private int? _lastSlotIndex;

    public FocusService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public DofusWindow? CurrentLeader
    {
        get
        {
            lock (_lock)
            {
                if (_leaderHandle != 0)
                    return _slots.FirstOrDefault(w => w.Handle == _leaderHandle);

                // Par défaut, le premier slot
                return _slots.Count > 0 ? _slots[0] : null;
            }
        }
    }

    public int? CurrentSlotIndex
    {
        get
        {
            lock (_lock) { return _currentSlotIndex; }
        }
    }

    public IReadOnlyList<DofusWindow> Slots
    {
        get
        {
            lock (_lock) { return _slots.ToList(); }
        }
    }

    public void UpdateSlots(IReadOnlyList<DofusWindow> windows)
    {
        lock (_lock)
        {
            _slots = new List<DofusWindow>(windows);
            Logger.Debug("Slots mis à jour : {Count} fenêtres", _slots.Count);
        }
    }

    public void SetLeader(nint windowHandle)
    {
        lock (_lock)
        {
            _leaderHandle = windowHandle;
            Logger.Information("Leader défini : Handle={Handle}", windowHandle);
        }
    }

    public FocusResult FocusSlot(int slotIndex)
    {
        lock (_lock)
        {
            if (_slots.Count == 0)
                return FocusResult.Error("Aucune fenêtre détectée.");

            if (slotIndex < 0 || slotIndex >= _slots.Count)
                return FocusResult.Error($"Slot {slotIndex} hors bornes (0–{_slots.Count - 1}).");

            return FocusWindowInternal(slotIndex);
        }
    }

    public FocusResult FocusNext()
    {
        lock (_lock)
        {
            if (_slots.Count == 0)
                return FocusResult.Error("Aucune fenêtre détectée.");

            var nextIndex = _currentSlotIndex.HasValue
                ? (_currentSlotIndex.Value + 1) % _slots.Count
                : 0;

            return FocusWindowInternal(nextIndex);
        }
    }

    public FocusResult FocusPrevious()
    {
        lock (_lock)
        {
            if (_slots.Count == 0)
                return FocusResult.Error("Aucune fenêtre détectée.");

            var prevIndex = _currentSlotIndex.HasValue
                ? (_currentSlotIndex.Value - 1 + _slots.Count) % _slots.Count
                : _slots.Count - 1;

            return FocusWindowInternal(prevIndex);
        }
    }

    public FocusResult FocusLast()
    {
        lock (_lock)
        {
            if (!_lastSlotIndex.HasValue)
                return FocusResult.Error("Aucune fenêtre précédemment focusée.");

            if (_lastSlotIndex.Value >= _slots.Count)
                return FocusResult.Error("La dernière fenêtre n'existe plus dans les slots.");

            return FocusWindowInternal(_lastSlotIndex.Value);
        }
    }

    public FocusResult FocusLeader()
    {
        lock (_lock)
        {
            var leader = CurrentLeader;
            if (leader is null)
                return FocusResult.Error("Aucun leader défini et aucune fenêtre détectée.");

            var index = _slots.FindIndex(w => w.Handle == leader.Handle);
            if (index < 0)
                return FocusResult.Error("Le leader n'est plus dans les slots.");

            return FocusWindowInternal(index);
        }
    }

    /// <summary>
    /// Focus la fenêtre au slot donné, met à jour le tracking current/last.
    /// Doit être appelé sous lock.
    /// </summary>
    private FocusResult FocusWindowInternal(int slotIndex)
    {
        var window = _slots[slotIndex];

        if (!_windowHelper.IsWindowValid(window.Handle))
        {
            Logger.Warning("Fenêtre disparue : {Title} (Handle={Handle})", window.Title, window.Handle);
            return FocusResult.Error($"La fenêtre « {window.Title} » n'existe plus.");
        }

        var success = _windowHelper.FocusWindow(window.Handle);
        if (success)
        {
            _lastSlotIndex = _currentSlotIndex;
            _currentSlotIndex = slotIndex;
            Logger.Information("Focus slot {Index} : {Title}", slotIndex, window.Title);
        }
        else
        {
            Logger.Warning("Échec focus slot {Index} : {Title}", slotIndex, window.Title);
        }

        return success
            ? FocusResult.Ok()
            : FocusResult.Error($"Impossible de focus la fenêtre « {window.Title} ».");
    }
}
