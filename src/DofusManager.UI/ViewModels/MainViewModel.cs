using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DofusManager.Core.Models;
using DofusManager.Core.Services;
using Serilog;

namespace DofusManager.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<MainViewModel>();

    private readonly IWindowDetectionService _detectionService;
    private readonly Dispatcher _dispatcher;

    public HotkeyViewModel HotkeyViewModel { get; }
    public ProfileViewModel ProfileViewModel { get; }

    public ObservableCollection<DofusWindow> Windows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PollingButtonText))]
    private bool _isPolling;

    [ObservableProperty]
    private string _statusText = "Prêt";

    public string PollingButtonText => IsPolling ? "Arrêter le polling" : "Démarrer le polling";

    public MainViewModel(IWindowDetectionService detectionService, HotkeyViewModel hotkeyViewModel, ProfileViewModel profileViewModel)
    {
        _detectionService = detectionService;
        _dispatcher = Dispatcher.CurrentDispatcher;
        HotkeyViewModel = hotkeyViewModel;
        ProfileViewModel = profileViewModel;

        _detectionService.WindowsChanged += OnWindowsChanged;

        // Polling actif par défaut
        _detectionService.StartPolling();
        IsPolling = true;
        StatusText = "Polling actif (500ms)";
    }

    [RelayCommand]
    private void Refresh()
    {
        Logger.Information("Scan manuel déclenché");
        var windows = _detectionService.DetectOnce();
        UpdateWindowList(windows);
        HotkeyViewModel.SyncSlots(windows);
    }

    [RelayCommand]
    private void TogglePolling()
    {
        if (IsPolling)
        {
            _detectionService.StopPolling();
            IsPolling = false;
            StatusText = "Polling arrêté";
            Logger.Information("Polling arrêté par l'utilisateur");
        }
        else
        {
            _detectionService.StartPolling();
            IsPolling = true;
            StatusText = "Polling actif (500ms)";
            Logger.Information("Polling démarré par l'utilisateur");
        }
    }

    private void OnWindowsChanged(object? sender, WindowsChangedEventArgs e)
    {
        _dispatcher.Invoke(() => UpdateWindowList(e.Current));
    }

    private void UpdateWindowList(IReadOnlyList<DofusWindow> windows)
    {
        Windows.Clear();
        foreach (var w in windows)
        {
            Windows.Add(w);
        }
        StatusText = $"{windows.Count} fenêtre(s) Dofus détectée(s)";
    }

    public void Dispose()
    {
        _detectionService.WindowsChanged -= OnWindowsChanged;
        HotkeyViewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
