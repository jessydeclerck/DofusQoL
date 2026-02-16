using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de gestion des raccourcis clavier globaux.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Initialise le service avec le handle de la fenêtre qui recevra les messages WM_HOTKEY.
    /// </summary>
    void Initialize(nint windowHandle);

    /// <summary>
    /// Enregistre un raccourci clavier global.
    /// </summary>
    bool Register(HotkeyBinding binding);

    /// <summary>
    /// Désenregistre un raccourci par son identifiant.
    /// </summary>
    void Unregister(int id);

    /// <summary>
    /// Désenregistre tous les raccourcis.
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Traite un message WM_HOTKEY reçu par la fenêtre. Appelé par le hook WndProc.
    /// </summary>
    bool ProcessMessage(nint wParam, nint lParam);

    /// <summary>
    /// Événement déclenché quand un raccourci enregistré est pressé.
    /// </summary>
    event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>
    /// Liste des raccourcis actuellement enregistrés.
    /// </summary>
    IReadOnlyList<HotkeyBinding> RegisteredHotkeys { get; }

    /// <summary>
    /// Indique si le service est initialisé.
    /// </summary>
    bool IsInitialized { get; }
}
