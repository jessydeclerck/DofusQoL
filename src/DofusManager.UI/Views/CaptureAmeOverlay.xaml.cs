using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusManager.UI.Views;

public partial class CaptureAmeOverlay : Window
{
    private readonly double _fontSize;
    private readonly int _blinkMs;

    public CaptureAmeOverlay(double fontSize = 20, int blinkMs = 1500)
    {
        _fontSize = fontSize;
        _blinkMs = blinkMs;
        InitializeComponent();
        OverlayText.FontSize = _fontSize;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyExtendedStyles();
        StartBlinkAnimation();
        PositionAtCenter();
    }

    private void ApplyExtendedStyles()
    {
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        var hwnd = new WindowInteropHelper(this).Handle;
        var hWnd = new Windows.Win32.Foundation.HWND(hwnd);
        var exStyle = PInvoke.GetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(hWnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    private void StartBlinkAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.45,
            Duration = TimeSpan.FromMilliseconds(_blinkMs),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        OverlayBorder.BeginAnimation(OpacityProperty, animation);
    }

    private void PositionAtCenter()
    {
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(DesiredSize));

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + workArea.Height * 0.30;
    }
}
