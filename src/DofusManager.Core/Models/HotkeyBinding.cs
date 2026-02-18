namespace DofusManager.Core.Models;

/// <summary>
/// Actions possibles déclenchées par un raccourci clavier.
/// </summary>
public enum HotkeyAction
{
    /// <summary>Focus la fenêtre au slot donné.</summary>
    FocusSlot,

    /// <summary>Focus la fenêtre suivante dans l'ordre.</summary>
    NextWindow,

    /// <summary>Focus la fenêtre précédente dans l'ordre.</summary>
    PreviousWindow,

    /// <summary>Focus la dernière fenêtre utilisée.</summary>
    LastWindow,

    /// <summary>Focus la fenêtre du leader.</summary>
    PanicLeader,

    /// <summary>Déclenche un broadcast d'input.</summary>
    Broadcast,

    /// <summary>Colle le contenu du presse-papier dans le chat de chaque fenêtre.</summary>
    PasteToChat
}

/// <summary>
/// Modificateurs de touche pour les hotkeys (correspond aux flags Win32 RegisterHotKey).
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
    NoRepeat = 0x4000
}

/// <summary>
/// Modèle d'un raccourci clavier global associé à une action.
/// </summary>
public class HotkeyBinding
{
    public required int Id { get; init; }
    public required HotkeyModifiers Modifiers { get; init; }
    public required uint VirtualKeyCode { get; init; }
    public required string DisplayName { get; init; }
    public required HotkeyAction Action { get; init; }

    /// <summary>
    /// Index du slot pour l'action FocusSlot. Ignoré pour les autres actions.
    /// </summary>
    public int? SlotIndex { get; init; }

    /// <summary>
    /// True si le raccourci utilise un bouton souris (XButton1/XButton2) au lieu d'une touche clavier.
    /// </summary>
    public bool IsMouseButton => VirtualKeyCode is 0x04 or 0x05 or 0x06;
}
