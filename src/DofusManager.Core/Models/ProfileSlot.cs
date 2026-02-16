namespace DofusManager.Core.Models;

/// <summary>
/// Un slot dans un profil : associe un personnage à une position, un pattern de titre et un hotkey.
/// </summary>
public class ProfileSlot
{
    public required int Index { get; set; }
    public required string CharacterName { get; set; }
    public string WindowTitlePattern { get; set; } = "*";
    public bool IsLeader { get; set; }
    public string? FocusHotkey { get; set; }
}
