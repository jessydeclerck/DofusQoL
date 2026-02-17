using System.Windows;
using System.Windows.Interop;
using DofusManager.Core.Services;
using DofusManager.UI.ViewModels;

namespace DofusManager.UI.Views;

public partial class MainWindow : Window
{
    private const int WM_HOTKEY = 0x0312;

    private readonly IHotkeyService _hotkeyService;
    private readonly DashboardViewModel _viewModel;

    public MainWindow(DashboardViewModel viewModel, IHotkeyService hotkeyService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _hotkeyService = hotkeyService;
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        _viewModel.InitializeHotkeys(hwnd);
        await _viewModel.InitializeProfilesAsync();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            handled = _hotkeyService.ProcessMessage(wParam, lParam);
        }

        return IntPtr.Zero;
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _viewModel.SaveSessionStateAsync();
        (DataContext as IDisposable)?.Dispose();
        base.OnClosed(e);
    }
}
