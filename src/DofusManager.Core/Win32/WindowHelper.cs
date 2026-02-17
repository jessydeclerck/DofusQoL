using System.Runtime.InteropServices;
using DofusManager.Core.Models;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusManager.Core.Win32;

/// <summary>
/// Implémentation Win32 via CsWin32 pour la gestion des fenêtres.
/// </summary>
public class WindowHelper : IWin32WindowHelper
{
    private static readonly ILogger Logger = Log.ForContext<WindowHelper>();

    public unsafe IReadOnlyList<DofusWindow> EnumerateAllWindows()
    {
        var windows = new List<DofusWindow>();

        PInvoke.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!PInvoke.IsWindowVisible(hWnd))
                    return true;

                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                uint processId = 0;
                PInvoke.GetWindowThreadProcessId(hWnd, &processId);

                var window = new DofusWindow
                {
                    Handle = hWnd.Value,
                    ProcessId = (int)processId,
                    Title = title,
                    IsVisible = true,
                    IsMinimized = PInvoke.IsIconic(hWnd),
                    ScreenName = GetMonitorName(hWnd)
                };

                windows.Add(window);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Erreur lors de l'énumération de la fenêtre {Handle}", hWnd.Value);
            }

            return true;
        }, 0);

        return windows;
    }

    private static unsafe string GetWindowTitle(HWND hWnd)
    {
        var length = PInvoke.GetWindowTextLength(hWnd);
        if (length == 0)
            return string.Empty;

        var buffer = new char[length + 1];
        fixed (char* pBuffer = buffer)
        {
            PInvoke.GetWindowText(hWnd, pBuffer, length + 1);
        }
        return new string(buffer, 0, length);
    }

    private static unsafe string GetMonitorName(HWND hWnd)
    {
        var monitor = PInvoke.MonitorFromWindow(hWnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (monitor.Value == 0)
            return "Unknown";

        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
        {
            return info.szDevice.ToString();
        }

        return "Unknown";
    }

    public unsafe bool FocusWindow(nint handle)
    {
        var hWnd = new HWND(handle);

        try
        {
            if (PInvoke.IsIconic(hWnd))
            {
                PInvoke.ShowWindow(hWnd, SHOW_WINDOW_CMD.SW_RESTORE);
            }

            // Injecter un INPUT_KEYBOARD no-op pour satisfaire la condition
            // "le process appelant a reçu le dernier input event" requise par
            // SetForegroundWindow. On envoie un key-up pour wVk=0 (aucune touche) :
            // Windows le comptabilise comme input mais aucun WM_KEYDOWN/WM_KEYUP
            // exploitable n'est généré, et zéro interaction avec l'état souris
            // (pas de WM_MOUSEMOVE parasite).
            var noop = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[1];
            noop[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            noop[0].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
            // wVk = 0, wScan = 0 → key-up for "no key"

            PInvoke.SendInput(noop.AsSpan(), sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));

            var result = PInvoke.SetForegroundWindow(hWnd);
            Logger.Debug("FocusWindow {Handle} → SetForegroundWindow={Result}", handle, result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Erreur lors du focus de la fenêtre {Handle}", handle);
            return false;
        }
    }

    public bool IsWindowValid(nint handle)
    {
        return PInvoke.IsWindow(new HWND(handle));
    }

    public bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKeyCode)
    {
        var result = PInvoke.RegisterHotKey(
            new HWND(windowHandle),
            id,
            (Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)modifiers,
            virtualKeyCode);

        if (result)
        {
            Logger.Information("Hotkey enregistré : id={Id}, modifiers={Modifiers}, vk={VK}", id, modifiers, virtualKeyCode);
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            Logger.Warning("Échec RegisterHotKey id={Id}, erreur={Error}", id, error);
        }

        return result;
    }

    public void UnregisterHotKey(nint windowHandle, int id)
    {
        var result = PInvoke.UnregisterHotKey(new HWND(windowHandle), id);
        if (!result)
        {
            Logger.Warning("Échec UnregisterHotKey id={Id}", id);
        }
    }

    public bool PostMessage(nint handle, uint msg, nint wParam, nint lParam)
    {
        try
        {
            var result = PInvoke.PostMessage(new HWND(handle), msg, (WPARAM)(nuint)wParam, (LPARAM)lParam);
            Logger.Debug("PostMessage handle={Handle} msg=0x{Msg:X4} → {Result}", handle, msg, result);
            return result;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Erreur PostMessage handle={Handle} msg=0x{Msg:X4}", handle, msg);
            return false;
        }
    }

    public (int Width, int Height)? GetClientRect(nint handle)
    {
        try
        {
            if (PInvoke.GetClientRect(new HWND(handle), out var rect))
            {
                return (rect.Width, rect.Height);
            }

            Logger.Warning("GetClientRect échoué pour handle={Handle}", handle);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Erreur GetClientRect handle={Handle}", handle);
            return null;
        }
    }

    public (int ClientX, int ClientY)? ScreenToClient(nint handle, int screenX, int screenY)
    {
        try
        {
            var point = new System.Drawing.Point(screenX, screenY);
            if (PInvoke.ScreenToClient(new HWND(handle), ref point))
            {
                return (point.X, point.Y);
            }

            Logger.Warning("ScreenToClient échoué pour handle={Handle}", handle);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Erreur ScreenToClient handle={Handle}", handle);
            return null;
        }
    }

    public nint GetForegroundWindow()
    {
        return PInvoke.GetForegroundWindow().Value;
    }

    public bool IsKeyDown(int virtualKeyCode)
    {
        // GetAsyncKeyState retourne un short : bit de poids fort (0x8000) = touche enfoncée
        var state = PInvoke.GetAsyncKeyState(virtualKeyCode);
        return (state & 0x8000) != 0;
    }

    public (int ScreenX, int ScreenY)? ClientToScreen(nint handle, int clientX, int clientY)
    {
        try
        {
            var point = new System.Drawing.Point(clientX, clientY);
            if (PInvoke.ClientToScreen(new HWND(handle), ref point))
            {
                return (point.X, point.Y);
            }

            Logger.Warning("ClientToScreen échoué pour handle={Handle}", handle);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Erreur ClientToScreen handle={Handle}", handle);
            return null;
        }
    }

    public bool SetCursorPos(int x, int y)
    {
        return PInvoke.SetCursorPos(x, y);
    }

    public (int X, int Y)? GetCursorPos()
    {
        if (PInvoke.GetCursorPos(out var point))
        {
            return (point.X, point.Y);
        }
        return null;
    }

    public unsafe bool SendMouseClick()
    {
        var inputs = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[2];

        // Mouse down + up en un seul SendInput atomique
        inputs[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN;

        inputs[1].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_MOUSE;
        inputs[1].Anonymous.mi.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;

        var sent = PInvoke.SendInput(inputs.AsSpan(), sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
        return sent == 2;
    }

    public unsafe bool SendMouseUp()
    {
        var inputs = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[1];
        inputs[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP;

        var sent = PInvoke.SendInput(inputs.AsSpan(), sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
        return sent == 1;
    }

    public unsafe bool SendKeyPress(ushort virtualKeyCode)
    {
        // Convertir le virtual key en hardware scan code pour compatibilité DirectInput
        var scanCode = PInvoke.MapVirtualKey(virtualKeyCode, Windows.Win32.UI.Input.KeyboardAndMouse.MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);

        var inputs = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[2];

        inputs[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wScan = (ushort)scanCode;
        inputs[0].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE;

        inputs[1].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wScan = (ushort)scanCode;
        inputs[1].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE |
                                          Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        var sent = PInvoke.SendInput(inputs.AsSpan(), sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
        return sent == 2;
    }

    public unsafe bool SendText(string text)
    {
        foreach (var c in text)
        {
            var inputs = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[2];

            inputs[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs[0].Anonymous.ki.wScan = c;
            inputs[0].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;

            inputs[1].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
            inputs[1].Anonymous.ki.wScan = c;
            inputs[1].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE |
                                              Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

            var sent = PInvoke.SendInput(inputs.AsSpan(), sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
            if (sent != 2)
            {
                Logger.Warning("SendText échoué pour le caractère '{Char}'", c);
                return false;
            }
        }
        return true;
    }

    public unsafe bool SendKeyCombination(ushort modifierVk, ushort keyVk)
    {
        var modScan = PInvoke.MapVirtualKey(modifierVk, Windows.Win32.UI.Input.KeyboardAndMouse.MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
        var keyScan = PInvoke.MapVirtualKey(keyVk, Windows.Win32.UI.Input.KeyboardAndMouse.MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);

        var inputs = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[4];

        // Modifier down
        inputs[0].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wScan = (ushort)modScan;
        inputs[0].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE;

        // Key down
        inputs[1].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wScan = (ushort)keyScan;
        inputs[1].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE;

        // Key up
        inputs[2].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
        inputs[2].Anonymous.ki.wScan = (ushort)keyScan;
        inputs[2].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE |
                                          Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        // Modifier up
        inputs[3].type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD;
        inputs[3].Anonymous.ki.wScan = (ushort)modScan;
        inputs[3].Anonymous.ki.dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE |
                                          Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        var sent = PInvoke.SendInput(inputs.AsSpan(), sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
        return sent == 4;
    }
}
