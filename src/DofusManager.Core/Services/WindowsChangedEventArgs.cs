using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

public class WindowsChangedEventArgs : EventArgs
{
    public required IReadOnlyList<DofusWindow> Added { get; init; }
    public required IReadOnlyList<DofusWindow> Removed { get; init; }
    public required IReadOnlyList<DofusWindow> Current { get; init; }
}
