using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.Core.Services;

public class ZaapTravelService : IZaapTravelService
{
    private static readonly ILogger Logger = Log.ForContext<ZaapTravelService>();
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_H = 0x48;
    private const int FocusDelayMs = 100;
    private const int FocusMaxRetries = 3;

    private readonly IWin32WindowHelper _windowHelper;

    public int ZaapClickX { get; set; }
    public int ZaapClickY { get; set; }
    public int HavreSacDelayMs { get; set; } = 2000;
    public int ZaapInterfaceDelayMs { get; set; } = 1500;
    public ushort HavreSacKeyCode { get; set; } = VK_H;

    public ZaapTravelService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public async Task<GroupInviteResult> TravelToZaapAsync(
        IReadOnlyList<DofusWindow> windows, DofusWindow leader, string territoryName)
    {
        if (windows.Count == 0)
            return new GroupInviteResult { Success = false, ErrorMessage = "Aucune fenêtre détectée" };

        if (ZaapClickX == 0 && ZaapClickY == 0)
            return new GroupInviteResult { Success = false, ErrorMessage = "Coordonnées du Zaap non configurées (X=0, Y=0)" };

        var traveled = 0;

        foreach (var window in windows)
        {
            // 1. Focus la fenêtre avec retry
            var focused = await FocusWithRetryAsync(window.Handle);
            if (!focused)
            {
                Logger.Warning("[ZAAP] Focus échoué pour {Handle}, skip", window.Handle);
                continue;
            }

            // 2. Ouvrir le havre-sac
            _windowHelper.SendKeyPress(HavreSacKeyCode);
            Logger.Information("[ZAAP] Havre-sac envoyé à {Handle}", window.Handle);

            // 3. Attendre le chargement du havre-sac
            await Task.Delay(HavreSacDelayMs);

            // 4. Cliquer sur le Zaap NPC
            var screenCoords = _windowHelper.ClientToScreen(window.Handle, ZaapClickX, ZaapClickY);
            if (screenCoords is null)
            {
                Logger.Warning("[ZAAP] ClientToScreen échoué pour {Handle}, skip", window.Handle);
                continue;
            }

            _windowHelper.SetCursorPos(screenCoords.Value.ScreenX, screenCoords.Value.ScreenY);
            _windowHelper.SendMouseClick();
            Logger.Information("[ZAAP] Clic Zaap à ({X},{Y}) pour {Handle}", screenCoords.Value.ScreenX, screenCoords.Value.ScreenY, window.Handle);

            // 5. Attendre l'interface Zaap
            await Task.Delay(ZaapInterfaceDelayMs);

            // 6. Taper le nom du territoire
            _windowHelper.SendText(territoryName);
            await Task.Delay(50);

            // 7. Valider avec ENTER
            await Task.Delay(50);
            _windowHelper.SendKeyPress(VK_RETURN);

            traveled++;
            Logger.Information("[ZAAP] Voyage vers '{Territory}' envoyé à {Handle}", territoryName, window.Handle);

            // Délai entre les fenêtres
            if (traveled < windows.Count)
                await Task.Delay(200);
        }

        // Restaurer le focus sur le leader
        await FocusWithRetryAsync(leader.Handle);

        Logger.Information("[ZAAP] Voyage terminé : {Count}/{Total} fenêtre(s)", traveled, windows.Count);
        return new GroupInviteResult { Success = true, Invited = traveled };
    }

    private async Task<bool> FocusWithRetryAsync(nint handle)
    {
        for (var attempt = 1; attempt <= FocusMaxRetries; attempt++)
        {
            _windowHelper.FocusWindow(handle);
            await Task.Delay(FocusDelayMs);

            if (_windowHelper.GetForegroundWindow() == handle)
                return true;
        }

        return false;
    }
}
