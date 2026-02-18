using System.Runtime.InteropServices;
using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusManager.Core.Services;

/// <summary>
/// Gère l'enregistrement des raccourcis clavier globaux et dispatch les événements.
/// Supporte aussi les boutons souris XButton1/XButton2 via un hook WH_MOUSE_LL.
/// </summary>
public class HotkeyService : IHotkeyService
{
    private static readonly ILogger Logger = Log.ForContext<HotkeyService>();

    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_XBUTTONDOWN = 0x020B;
    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12;

    private readonly IWin32WindowHelper _windowHelper;
    private readonly Dictionary<int, HotkeyBinding> _bindings = new();
    private readonly Dictionary<int, HotkeyBinding> _mouseBindings = new();
    private readonly object _lock = new();

    private nint _windowHandle;
    private bool _disposed;

    // Hook WH_MOUSE_LL pour les boutons souris
    private UnhookWindowsHookExSafeHandle? _mouseHookHandle;
    private HOOKPROC? _mouseHookProc;

    public HotkeyService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public bool IsInitialized => _windowHandle != 0;

    public IReadOnlyList<HotkeyBinding> RegisteredHotkeys
    {
        get
        {
            lock (_lock)
            {
                return _bindings.Values.Concat(_mouseBindings.Values).ToList();
            }
        }
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Initialize(nint windowHandle)
    {
        if (windowHandle == 0)
            throw new ArgumentException("Le handle de fenêtre ne peut pas être nul.", nameof(windowHandle));

        _windowHandle = windowHandle;
        InstallMouseHook();
        Logger.Information("HotkeyService initialisé avec HWND={Handle}", windowHandle);
    }

    public bool Register(HotkeyBinding binding)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Le service doit être initialisé avant d'enregistrer des hotkeys.");

        lock (_lock)
        {
            // Nettoyer l'ancien binding si le même ID existe
            if (_bindings.ContainsKey(binding.Id))
            {
                Logger.Warning("Hotkey id={Id} déjà enregistré, désenregistrement préalable", binding.Id);
                UnregisterInternal(binding.Id);
            }
            else if (_mouseBindings.ContainsKey(binding.Id))
            {
                _mouseBindings.Remove(binding.Id);
            }

            // Les boutons souris sont gérés via le hook, pas RegisterHotKey
            if (binding.IsMouseButton)
            {
                _mouseBindings[binding.Id] = binding;
                Logger.Information("Mouse binding enregistré : {DisplayName} (id={Id})", binding.DisplayName, binding.Id);
                return true;
            }

            // MOD_NOREPEAT empêche l'auto-repeat clavier de déclencher plusieurs WM_HOTKEY
            var modifiers = (uint)(binding.Modifiers | HotkeyModifiers.NoRepeat);
            var result = _windowHelper.RegisterHotKey(
                _windowHandle,
                binding.Id,
                modifiers,
                binding.VirtualKeyCode);

            if (result)
            {
                _bindings[binding.Id] = binding;
                Logger.Information("Hotkey enregistré : {DisplayName} (id={Id})", binding.DisplayName, binding.Id);
            }
            else
            {
                Logger.Warning("Échec enregistrement hotkey : {DisplayName} (id={Id})", binding.DisplayName, binding.Id);
            }

            return result;
        }
    }

    public void Unregister(int id)
    {
        lock (_lock)
        {
            if (!_mouseBindings.Remove(id))
                UnregisterInternal(id);
        }
    }

    public void UnregisterAll()
    {
        lock (_lock)
        {
            var ids = _bindings.Keys.ToList();
            foreach (var id in ids)
            {
                UnregisterInternal(id);
            }

            _mouseBindings.Clear();
        }
    }

