namespace DofusManager.Core.Models;

/// <summary>
/// Résultat d'une tentative de focus sur une fenêtre.
/// </summary>
public record FocusResult(bool Success, string? ErrorMessage = null)
{
    public static FocusResult Ok() => new(true);
    public static FocusResult Error(string message) => new(false, message);
}
