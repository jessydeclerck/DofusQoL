using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.UI.ViewModels;

public partial class BroadcastViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<BroadcastViewModel>();

    private readonly IBroadcastService _broadcastService;
    private readonly IWindowDetectionService _detectionService;
    private readonly IFocusService _focusService;
    private readonly IPushToBroadcastService _pushToBroadcastService;
    private readonly IWin32WindowHelper _windowHelper;
    private readonly Dispatcher _dispatcher;

    // Timer pour détecter l'état de la touche Ctrl
    private DispatcherTimer? _ctrlPollTimer;
    private const int VK_MENU = 0x12; // Alt
    private bool _listeningMode;

    public ObservableCollection<BroadcastPresetDisplayItem> Presets { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemovePresetCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestBroadcastCommand))]
    private BroadcastPresetDisplayItem? _selectedPreset;

    // Champs du formulaire d'édition
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddPresetCommand))]
    private string _presetName = string.Empty;

    [ObservableProperty]
    private string _selectedInputType = "key";

    [ObservableProperty]
    private string _keyName = "Enter";

    [ObservableProperty]
    private int _clickX;

    [ObservableProperty]
    private int _clickY;

    [ObservableProperty]
    private string _selectedClickButton = "left";

    [ObservableProperty]
    private string _selectedTargets = "all";

    [ObservableProperty]
    private int _delayMin = 80;

    [ObservableProperty]
    private int _delayMax = 300;

    [ObservableProperty]
    private string _selectedOrderMode = "profile";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseButtonText))]
    private bool _isPaused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastIndicator))]
    private bool _isArmed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastButtonText))]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastIndicator))]
    private bool _isListening;

    [ObservableProperty]
    private string _statusText = "Broadcast prêt";

    public string PauseButtonText => IsPaused ? "Reprendre les broadcasts" : "Pause broadcasts";
    public string PushToBroadcastButtonText => IsListening ? "Désactiver" : "Activer";
    public string PushToBroadcastIndicator => IsArmed ? "Ctrl maintenu" : IsListening ? "En attente de Alt" : "Inactif";

    public static string[] InputTypes => BroadcastPreset.ValidInputTypes;
    public static string[] TargetModes => BroadcastPreset.ValidTargets;
    public static string[] ClickButtons => BroadcastPreset.ValidClickButtons;
    public static string[] OrderModes => BroadcastPreset.ValidOrderModes;

    public BroadcastViewModel(
        IBroadcastService broadcastService,
        IWindowDetectionService detectionService,
        IFocusService focusService,
        IPushToBroadcastService pushToBroadcastService,
        IWin32WindowHelper windowHelper)
    {
        _broadcastService = broadcastService;
        _detectionService = detectionService;
        _focusService = focusService;
        _pushToBroadcastService = pushToBroadcastService;
        _windowHelper = windowHelper;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _pushToBroadcastService.BroadcastPerformed += OnBroadcastPerformed;
    }

    [RelayCommand(CanExecute = nameof(CanAddPreset))]
    private void AddPreset()
    {
        var preset = new BroadcastPreset
        {
            Name = PresetName.Trim(),
            InputType = SelectedInputType,
            Key = SelectedInputType == "key" ? KeyName : null,
            ClickX = SelectedInputType != "key" ? ClickX : null,
            ClickY = SelectedInputType != "key" ? ClickY : null,
            ClickButton = SelectedClickButton,
            Targets = SelectedTargets,
            DelayMin = DelayMin,
            DelayMax = DelayMax,
            OrderMode = SelectedOrderMode
        };

        var error = preset.Validate();
        if (error is not null)
        {
            StatusText = error;
            return;
        }

        Presets.Add(new BroadcastPresetDisplayItem
        {
            Name = preset.Name,
            InputType = preset.InputType,
            Description = FormatPresetDescription(preset)
        });

        PresetName = string.Empty;
        StatusText = $"Preset '{preset.Name}' ajouté";
        Logger.Information("Preset broadcast ajouté : {Name}", preset.Name);
    }

    private bool CanAddPreset() => !string.IsNullOrWhiteSpace(PresetName);

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private void RemovePreset()
    {
        if (SelectedPreset is null) return;

        var name = SelectedPreset.Name;
        Presets.Remove(SelectedPreset);
        SelectedPreset = null;
        StatusText = $"Preset '{name}' supprimé";
        Logger.Information("Preset broadcast supprimé : {Name}", name);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPreset))]
    private async Task TestBroadcast()
    {
        if (SelectedPreset is null) return;

        var preset = BuildPresetFromForm();
        if (preset is null) return;

        var windows = _detectionService.DetectedWindows;
        var leaderHandle = _focusService.CurrentLeader?.Handle;

        StatusText = $"Broadcast '{preset.Name}' en cours...";

        try
        {
            var result = await Task.Run(() =>
                _broadcastService.ExecuteBroadcastAsync(preset, windows, leaderHandle));

            _dispatcher.Invoke(() =>
            {
                if (result.Success)
                {
                    StatusText = $"Broadcast '{preset.Name}' : {result.WindowsReached}/{result.WindowsTargeted} fenêtres";
                }
                else
                {
                    StatusText = $"Broadcast échoué : {result.ErrorMessage}";
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur lors du broadcast de test");
            StatusText = $"Erreur : {ex.Message}";
        }
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        _broadcastService.IsPaused = IsPaused;
        StatusText = IsPaused ? "Broadcasts en pause" : "Broadcasts actifs";
        Logger.Information("Broadcast pause={IsPaused}", IsPaused);
    }

    /// <summary>
    /// Active/désactive le mode d'écoute Push-to-Broadcast.
    /// Quand actif, maintenir Ctrl arme le broadcast des clics.
    /// </summary>
    [RelayCommand]
    public void TogglePushToBroadcast()
    {
        if (_listeningMode)
        {
            StopListening();
        }
        else
        {
            StartListening();
        }
    }

    private void StartListening()
    {
        _listeningMode = true;
        IsListening = true;
        StatusText = "Push-to-Broadcast actif — maintenez Alt pour broadcaster les clics";

        _ctrlPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _ctrlPollTimer.Tick += OnCtrlPollTimerTick;
        _ctrlPollTimer.Start();

        Logger.Information("Push-to-Broadcast mode écoute activé");
    }

    private void StopListening()
    {
        // Désarmer si armé
        if (IsArmed)
        {
            _pushToBroadcastService.Disarm();
            IsArmed = false;
        }

        StopCtrlPollTimer();
        _listeningMode = false;
        IsListening = false;
        StatusText = "Push-to-Broadcast désactivé";

        Logger.Information("Push-to-Broadcast mode écoute désactivé");
    }

    private void OnCtrlPollTimerTick(object? sender, EventArgs e)
    {
        var ctrlDown = _windowHelper.IsKeyDown(VK_MENU);

        if (ctrlDown && !IsArmed)
        {
            // Ctrl enfoncé → armer
            var windows = _detectionService.DetectedWindows;
            if (windows.Count == 0) return;

            _pushToBroadcastService.Arm(windows);
            IsArmed = true;
            StatusText = $"Push-to-Broadcast ARMÉ ({windows.Count} fenêtres)";
            Logger.Information("Push-to-Broadcast armé (Alt enfoncé) avec {Count} fenêtres", windows.Count);
        }
        else if (!ctrlDown && IsArmed)
        {
            // Ctrl relâché → désarmer
            _pushToBroadcastService.Disarm();
            IsArmed = false;
            StatusText = "Push-to-Broadcast — en attente de Alt";
            Logger.Information("Push-to-Broadcast désarmé (Alt relâché)");
        }
    }

    private void StopCtrlPollTimer()
    {
        if (_ctrlPollTimer is not null)
        {
            _ctrlPollTimer.Tick -= OnCtrlPollTimerTick;
            _ctrlPollTimer.Stop();
            _ctrlPollTimer = null;
        }
    }

    private void OnBroadcastPerformed(object? sender, int windowCount)
    {
        _dispatcher.Invoke(() =>
        {
            StatusText = $"Push-to-Broadcast : clic envoyé à {windowCount} fenêtre(s)";
        });
    }

    private bool HasSelectedPreset() => SelectedPreset is not null;

    /// <summary>
    /// Charge les presets depuis un profil.
    /// </summary>
    public void LoadPresetsFromProfile(IReadOnlyList<BroadcastPreset> presets)
    {
        Presets.Clear();
        foreach (var preset in presets)
        {
            Presets.Add(new BroadcastPresetDisplayItem
            {
                Name = preset.Name,
                InputType = preset.InputType,
                Description = FormatPresetDescription(preset)
            });
        }
        StatusText = $"{presets.Count} preset(s) chargé(s)";
    }

    /// <summary>
    /// Exporte les presets sous forme de liste BroadcastPreset pour la sauvegarde dans un profil.
    /// </summary>
    public List<BroadcastPreset> ExportPresets()
    {
        return Presets.Select(p => new BroadcastPreset
        {
            Name = p.Name,
            InputType = p.InputType,
            Key = p.InputType == "key" ? KeyName : null,
            ClickX = p.InputType != "key" ? ClickX : null,
            ClickY = p.InputType != "key" ? ClickY : null,
            ClickButton = SelectedClickButton,
            Targets = SelectedTargets,
            DelayMin = DelayMin,
            DelayMax = DelayMax,
            OrderMode = SelectedOrderMode
        }).ToList();
    }

    private BroadcastPreset? BuildPresetFromForm()
    {
        if (SelectedPreset is null) return null;

        var preset = new BroadcastPreset
        {
            Name = SelectedPreset.Name,
            InputType = SelectedInputType,
            Key = SelectedInputType == "key" ? KeyName : null,
            ClickX = SelectedInputType != "key" ? ClickX : null,
            ClickY = SelectedInputType != "key" ? ClickY : null,
            ClickButton = SelectedClickButton,
            Targets = SelectedTargets,
            DelayMin = DelayMin,
            DelayMax = DelayMax,
            OrderMode = SelectedOrderMode
        };

        var error = preset.Validate();
        if (error is not null)
        {
            StatusText = error;
            return null;
        }

        return preset;
    }

    private static string FormatPresetDescription(BroadcastPreset preset)
    {
        var input = preset.InputType switch
        {
            "key" => $"Touche {preset.Key}",
            "clickAtPosition" => $"Clic ({preset.ClickX}, {preset.ClickY})",
            "clickAtCursor" => "Clic au curseur",
            _ => preset.InputType
        };

        return $"{input} → {preset.Targets} [{preset.DelayMin}-{preset.DelayMax}ms]";
    }
}

public class BroadcastPresetDisplayItem
{
    public required string Name { get; init; }
    public required string InputType { get; init; }
    public required string Description { get; init; }
}
