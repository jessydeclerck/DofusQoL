using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DofusManager.Core.Helpers;
using DofusManager.Core.Models;
using DofusManager.Core.Services;
using Serilog;

namespace DofusManager.UI.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<ProfileViewModel>();

    private readonly IProfileService _profileService;
    private readonly IWindowDetectionService _detectionService;
    private readonly IFocusService _focusService;
    private readonly HotkeyViewModel _hotkeyViewModel;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<ProfileListItem> Profiles { get; } = [];
    public ObservableCollection<ProfileSlotDisplayItem> Slots { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    private ProfileListItem? _selectedProfile;

    [ObservableProperty]
    private string _statusText = "Aucun profil chargé";

    public ProfileViewModel(
        IProfileService profileService,
        IWindowDetectionService detectionService,
        IFocusService focusService,
        HotkeyViewModel hotkeyViewModel)
    {
        _profileService = profileService;
        _detectionService = detectionService;
        _focusService = focusService;
        _hotkeyViewModel = hotkeyViewModel;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _profileService.ProfilesChanged += OnProfilesChanged;
    }

    /// <summary>
    /// Charge les profils depuis le fichier par défaut au démarrage.
    /// </summary>
    public async Task InitializeAsync()
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

    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private async Task CreateProfile()
    {
        var windows = _detectionService.DetectedWindows;

        var profile = new Profile
        {
            ProfileName = NewProfileName.Trim()
        };

        // Créer les slots à partir des fenêtres actuellement détectées
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            var isLeader = _focusService.CurrentLeader?.Handle == w.Handle;
            profile.Slots.Add(new ProfileSlot
            {
                Index = i,
                CharacterName = w.Title,
                WindowTitlePattern = $"*{ExtractCharacterName(w.Title)}*",
                IsLeader = isLeader,
                FocusHotkey = i < 8 ? $"F{i + 1}" : null
            });
        }

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

        // Mettre à jour les slots avec l'état actuel
        var windows = _detectionService.DetectedWindows;
        profile.Slots.Clear();
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            var isLeader = _focusService.CurrentLeader?.Handle == w.Handle;
            profile.Slots.Add(new ProfileSlot
            {
                Index = i,
                CharacterName = w.Title,
                WindowTitlePattern = $"*{ExtractCharacterName(w.Title)}*",
                IsLeader = isLeader,
                FocusHotkey = i < 8 ? $"F{i + 1}" : null
            });
        }

        _profileService.UpdateProfile(profile);
        await _profileService.SaveAsync();
        UpdateSlotDisplay(profile);
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
        Slots.Clear();
        StatusText = $"Profil '{name}' supprimé";
        Logger.Information("Profil supprimé : {ProfileName}", name);
    }

    private bool HasSelectedProfile() => SelectedProfile is not null;

    private void ApplyProfile(Profile profile)
    {
        var detectedWindows = _detectionService.DetectedWindows;
        var matchedWindows = new List<DofusWindow>();
        nint leaderHandle = 0;

        foreach (var slot in profile.Slots.OrderBy(s => s.Index))
        {
            var match = detectedWindows.FirstOrDefault(w =>
                GlobMatcher.IsMatch(slot.WindowTitlePattern, w.Title) &&
                !matchedWindows.Contains(w));

            if (match is not null)
            {
                matchedWindows.Add(match);
                if (slot.IsLeader)
                {
                    leaderHandle = match.Handle;
                }
            }
        }

        // Appliquer les slots au FocusService
        _focusService.UpdateSlots(matchedWindows);

        if (leaderHandle != 0)
        {
            _focusService.SetLeader(leaderHandle);
        }

        // Sync le HotkeyViewModel
        _hotkeyViewModel.SyncSlots(matchedWindows);

        UpdateSlotDisplay(profile);
    }

    private void UpdateSlotDisplay(Profile profile)
    {
        Slots.Clear();
        foreach (var slot in profile.Slots.OrderBy(s => s.Index))
        {
            Slots.Add(new ProfileSlotDisplayItem
            {
                Index = slot.Index,
                CharacterName = slot.CharacterName,
                WindowTitlePattern = slot.WindowTitlePattern,
                IsLeader = slot.IsLeader,
                FocusHotkey = slot.FocusHotkey ?? "—"
            });
        }
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        _dispatcher.Invoke(RefreshProfileList);
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
        {
            SelectedProfile = Profiles.FirstOrDefault(p => p.ProfileName == selected);
        }
    }

    /// <summary>
    /// Extrait le nom du personnage du titre de la fenêtre Dofus.
    /// Ex: "Dofus - Panda-Main" → "Panda-Main"
    /// </summary>
    private static string ExtractCharacterName(string windowTitle)
    {
        const string prefix = "Dofus - ";
        if (windowTitle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return windowTitle[prefix.Length..];
        }
        return windowTitle;
    }
}

public class ProfileListItem
{
    public required string ProfileName { get; init; }
    public int SlotCount { get; init; }
    public DateTime LastModified { get; init; }
}

public class ProfileSlotDisplayItem
{
    public int Index { get; init; }
    public required string CharacterName { get; init; }
    public required string WindowTitlePattern { get; init; }
    public bool IsLeader { get; init; }
    public required string FocusHotkey { get; init; }
}
