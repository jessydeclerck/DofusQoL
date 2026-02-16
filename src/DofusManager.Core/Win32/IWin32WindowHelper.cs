using DofusManager.Core.Models;

namespace DofusManager.Core.Win32;

/// <summary>
/// Abstraction sur les appels Win32 pour la gestion des fenêtres.
/// Permet le mocking dans les tests unitaires.
/// </summary>
public interface IWin32WindowHelper
{
    /// <summary>
    /// Énumère toutes les fenêtres visibles du système avec leurs métadonnées.
    /// </summary>
    IReadOnlyList<DofusWindow> EnumerateAllWindows();

    /// <summary>
    /// Met une fenêtre au premier plan et la restaure si minimisée.
    /// </summary>
    bool FocusWindow(nint handle);

    /// <summary>
    /// Vérifie si un handle de fenêtre est encore valide.
    /// </summary>
    bool IsWindowValid(nint handle);

    /// <summary>
    /// Enregistre un raccourci clavier global via Win32 RegisterHotKey.
    /// </summary>
    bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKeyCode);

    /// <summary>
    /// Désenregistre un raccourci clavier global via Win32 UnregisterHotKey.
    /// </summary>
    void UnregisterHotKey(nint windowHandle, int id);
}
