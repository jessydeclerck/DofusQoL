using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DofusManager.Core.Models;
using DofusManager.Core.Services;
using Serilog;

namespace DofusManager.UI.ViewModels;

public partial class HotkeyViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<HotkeyViewModel>();

    private readonly IHotkeyService _hotkeyService;
    private readonly IFocusService _focusService;
    private readonly IWindowDetectionService _detectionService;
    private readonly Dispatcher _dispatcher;

    // Constantes Virtual Key Codes
    private const uint VK_F1 = 0x70;
    private const uint VK_TAB = 0x09;
    private const uint VK_SPACE = 0x20;
    private const uint VK_OEM_3 = 0xC0; // touche ` (backtick)

    // IDs pour les hotkeys spéciaux (au-delà des slots)
    private const int NextWindowId = 100;
    private const int PrevWindowId = 101;
    private const int LastWindowId = 102;
    private const int PanicLeaderId = 103;

    public ObservableCollection<SlotDisplayItem> Slots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeysButtonText))]
    private bool _hotkeysActive;

    [ObservableProperty]
    private string _statusText = "Raccourcis inactifs";

    [ObservableProperty]
    private int _selectedLeaderIndex = -1;

    public string HotkeysButtonText => HotkeysActive
        ? "Désactiver les raccourcis"
        : "Activer les raccourcis";

    public HotkeyViewModel(
        IHotkeyService hotkeyService,
        IFocusService focusService,
        IWindowDetectionService detectionService)
    {
        _hotkeyService = hotkeyService;
        _focusService = focusService;
        _detectionService = detectionService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _detectionService.WindowsChanged += OnWindowsChanged;
    }

    /// <summary>
    /// Appelé par MainWindow après OnSourceInitialized pour fournir le HWND.
    /// </summary>
    public void InitializeHotkeys(nint windowHandle)
    {
        _hotkeyService.Initialize(windowHandle);
        Logger.Information("HotkeyViewModel initialisé avec HWND={Handle}", windowHandle);

        // Raccourcis actifs par défaut
        RegisterDefaultHotkeys();
        HotkeysActive = true;
        StatusText = $"Raccourcis activés ({_hotkeyService.RegisteredHotkeys.Count} enregistrés)";
    }

    [RelayCommand]
    private void ToggleHotkeys()
    {
        if (!_hotkeyService.IsInitialized)
        {
            StatusText = "Service non initialisé";
            return;
        }

        if (HotkeysActive)
        {
            _hotkeyService.UnregisterAll();
            HotkeysActive = false;
            StatusText = "Raccourcis désactivés";
            Logger.Information("Hotkeys désactivés par l'utilisateur");
        }
        else
        {
            RegisterDefaultHotkeys();
            HotkeysActive = true;
            StatusText = $"Raccourcis activés ({_hotkeyService.RegisteredHotkeys.Count} enregistrés)";
            Logger.Information("Hotkeys activés par l'utilisateur");
        }
    }

    [RelayCommand]
    private void SetLeader()
    {
        if (SelectedLeaderIndex < 0 || SelectedLeaderIndex >= Slots.Count)
            return;

        var slot = Slots[SelectedLeaderIndex];
        _focusService.SetLeader(slot.Handle);

        // Mettre à jour l'affichage
        foreach (var s in Slots)
            s.IsLeader = s.Handle == slot.Handle;

        StatusText = $"Leader : {slot.Title}";
        Logger.Information("Leader défini : {Title}", slot.Title);
    }

    private void RegisterDefaultHotkeys()
    {
        // Toujours enregistrer les 8 F-keys ; FocusService gère les slots hors bornes
        for (var i = 0; i < 8; i++)
        {
            _hotkeyService.Register(new HotkeyBinding
            {
                Id = i + 1,
                Modifiers = HotkeyModifiers.None,
                VirtualKeyCode = VK_F1 + (uint)i,
                DisplayName = $"F{i + 1}",
                Action = HotkeyAction.FocusSlot,
                SlotIndex = i
            });
        }

        // Ctrl+Tab → Next (fallback Ctrl+Espace si réservé par le système)
        if (!_hotkeyService.Register(new HotkeyBinding
            {
                Id = NextWindowId,
                Modifiers = HotkeyModifiers.Control,
                VirtualKeyCode = VK_TAB,
                DisplayName = "Ctrl+Tab",
                Action = HotkeyAction.NextWindow
            }))
        {
            Logger.Warning("Ctrl+Tab réservé, fallback vers Ctrl+Espace");
            _hotkeyService.Register(new HotkeyBinding
            {
                Id = NextWindowId,
                Modifiers = HotkeyModifiers.Control,
                VirtualKeyCode = VK_SPACE,
                DisplayName = "Ctrl+Espace",
                Action = HotkeyAction.NextWindow
            });
        }

        // Ctrl+Shift+Tab → Previous (fallback Ctrl+Shift+Espace)
        if (!_hotkeyService.Register(new HotkeyBinding
            {
                Id = PrevWindowId,
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKeyCode = VK_TAB,
                DisplayName = "Ctrl+Shift+Tab",
                Action = HotkeyAction.PreviousWindow
            }))
        {
            Logger.Warning("Ctrl+Shift+Tab réservé, fallback vers Ctrl+Shift+Espace");
            _hotkeyService.Register(new HotkeyBinding
            {
                Id = PrevWindowId,
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKeyCode = VK_SPACE,
                DisplayName = "Ctrl+Shift+Espace",
                Action = HotkeyAction.PreviousWindow
            });
        }

        // Ctrl+` → Last Window
        _hotkeyService.Register(new HotkeyBinding
        {
            Id = LastWindowId,
            Modifiers = HotkeyModifiers.Control,
            VirtualKeyCode = VK_OEM_3,
            DisplayName = "Ctrl+`",
            Action = HotkeyAction.LastWindow
        });

        // Ctrl+F1 → Panic Leader
        _hotkeyService.Register(new HotkeyBinding
        {
            Id = PanicLeaderId,
            Modifiers = HotkeyModifiers.Control,
            VirtualKeyCode = VK_F1,
            DisplayName = "Ctrl+F1",
            Action = HotkeyAction.PanicLeader
        });

    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            var result = e.Binding.Action switch
            {
                HotkeyAction.FocusSlot => _focusService.FocusSlot(e.Binding.SlotIndex ?? 0),
                HotkeyAction.NextWindow => _focusService.FocusNext(),
                HotkeyAction.PreviousWindow => _focusService.FocusPrevious(),
                HotkeyAction.LastWindow => _focusService.FocusLast(),
                HotkeyAction.PanicLeader => _focusService.FocusLeader(),
                _ => FocusResult.Error("Action inconnue")
            };

            if (result.Success)
            {
                StatusText = $"{e.Binding.DisplayName} → slot {_focusService.CurrentSlotIndex}";
            }
            else
            {
                StatusText = $"{e.Binding.DisplayName} : {result.ErrorMessage}";
                Logger.Warning("Focus échoué : {Action} → {Error}", e.Binding.DisplayName, result.ErrorMessage);
            }
        });
    }

    private void OnWindowsChanged(object? sender, WindowsChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            _focusService.UpdateSlots(e.Current);
            UpdateSlotDisplay(e.Current);
        });
    }

    /// <summary>
    /// Appelé par MainViewModel lors d'un scan manuel pour synchroniser les slots.
    /// </summary>
    public void SyncSlots(IReadOnlyList<DofusWindow> windows)
    {
        _focusService.UpdateSlots(windows);
        UpdateSlotDisplay(windows);
    }

    private void UpdateSlotDisplay(IReadOnlyList<DofusWindow> windows)
    {
        var leaderHandle = _focusService.CurrentLeader?.Handle ?? 0;

        Slots.Clear();
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            var hotkeyLabel = i < 8 ? $"F{i + 1}" : "—";
            Slots.Add(new SlotDisplayItem
            {
                SlotIndex = i,
                Handle = w.Handle,
                Title = w.Title,
                ProcessId = w.ProcessId,
                HotkeyLabel = hotkeyLabel,
                IsLeader = w.Handle == leaderHandle
            });
        }
    }

    public void Dispose()
    {
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _detectionService.WindowsChanged -= OnWindowsChanged;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Item affiché dans la liste des slots du HotkeyView.
/// </summary>
public partial class SlotDisplayItem : ObservableObject
{
    public int SlotIndex { get; init; }
    public nint Handle { get; init; }
    public required string Title { get; init; }
    public int ProcessId { get; init; }
    public required string HotkeyLabel { get; init; }

    [ObservableProperty]
    private bool _isLeader;
}
