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
/// Configuration des 4 raccourcis globaux de navigation.
/// </summary>
public class GlobalHotkeyConfig
{
    public HotkeyBindingConfig NextWindow { get; set; } = new();
    public HotkeyBindingConfig PreviousWindow { get; set; } = new();
    public HotkeyBindingConfig LastWindow { get; set; } = new();
    public HotkeyBindingConfig FocusLeader { get; set; } = new();

    private const uint VK_TAB = 0x09;
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
        }
    };
}
