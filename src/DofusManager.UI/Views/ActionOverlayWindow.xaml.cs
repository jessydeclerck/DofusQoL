using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DofusManager.UI.ViewModels;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusManager.UI.Views;

public partial class ActionOverlayWindow : Window
{
    public ActionOverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyNoActivate();
    }

    /// <summary>
    /// WS_EX_NOACTIVATE empêche la fenêtre de voler le focus au jeu.
    /// Les boutons restent cliquables (contrairement à WS_EX_TRANSPARENT).
    /// </summary>
    private void ApplyNoActivate()
    {
        const int WS_EX_NOACTIVATE = 0x08000000;
        var hwnd = new WindowInteropHelper(this).Handle;
        var hWnd = new Windows.Win32.Foundation.HWND(hwnd);
        var exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            exStyle | WS_EX_NOACTIVATE);
    }

    private void Grip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (DataContext is DashboardViewModel vm)
        {
            vm.ActionOverlayLeft = Left;
            vm.ActionOverlayTop = Top;
        }
    }
}
