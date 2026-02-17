using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using DofusManager.UI.ViewModels;
using Moq;
using Xunit;

namespace DofusManager.Tests.ViewModels;

public class SessionPersistenceTests : IDisposable
{
    private readonly Mock<IWindowDetectionService> _mockDetection;
    private readonly Mock<IHotkeyService> _mockHotkey;
    private readonly Mock<IFocusService> _mockFocus;
    private readonly Mock<IProfileService> _mockProfile;
    private readonly Mock<IAppStateService> _mockAppState;
    private readonly Mock<IPushToBroadcastService> _mockBroadcast;
    private readonly Mock<IGroupInviteService> _mockGroupInvite;
    private readonly Mock<IWin32WindowHelper> _mockWindowHelper;

    private readonly DashboardViewModel _vm;

    public SessionPersistenceTests()
    {
        _mockDetection = new Mock<IWindowDetectionService>();
        _mockHotkey = new Mock<IHotkeyService>();
        _mockFocus = new Mock<IFocusService>();
        _mockProfile = new Mock<IProfileService>();
        _mockAppState = new Mock<IAppStateService>();
        _mockBroadcast = new Mock<IPushToBroadcastService>();
        _mockGroupInvite = new Mock<IGroupInviteService>();
        _mockWindowHelper = new Mock<IWin32WindowHelper>();

        _mockDetection.Setup(d => d.DetectedWindows).Returns(new List<DofusWindow>());
        _mockHotkey.Setup(h => h.RegisteredHotkeys).Returns(new List<HotkeyBinding>());

        _vm = new DashboardViewModel(
            _mockDetection.Object,
            _mockHotkey.Object,
            _mockFocus.Object,
            _mockProfile.Object,
            _mockAppState.Object,
            _mockBroadcast.Object,
            _mockGroupInvite.Object,
            _mockWindowHelper.Object);
    }

    public void Dispose()
    {
        _vm.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- Helpers ---

    private static List<DofusWindow> CreateWindows(params string[] names)
    {
        return names.Select((name, i) => new DofusWindow
        {
            Handle = (nint)((i + 1) * 100),
            ProcessId = i + 1,
            Title = $"{name} - Classe - 3.4.18.19 - Release",
            IsVisible = true,
            IsMinimized = false
        }).ToList();
    }

    private static Profile CreateSnapshot(params string[] names)
    {
        var profile = new Profile { ProfileName = "__session__" };
        for (var i = 0; i < names.Length; i++)
        {
            profile.Slots.Add(new ProfileSlot
            {
                Index = i,
                CharacterName = $"{names[i]} - Classe - 3.4.18.19 - Release",
                WindowTitlePattern = $"*{names[i]}*",
                IsLeader = i == 0
            });
        }
        return profile;
    }

    private async Task SetupSnapshotAndRestore(Profile snapshot, List<DofusWindow> windows, string? activeProfileName = null)
    {
        var appState = new AppState
        {
            ActiveProfileName = activeProfileName,
            SessionSnapshot = snapshot
        };
        _mockAppState.Setup(s => s.LoadAsync()).ReturnsAsync(appState);
        _mockDetection.Setup(d => d.DetectedWindows).Returns(windows);
        _mockProfile.Setup(p => p.GetAllProfiles()).Returns(new List<Profile>());

        await _vm.InitializeProfilesAsync();
    }

    // --- Tests : Merge avec snapshot ---

    [Fact]
    public async Task Merge_WithSnapshot_MatchedSlotsAreConnected()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var windows = CreateWindows("Cuckoolo", "Artega");

        await SetupSnapshotAndRestore(snapshot, windows);

        Assert.Equal(2, _vm.Characters.Count);
        Assert.True(_vm.Characters[0].IsConnected);
        Assert.True(_vm.Characters[1].IsConnected);
        Assert.Contains("Cuckoolo", _vm.Characters[0].Title);
        Assert.Contains("Artega", _vm.Characters[1].Title);
    }

