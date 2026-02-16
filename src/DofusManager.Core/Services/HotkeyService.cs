using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.Core.Services;

/// <summary>
/// Gère l'enregistrement des raccourcis clavier globaux et dispatch les événements.
/// </summary>
public class HotkeyService : IHotkeyService
{
    private static readonly ILogger Logger = Log.ForContext<HotkeyService>();

    private readonly IWin32WindowHelper _windowHelper;
    private readonly Dictionary<int, HotkeyBinding> _bindings = new();
    private readonly object _lock = new();

    private nint _windowHandle;
    private bool _disposed;

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
                return _bindings.Values.ToList();
            }
        }
    }

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    public void Initialize(nint windowHandle)
    {
        if (windowHandle == 0)
            throw new ArgumentException("Le handle de fenêtre ne peut pas être nul.", nameof(windowHandle));

        _windowHandle = windowHandle;
        Logger.Information("HotkeyService initialisé avec HWND={Handle}", windowHandle);
    }

    public bool Register(HotkeyBinding binding)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Le service doit être initialisé avant d'enregistrer des hotkeys.");

        lock (_lock)
        {
            if (_bindings.ContainsKey(binding.Id))
            {
                Logger.Warning("Hotkey id={Id} déjà enregistré, désenregistrement préalable", binding.Id);
                UnregisterInternal(binding.Id);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        Logger.Information("HotkeyService disposé");
    }
}
