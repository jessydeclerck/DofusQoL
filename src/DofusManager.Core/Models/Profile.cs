namespace DofusManager.Core.Models;

/// <summary>
/// Profil de team : contient les slots (mapping perso → fenêtre) et les presets broadcast.
/// </summary>
public class Profile
{
    public required string ProfileName { get; set; }
    public List<ProfileSlot> Slots { get; set; } = new();
    public GlobalHotkeyConfig GlobalHotkeys { get; set; } = GlobalHotkeyConfig.CreateDefault();

    [System.Obsolete("Conservé pour la compatibilité JSON. Utilisez les raccourcis configurables.")]
    public List<BroadcastPreset> BroadcastPresets { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
}