    public bool ProcessMessage(nint wParam, nint lParam)
    {
        var hotkeyId = (int)wParam;

        HotkeyBinding? binding;
        lock (_lock)
        {
            _bindings.TryGetValue(hotkeyId, out binding);
        }

        if (binding is null)
        {
            Logger.Debug("WM_HOTKEY reçu pour id={Id} non enregistré", hotkeyId);
            return false;
        }

        Logger.Debug("Hotkey pressé : {DisplayName} (id={Id})", binding.DisplayName, binding.Id);
        HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
        {
            HotkeyId = hotkeyId,
            Binding = binding
        });

        return true;
    }

    private void UnregisterInternal(int id)
    {
        if (_bindings.Remove(id, out var binding))
        {
            _windowHelper.UnregisterHotKey(_windowHandle, id);
            Logger.Information("Hotkey désenregistré : {DisplayName} (id={Id})", binding.DisplayName, id);
        }
    }

    // ===== Hook WH_MOUSE_LL pour XButton1/XButton2 =====

    private void InstallMouseHook()
    {
        _mouseHookProc = MouseHookCallback;
        _mouseHookHandle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_MOUSE_LL,
            _mouseHookProc,
            PInvoke.GetModuleHandle((string?)null),
            0);

        if (_mouseHookHandle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            Logger.Error("Échec SetWindowsHookEx WH_MOUSE_LL pour HotkeyService, erreur={Error}", error);
            _mouseHookHandle = null;
            _mouseHookProc = null;
        }
        else
        {
            Logger.Debug("Hook WH_MOUSE_LL installé pour HotkeyService (XButton)");
        }
    }

    private void RemoveMouseHook()
    {
        if (_mouseHookHandle is not null && !_mouseHookHandle.IsInvalid)
        {
            _mouseHookHandle.Dispose();
            Logger.Debug("Hook WH_MOUSE_LL supprimé pour HotkeyService");
        }

        _mouseHookHandle = null;
        _mouseHookProc = null;
    }

    private unsafe LRESULT MouseHookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            var msg = (uint)wParam.Value;
            uint vk = 0;

            if (msg == WM_MBUTTONDOWN)
            {
                vk = 0x04; // VK_MBUTTON
            }
            else if (msg == WM_XBUTTONDOWN)
            {
                var hookStruct = (MSLLHOOKSTRUCT*)lParam.Value;

                // HIWORD de mouseData : 1 = XBUTTON1, 2 = XBUTTON2
                var xButton = (int)((hookStruct->mouseData >> 16) & 0xFFFF);
                vk = xButton switch
                {
                    1 => 0x05, // VK_XBUTTON1
                    2 => 0x06, // VK_XBUTTON2
                    _ => 0
                };
            }

            if (vk != 0)
            {
                // Lire les modifiers clavier actuels
                var modifiers = HotkeyModifiers.None;
                if (_windowHelper.IsKeyDown(VK_CONTROL))
                    modifiers |= HotkeyModifiers.Control;
                if (_windowHelper.IsKeyDown(VK_SHIFT))
                    modifiers |= HotkeyModifiers.Shift;
                if (_windowHelper.IsKeyDown(VK_MENU))
                    modifiers |= HotkeyModifiers.Alt;

                // Chercher un binding correspondant
                HotkeyBinding? matched = null;
                lock (_lock)
                {
                    foreach (var binding in _mouseBindings.Values)
                    {
                        if (binding.VirtualKeyCode == vk && binding.Modifiers == modifiers)
                        {
                            matched = binding;
                            break;
                        }
                    }
                }

                if (matched is not null)
                {
                    Logger.Debug("Mouse button pressé : {DisplayName} (id={Id})", matched.DisplayName, matched.Id);
                    HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs
                    {
                        HotkeyId = matched.Id,
                        Binding = matched
                    });

                    // Consommer l'événement (cohérent avec RegisterHotKey qui mange les touches)
                    return new LRESULT(1);
                }
            }
        }

        return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        RemoveMouseHook();
        Logger.Information("HotkeyService disposé");
    }
}
