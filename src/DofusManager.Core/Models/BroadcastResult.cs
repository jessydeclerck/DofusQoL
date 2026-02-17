namespace DofusManager.Core.Models;

/// <summary>
/// Résultat d'une exécution de broadcast.
/// </summary>
public record BroadcastResult(bool Success, int WindowsTargeted, int WindowsReached, string? ErrorMessage = null)
{
    public static BroadcastResult Ok(int targeted, int reached) => new(true, targeted, reached);
    public static BroadcastResult Error(string message) => new(false, 0, 0, message);
}
