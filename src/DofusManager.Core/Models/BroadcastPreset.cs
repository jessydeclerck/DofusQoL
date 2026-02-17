namespace DofusManager.Core.Models;

/// <summary>
/// Preset de broadcast d'input vers plusieurs fenêtres Dofus.
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
    public List<int>? CustomTargetSlotIndices { get; set; }
    public int DelayMin { get; set; } = 80;
    public int DelayMax { get; set; } = 300;
    public string OrderMode { get; set; } = "profile"; // "profile", "random"

    public static readonly string[] ValidInputTypes = ["key", "clickAtPosition", "clickAtCursor"];
    public static readonly string[] ValidTargets = ["all", "allExceptLeader", "custom"];
    public static readonly string[] ValidClickButtons = ["left", "right"];
    public static readonly string[] ValidOrderModes = ["profile", "random"];

    /// <summary>
    /// Valide la cohérence du preset. Retourne null si valide, sinon le message d'erreur.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "Le nom du preset est requis.";

        if (!ValidInputTypes.Contains(InputType))
            return $"InputType invalide : '{InputType}'. Valeurs acceptées : {string.Join(", ", ValidInputTypes)}";

        if (InputType == "key" && string.IsNullOrWhiteSpace(Key))
            return "La touche (Key) est requise pour le type 'key'.";

        if (InputType == "clickAtPosition" && (ClickX is null || ClickY is null))
            return "Les coordonnées (ClickX, ClickY) sont requises pour le type 'clickAtPosition'.";

        if (!ValidClickButtons.Contains(ClickButton))
            return $"ClickButton invalide : '{ClickButton}'. Valeurs acceptées : {string.Join(", ", ValidClickButtons)}";

        if (!ValidTargets.Contains(Targets))
            return $"Targets invalide : '{Targets}'. Valeurs acceptées : {string.Join(", ", ValidTargets)}";

        if (Targets == "custom" && (CustomTargetSlotIndices is null || CustomTargetSlotIndices.Count == 0))
            return "CustomTargetSlotIndices requis pour le mode 'custom'.";

        if (DelayMin < 0)
            return "DelayMin doit être >= 0.";

        if (DelayMax < DelayMin)
            return "DelayMax doit être >= DelayMin.";

        if (!ValidOrderModes.Contains(OrderMode))
            return $"OrderMode invalide : '{OrderMode}'. Valeurs acceptées : {string.Join(", ", ValidOrderModes)}";

        return null;
    }
}
