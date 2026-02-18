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

    /// <summary>
    /// Envoie un message à une fenêtre via Win32 PostMessage (non-bloquant).
    /// </summary>
    bool PostMessage(nint handle, uint msg, nint wParam, nint lParam);

    /// <summary>
    /// Récupère les coordonnées de la zone client d'une fenêtre.
    /// Retourne (width, height) de la zone client, ou null si échec.
    /// </summary>
    (int Width, int Height)? GetClientRect(nint handle);

    /// <summary>
    /// Convertit des coordonnées écran en coordonnées client-relatives pour une fenêtre.
    /// Retourne (clientX, clientY) ou null si échec.
    /// </summary>
    (int ClientX, int ClientY)? ScreenToClient(nint handle, int screenX, int screenY);

    /// <summary>
    /// Retourne le handle de la fenêtre actuellement au premier plan.
    /// </summary>
    nint GetForegroundWindow();

    /// <summary>
    /// Vérifie si une touche est actuellement enfoncée via GetAsyncKeyState.
    /// Retourne true si la touche est enfoncée au moment de l'appel.
    /// </summary>
    bool IsKeyDown(int virtualKeyCode);

    /// <summary>
    /// Convertit des coordonnées client-relatives en coordonnées écran.
    /// Retourne (screenX, screenY) ou null si échec.
    /// </summary>
    (int ScreenX, int ScreenY)? ClientToScreen(nint handle, int clientX, int clientY);

    /// <summary>
    /// Déplace le curseur à une position écran absolue.
    /// </summary>
    bool SetCursorPos(int x, int y);

    /// <summary>
    /// Récupère la position actuelle du curseur en coordonnées écran.
    /// </summary>
    (int X, int Y)? GetCursorPos();

    /// <summary>
    /// Injecte un clic gauche hardware via SendInput à la position actuelle du curseur.
    /// </summary>
    bool SendMouseClick();

    /// <summary>
    /// Injecte un relâchement du bouton gauche via SendInput (LBUTTONUP uniquement).
    /// Sert à fermer un clic en cours pour éviter un état "drag" fantôme.
    /// </summary>
    bool SendMouseUp();

    /// <summary>
    /// Injecte une frappe clavier (key down + key up) pour une touche virtuelle via SendInput.
    /// </summary>
    bool SendKeyPress(ushort virtualKeyCode);

    /// <summary>
    /// Injecte du texte caractère par caractère via SendInput + KEYEVENTF_UNICODE.
    /// Indépendant du layout clavier.
    /// </summary>
    bool SendText(string text);

    /// <summary>
    /// Injecte un raccourci clavier (modifier down → touche down+up → modifier up) via scan codes.
    /// </summary>
    bool SendKeyCombination(ushort modifierVk, ushort keyVk);

    /// <summary>
    /// Retourne le handle de la fenêtre top-level aux coordonnées écran données.
    /// </summary>
    nint GetWindowFromPoint(int screenX, int screenY);
}
