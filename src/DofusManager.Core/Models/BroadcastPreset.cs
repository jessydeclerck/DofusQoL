namespace DofusManager.Core.Models;

/// <summary>
/// Preset de broadcast d'input. Stub pour l'itération 3, sera implémenté en itération 4 (F4).
/// </summary>
public class BroadcastPreset
{
    public required string Name { get; set; }
    public string? Hotkey { get; set; }
    public required string InputType { get; set; } // "key", "clickAtPosition", "clickAtCursor"
    public string? Key { get; set; }
    public int? ClickX { get; set; }
    public int? ClickY { get; set; }
    public string ClickButton { get; set; } = "left";
    public string Targets { get; set; } = "all"; // "all", "allExceptLeader", "custom"
    public int DelayMin { get; set; } = 80;
    public int DelayMax { get; set; } = 300;
    public string OrderMode { get; set; } = "profile"; // "profile", "random"
}