    [Fact]
    public async Task Merge_WithSnapshot_UnmatchedSlotsAreGrayed()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega", "Xelor");
        var windows = CreateWindows("Cuckoolo"); // seul Cuckoolo est connecté

        await SetupSnapshotAndRestore(snapshot, windows);

        Assert.Equal(3, _vm.Characters.Count);
        Assert.True(_vm.Characters[0].IsConnected);
        Assert.False(_vm.Characters[1].IsConnected); // Artega grisé
        Assert.False(_vm.Characters[2].IsConnected); // Xelor grisé
    }

    [Fact]
    public async Task Merge_WithoutWindows_AllSlotsGrayed()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var windows = new List<DofusWindow>(); // aucune fenêtre

        await SetupSnapshotAndRestore(snapshot, windows);

        Assert.Equal(2, _vm.Characters.Count);
        Assert.All(_vm.Characters, c => Assert.False(c.IsConnected));
    }

    [Fact]
    public async Task Merge_NewWindowNotInSnapshot_AddedAtEnd()
    {
        var snapshot = CreateSnapshot("Cuckoolo");
        var windows = CreateWindows("Cuckoolo", "Artega"); // Artega est nouveau

        await SetupSnapshotAndRestore(snapshot, windows);

        Assert.Equal(2, _vm.Characters.Count);
        Assert.Contains("Cuckoolo", _vm.Characters[0].Title);
        Assert.Contains("Artega", _vm.Characters[1].Title);
        Assert.True(_vm.Characters[1].IsConnected);
    }

    [Fact]
    public async Task Merge_NoSnapshot_FreshMode()
    {
        // Pas de snapshot — premier lancement
        var appState = new AppState { SessionSnapshot = null };
        _mockAppState.Setup(s => s.LoadAsync()).ReturnsAsync(appState);

        var windows = CreateWindows("Cuckoolo", "Artega");
        _mockDetection.Setup(d => d.DetectedWindows).Returns(windows);
        _mockDetection.Setup(d => d.DetectOnce()).Returns(windows);
        _mockProfile.Setup(p => p.GetAllProfiles()).Returns(new List<Profile>());

        await _vm.InitializeProfilesAsync();

        // Aucun personnage car le snapshot n'existe pas et le polling n'a pas encore déclenché
        // Simuler un refresh (scan manuel) pour le mode fresh
        // Le mode fresh ajoute les fenêtres découvertes
        _vm.Characters.Clear(); // reset from init
        // Simulate OnWindowsChanged via Refresh
        _mockDetection.Setup(d => d.DetectOnce()).Returns(windows);

        // Call the private method indirectly via the Refresh command
        var refreshCommand = _vm.RefreshCommand;
        refreshCommand.Execute(null);

        Assert.Equal(2, _vm.Characters.Count);
        Assert.All(_vm.Characters, c => Assert.True(c.IsConnected));
    }

    // --- Tests : Auto-save ---

    [Fact]
    public async Task UpdateSessionSnapshot_TriggersAutoSave()
    {
        var snapshot = CreateSnapshot("Cuckoolo");
        var windows = CreateWindows("Cuckoolo");

        await SetupSnapshotAndRestore(snapshot, windows);

        // MoveCharacter déclenche UpdateSessionSnapshot → ScheduleAutoSave
        // Mais on a 1 seul perso, testons avec ToggleLeader
        _vm.ToggleLeaderCommand.Execute(_vm.Characters[0]);

        // Attendre le debounce (1500ms + marge)
        await Task.Delay(2000);

        _mockAppState.Verify(s => s.SaveAsync(It.IsAny<AppState>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SaveSessionStateAsync_CancelsDebounce()
    {
        var snapshot = CreateSnapshot("Cuckoolo");
        var windows = CreateWindows("Cuckoolo");

        await SetupSnapshotAndRestore(snapshot, windows);

        // Déclencher un auto-save via ToggleLeader
        _vm.ToggleLeaderCommand.Execute(_vm.Characters[0]);

        // Sauvegarder immédiatement — annule le debounce
        await _vm.SaveSessionStateAsync();

        // Le SaveAsync doit avoir été appelé au moins 1 fois (par SaveSessionStateAsync)
        _mockAppState.Verify(s => s.SaveAsync(It.IsAny<AppState>()), Times.AtLeastOnce);
    }

    // --- Tests : MoveCharacter / ToggleLeader / OnHotkeyConfigChanged ---

    [Fact]
    public async Task MoveCharacter_UpdatesSnapshotAndTriggersAutoSave()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var windows = CreateWindows("Cuckoolo", "Artega");

        await SetupSnapshotAndRestore(snapshot, windows);

        _vm.MoveCharacter(0, 1);

        // Vérifier que l'ordre a changé
        Assert.Contains("Artega", _vm.Characters[0].Title);
        Assert.Contains("Cuckoolo", _vm.Characters[1].Title);

        // Attendre le debounce
        await Task.Delay(2000);
        _mockAppState.Verify(s => s.SaveAsync(It.IsAny<AppState>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ToggleLeader_UpdatesSnapshotAndTriggersAutoSave()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var windows = CreateWindows("Cuckoolo", "Artega");

        await SetupSnapshotAndRestore(snapshot, windows);

        _vm.ToggleLeaderCommand.Execute(_vm.Characters[1]);

        Assert.True(_vm.Characters[1].IsLeader);
        Assert.False(_vm.Characters[0].IsLeader);

        await Task.Delay(2000);
        _mockAppState.Verify(s => s.SaveAsync(It.IsAny<AppState>()), Times.AtLeastOnce);
    }

    // --- Tests : Restore au démarrage ---

    [Fact]
    public async Task Restore_WithSnapshot_ShowsGrayedCharactersImmediately()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega", "Xelor");
        // Aucune fenêtre détectée au démarrage
        var windows = new List<DofusWindow>();

        await SetupSnapshotAndRestore(snapshot, windows);

        // Les 3 personnages sont affichés en grisé
        Assert.Equal(3, _vm.Characters.Count);
        Assert.All(_vm.Characters, c => Assert.False(c.IsConnected));
    }

    [Fact]
    public async Task Restore_WindowReconnects_SwitchesFromGrayToConnected()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var noWindows = new List<DofusWindow>();

        await SetupSnapshotAndRestore(snapshot, noWindows);

        // Vérifier que les 2 sont grisés
        Assert.All(_vm.Characters, c => Assert.False(c.IsConnected));

        // Simuler des fenêtres qui apparaissent (polling)
        var windows = CreateWindows("Cuckoolo", "Artega");
        _mockDetection.Setup(d => d.DetectedWindows).Returns(windows);
        _mockDetection.Setup(d => d.DetectOnce()).Returns(windows);

        // Simuler un refresh (équivalent au polling callback)
        _vm.RefreshCommand.Execute(null);

        // Les 2 personnages passent de grisé à connecté
        Assert.Equal(2, _vm.Characters.Count);
        Assert.All(_vm.Characters, c => Assert.True(c.IsConnected));
    }

    // --- Tests : Idempotence ---

    [Fact]
    public async Task Merge_TwoIdenticalCalls_SameResult()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var windows = CreateWindows("Cuckoolo", "Artega");

        await SetupSnapshotAndRestore(snapshot, windows);

        // Capturer l'état après le premier merge
        var firstMergeCharacters = _vm.Characters.Select(c => (c.Title, c.IsConnected, c.SlotIndex)).ToList();

        // Simuler un deuxième merge avec les mêmes fenêtres (polling)
        _mockDetection.Setup(d => d.DetectOnce()).Returns(windows);
        _vm.RefreshCommand.Execute(null);

        // L'état doit être identique
        var secondMergeCharacters = _vm.Characters.Select(c => (c.Title, c.IsConnected, c.SlotIndex)).ToList();
        Assert.Equal(firstMergeCharacters.Count, secondMergeCharacters.Count);
        for (var i = 0; i < firstMergeCharacters.Count; i++)
        {
            Assert.Equal(firstMergeCharacters[i].Title, secondMergeCharacters[i].Title);
            Assert.Equal(firstMergeCharacters[i].IsConnected, secondMergeCharacters[i].IsConnected);
        }
    }

    // --- Tests : Profil avec snapshot ---

    [Fact]
    public async Task Restore_WithActiveProfile_ShowsGrayedCharacters()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        var profile = new Profile { ProfileName = "Team1" };
        profile.Slots.Add(new ProfileSlot { Index = 0, CharacterName = "Cuckoolo", WindowTitlePattern = "*Cuckoolo*", IsLeader = true });
        profile.Slots.Add(new ProfileSlot { Index = 1, CharacterName = "Artega", WindowTitlePattern = "*Artega*" });

        _mockProfile.Setup(p => p.GetProfile("Team1")).Returns(profile);
        _mockProfile.Setup(p => p.GetAllProfiles()).Returns(new List<Profile> { profile });

        var windows = new List<DofusWindow>(); // aucune fenêtre

        await SetupSnapshotAndRestore(snapshot, windows, activeProfileName: "Team1");

        // Les personnages sont affichés grisés depuis le snapshot
        Assert.Equal(2, _vm.Characters.Count);
        Assert.All(_vm.Characters, c => Assert.False(c.IsConnected));
    }

    [Fact]
    public async Task Merge_PreservesLeaderFromSnapshot()
    {
        var snapshot = CreateSnapshot("Cuckoolo", "Artega");
        // Artega est le leader dans le snapshot
        snapshot.Slots[0].IsLeader = false;
        snapshot.Slots[1].IsLeader = true;

        var windows = CreateWindows("Cuckoolo", "Artega");

        await SetupSnapshotAndRestore(snapshot, windows);

        Assert.False(_vm.Characters[0].IsLeader);
        Assert.True(_vm.Characters[1].IsLeader);

        // Vérifier que FocusService a été appelé avec le bon leader
        _mockFocus.Verify(f => f.SetLeader((nint)200), Times.AtLeastOnce);
    }

    // --- Tests : Fenêtres en cours de connexion ---

    [Fact]
    public async Task Merge_ConnectingWindows_NotAddedAsCharacters()
    {
        var snapshot = CreateSnapshot("Cuckoolo");
        // Fenêtre prête + fenêtre en cours de connexion (titre générique)
        var windows = new List<DofusWindow>
        {
            new() { Handle = 100, ProcessId = 1, Title = "Cuckoolo - Pandawa - 3.4.18.19 - Release", IsVisible = true, IsMinimized = false },
            new() { Handle = 200, ProcessId = 2, Title = "Dofus 3.4.18.19 - Release", IsVisible = true, IsMinimized = false }
        };

        await SetupSnapshotAndRestore(snapshot, windows);

        // Seul Cuckoolo est dans la liste, pas la fenêtre "Dofus"
        Assert.Equal(1, _vm.Characters.Count);
        Assert.Contains("Cuckoolo", _vm.Characters[0].Title);
    }

    [Fact]
    public async Task Merge_ConnectingWindowBecomesCharacter_AddedOnTitleChange()
    {
        var snapshot = CreateSnapshot("Cuckoolo");
        // D'abord la fenêtre est en cours de connexion
        var connectingWindows = new List<DofusWindow>
        {
            new() { Handle = 100, ProcessId = 1, Title = "Cuckoolo - Pandawa - 3.4.18.19 - Release", IsVisible = true, IsMinimized = false },
            new() { Handle = 200, ProcessId = 2, Title = "Dofus 3.4.18.19 - Release", IsVisible = true, IsMinimized = false }
        };

        await SetupSnapshotAndRestore(snapshot, connectingWindows);
        Assert.Equal(1, _vm.Characters.Count);

        // Le titre évolue vers un vrai nom de personnage
        var readyWindows = new List<DofusWindow>
        {
            new() { Handle = 100, ProcessId = 1, Title = "Cuckoolo - Pandawa - 3.4.18.19 - Release", IsVisible = true, IsMinimized = false },
            new() { Handle = 200, ProcessId = 2, Title = "Artega - Xelor - 3.4.18.19 - Release", IsVisible = true, IsMinimized = false }
        };
        _mockDetection.Setup(d => d.DetectedWindows).Returns(readyWindows);
        _mockDetection.Setup(d => d.DetectOnce()).Returns(readyWindows);

        _vm.RefreshCommand.Execute(null);

        // Les deux personnages sont maintenant dans la liste
        Assert.Equal(2, _vm.Characters.Count);
        Assert.Contains("Cuckoolo", _vm.Characters[0].Title);
        Assert.Contains("Artega", _vm.Characters[1].Title);
    }

    [Fact]
    public async Task Snapshot_ExcludesConnectingWindows()
    {
        // Mode fresh (pas de snapshot) : simuler directement
        _mockAppState.Setup(s => s.LoadAsync()).ReturnsAsync((AppState?)null);
        _mockProfile.Setup(p => p.GetAllProfiles()).Returns(new List<Profile>());

        // Fenêtre connectée + fenêtre en connexion
        var windows = new List<DofusWindow>
        {
            new() { Handle = 100, ProcessId = 1, Title = "Cuckoolo - Pandawa - 3.4.18.19 - Release", IsVisible = true, IsMinimized = false },
            new() { Handle = 200, ProcessId = 2, Title = "Dofus 3.4.18.19 - Release", IsVisible = true, IsMinimized = false }
        };
        _mockDetection.Setup(d => d.DetectedWindows).Returns(windows);
        _mockDetection.Setup(d => d.DetectOnce()).Returns(windows);

        await _vm.InitializeProfilesAsync();
        _vm.RefreshCommand.Execute(null);

        // Seul Cuckoolo est dans Characters (la fenêtre en connexion est exclue)
        Assert.Equal(1, _vm.Characters.Count);
        Assert.Contains("Cuckoolo", _vm.Characters[0].Title);

        // Sauvegarder la session — le snapshot ne doit pas contenir "Dofus"
        await _vm.SaveSessionStateAsync();

        _mockAppState.Verify(s => s.SaveAsync(It.Is<AppState>(state =>
            state.SessionSnapshot != null &&
            state.SessionSnapshot.Slots.All(slot => !slot.CharacterName.Equals("Dofus", StringComparison.OrdinalIgnoreCase))
        )), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StatusText_ShowsConnectingCount()
    {
        var snapshot = CreateSnapshot("Cuckoolo");
        var windows = new List<DofusWindow>
        {
            new() { Handle = 100, ProcessId = 1, Title = "Cuckoolo - Pandawa - 3.4.18.19 - Release", IsVisible = true, IsMinimized = false },
            new() { Handle = 200, ProcessId = 2, Title = "Dofus 3.4.18.19 - Release", IsVisible = true, IsMinimized = false },
            new() { Handle = 300, ProcessId = 3, Title = "Dofus", IsVisible = true, IsMinimized = false }
        };

        await SetupSnapshotAndRestore(snapshot, windows);

        Assert.Contains("en cours de connexion", _vm.StatusText);
        Assert.Contains("2", _vm.StatusText); // 2 fenêtres en connexion
    }

    [Theory]
    [InlineData("Dofus", true)]
    [InlineData("Dofus 3.4.18.19 - Release", true)]
    [InlineData("dofus 3.4.18.19 - Release", true)]
    [InlineData("Dofus - 3.4.18.19 - Release", true)]
    [InlineData("Cuckoolo - Pandawa - 3.4.18.19 - Release", false)]
    [InlineData("Artega - Xelor - 3.4.18.19 - Release", false)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    public void IsConnectingTitle_IdentifiesGenericTitles(string title, bool expected)
    {
        Assert.Equal(expected, DashboardViewModel.IsConnectingTitle(title));
    }
}
