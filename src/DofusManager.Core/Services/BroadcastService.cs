using System.Diagnostics;
using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.Core.Services;

/// <summary>
/// Envoie des inputs (touche ou clic) à plusieurs fenêtres Dofus via PostMessage.
/// </summary>
public class BroadcastService : IBroadcastService
{
    private static readonly ILogger Logger = Log.ForContext<BroadcastService>();

    // Constantes Win32 pour les messages
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const nint MK_LBUTTON = 0x0001;
    private const nint MK_RBUTTON = 0x0002;

    // Délai entre keydown et keyup pour simuler un appui réaliste
    private const int KeyPressDurationMs = 50;

    private readonly IWin32WindowHelper _windowHelper;
    private readonly Stopwatch _cooldownStopwatch = new();
    private readonly object _lock = new();

    public int CooldownMs { get; set; } = 500;
    public bool IsPaused { get; set; }

    public bool IsOnCooldown
    {
        get
        {
            lock (_lock)
            {
                return _cooldownStopwatch.IsRunning && _cooldownStopwatch.ElapsedMilliseconds < CooldownMs;
            }
        }
    }

    public BroadcastService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public async Task<BroadcastResult> ExecuteBroadcastAsync(
        BroadcastPreset preset,
        IReadOnlyList<DofusWindow> allWindows,
        nint? leaderHandle,
        CancellationToken cancellationToken = default)
    {
        // Vérifications préalables
        if (IsPaused)
        {
            Logger.Information("Broadcast ignoré : pause globale active");
            return BroadcastResult.Error("Broadcast en pause.");
        }

        if (IsOnCooldown)
        {
            Logger.Information("Broadcast ignoré : cooldown actif");
            return BroadcastResult.Error("Cooldown anti-rafale actif.");
        }

        var validationError = preset.Validate();
        if (validationError is not null)
        {
            Logger.Warning("Broadcast refusé : {Error}", validationError);
            return BroadcastResult.Error(validationError);
        }

        // Résoudre les cibles
        var targets = ResolveTargets(preset, allWindows, leaderHandle);
        if (targets.Count == 0)
        {
            Logger.Warning("Broadcast '{Name}' : aucune cible trouvée", preset.Name);
            return BroadcastResult.Error("Aucune fenêtre cible.");
        }

        // Appliquer l'ordre
        var orderedTargets = ApplyOrder(targets, preset.OrderMode);

        // Démarrer le cooldown
        lock (_lock)
        {
            _cooldownStopwatch.Restart();
        }

        Logger.Information("Broadcast '{Name}' vers {Count} fenêtres (type={InputType})",
            preset.Name, orderedTargets.Count, preset.InputType);

        // Exécuter l'envoi
        var reached = 0;
        for (var i = 0; i < orderedTargets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = orderedTargets[i];

            // Vérifier que la fenêtre existe toujours
            if (!_windowHelper.IsWindowValid(window.Handle))
            {
                Logger.Warning("Fenêtre disparue pendant le broadcast : {Title} (Handle={Handle})",
                    window.Title, window.Handle);
                continue;
            }

            var sent = preset.InputType switch
            {
                "key" => SendKey(window.Handle, preset.Key!),
                "clickAtPosition" => SendClick(window.Handle, preset.ClickX!.Value, preset.ClickY!.Value, preset.ClickButton),
                "clickAtCursor" => SendClick(window.Handle, preset.ClickX!.Value, preset.ClickY!.Value, preset.ClickButton),
                _ => false
            };

            if (sent) reached++;

            // Délai aléatoire entre les fenêtres (pas après la dernière)
            if (i < orderedTargets.Count - 1)
            {
                var delay = Random.Shared.Next(preset.DelayMin, preset.DelayMax + 1);
                await Task.Delay(delay, cancellationToken);
            }
        }

        Logger.Information("Broadcast '{Name}' terminé : {Reached}/{Targeted} fenêtres",
            preset.Name, reached, orderedTargets.Count);

        return BroadcastResult.Ok(orderedTargets.Count, reached);
    }

    internal List<DofusWindow> ResolveTargets(
        BroadcastPreset preset,
        IReadOnlyList<DofusWindow> allWindows,
        nint? leaderHandle)
    {
        return preset.Targets switch
        {
            "all" => allWindows.ToList(),
            "allExceptLeader" when leaderHandle.HasValue =>
                allWindows.Where(w => w.Handle != leaderHandle.Value).ToList(),
            "allExceptLeader" => allWindows.ToList(), // pas de leader → toutes
            "custom" when preset.CustomTargetSlotIndices is not null =>
                preset.CustomTargetSlotIndices
                    .Where(i => i >= 0 && i < allWindows.Count)
                    .Select(i => allWindows[i])
                    .ToList(),
            _ => allWindows.ToList()
        };
    }

    internal static List<DofusWindow> ApplyOrder(List<DofusWindow> targets, string orderMode)
    {
        if (orderMode == "random")
        {
            var shuffled = new List<DofusWindow>(targets);
            Random.Shared.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(shuffled));
            return shuffled;
        }

        return targets; // "profile" = ordre original
    }

    private bool SendKey(nint handle, string key)
    {
        var vk = KeyNameToVirtualKey(key);
        if (vk == 0)
        {
            Logger.Warning("Touche inconnue : '{Key}'", key);
            return false;
        }

        var down = _windowHelper.PostMessage(handle, WM_KEYDOWN, (nint)vk, 0);
        var up = _windowHelper.PostMessage(handle, WM_KEYUP, (nint)vk, 0);

        return down && up;
    }

    private bool SendClick(nint handle, int x, int y, string button)
    {
        var lParam = MakeLParam(x, y);

        uint msgDown, msgUp;
        nint wParam;

        if (button == "right")
        {
            msgDown = WM_RBUTTONDOWN;
            msgUp = WM_RBUTTONUP;
            wParam = MK_RBUTTON;
        }
        else
        {
            msgDown = WM_LBUTTONDOWN;
            msgUp = WM_LBUTTONUP;
            wParam = MK_LBUTTON;
        }

        var down = _windowHelper.PostMessage(handle, msgDown, wParam, lParam);
        var up = _windowHelper.PostMessage(handle, msgUp, wParam, lParam);

        return down && up;
    }

    internal static nint MakeLParam(int x, int y)
    {
        return (nint)((y << 16) | (x & 0xFFFF));
    }

    internal static uint KeyNameToVirtualKey(string keyName)
    {
        return keyName.ToUpperInvariant() switch
        {
            "ENTER" or "RETURN" => 0x0D,
            "ESCAPE" or "ESC" => 0x1B,
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "BACKSPACE" => 0x08,
            "DELETE" => 0x2E,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            // Lettres A-Z
            var s when s.Length == 1 && s[0] >= 'A' && s[0] <= 'Z' => (uint)s[0],
            // Chiffres 0-9
            var s when s.Length == 1 && s[0] >= '0' && s[0] <= '9' => (uint)s[0],
            _ => 0
        };
    }
}
