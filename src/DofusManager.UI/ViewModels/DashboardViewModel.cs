using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DofusManager.Core.Helpers;
using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using DofusManager.UI.Helpers;
using Serilog;

namespace DofusManager.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<DashboardViewModel>();

    private readonly IWindowDetectionService _detectionService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IFocusService _focusService;
    private readonly IProfileService _profileService;
    private readonly IAppStateService _appStateService;
    private readonly IPushToBroadcastService _pushToBroadcastService;
    private readonly IGroupInviteService _groupInviteService;
    private readonly IWin32WindowHelper _windowHelper;
    private readonly Dispatcher _dispatcher;

    // Suivi du profil actif pour la persistance de session
    private string? _activeProfileName;
    private string? _pendingProfileName;
    private Profile? _sessionSnapshot; // snapshot implicite quand aucun profil n'est actif
    private bool _applyingProfile; // guard contre les callbacks durant ApplyProfile

    // Auto-save débounced
    private CancellationTokenSource? _autoSaveCts;
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(1500);

    // Virtual Key constants
    private const uint VK_TAB = 0x09;
    private const uint VK_SPACE = 0x20;
    private const uint VK_OEM_3 = 0xC0;
    private const uint VK_F1 = 0x70;

    // Hotkey ID ranges
    private const int GlobalHotkeyBaseId = 100;

    // Push-to-broadcast
    private DispatcherTimer? _altPollTimer;
    private bool _listeningMode;

    // --- Collections ---

    public ObservableCollection<CharacterRowViewModel> Characters { get; } = [];
    public ObservableCollection<GlobalHotkeyRowViewModel> GlobalHotkeys { get; } = [];
    public ObservableCollection<ProfileListItem> Profiles { get; } = [];

    // --- Polling ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PollingButtonText))]
    private bool _isPolling;

    public string PollingButtonText => IsPolling ? "Arrêter le polling" : "Démarrer le polling";

    // --- Hotkeys ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeysButtonText))]
    private bool _hotkeysActive;

    public string HotkeysButtonText => HotkeysActive ? "Désactiver raccourcis" : "Activer raccourcis";

    // --- Push-to-Broadcast ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastIndicator))]
    private bool _isArmed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastButtonText))]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastIndicator))]
    private bool _isListening;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastIndicator))]
    [NotifyPropertyChangedFor(nameof(BroadcastKeyHelpText))]
    private uint _broadcastKeyVirtualKeyCode = 0x12; // VK_MENU (Alt)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PushToBroadcastIndicator))]
    [NotifyPropertyChangedFor(nameof(BroadcastKeyHelpText))]
    private string _broadcastKeyDisplay = "Alt";

    [ObservableProperty]
    private bool _returnToLeaderAfterBroadcast;

    [ObservableProperty]
    private int _broadcastDelayMs = 65;

    [ObservableProperty]
    private int _broadcastDelayRandomMs = 25;

    [ObservableProperty]
    private bool _pasteToChatDoubleEnter;

    [ObservableProperty]
    private int _pasteToChatDoubleEnterDelayMs = 500;

    [ObservableProperty]
    private bool _pasteToChatAlwaysLeader;

    public string PushToBroadcastButtonText => IsListening ? "Désactiver" : "Activer";
    public string PushToBroadcastIndicator => IsArmed
        ? $"{BroadcastKeyDisplay} maintenu"
        : IsListening
            ? $"En attente de {BroadcastKeyDisplay}"
            : "Inactif";
    public string BroadcastKeyHelpText => $"Maintenez {BroadcastKeyDisplay} : chaque clic est broadcasté.";

    partial void OnReturnToLeaderAfterBroadcastChanged(bool value)
    {
        _pushToBroadcastService.ReturnToLeaderAfterBroadcast = value;
        UpdateSessionSnapshot();
    }

    partial void OnBroadcastDelayMsChanged(int value)
    {
        _pushToBroadcastService.BroadcastDelayMs = value;
        UpdateSessionSnapshot();
    }

    partial void OnBroadcastDelayRandomMsChanged(int value)
    {
        _pushToBroadcastService.BroadcastDelayRandomMs = value;
        UpdateSessionSnapshot();
    }

    partial void OnPasteToChatDoubleEnterChanged(bool value)
    {
        UpdateSessionSnapshot();
    }

    partial void OnPasteToChatDoubleEnterDelayMsChanged(int value)
    {
        UpdateSessionSnapshot();
    }

    partial void OnPasteToChatAlwaysLeaderChanged(bool value)
    {
        UpdateSessionSnapshot();
    }

    // --- Profils ---

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    private ProfileListItem? _selectedProfile;

    // --- Topmost ---

    [ObservableProperty]
    private bool _isTopmost;

    partial void OnIsTopmostChanged(bool value) => ScheduleAutoSave();

    // --- Status ---

    [ObservableProperty]
    private string _statusText = "Prêt";

    public DashboardViewModel(
        IWindowDetectionService detectionService,
        IHotkeyService hotkeyService,
        IFocusService focusService,
        IProfileService profileService,
        IAppStateService appStateService,
        IPushToBroadcastService pushToBroadcastService,
        IGroupInviteService groupInviteService,
        IWin32WindowHelper windowHelper)
    {
        _detectionService = detectionService;
        _hotkeyService = hotkeyService;
        _focusService = focusService;
        _profileService = profileService;
        _appStateService = appStateService;
        _pushToBroadcastService = pushToBroadcastService;
        _groupInviteService = groupInviteService;
        _windowHelper = windowHelper;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _detectionService.WindowsChanged += OnWindowsChanged;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _profileService.ProfilesChanged += OnProfilesChanged;
        _pushToBroadcastService.BroadcastPerformed += OnBroadcastPerformed;

        // Initialiser les raccourcis globaux + touche broadcast avec les valeurs par défaut
        var defaultConfig = GlobalHotkeyConfig.CreateDefault();
        InitializeGlobalHotkeys(defaultConfig);
        InitializeBroadcastKey(defaultConfig.BroadcastKey);

        // Polling actif par défaut
        _detectionService.StartPolling();
        IsPolling = true;
        StatusText = "Polling actif (500ms)";

        // Push-to-Broadcast actif par défaut
        TogglePushToBroadcast();
    }

    /// <summary>
    /// Appelé par MainWindow après OnSourceInitialized pour fournir le HWND.
    /// </summary>
    public void InitializeHotkeys(nint windowHandle)
    {
        _hotkeyService.Initialize(windowHandle);
        Logger.Information("DashboardViewModel initialisé avec HWND={Handle}", windowHandle);

        RegisterAllHotkeys();
        HotkeysActive = true;
        StatusText = $"Raccourcis activés ({_hotkeyService.RegisteredHotkeys.Count} enregistrés)";
    }

    public async Task InitializeProfilesAsync()
    {
        try
        {
            await _profileService.LoadAsync();
            RefreshProfileList();

            // Restaurer l'état de la dernière session
            var appState = await _appStateService.LoadAsync();
            if (appState is not null)
            {
                await RestoreSessionStateAsync(appState);
            }
            else
            {
                StatusText = $"{Profiles.Count} profil(s) chargé(s)";
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur au chargement des profils");
            StatusText = "Erreur au chargement des profils";
        }
    }

    private async Task RestoreSessionStateAsync(AppState appState)
    {
        // Restaurer la préférence Topmost
        IsTopmost = appState.IsTopmost;

        // Restaurer le snapshot de session (inclut ordre slots, leader, hotkeys)
        var snapshot = appState.SessionSnapshot;
        if (snapshot is not null)
        {
            _sessionSnapshot = snapshot;
        }
        else if (appState.LastHotkeyConfig is not null)
        {
            // Backward compat : ancien format sans snapshot — appliquer les hotkeys
            InitializeGlobalHotkeys(appState.LastHotkeyConfig);
            InitializeBroadcastKey(appState.LastHotkeyConfig.BroadcastKey);
            ReturnToLeaderAfterBroadcast = appState.LastHotkeyConfig.ReturnToLeaderAfterBroadcast;
            _pushToBroadcastService.ReturnToLeaderAfterBroadcast = ReturnToLeaderAfterBroadcast;
            BroadcastDelayMs = appState.LastHotkeyConfig.BroadcastDelayMs;
            _pushToBroadcastService.BroadcastDelayMs = BroadcastDelayMs;
            BroadcastDelayRandomMs = appState.LastHotkeyConfig.BroadcastDelayRandomMs;
            _pushToBroadcastService.BroadcastDelayRandomMs = BroadcastDelayRandomMs;
            PasteToChatDoubleEnter = appState.LastHotkeyConfig.PasteToChatDoubleEnter;
            PasteToChatDoubleEnterDelayMs = appState.LastHotkeyConfig.PasteToChatDoubleEnterDelayMs;
            PasteToChatAlwaysLeader = appState.LastHotkeyConfig.PasteToChatAlwaysLeader;
            if (HotkeysActive) RegisterAllHotkeys();
        }

        if (appState.ActiveProfileName is not null)
        {
            _pendingProfileName = appState.ActiveProfileName;
            SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileName == appState.ActiveProfileName);
        }

        // Toujours appliquer le merge — même sans fenêtres détectées.
        // Les personnages apparaissent grisés immédiatement au démarrage.
        // Quand les fenêtres sont détectées par le polling, elles se reconnectent automatiquement.
        var currentWindows = _detectionService.DetectedWindows;
        MergeWindowsWithSnapshot(currentWindows);

        var connected = Characters.Count(c => c.IsConnected);
        var connectingCount = currentWindows.Count(w => IsConnectingTitle(w.Title));
        string statusBase;

        if (_activeProfileName is not null)
        {
            statusBase = connected > 0
                ? $"Profil '{_activeProfileName}' restauré ({connected}/{Characters.Count} connectés)"
                : $"Profil '{_activeProfileName}' — en attente de fenêtres ({Characters.Count} personnages)";
            Logger.Information("Profil restauré au démarrage : {ProfileName}", _activeProfileName);
        }
        else if (snapshot is not null)
        {
            statusBase = connected > 0
                ? $"Configuration restaurée ({connected}/{Characters.Count} connectés)"
                : $"Configuration restaurée — en attente de fenêtres ({Characters.Count} personnages)";
            Logger.Information("Configuration restaurée depuis la dernière session");
        }
        else
        {
            statusBase = $"{Profiles.Count} profil(s) chargé(s)";
        }

        StatusText = connectingCount > 0
            ? $"{statusBase} + {connectingCount} en cours de connexion"
            : statusBase;
    }

    /// <summary>
    /// Sauvegarde l'état de la session actuelle (profil actif + config hotkeys).
    /// Appelé à la fermeture de l'application.
    /// </summary>
    public async Task SaveSessionStateAsync()
    {
        try
        {
            // Annuler le debounce en cours — on sauvegarde immédiatement
            _autoSaveCts?.Cancel();
            _autoSaveCts?.Dispose();
            _autoSaveCts = null;

            // Toujours sauvegarder un snapshot frais de l'état actuel
            var snapshot = Characters.Any(c => c.IsConnected)
                ? SnapshotCurrentProfile("__session__")
                : _sessionSnapshot; // garder le dernier snapshot connu si plus de fenêtres connectées

            var state = new AppState
            {
                ActiveProfileName = _activeProfileName,
                SessionSnapshot = snapshot,
                IsTopmost = IsTopmost
            };
            await _appStateService.SaveAsync(state);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur lors de la sauvegarde de l'état de session");
        }
    }

    // ===== POLLING =====

    [RelayCommand]
    private void Refresh()
    {
        Logger.Information("Scan manuel déclenché");
        var windows = _detectionService.DetectOnce();
        MergeWindowsWithSnapshot(windows);
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

    // ===== HOTKEYS =====

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
        }
        else
        {
            RegisterAllHotkeys();
            HotkeysActive = true;
            StatusText = $"Raccourcis activés ({_hotkeyService.RegisteredHotkeys.Count} enregistrés)";
        }
    }

    /// <summary>
    /// Enregistre tous les hotkeys : slots + globaux. Data-driven depuis les ViewModels.
    /// </summary>
    private void RegisterAllHotkeys()
    {
        if (!_hotkeyService.IsInitialized) return;

        _hotkeyService.UnregisterAll();

        // Slot hotkeys (uniquement les connectés)
        for (var i = 0; i < Characters.Count; i++)
        {
            var row = Characters[i];
            if (!row.IsConnected || row.VirtualKeyCode == 0) continue;

            _hotkeyService.Register(new HotkeyBinding
            {
                Id = i + 1,
                Modifiers = (HotkeyModifiers)row.HotkeyModifiers,
                VirtualKeyCode = row.VirtualKeyCode,
                DisplayName = row.HotkeyDisplay,
                Action = HotkeyAction.FocusSlot,
                SlotIndex = i
            });
        }

        // Global hotkeys
        foreach (var gh in GlobalHotkeys)
        {
            if (gh.VirtualKeyCode == 0) continue;

            var registered = _hotkeyService.Register(new HotkeyBinding
            {
                Id = gh.HotkeyId,
                Modifiers = (HotkeyModifiers)gh.HotkeyModifiers,
                VirtualKeyCode = gh.VirtualKeyCode,
                DisplayName = gh.HotkeyDisplay,
                Action = gh.Action
            });

            // Fallback pour Ctrl+Tab / Ctrl+Shift+Tab (réservés par le système)
            if (!registered && gh.VirtualKeyCode == VK_TAB)
            {
                var fallbackDisplay = gh.HotkeyDisplay.Replace("Tab", "Espace");
                Logger.Warning("{Display} réservé, fallback vers {Fallback}", gh.HotkeyDisplay, fallbackDisplay);
                _hotkeyService.Register(new HotkeyBinding
                {
                    Id = gh.HotkeyId,
                    Modifiers = (HotkeyModifiers)gh.HotkeyModifiers,
                    VirtualKeyCode = VK_SPACE,
                    DisplayName = fallbackDisplay,
                    Action = gh.Action
                });
            }
        }

        Logger.Information("{Count} raccourcis enregistrés", _hotkeyService.RegisteredHotkeys.Count);
    }

    /// <summary>
    /// Appelé quand un raccourci de slot ou global est modifié par l'utilisateur.
    /// </summary>
    public void OnHotkeyConfigChanged()
    {
        if (_applyingProfile) return;
        if (HotkeysActive)
            RegisterAllHotkeys();
        UpdateSessionSnapshot();
    }

    // ===== CHARACTERS (ordonnement, leader) =====

    /// <summary>
    /// Déplace un personnage d'un index à un autre (appelé par le drag-and-drop).
    /// </summary>
    public void MoveCharacter(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= Characters.Count) return;
        if (toIndex < 0 || toIndex >= Characters.Count) return;

        Characters.Move(fromIndex, toIndex);
        ReindexSlots();
        _focusService.UpdateSlots(GetCurrentDofusWindows());
        if (HotkeysActive) RegisterAllHotkeys();
        UpdateSessionSnapshot();
    }

    [RelayCommand]
    private void ToggleLeader(CharacterRowViewModel? row)
    {
        if (row is null) return;

        // Radio behavior : un seul leader
        foreach (var c in Characters)
            c.IsLeader = false;

        row.IsLeader = true;
        _focusService.SetLeader(row.Handle);
        _pushToBroadcastService.LeaderHandle = row.Handle;
        UpdateSessionSnapshot();
        StatusText = $"Leader : {row.DisplayName}";
        Logger.Information("Leader défini : {DisplayName}", row.DisplayName);
    }

    private void ReindexSlots()
    {
        for (var i = 0; i < Characters.Count; i++)
        {
            var c = Characters[i];
            c.SlotIndex = i;

            // Réassigner le hotkey par défaut si le slot n'a pas de hotkey custom
            var def = HotkeyDefaults.GetDefaultSlotHotkey(i);
            if (def.HasValue && c.VirtualKeyCode >= VK_F1 && c.VirtualKeyCode <= VK_F1 + 7)
            {
                c.HotkeyModifiers = (uint)def.Value.Modifiers;
                c.VirtualKeyCode = def.Value.VirtualKeyCode;
                c.HotkeyDisplay = def.Value.DisplayName;
            }
        }
    }

    // ===== PROFILS =====

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private async Task CreateProfile()
    {
        var profile = SnapshotCurrentProfile(NewProfileName.Trim());

        try
        {
            _profileService.CreateProfile(profile);
            await _profileService.SaveAsync();
            _activeProfileName = profile.ProfileName;
            NewProfileName = string.Empty;
            StatusText = $"Profil '{profile.ProfileName}' créé ({profile.Slots.Count} slots)";
            Logger.Information("Profil créé : {ProfileName}", profile.ProfileName);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
        }
    }

    private bool CanCreateProfile() => !string.IsNullOrWhiteSpace(NewProfileName);

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void LoadProfile()
    {
        if (SelectedProfile is null) return;

        var profile = _profileService.GetProfile(SelectedProfile.ProfileName);
        if (profile is null)
        {
            StatusText = "Profil introuvable";
            return;
        }

        ApplyProfile(profile);
        _activeProfileName = profile.ProfileName;
        StatusText = $"Profil '{profile.ProfileName}' chargé";
        Logger.Information("Profil chargé : {ProfileName}", profile.ProfileName);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private async Task SaveProfile()
    {
        if (SelectedProfile is null) return;

        var profile = _profileService.GetProfile(SelectedProfile.ProfileName);
        if (profile is null) return;

        var snapshot = SnapshotCurrentProfile(profile.ProfileName);
        snapshot.CreatedAt = profile.CreatedAt;
        _profileService.UpdateProfile(snapshot);
        await _profileService.SaveAsync();
        StatusText = $"Profil '{profile.ProfileName}' sauvegardé";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private async Task DeleteProfile()
    {
        if (SelectedProfile is null) return;

        var name = SelectedProfile.ProfileName;
        _profileService.DeleteProfile(name);
        await _profileService.SaveAsync();
        SelectedProfile = null;
        if (_activeProfileName == name)
            _activeProfileName = null;
        StatusText = $"Profil '{name}' supprimé";
        Logger.Information("Profil supprimé : {ProfileName}", name);
    }

    private bool HasSelectedProfile() => SelectedProfile is not null;

    [RelayCommand]
    private void ResetDefaults()
    {
        // Reset slot hotkeys to F1-F8
        for (var i = 0; i < Characters.Count; i++)
        {
            var def = HotkeyDefaults.GetDefaultSlotHotkey(i);
            var c = Characters[i];
            if (def.HasValue)
            {
                c.HotkeyModifiers = (uint)def.Value.Modifiers;
                c.VirtualKeyCode = def.Value.VirtualKeyCode;
                c.HotkeyDisplay = def.Value.DisplayName;
            }
            else
            {
                c.HotkeyModifiers = 0;
                c.VirtualKeyCode = 0;
                c.HotkeyDisplay = string.Empty;
            }
        }

        // Reset global hotkeys + broadcast key
        var defaults = GlobalHotkeyConfig.CreateDefault();
        InitializeGlobalHotkeys(defaults);
        InitializeBroadcastKey(defaults.BroadcastKey);
        ReturnToLeaderAfterBroadcast = false;
        _pushToBroadcastService.ReturnToLeaderAfterBroadcast = false;
        BroadcastDelayMs = 65;
        _pushToBroadcastService.BroadcastDelayMs = 65;
        BroadcastDelayRandomMs = 25;
        _pushToBroadcastService.BroadcastDelayRandomMs = 25;
        PasteToChatDoubleEnter = false;
        PasteToChatDoubleEnterDelayMs = 500;
        PasteToChatAlwaysLeader = false;

        if (HotkeysActive) RegisterAllHotkeys();
        _activeProfileName = null;
        StatusText = "Raccourcis réinitialisés aux valeurs par défaut";
        Logger.Information("Raccourcis réinitialisés");
    }

    // ===== PUSH-TO-BROADCAST =====

    [RelayCommand]
    public void TogglePushToBroadcast()
    {
        if (_listeningMode)
            StopListening();
        else
            StartListening();
    }

    private void StartListening()
    {
        _listeningMode = true;
        IsListening = true;
        StatusText = $"Push-to-Broadcast actif — maintenez {BroadcastKeyDisplay} pour broadcaster les clics";

        _altPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _altPollTimer.Tick += OnAltPollTimerTick;
        _altPollTimer.Start();

        Logger.Information("Push-to-Broadcast activé");
    }

    private void StopListening()
    {
        if (IsArmed)
        {
            _pushToBroadcastService.Disarm();
            IsArmed = false;
        }

        StopAltPollTimer();
        _listeningMode = false;
        IsListening = false;
        StatusText = "Push-to-Broadcast désactivé";
        Logger.Information("Push-to-Broadcast désactivé");
    }

    private void OnAltPollTimerTick(object? sender, EventArgs e)
    {
        var keyDown = _windowHelper.IsKeyDown((int)BroadcastKeyVirtualKeyCode);

        if (keyDown && !IsArmed)
        {
            var windows = _detectionService.DetectedWindows;
            if (windows.Count == 0) return;

            _pushToBroadcastService.LeaderHandle = _focusService.CurrentLeader?.Handle;
            _pushToBroadcastService.Arm(windows);
            IsArmed = true;
            StatusText = $"Push-to-Broadcast ARMÉ ({windows.Count} fenêtres)";
        }
        else if (!keyDown && IsArmed)
        {
            _pushToBroadcastService.Disarm();
            IsArmed = false;
            StatusText = $"Push-to-Broadcast — en attente de {BroadcastKeyDisplay}";
        }
    }

    private void StopAltPollTimer()
    {
        if (_altPollTimer is not null)
        {
            _altPollTimer.Tick -= OnAltPollTimerTick;
            _altPollTimer.Stop();
            _altPollTimer = null;
        }
    }

    // ===== GROUPE =====

    [RelayCommand]
    private async Task InviteAll()
    {
        var windows = _detectionService.DetectedWindows;
        var leader = _focusService.CurrentLeader;

        if (leader is null)
        {
            StatusText = "Aucun leader désigné — impossible d'inviter";
            return;
        }

        if (windows.Count <= 1)
        {
            StatusText = "Pas assez de fenêtres pour inviter";
            return;
        }

        StatusText = "Invitation du groupe en cours...";

        try
        {
            var result = await _groupInviteService.InviteAllAsync(windows, leader);
            _dispatcher.Invoke(() =>
            {
                StatusText = result.Success
                    ? $"Groupe : {result.Invited} personnage(s) invité(s)"
                    : $"Invitation échouée : {result.ErrorMessage}";
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur lors de l'invitation du groupe");
            StatusText = $"Erreur : {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleAutoFollow()
    {
        var windows = _detectionService.DetectedWindows;
        var leader = _focusService.CurrentLeader;

        if (leader is null)
        {
            StatusText = "Aucun leader désigné — impossible de toggle autofollow";
            return;
        }

        if (windows.Count <= 1)
        {
            StatusText = "Pas assez de fenêtres pour autofollow";
            return;
        }

        StatusText = "Toggle autofollow en cours...";

        try
        {
            var result = await _groupInviteService.ToggleAutoFollowAsync(windows, leader);
            _dispatcher.Invoke(() =>
            {
                StatusText = result.Success
                    ? $"Autofollow : Ctrl+W envoyé à {result.Invited} fenêtre(s)"
                    : $"Autofollow échoué : {result.ErrorMessage}";
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur lors du toggle autofollow");
            StatusText = $"Erreur : {ex.Message}";
        }
    }

    // ===== INTERNAL HELPERS =====

    private void OnWindowsChanged(object? sender, WindowsChangedEventArgs e)
    {
        _dispatcher.Invoke(() => MergeWindowsWithSnapshot(e.Current));
    }

    /// <summary>
    /// Algorithme unifié de merge : matche les fenêtres détectées contre le snapshot de session
    /// (ou profil pending au démarrage). Les slots non matchés apparaissent grisés (déconnectés).
    /// Les fenêtres non matchées sont ajoutées en fin de liste.
    /// </summary>
    private void MergeWindowsWithSnapshot(IReadOnlyList<DofusWindow> currentWindows)
    {
        // Séparer fenêtres prêtes (titre personnage) et en cours de connexion (titre générique)
        var readyWindows = currentWindows.Where(w => !IsConnectingTitle(w.Title)).ToList();
        var connectingCount = currentWindows.Count - readyWindows.Count;

        // Consommer le pending profile name si présent (startup)
        if (_pendingProfileName is not null && _sessionSnapshot is null)
        {
            var pendingProfile = _profileService.GetProfile(_pendingProfileName);
            if (pendingProfile is not null)
                _sessionSnapshot = pendingProfile;
            _activeProfileName = _pendingProfileName;
            _pendingProfileName = null;
        }

        if (_sessionSnapshot is not null)
        {
            // Phase 1 : Matcher chaque slot du snapshot contre les fenêtres prêtes
            var allRows = new List<CharacterRowViewModel>();
            var usedHandles = new HashSet<nint>();
            nint leaderHandle = 0;

            _applyingProfile = true;
            try
            {
                foreach (var slot in _sessionSnapshot.Slots.OrderBy(s => s.Index))
                {
                    uint modifiers = slot.FocusHotkeyModifiers;
                    uint vk = slot.FocusHotkeyVirtualKeyCode;
                    string display = slot.FocusHotkey ?? string.Empty;

                    if (vk == 0)
                    {
                        var def = HotkeyDefaults.GetDefaultSlotHotkey(slot.Index);
                        if (def.HasValue)
                        {
                            modifiers = (uint)def.Value.Modifiers;
                            vk = def.Value.VirtualKeyCode;
                            display = def.Value.DisplayName;
                        }
                    }

                    var match = readyWindows.FirstOrDefault(w =>
                        GlobMatcher.IsMatch(slot.WindowTitlePattern, w.Title) &&
                        !usedHandles.Contains(w.Handle));

                    if (match is not null)
                    {
                        usedHandles.Add(match.Handle);
                        allRows.Add(new CharacterRowViewModel(this)
                        {
                            Handle = match.Handle,
                            SlotIndex = allRows.Count,
                            Title = match.Title,
                            ProcessId = match.ProcessId,
                            IsLeader = slot.IsLeader,
                            HotkeyModifiers = modifiers,
                            VirtualKeyCode = vk,
                            HotkeyDisplay = display
                        });

                        if (slot.IsLeader)
                            leaderHandle = match.Handle;
                    }
                    else
                    {
                        // Déconnecté — placeholder grisé
                        allRows.Add(new CharacterRowViewModel(this)
                        {
                            Handle = 0,
                            SlotIndex = allRows.Count,
                            Title = slot.CharacterName,
                            ProcessId = 0,
                            IsLeader = slot.IsLeader,
                            HotkeyModifiers = modifiers,
                            VirtualKeyCode = vk,
                            HotkeyDisplay = display
                        });
                    }
                }

                // Phase 2 : Ajouter les fenêtres prêtes non matchées en fin de liste (nouvelles fenêtres)
                foreach (var w in readyWindows)
                {
                    if (!usedHandles.Contains(w.Handle))
                    {
                        var slotIndex = allRows.Count;
                        var def = HotkeyDefaults.GetDefaultSlotHotkey(slotIndex);
                        allRows.Add(new CharacterRowViewModel(this)
                        {
                            Handle = w.Handle,
                            SlotIndex = slotIndex,
                            Title = w.Title,
                            ProcessId = w.ProcessId,
                            HotkeyModifiers = def.HasValue ? (uint)def.Value.Modifiers : 0,
                            VirtualKeyCode = def.HasValue ? def.Value.VirtualKeyCode : 0,
                            HotkeyDisplay = def?.DisplayName ?? string.Empty
                        });
                    }
                }

                // Remplacer la liste des personnages
                Characters.Clear();
                foreach (var row in allRows)
                    Characters.Add(row);

                // Phase 3 : Mettre à jour FocusService, leader, hotkeys
                _focusService.UpdateSlots(GetCurrentDofusWindows());
                if (leaderHandle != 0)
                {
                    _focusService.SetLeader(leaderHandle);
                    _pushToBroadcastService.LeaderHandle = leaderHandle;
                }

                InitializeGlobalHotkeys(_sessionSnapshot.GlobalHotkeys);
                InitializeBroadcastKey(_sessionSnapshot.GlobalHotkeys.BroadcastKey);
                ReturnToLeaderAfterBroadcast = _sessionSnapshot.GlobalHotkeys.ReturnToLeaderAfterBroadcast;
                _pushToBroadcastService.ReturnToLeaderAfterBroadcast = ReturnToLeaderAfterBroadcast;
                BroadcastDelayMs = _sessionSnapshot.GlobalHotkeys.BroadcastDelayMs;
                _pushToBroadcastService.BroadcastDelayMs = BroadcastDelayMs;
                BroadcastDelayRandomMs = _sessionSnapshot.GlobalHotkeys.BroadcastDelayRandomMs;
                _pushToBroadcastService.BroadcastDelayRandomMs = BroadcastDelayRandomMs;
                PasteToChatDoubleEnter = _sessionSnapshot.GlobalHotkeys.PasteToChatDoubleEnter;
                PasteToChatDoubleEnterDelayMs = _sessionSnapshot.GlobalHotkeys.PasteToChatDoubleEnterDelayMs;
                PasteToChatAlwaysLeader = _sessionSnapshot.GlobalHotkeys.PasteToChatAlwaysLeader;

                if (HotkeysActive)
                    RegisterAllHotkeys();
            }
            finally
            {
                _applyingProfile = false;
            }

            var connected = Characters.Count(c => c.IsConnected);
            var statusBase = _activeProfileName is not null
                ? $"Profil '{_activeProfileName}' — {connected}/{Characters.Count} connectés"
                : $"{connected}/{Characters.Count} personnage(s) connecté(s)";
            StatusText = connectingCount > 0
                ? $"{statusBase} + {connectingCount} en cours de connexion"
                : statusBase;
        }
        else
        {
            // Pas de snapshot — mode premier lancement
            SyncCharactersFresh(currentWindows, connectingCount);
        }

        // Capturer l'état et auto-sauvegarder
        UpdateSessionSnapshot();
    }

    /// <summary>
    /// Synchronisation simple quand aucun snapshot n'existe (premier lancement).
    /// Préserve l'ordre existant, ajoute les nouvelles fenêtres en fin, supprime les disparues.
    /// Les fenêtres en cours de connexion (titre générique) sont ignorées.
    /// </summary>
    private void SyncCharactersFresh(IReadOnlyList<DofusWindow> windows, int connectingCount)
    {
        var existingHandles = Characters
            .Where(c => c.IsConnected)
            .Select(c => c.Handle)
            .ToHashSet();
        var currentHandles = windows.Select(w => w.Handle).ToHashSet();

        // Supprimer les connectés qui ont disparu ou dont le titre est devenu générique
        for (var i = Characters.Count - 1; i >= 0; i--)
        {
            if (Characters[i].IsConnected &&
                (!currentHandles.Contains(Characters[i].Handle) || IsConnectingTitle(Characters[i].Title)))
                Characters.RemoveAt(i);
        }

        // Mettre à jour les titres des fenêtres existantes
        foreach (var c in Characters)
        {
            var updated = windows.FirstOrDefault(w => w.Handle == c.Handle);
            if (updated is not null)
                c.Title = updated.Title;
        }

        // Ajouter les nouvelles fenêtres prêtes en fin (ignorer les titres génériques)
        foreach (var w in windows)
        {
            if (!existingHandles.Contains(w.Handle) && !IsConnectingTitle(w.Title))
            {
                var slotIndex = Characters.Count;
                var def = HotkeyDefaults.GetDefaultSlotHotkey(slotIndex);
                Characters.Add(new CharacterRowViewModel(this)
                {
                    Handle = w.Handle,
                    SlotIndex = slotIndex,
                    Title = w.Title,
                    ProcessId = w.ProcessId,
                    HotkeyModifiers = def.HasValue ? (uint)def.Value.Modifiers : 0,
                    VirtualKeyCode = def.HasValue ? def.Value.VirtualKeyCode : 0,
                    HotkeyDisplay = def?.DisplayName ?? string.Empty
                });
            }
        }

        // Mettre à jour le FocusService
        _focusService.UpdateSlots(GetCurrentDofusWindows());

        // Leader par défaut : le premier connecté si aucun leader n'est défini
        var leaderRow = Characters.FirstOrDefault(c => c.IsLeader && c.IsConnected);
        if (leaderRow is null)
        {
            var firstConnected = Characters.FirstOrDefault(c => c.IsConnected);
            if (firstConnected is not null)
            {
                firstConnected.IsLeader = true;
                leaderRow = firstConnected;
            }
        }

        if (leaderRow is not null)
        {
            _focusService.SetLeader(leaderRow.Handle);
            _pushToBroadcastService.LeaderHandle = leaderRow.Handle;
        }

        var connectedCount = Characters.Count(c => c.IsConnected);
        var statusBase = $"{connectedCount} fenêtre(s) Dofus détectée(s)";
        StatusText = connectingCount > 0
            ? $"{statusBase} + {connectingCount} en cours de connexion"
            : statusBase;
    }

    /// <summary>
    /// Met à jour le snapshot de session implicite (ordre des slots, leader, hotkeys)
    /// et planifie une sauvegarde automatique sur disque.
    /// </summary>
    private void UpdateSessionSnapshot()
    {
        if (_applyingProfile) return;
        if (Characters.Count > 0)
            _sessionSnapshot = SnapshotCurrentProfile("__session__");
        ScheduleAutoSave();
    }

    /// <summary>
    /// Planifie une sauvegarde automatique sur disque avec debounce.
    /// Annule le save précédent si appelé à nouveau avant le délai.
    /// </summary>
    private void ScheduleAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _autoSaveCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(AutoSaveDelay, cts.Token);
                var snapshot = _sessionSnapshot;
                var state = new AppState
                {
                    ActiveProfileName = _activeProfileName,
                    SessionSnapshot = snapshot,
                    IsTopmost = IsTopmost
                };
                await _appStateService.SaveAsync(state);
                Logger.Debug("Auto-save effectué");
            }
            catch (TaskCanceledException)
            {
                // Debounce annulé, ignoré
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Erreur lors de l'auto-save");
            }
        }, CancellationToken.None);
    }

    private IReadOnlyList<DofusWindow> GetCurrentDofusWindows()
    {
        // Reconstruire la liste ordonnée de DofusWindow depuis les rows
        var windows = _detectionService.DetectedWindows;
        return Characters
            .Select(c => windows.FirstOrDefault(w => w.Handle == c.Handle))
            .Where(w => w is not null)
            .ToList()!;
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            if (e.Binding.Action == HotkeyAction.PasteToChat)
            {
                StatusText = "Collage dans le chat en cours...";
                var allWindows = _detectionService.DetectedWindows;
                nint targetHandle;
                if (PasteToChatAlwaysLeader)
                {
                    // Toujours coller dans la fenêtre du leader
                    targetHandle = _focusService.CurrentLeader?.Handle ?? 0;
                }
                else
                {
                    // Capturer la fenêtre sous le curseur MAINTENANT (thread UI)
                    // WindowFromPoint peut retourner un child/overlay → fallback GetForegroundWindow
                    var cursorPos = _windowHelper.GetCursorPos();
                    var pointHandle = cursorPos is not null
                        ? _windowHelper.GetWindowFromPoint(cursorPos.Value.X, cursorPos.Value.Y)
                        : (nint)0;
                    targetHandle = allWindows.Any(w => w.Handle == pointHandle)
                        ? pointHandle
                        : _windowHelper.GetForegroundWindow();
                }
                _ = Task.Run(async () =>
                {
                    var targetWindow = allWindows.FirstOrDefault(w => w.Handle == targetHandle);
                    if (targetWindow is null)
                    {
                        var msg = PasteToChatAlwaysLeader
                            ? "Aucun leader désigné"
                            : "Aucune fenêtre Dofus sous le curseur";
                        _dispatcher.Invoke(() => StatusText = msg);
                        return;
                    }
                    var windows = new[] { targetWindow };
                    var leader = _focusService.CurrentLeader;
                    var result = await _groupInviteService.PasteToChatAsync(windows, leader, PasteToChatDoubleEnter, PasteToChatDoubleEnterDelayMs);
                    _dispatcher.Invoke(() =>
                    {
                        StatusText = result.Success
                            ? $"Collé dans {result.Invited} fenêtre(s)"
                            : $"Erreur : {result.ErrorMessage}";
                    });
                });
                return;
            }

            var focusResult = e.Binding.Action switch
            {
                HotkeyAction.FocusSlot => _focusService.FocusSlot(e.Binding.SlotIndex ?? 0),
                HotkeyAction.NextWindow => _focusService.FocusNext(),
                HotkeyAction.PreviousWindow => _focusService.FocusPrevious(),
                HotkeyAction.LastWindow => _focusService.FocusLast(),
                HotkeyAction.PanicLeader => _focusService.FocusLeader(),
                _ => FocusResult.Error("Action inconnue")
            };

            if (focusResult.Success)
                StatusText = $"{e.Binding.DisplayName} → slot {_focusService.CurrentSlotIndex}";
            else
            {
                StatusText = $"{e.Binding.DisplayName} : {focusResult.ErrorMessage}";
                Logger.Warning("Focus échoué : {Action} → {Error}", e.Binding.DisplayName, focusResult.ErrorMessage);
            }
        });
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        _dispatcher.Invoke(RefreshProfileList);
    }

    private void OnBroadcastPerformed(object? sender, int windowCount)
    {
        _dispatcher.Invoke(() =>
        {
            StatusText = $"Push-to-Broadcast : clic envoyé à {windowCount} fenêtre(s)";
        });
    }

    private void RefreshProfileList()
    {
        var selected = SelectedProfile?.ProfileName;
        Profiles.Clear();

        foreach (var profile in _profileService.GetAllProfiles())
        {
            Profiles.Add(new ProfileListItem
            {
                ProfileName = profile.ProfileName,
                SlotCount = profile.Slots.Count,
                LastModified = profile.LastModifiedAt
            });
        }

        if (selected is not null)
            SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileName == selected);
    }

    private void InitializeBroadcastKey(HotkeyBindingConfig config)
    {
        if (config.VirtualKeyCode != 0)
        {
            BroadcastKeyVirtualKeyCode = config.VirtualKeyCode;
            BroadcastKeyDisplay = config.DisplayName;
        }
        else
        {
            // Fallback vers Alt si la config est vide (profil legacy)
            BroadcastKeyVirtualKeyCode = 0x12;
            BroadcastKeyDisplay = "Alt";
        }
    }

    private void InitializeGlobalHotkeys(GlobalHotkeyConfig config)
    {
        GlobalHotkeys.Clear();
        GlobalHotkeys.Add(new GlobalHotkeyRowViewModel(this)
        {
            Label = "Fenêtre suivante",
            Action = HotkeyAction.NextWindow,
            HotkeyId = GlobalHotkeyBaseId,
            HotkeyModifiers = config.NextWindow.Modifiers,
            VirtualKeyCode = config.NextWindow.VirtualKeyCode,
            HotkeyDisplay = config.NextWindow.DisplayName
        });
        GlobalHotkeys.Add(new GlobalHotkeyRowViewModel(this)
        {
            Label = "Fenêtre précédente",
            Action = HotkeyAction.PreviousWindow,
            HotkeyId = GlobalHotkeyBaseId + 1,
            HotkeyModifiers = config.PreviousWindow.Modifiers,
            VirtualKeyCode = config.PreviousWindow.VirtualKeyCode,
            HotkeyDisplay = config.PreviousWindow.DisplayName
        });
        GlobalHotkeys.Add(new GlobalHotkeyRowViewModel(this)
        {
            Label = "Dernière fenêtre",
            Action = HotkeyAction.LastWindow,
            HotkeyId = GlobalHotkeyBaseId + 2,
            HotkeyModifiers = config.LastWindow.Modifiers,
            VirtualKeyCode = config.LastWindow.VirtualKeyCode,
            HotkeyDisplay = config.LastWindow.DisplayName
        });
        GlobalHotkeys.Add(new GlobalHotkeyRowViewModel(this)
        {
            Label = "Focus leader",
            Action = HotkeyAction.PanicLeader,
            HotkeyId = GlobalHotkeyBaseId + 3,
            HotkeyModifiers = config.FocusLeader.Modifiers,
            VirtualKeyCode = config.FocusLeader.VirtualKeyCode,
            HotkeyDisplay = config.FocusLeader.DisplayName
        });
        GlobalHotkeys.Add(new GlobalHotkeyRowViewModel(this)
        {
            Label = "Coller dans le chat",
            Action = HotkeyAction.PasteToChat,
            HotkeyId = GlobalHotkeyBaseId + 4,
            HotkeyModifiers = config.PasteToChat.Modifiers,
            VirtualKeyCode = config.PasteToChat.VirtualKeyCode,
            HotkeyDisplay = config.PasteToChat.DisplayName
        });
    }

    /// <summary>
    /// Snapshot l'état actuel en un Profile sérialisable.
    /// </summary>
    private Profile SnapshotCurrentProfile(string profileName)
    {
        var profile = new Profile { ProfileName = profileName };
        var slotIndex = 0;

        for (var i = 0; i < Characters.Count; i++)
        {
            var c = Characters[i];

            // Exclure les fenêtres connectées avec un titre générique (en cours de connexion)
            if (c.IsConnected && IsConnectingTitle(c.Title))
                continue;

            profile.Slots.Add(new ProfileSlot
            {
                Index = slotIndex++,
                CharacterName = c.Title,
                WindowTitlePattern = $"*{ExtractCharacterName(c.Title)}*",
                IsLeader = c.IsLeader,
                FocusHotkey = c.HotkeyDisplay,
                FocusHotkeyModifiers = c.HotkeyModifiers,
                FocusHotkeyVirtualKeyCode = c.VirtualKeyCode
            });
        }

        // Sauvegarder les raccourcis globaux + touche broadcast
        if (GlobalHotkeys.Count >= 5)
        {
            profile.GlobalHotkeys = new GlobalHotkeyConfig
            {
                NextWindow = ToBindingConfig(GlobalHotkeys[0]),
                PreviousWindow = ToBindingConfig(GlobalHotkeys[1]),
                LastWindow = ToBindingConfig(GlobalHotkeys[2]),
                FocusLeader = ToBindingConfig(GlobalHotkeys[3]),
                PasteToChat = ToBindingConfig(GlobalHotkeys[4]),
                BroadcastKey = new HotkeyBindingConfig
                {
                    DisplayName = BroadcastKeyDisplay,
                    Modifiers = 0,
                    VirtualKeyCode = BroadcastKeyVirtualKeyCode
                },
                ReturnToLeaderAfterBroadcast = ReturnToLeaderAfterBroadcast,
                BroadcastDelayMs = BroadcastDelayMs,
                BroadcastDelayRandomMs = BroadcastDelayRandomMs,
                PasteToChatDoubleEnter = PasteToChatDoubleEnter,
                PasteToChatDoubleEnterDelayMs = PasteToChatDoubleEnterDelayMs,
                PasteToChatAlwaysLeader = PasteToChatAlwaysLeader
            };
        }

        return profile;
    }

    private static HotkeyBindingConfig ToBindingConfig(GlobalHotkeyRowViewModel row) => new()
    {
        DisplayName = row.HotkeyDisplay,
        Modifiers = row.HotkeyModifiers,
        VirtualKeyCode = row.VirtualKeyCode
    };

    private void ApplyProfile(Profile profile)
    {
        _applyingProfile = true;
        try
        {
            var detectedWindows = _detectionService.DetectedWindows;
            var allRows = new List<CharacterRowViewModel>();
            var usedHandles = new HashSet<nint>();
            nint leaderHandle = 0;

            foreach (var slot in profile.Slots.OrderBy(s => s.Index))
            {
                // Déterminer le raccourci
                uint modifiers = slot.FocusHotkeyModifiers;
                uint vk = slot.FocusHotkeyVirtualKeyCode;
                string display = slot.FocusHotkey ?? string.Empty;

                if (vk == 0)
                {
                    var def = HotkeyDefaults.GetDefaultSlotHotkey(slot.Index);
                    if (def.HasValue)
                    {
                        modifiers = (uint)def.Value.Modifiers;
                        vk = def.Value.VirtualKeyCode;
                        display = def.Value.DisplayName;
                    }
                }

                var match = detectedWindows.FirstOrDefault(w =>
                    GlobMatcher.IsMatch(slot.WindowTitlePattern, w.Title) &&
                    !usedHandles.Contains(w.Handle));

                if (match is not null)
                {
                    // Connecté
                    usedHandles.Add(match.Handle);
                    allRows.Add(new CharacterRowViewModel(this)
                    {
                        Handle = match.Handle,
                        SlotIndex = allRows.Count,
                        Title = match.Title,
                        ProcessId = match.ProcessId,
                        IsLeader = slot.IsLeader,
                        HotkeyModifiers = modifiers,
                        VirtualKeyCode = vk,
                        HotkeyDisplay = display
                    });

                    if (slot.IsLeader)
                        leaderHandle = match.Handle;
                }
                else
                {
                    // Déconnecté — placeholder grisé
                    allRows.Add(new CharacterRowViewModel(this)
                    {
                        Handle = 0,
                        SlotIndex = allRows.Count,
                        Title = slot.CharacterName,
                        ProcessId = 0,
                        IsLeader = slot.IsLeader,
                        HotkeyModifiers = modifiers,
                        VirtualKeyCode = vk,
                        HotkeyDisplay = display
                    });
                }
            }

            // Remplacer la liste des personnages
            Characters.Clear();
            foreach (var row in allRows)
                Characters.Add(row);

            // Mettre à jour FocusService (uniquement les connectés)
            _focusService.UpdateSlots(GetCurrentDofusWindows());
            if (leaderHandle != 0)
            {
                _focusService.SetLeader(leaderHandle);
                _pushToBroadcastService.LeaderHandle = leaderHandle;
            }

            // Charger les raccourcis globaux + touche broadcast
            InitializeGlobalHotkeys(profile.GlobalHotkeys);
            InitializeBroadcastKey(profile.GlobalHotkeys.BroadcastKey);
            ReturnToLeaderAfterBroadcast = profile.GlobalHotkeys.ReturnToLeaderAfterBroadcast;
            _pushToBroadcastService.ReturnToLeaderAfterBroadcast = ReturnToLeaderAfterBroadcast;
            BroadcastDelayMs = profile.GlobalHotkeys.BroadcastDelayMs;
            _pushToBroadcastService.BroadcastDelayMs = BroadcastDelayMs;
            BroadcastDelayRandomMs = profile.GlobalHotkeys.BroadcastDelayRandomMs;
            _pushToBroadcastService.BroadcastDelayRandomMs = BroadcastDelayRandomMs;
            PasteToChatDoubleEnter = profile.GlobalHotkeys.PasteToChatDoubleEnter;
            PasteToChatDoubleEnterDelayMs = profile.GlobalHotkeys.PasteToChatDoubleEnterDelayMs;
            PasteToChatAlwaysLeader = profile.GlobalHotkeys.PasteToChatAlwaysLeader;

            if (HotkeysActive)
                RegisterAllHotkeys();
        }
        finally
        {
            _applyingProfile = false;
        }
        UpdateSessionSnapshot();
    }

    private static string ExtractCharacterName(string windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle)) return windowTitle;
        var dashIndex = windowTitle.IndexOf(" - ", StringComparison.Ordinal);
        return dashIndex > 0 ? windowTitle[..dashIndex].Trim() : windowTitle.Trim();
    }

    /// <summary>
    /// Identifie les titres génériques d'une fenêtre Dofus en cours de connexion.
    /// "Dofus" → true, "Dofus - 3.4.18.19 - Release" → true, "Cuckoolo - Pandawa - ..." → false.
    /// </summary>
    internal static bool IsConnectingTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return true;
        var name = ExtractCharacterName(title);
        return name.Equals("Dofus", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Dofus ", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _detectionService.WindowsChanged -= OnWindowsChanged;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _profileService.ProfilesChanged -= OnProfilesChanged;
        _pushToBroadcastService.BroadcastPerformed -= OnBroadcastPerformed;
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        StopAltPollTimer();
        _hotkeyService.Dispose();
        GC.SuppressFinalize(this);
    }
}

