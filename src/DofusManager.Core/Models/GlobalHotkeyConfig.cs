namespace DofusManager.Core.Models;

/// <summary>
/// Configuration d'un raccourci clavier (modifiers + touche + nom affiché).
/// </summary>
public class HotkeyBindingConfig
{
    public string DisplayName { get; set; } = string.Empty;
    public uint Modifiers { get; set; }
    public uint VirtualKeyCode { get; set; }
}

/// <summary>
/// Configuration des 4 raccourcis globaux de navigation + touche broadcast.
/// </summary>
public class GlobalHotkeyConfig
{
    public HotkeyBindingConfig NextWindow { get; set; } = new();
    public HotkeyBindingConfig PreviousWindow { get; set; } = new();
    public HotkeyBindingConfig LastWindow { get; set; } = new();
    public HotkeyBindingConfig FocusLeader { get; set; } = new();

    /// <summary>
    /// Raccourci pour coller le contenu du presse-papier dans le chat de chaque fenêtre.
    /// Par défaut : clic molette (VK_MBUTTON).
    /// </summary>
    public HotkeyBindingConfig PasteToChat { get; set; } = new();

    /// <summary>
    /// Touche à maintenir pour activer le broadcast de clics (défaut : Alt).
    /// Seul VirtualKeyCode est utilisé (pas de modifiers — c'est une touche "hold").
    /// </summary>
    public HotkeyBindingConfig BroadcastKey { get; set; } = new()
    {
        DisplayName = "Alt",
        VirtualKeyCode = VK_MENU
    };

    /// <summary>
    /// Si true, le focus revient au leader après chaque broadcast (au lieu de la fenêtre source).
    /// </summary>
    public bool ReturnToLeaderAfterBroadcast { get; set; }

    /// <summary>
    /// Délai moyen (ms) entre chaque fenêtre lors du broadcast.
    /// </summary>
    public int BroadcastDelayMs { get; set; } = 65;

    /// <summary>
    /// Variation aléatoire (±ms) appliquée au délai entre chaque fenêtre lors du broadcast.
    /// </summary>
    public int BroadcastDelayRandomMs { get; set; } = 25;

    /// <summary>
    /// Si true, un 2e ENTER est envoyé après le collage pour confirmer l'action.
    /// </summary>
    public bool PasteToChatDoubleEnter { get; set; }

    /// <summary>
    /// Délai (ms) entre le 1er et le 2e ENTER lors du collage avec double entrée.
    /// </summary>
    public int PasteToChatDoubleEnterDelayMs { get; set; } = 500;

    private const uint VK_TAB = 0x09;
    private const uint VK_MENU = 0x12;
    private const uint VK_OEM_3 = 0xC0; // touche ` (backtick)
    private const uint VK_F1 = 0x70;

    public static GlobalHotkeyConfig CreateDefault() => new()
    {
        NextWindow = new HotkeyBindingConfig
        {
            DisplayName = "Ctrl+Tab",
            Modifiers = (uint)HotkeyModifiers.Control,
            VirtualKeyCode = VK_TAB
        },
        PreviousWindow = new HotkeyBindingConfig
        {
            DisplayName = "Ctrl+Shift+Tab",
            Modifiers = (uint)(HotkeyModifiers.Control | HotkeyModifiers.Shift),
            VirtualKeyCode = VK_TAB
        },
        LastWindow = new HotkeyBindingConfig
        {
            DisplayName = "Ctrl+`",
            Modifiers = (uint)HotkeyModifiers.Control,
            VirtualKeyCode = VK_OEM_3
        },
        FocusLeader = new HotkeyBindingConfig
        {
            DisplayName = "Ctrl+F1",
            Modifiers = (uint)HotkeyModifiers.Control,
            VirtualKeyCode = VK_F1
        },
        PasteToChat = new HotkeyBindingConfig
        {
            DisplayName = "Clic molette",
            VirtualKeyCode = 0x04 // VK_MBUTTON
        },
        BroadcastKey = new HotkeyBindingConfig
        {
            DisplayName = "Alt",
            VirtualKeyCode = VK_MENU
        }
    };
}
