using System.Runtime.InteropServices;
using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusManager.Core.Services;

/// <summary>
/// Implémente le mode "hold to broadcast" : tant que la touche est maintenue,
/// chaque clic souris est automatiquement broadcasté à toutes les fenêtres Dofus.
/// Utilise SetForegroundWindow + SetCursorPos + SendInput car Unity ignore PostMessage.
/// </summary>
public class PushToBroadcastService : IPushToBroadcastService
{
    private static readonly ILogger Logger = Log.ForContext<PushToBroadcastService>();

    private const uint WM_LBUTTONDOWN = 0x0201;

    private readonly IWin32WindowHelper _windowHelper;
    private readonly object _lock = new();

    // Délai initial pour laisser le clic original être traité par la source
    private const int InitialDelayMs = 50;
    // Délai après FocusWindow pour laisser Windows changer le foreground
    private const int FocusDelayMs = 100;
    // Délai aléatoire après chaque clic pour simuler un comportement humain
    private const int ClickDelayMinMs = 40;
    private const int ClickDelayMaxMs = 90;

    private IReadOnlyList<DofusWindow> _dofusWindows = [];
    private UnhookWindowsHookExSafeHandle? _hookHandle;
    private int _processing; // 0 = idle, 1 = processing (guard anti-concurrence)

    // Stocker le delegate en champ pour éviter le garbage collection
    private HOOKPROC? _hookProc;

    private bool _isArmed;
    private bool _disposed;

    public bool IsArmed
    {
        get { lock (_lock) { return _isArmed; } }
    }

    public event EventHandler<int>? BroadcastPerformed;

    public PushToBroadcastService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public void Arm(IReadOnlyList<DofusWindow> dofusWindows)
    {
        lock (_lock)
        {
            if (_isArmed)
            {
                Logger.Warning("PushToBroadcast déjà armé, ignoré");
                return;
            }

            if (dofusWindows.Count == 0)
            {
                Logger.Warning("Aucune fenêtre Dofus détectée, impossible d'armer");
                return;
            }

            _dofusWindows = dofusWindows;
            InstallHook();
            _isArmed = true;

            Logger.Information("PushToBroadcast armé avec {Count} fenêtres", dofusWindows.Count);
        }
    }

    public void Disarm()
    {
        lock (_lock)
        {
            if (!_isArmed) return;

            RemoveHook();
            _isArmed = false;
            _dofusWindows = [];

            Logger.Information("PushToBroadcast désarmé");
        }
    }

    /// <summary>
    /// Traite un clic souris capturé par le hook.
    /// Pour chaque autre fenêtre Dofus : SetForegroundWindow → SetCursorPos → SendInput.
    /// Puis restaure la fenêtre source et la position du curseur.
    /// </summary>
    /// <param name="sourceHandle">Handle de la fenêtre source, capturé dans le hook callback.</param>
    internal int ProcessMouseClick(int screenX, int screenY, nint sourceHandle)
    {
        // Éviter les broadcasts concurrents (le précédent n'est pas encore terminé)
        if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0)
            return 0;