// ===== Row ViewModels =====

public partial class CharacterRowViewModel : ObservableObject
{
    private readonly DashboardViewModel _parent;

    public CharacterRowViewModel(DashboardViewModel parent) => _parent = parent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private nint _handle;

    [ObservableProperty]
    private int _processId;

    /// <summary>
    /// True si une fenêtre Dofus est associée à ce slot, false si le personnage est attendu mais déconnecté.
    /// </summary>
    public bool IsConnected => Handle != 0;

    [ObservableProperty]
    private int _slotIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(ClassName))]
    [NotifyPropertyChangedFor(nameof(ClassIcon))]
    private string _title = string.Empty;

    /// <summary>
    /// Nom du personnage extrait du titre (sans classe ni version).
    /// Ex: "Cuckoolo - Pandawa - 3.4.18.19 - Release" → "Cuckoolo"
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return string.Empty;
            var dashIndex = Title.IndexOf(" - ", StringComparison.Ordinal);
            return dashIndex > 0 ? Title[..dashIndex].Trim() : Title.Trim();
        }
    }

    /// <summary>
    /// Classe du personnage extraite du titre.
    /// Ex: "Cuckoolo - Pandawa - 3.4.18.19 - Release" → "Pandawa"
    /// </summary>
    public string? ClassName => DofusClassHelper.ExtractClassName(Title);

    /// <summary>
    /// Icône de classe (portrait) correspondant au personnage.
    /// </summary>
    public System.Windows.Media.Imaging.BitmapImage? ClassIcon => DofusClassHelper.GetClassIcon(Title);

    [ObservableProperty]
    private bool _isLeader;

    [ObservableProperty]
    private uint _hotkeyModifiers;

    [ObservableProperty]
    private uint _virtualKeyCode;

    [ObservableProperty]
    private string _hotkeyDisplay = string.Empty;

    partial void OnHotkeyModifiersChanged(uint value) => _parent.OnHotkeyConfigChanged();
    partial void OnVirtualKeyCodeChanged(uint value) => _parent.OnHotkeyConfigChanged();
}

public partial class GlobalHotkeyRowViewModel : ObservableObject
{
    private readonly DashboardViewModel _parent;

    public GlobalHotkeyRowViewModel(DashboardViewModel parent) => _parent = parent;

    public required string Label { get; init; }
    public required HotkeyAction Action { get; init; }
    public required int HotkeyId { get; init; }

    [ObservableProperty]
    private uint _hotkeyModifiers;

    [ObservableProperty]
    private uint _virtualKeyCode;

    [ObservableProperty]
    private string _hotkeyDisplay = string.Empty;

    partial void OnHotkeyModifiersChanged(uint value) => _parent.OnHotkeyConfigChanged();
    partial void OnVirtualKeyCodeChanged(uint value) => _parent.OnHotkeyConfigChanged();
}

public class ProfileListItem
{
    public required string ProfileName { get; init; }
    public int SlotCount { get; init; }
    public DateTime LastModified { get; init; }
}
