using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusManager.UI.Views;

public partial class CaptureAmeOverlay : Window
{
    public CaptureAmeOverlay()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyClickThrough();
        PositionAtTopCenter();
    }

    private void ApplyClickThrough()
    {
        const int WS_EX_TRANSPARENT = 0x00000020;
        var hwnd = new WindowInteropHelper(this).Handle;
        var hWnd = new Windows.Win32.Foundation.HWND(hwnd);
        var exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        // WS_EX_TRANSPARENT rend la fenêtre click-through au niveau OS
        PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            exStyle | WS_EX_TRANSPARENT);
    }

    private void PositionAtTopCenter()
    {
        // Forcer le layout pour obtenir les dimensions réelles
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + workArea.Height * 0.30; // ~30% depuis le haut
    }
}
