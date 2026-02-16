namespace DofusManager.Core.Models;

/// <summary>
/// Représente une fenêtre Dofus détectée sur le système.
/// </summary>
public class DofusWindow : IEquatable<DofusWindow>
{
    public required nint Handle { get; init; }
    public required int ProcessId { get; init; }
    public required string Title { get; set; }
    public required bool IsVisible { get; set; }
    public required bool IsMinimized { get; set; }
    public string ScreenName { get; set; } = string.Empty;
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    public bool Equals(DofusWindow? other)
    {
        if (other is null) return false;
        return Handle == other.Handle;
    }

    public override bool Equals(object? obj) => Equals(obj as DofusWindow);

    public override int GetHashCode() => Handle.GetHashCode();

    public override string ToString() => $"[{ProcessId}] {Title}";
}
