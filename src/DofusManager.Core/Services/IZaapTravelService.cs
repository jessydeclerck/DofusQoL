using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

public interface IZaapTravelService
{
    /// <summary>
    /// Coordonnée X (client) du clic sur le Zaap NPC dans le havre-sac.
    /// </summary>
    int ZaapClickX { get; set; }

    /// <summary>
    /// Coordonnée Y (client) du clic sur le Zaap NPC dans le havre-sac.
    /// </summary>
    int ZaapClickY { get; set; }

    /// <summary>
    /// Délai (ms) après l'ouverture du havre-sac avant de cliquer sur le Zaap.
    /// </summary>
    int HavreSacDelayMs { get; set; }

    /// <summary>
    /// Délai (ms) après le clic Zaap avant de taper le nom du territoire.
    /// </summary>
    int ZaapInterfaceDelayMs { get; set; }

    /// <summary>
    /// Touche pour ouvrir le havre-sac (défaut : VK_H = 0x48).
    /// </summary>
    ushort HavreSacKeyCode { get; set; }

    /// <summary>
    /// Voyage automatisé vers un Zaap pour toutes les fenêtres.
    /// Séquence par fenêtre : havre-sac → clic Zaap NPC → taper le nom → ENTER.
    /// </summary>
    Task<GroupInviteResult> TravelToZaapAsync(IReadOnlyList<DofusWindow> windows, DofusWindow leader, string territoryName);
}