        try
        {
            return ProcessMouseClickCore(screenX, screenY, sourceHandle);
        }
        finally
        {
            Interlocked.Exchange(ref _processing, 0);
        }
    }

    private int ProcessMouseClickCore(int screenX, int screenY, nint sourceHandle)
    {
        IReadOnlyList<DofusWindow> windows;
        lock (_lock)
        {
            if (!_isArmed) return 0;
            windows = _dofusWindows;
        }

        // Identifier la fenêtre source (handle capturé dans le hook, pas de race condition)
        var sourceWindow = windows.FirstOrDefault(w => w.Handle == sourceHandle);

        if (sourceWindow is null)
        {
            Logger.Debug("Clic ignoré : la fenêtre active (handle={SourceHandle}) n'est pas une fenêtre Dofus", sourceHandle);
            return 0;
        }

        Logger.Information("[BROADCAST-START] Source={Title} (handle={Handle}) screen=({ScreenX},{ScreenY})",
            sourceWindow.Title, sourceWindow.Handle, screenX, screenY);

        // Attendre que le clic original soit traité par la fenêtre source
        Thread.Sleep(InitialDelayMs);

        // Convertir les coordonnées écran en coordonnées client de la source
        var clientCoords = _windowHelper.ScreenToClient(sourceWindow.Handle, screenX, screenY);
        if (clientCoords is null)
        {
            Logger.Warning("[BROADCAST-ABORT] ScreenToClient échoué pour handle={Handle}", sourceWindow.Handle);
            return 0;
        }

        var (clientX, clientY) = clientCoords.Value;
        Logger.Information("[BROADCAST] Coords écran ({ScreenX},{ScreenY}) → client source ({ClientX},{ClientY})",
            screenX, screenY, clientX, clientY);

        // Sauvegarder la position du curseur
        var originalCursorPos = _windowHelper.GetCursorPos();
        Logger.Information("[BROADCAST] Cursor sauvegardé à ({CursorX},{CursorY})",
            originalCursorPos?.X ?? -1, originalCursorPos?.Y ?? -1);

        var reached = 0;
        var windowIndex = 0;

        // Envoyer le clic à toutes les AUTRES fenêtres Dofus via hardware input
        foreach (var window in windows)
        {
            if (window.Handle == sourceWindow.Handle) continue;
            if (!_windowHelper.IsWindowValid(window.Handle))
            {
                Logger.Warning("[BROADCAST] Fenêtre invalide : {Title} (handle={Handle})", window.Title, window.Handle);
                continue;
            }

            windowIndex++;
            Logger.Information("[BROADCAST-TARGET #{Index}] {Title} (handle={Handle})",
                windowIndex, window.Title, window.Handle);

            // Amener la fenêtre cible au premier plan
            var focused = _windowHelper.FocusWindow(window.Handle);
            Logger.Information("[BROADCAST-TARGET #{Index}] FocusWindow → {Result}", windowIndex, focused);
            Thread.Sleep(FocusDelayMs);

            // Vérifier quel est le vrai foreground après le focus
            var actualForeground = _windowHelper.GetForegroundWindow();
            Logger.Information("[BROADCAST-TARGET #{Index}] Foreground après focus : {Foreground} (attendu={Expected}, match={Match})",
                windowIndex, actualForeground, window.Handle, actualForeground == window.Handle);

            // SAFETY NET : si le focus n'est pas sur la cible, ne PAS envoyer le clic
            // sinon il atterrirait sur la fenêtre source (clic parasite)
            if (actualForeground != window.Handle)
            {
                Logger.Warning("[BROADCAST-TARGET #{Index}] Focus raté → skip clic pour éviter clic parasite sur la source", windowIndex);
                continue;
            }

            // Calculer les coordonnées écran APRÈS le focus
            var targetScreenCoords = _windowHelper.ClientToScreen(window.Handle, clientX, clientY);
            if (targetScreenCoords is null)
            {
                Logger.Warning("[BROADCAST-TARGET #{Index}] ClientToScreen échoué", windowIndex);
                continue;
            }

            Logger.Information("[BROADCAST-TARGET #{Index}] ClientToScreen client ({ClientX},{ClientY}) → écran ({TargetX},{TargetY})",
                windowIndex, clientX, clientY, targetScreenCoords.Value.ScreenX, targetScreenCoords.Value.ScreenY);

            // Déplacer le curseur et injecter un clic hardware
            _windowHelper.SetCursorPos(targetScreenCoords.Value.ScreenX, targetScreenCoords.Value.ScreenY);
            var clicked = _windowHelper.SendMouseClick();
            Logger.Information("[BROADCAST-TARGET #{Index}] SendMouseClick → {Result}", windowIndex, clicked);

            // Délai aléatoire entre les fenêtres pour simuler un comportement humain
            Thread.Sleep(Random.Shared.Next(ClickDelayMinMs, ClickDelayMaxMs + 1));

            if (clicked) reached++;
        }

        // Restaurer la position du curseur AVANT le focus source
        if (originalCursorPos is not null)
        {
            _windowHelper.SetCursorPos(originalCursorPos.Value.X, originalCursorPos.Value.Y);
            Logger.Information("[BROADCAST-RESTORE] Cursor restauré à ({X},{Y})", originalCursorPos.Value.X, originalCursorPos.Value.Y);
        }

        // Restaurer la fenêtre source au premier plan
        var restoreFocused = _windowHelper.FocusWindow(sourceWindow.Handle);
        Logger.Information("[BROADCAST-RESTORE] FocusWindow source → {Result}", restoreFocused);

        var finalForeground = _windowHelper.GetForegroundWindow();
        Logger.Information("[BROADCAST-END] Foreground final : {Foreground} (source={Source}, match={Match}) — {Reached}/{Total} fenêtres",
            finalForeground, sourceWindow.Handle, finalForeground == sourceWindow.Handle, reached, windowIndex);

        if (reached > 0)
        {
            BroadcastPerformed?.Invoke(this, reached);
        }

        return reached;
    }

    private void InstallHook()
    {
        _hookProc = HookCallback;
        _hookHandle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_MOUSE_LL,
            _hookProc,
            PInvoke.GetModuleHandle((string?)null),
            0);

        if (_hookHandle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            Logger.Error("Échec SetWindowsHookEx WH_MOUSE_LL, erreur={Error}", error);
            _hookHandle = null;
            _hookProc = null;
        }
        else
        {
            Logger.Debug("Hook WH_MOUSE_LL installé");
        }
    }

    private void RemoveHook()
    {
        if (_hookHandle is not null && !_hookHandle.IsInvalid)
        {
            _hookHandle.Dispose();
            Logger.Debug("Hook WH_MOUSE_LL supprimé");
        }

        _hookHandle = null;
        _hookProc = null;
    }

    private unsafe LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && (uint)wParam.Value == WM_LBUTTONDOWN)
        {
            var hookStruct = (MSLLHOOKSTRUCT*)lParam.Value;
            var flags = hookStruct->flags;

            const uint LLMHF_INJECTED = 0x01;
            var isInjected = (flags & LLMHF_INJECTED) != 0;

            Logger.Information("[HOOK] WM_LBUTTONDOWN at ({X},{Y}) flags=0x{Flags:X} injected={Injected} processing={Processing}",
                hookStruct->pt.X, hookStruct->pt.Y, (uint)flags, isInjected, _processing);

            if (!isInjected)
            {
                var x = hookStruct->pt.X;
                var y = hookStruct->pt.Y;
                var foregroundHandle = _windowHelper.GetForegroundWindow();
                Logger.Information("[HOOK] Dispatching broadcast: screen=({X},{Y}) foreground={Foreground}",
                    x, y, foregroundHandle);
                Task.Run(() => ProcessMouseClick(x, y, foregroundHandle));
            }
        }

        return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Disarm();
        GC.SuppressFinalize(this);
    }
}
