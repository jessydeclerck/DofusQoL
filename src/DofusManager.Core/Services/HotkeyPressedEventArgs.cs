using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Données de l'événement déclenché quand un raccourci global est pressé.
/// </summary>
public class HotkeyPressedEventArgs : EventArgs
{
    public required int HotkeyId { get; init; }
    public required HotkeyBinding Binding { get; init; }
}
