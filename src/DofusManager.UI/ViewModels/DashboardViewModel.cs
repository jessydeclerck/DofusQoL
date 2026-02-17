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
    private readonly IPushToBroadcastService _pushToBroadcastService;
    private readonly IGroupInviteService _groupInviteService;
    private readonly IWin32WindowHelper _windowHelper;
    private readonly Dispatcher _dispatcher;

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

    public string PushToBroadcastButtonText => IsListening ? "Désactiver" : "Activer";
    public string PushToBroadcastIndicator => IsArmed
        ? $"{BroadcastKeyDisplay} maintenu"
        : IsListening
            ? $"En attente de {BroadcastKeyDisplay}"
            : "Inactif";
    public string BroadcastKeyHelpText => $"Maintenez {BroadcastKeyDisplay} : chaque clic est broadcasté.";

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

    // --- Status ---

    [ObservableProperty]
    private string _statusText = "Prêt";

    public DashboardViewModel(
        IWindowDetectionService detectionService,
        IHotkeyService hotkeyService,
        IFocusService focusService,
        IProfileService profileService,
        IPushToBroadcastService pushToBroadcastService,
        IGroupInviteService groupInviteService,
        IWin32WindowHelper windowHelper)
    {
        _detectionService = detectionService;
        _hotkeyService = hotkeyService;
        _focusService = focusService;
        _profileService = profileService;
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
            StatusText = $"{Profiles.Count} profil(s) chargé(s)";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur au chargement des profils");
            StatusText = "Erreur au chargement des profils";
        }
    }

    // ===== POLLING =====

    [RelayCommand]
    private void Refresh()
    {
        Logger.Information("Scan manuel déclenché");
        var windows = _detectionService.DetectOnce();
        SyncCharacters(windows);
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

        // Slot hotkeys
        for (var i = 0; i < Characters.Count; i++)
        {
            var row = Characters[i];
            if (row.VirtualKeyCode == 0) continue;

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
        if (HotkeysActive)
            RegisterAllHotkeys();
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

        if (HotkeysActive) RegisterAllHotkeys();
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
        _dispatcher.Invoke(() => SyncCharacters(e.Current));
    }

    /// <summary>
    /// Synchronise la liste des personnages avec les fenêtres détectées.
    /// Préserve l'ordre existant, ajoute les nouvelles en fin, supprime les disparues.
    /// </summary>
    private void SyncCharacters(IReadOnlyList<DofusWindow> windows)
    {
        var existingHandles = Characters.Select(c => c.Handle).ToHashSet();
        var currentHandles = windows.Select(w => w.Handle).ToHashSet();

        // Supprimer les disparues
        for (var i = Characters.Count - 1; i >= 0; i--)
        {
            if (!currentHandles.Contains(Characters[i].Handle))
                Characters.RemoveAt(i);
        }

        // Mettre à jour les titres des fenêtres existantes
        foreach (var c in Characters)
        {
            var updated = windows.FirstOrDefault(w => w.Handle == c.Handle);
            if (updated is not null)
                c.Title = updated.Title;
        }

        // Ajouter les nouvelles fenêtres en fin
        foreach (var w in windows)
        {
            if (!existingHandles.Contains(w.Handle))
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

        // Leader par défaut : le premier slot si aucun leader n'est défini
        var leaderRow = Characters.FirstOrDefault(c => c.IsLeader);
        if (leaderRow is null && Characters.Count > 0)
        {
            Characters[0].IsLeader = true;
            leaderRow = Characters[0];
        }

        if (leaderRow is not null)
            _focusService.SetLeader(leaderRow.Handle);

        StatusText = $"{Characters.Count} fenêtre(s) Dofus détectée(s)";
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
                StatusText = $"{e.Binding.DisplayName} → slot {_focusService.CurrentSlotIndex}";
            else
            {
                StatusText = $"{e.Binding.DisplayName} : {result.ErrorMessage}";
                Logger.Warning("Focus échoué : {Action} → {Error}", e.Binding.DisplayName, result.ErrorMessage);
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
    }

    /// <summary>
    /// Snapshot l'état actuel en un Profile sérialisable.
    /// </summary>
    private Profile SnapshotCurrentProfile(string profileName)
    {
        var profile = new Profile { ProfileName = profileName };

        for (var i = 0; i < Characters.Count; i++)
        {
            var c = Characters[i];
            profile.Slots.Add(new ProfileSlot
            {
                Index = i,
                CharacterName = c.Title,
                WindowTitlePattern = $"*{ExtractCharacterName(c.Title)}*",
                IsLeader = c.IsLeader,
                FocusHotkey = c.HotkeyDisplay,
                FocusHotkeyModifiers = c.HotkeyModifiers,
                FocusHotkeyVirtualKeyCode = c.VirtualKeyCode
            });
        }

        // Sauvegarder les raccourcis globaux + touche broadcast
        if (GlobalHotkeys.Count == 4)
        {
            profile.GlobalHotkeys = new GlobalHotkeyConfig
            {
                NextWindow = ToBindingConfig(GlobalHotkeys[0]),
                PreviousWindow = ToBindingConfig(GlobalHotkeys[1]),
                LastWindow = ToBindingConfig(GlobalHotkeys[2]),
                FocusLeader = ToBindingConfig(GlobalHotkeys[3]),
                BroadcastKey = new HotkeyBindingConfig
                {
                    DisplayName = BroadcastKeyDisplay,
                    Modifiers = 0,
                    VirtualKeyCode = BroadcastKeyVirtualKeyCode
                }
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
        var detectedWindows = _detectionService.DetectedWindows;
        var matchedRows = new List<CharacterRowViewModel>();
        nint leaderHandle = 0;

        foreach (var slot in profile.Slots.OrderBy(s => s.Index))
        {
            var match = detectedWindows.FirstOrDefault(w =>
                GlobMatcher.IsMatch(slot.WindowTitlePattern, w.Title) &&
                matchedRows.All(r => r.Handle != w.Handle));

            if (match is null) continue;

            // Déterminer le raccourci : d'abord les nouveaux champs, sinon fallback sur le défaut
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

            matchedRows.Add(new CharacterRowViewModel(this)
            {
                Handle = match.Handle,
                SlotIndex = matchedRows.Count,
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

        // Remplacer la liste des personnages
        Characters.Clear();
        foreach (var row in matchedRows)
            Characters.Add(row);

        // Mettre à jour FocusService
        _focusService.UpdateSlots(GetCurrentDofusWindows());
        if (leaderHandle != 0)
            _focusService.SetLeader(leaderHandle);

        // Charger les raccourcis globaux + touche broadcast
        InitializeGlobalHotkeys(profile.GlobalHotkeys);
        InitializeBroadcastKey(profile.GlobalHotkeys.BroadcastKey);

        if (HotkeysActive)
            RegisterAllHotkeys();
    }

    private static string ExtractCharacterName(string windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle)) return windowTitle;
        var dashIndex = windowTitle.IndexOf(" - ", StringComparison.Ordinal);
        return dashIndex > 0 ? windowTitle[..dashIndex].Trim() : windowTitle.Trim();
    }

    public void Dispose()
    {
        _detectionService.WindowsChanged -= OnWindowsChanged;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _profileService.ProfilesChanged -= OnProfilesChanged;
        _pushToBroadcastService.BroadcastPerformed -= OnBroadcastPerformed;
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

    public nint Handle { get; init; }
    public int ProcessId { get; init; }

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
