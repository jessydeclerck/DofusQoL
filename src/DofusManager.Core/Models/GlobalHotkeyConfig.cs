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
    /// Touche à maintenir pour activer le broadcast de clics (défaut : Alt).
    /// Seul VirtualKeyCode est utilisé (pas de modifiers — c'est une touche "hold").
    /// </summary>
    public HotkeyBindingConfig BroadcastKey { get; set; } = new()
    {
        DisplayName = "Alt",
        VirtualKeyCode = VK_MENU
    };

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
        BroadcastKey = new HotkeyBindingConfig
        {
            DisplayName = "Alt",
            VirtualKeyCode = VK_MENU
        }
    };
}
