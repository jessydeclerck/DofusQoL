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

    public bool FocusWindow(nint handle)
    {
        var hWnd = new HWND(handle);

        try
        {
            if (PInvoke.IsIconic(hWnd))
            {
                PInvoke.ShowWindow(hWnd, SHOW_WINDOW_CMD.SW_RESTORE);
            }

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
}
