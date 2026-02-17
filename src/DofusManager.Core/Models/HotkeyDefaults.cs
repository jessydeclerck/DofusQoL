namespace DofusManager.Core.Models;

/// <summary>
/// Raccourcis par défaut pour les slots et les actions globales.
/// </summary>
public static class HotkeyDefaults
{
    private const uint VK_F1 = 0x70;

    /// <summary>
    /// Retourne le raccourci par défaut pour un slot donné (F1-F8).
    /// Retourne null pour les slots >= 8.
    /// </summary>
    public static (HotkeyModifiers Modifiers, uint VirtualKeyCode, string DisplayName)? GetDefaultSlotHotkey(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 8)
            return null;

        return (HotkeyModifiers.None, VK_F1 + (uint)slotIndex, $"F{slotIndex + 1}");
    }
}
