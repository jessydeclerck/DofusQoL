using DofusManager.Core.Models;
using DofusManager.Core.Services;
using Xunit;

namespace DofusManager.Tests.Services;

public class AppStateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;
    private readonly AppStateService _service;

    public AppStateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DofusManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "appstate.json");
        _service = new AppStateService(_tempFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_FichierInexistant_RetourneNull()
    {
        var result = await _service.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_RoundTrip_SansProfile()
    {
        var snapshot = new Profile
        {
            ProfileName = "__session__",
            GlobalHotkeys = GlobalHotkeyConfig.CreateDefault()
        };

        var state = new AppState
        {
            ActiveProfileName = null,
            SessionSnapshot = snapshot
        };

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Null(loaded.ActiveProfileName);
        Assert.NotNull(loaded.SessionSnapshot);
        Assert.Equal("Ctrl+Tab", loaded.SessionSnapshot.GlobalHotkeys.NextWindow.DisplayName);
        Assert.Equal("Alt", loaded.SessionSnapshot.GlobalHotkeys.BroadcastKey.DisplayName);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_RoundTrip_AvecProfile()
    {
        var state = new AppState
        {
            ActiveProfileName = "Team Donjon",
            SessionSnapshot = new Profile { ProfileName = "__session__" }
        };

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Team Donjon", loaded.ActiveProfileName);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_SnapshotAvecSlots_PreserveValeurs()
    {
        var snapshot = new Profile
        {
            ProfileName = "__session__",
            GlobalHotkeys = new GlobalHotkeyConfig
            {
                NextWindow = new HotkeyBindingConfig
                {
                    DisplayName = "Ctrl+Space",
                    Modifiers = 2,
                    VirtualKeyCode = 0x20
                },
                PreviousWindow = GlobalHotkeyConfig.CreateDefault().PreviousWindow,
                LastWindow = GlobalHotkeyConfig.CreateDefault().LastWindow,
                FocusLeader = GlobalHotkeyConfig.CreateDefault().FocusLeader,
                BroadcastKey = new HotkeyBindingConfig
                {
                    DisplayName = "Ctrl",
                    VirtualKeyCode = 0xA2
                }
            }
        };
        snapshot.Slots.Add(new ProfileSlot
        {
            Index = 0,
            CharacterName = "Cuckoolo",
            WindowTitlePattern = "*Cuckoolo*",
            IsLeader = true,
            FocusHotkey = "F1",
            FocusHotkeyVirtualKeyCode = 0x70
        });
        snapshot.Slots.Add(new ProfileSlot
        {
            Index = 1,
            CharacterName = "Altchar",
            WindowTitlePattern = "*Altchar*",
            IsLeader = false,
            FocusHotkey = "F2",
            FocusHotkeyVirtualKeyCode = 0x71
        });

        var state = new AppState
        {
            ActiveProfileName = null,
            SessionSnapshot = snapshot
        };

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.SessionSnapshot);
        Assert.Equal(2, loaded.SessionSnapshot.Slots.Count);
        Assert.Equal("Cuckoolo", loaded.SessionSnapshot.Slots[0].CharacterName);
        Assert.True(loaded.SessionSnapshot.Slots[0].IsLeader);
        Assert.Equal("Altchar", loaded.SessionSnapshot.Slots[1].CharacterName);
        Assert.Equal("Ctrl+Space", loaded.SessionSnapshot.GlobalHotkeys.NextWindow.DisplayName);
        Assert.Equal("Ctrl", loaded.SessionSnapshot.GlobalHotkeys.BroadcastKey.DisplayName);
    }

    [Fact]
    public async Task LoadAsync_FichierCorrompu_RetourneNull()
    {
        await File.WriteAllTextAsync(_tempFile, "{ invalid json !!! }");

        var result = await _service.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_CreeLeDossierSiInexistant()
    {
        var nestedDir = Path.Combine(_tempDir, "sub", "dir");
        var nestedFile = Path.Combine(nestedDir, "appstate.json");
        var service = new AppStateService(nestedFile);

        await service.SaveAsync(new AppState());

        Assert.True(File.Exists(nestedFile));
    }

    [Fact]
    public async Task SaveAsync_EcraseLeFichierExistant()
    {
        await _service.SaveAsync(new AppState { ActiveProfileName = "Ancien" });
        await _service.SaveAsync(new AppState { ActiveProfileName = "Nouveau" });

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Equal("Nouveau", loaded.ActiveProfileName);
    }

    [Fact]
    public async Task LoadAsync_BackwardCompat_LastHotkeyConfig()
    {
        // Ancien format avec LastHotkeyConfig, sans SessionSnapshot
        var state = new AppState
        {
            ActiveProfileName = "Team",
            LastHotkeyConfig = GlobalHotkeyConfig.CreateDefault(),
            SessionSnapshot = null
        };
        await _service.SaveAsync(state);

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.LastHotkeyConfig);
        Assert.Null(loaded.SessionSnapshot);
    }

    [Fact]
    public async Task LoadAsync_SansSnapshot_RetourneNull()
    {
        var state = new AppState { ActiveProfileName = "Team", SessionSnapshot = null };
        await _service.SaveAsync(state);

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Null(loaded.SessionSnapshot);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_ShowCaptureAmeReminder_Preserved()
    {
        var state = new AppState { ShowCaptureAmeReminder = true };
        await _service.SaveAsync(state);

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.True(loaded.ShowCaptureAmeReminder);
    }

    [Fact]
    public async Task LoadAsync_AncienFormat_SansCaptureAme_DefaultFalse()
    {
        // Simuler un JSON ancien sans le champ ShowCaptureAmeReminder
        await File.WriteAllTextAsync(_tempFile, """{"isTopmost":false,"activeProfileName":null}""");

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.False(loaded.ShowCaptureAmeReminder);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_ActionOverlay_Preserved()
    {
        var state = new AppState
        {
            ShowActionOverlay = true,
            ActionOverlayLeft = 100.5,
            ActionOverlayTop = 200.0
        };
        await _service.SaveAsync(state);

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.True(loaded.ShowActionOverlay);
        Assert.Equal(100.5, loaded.ActionOverlayLeft);
        Assert.Equal(200.0, loaded.ActionOverlayTop);
    }

    [Fact]
    public async Task LoadAsync_AncienFormat_SansActionOverlay_DefaultFalse()
    {
        await File.WriteAllTextAsync(_tempFile, """{"isTopmost":false}""");

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.False(loaded.ShowActionOverlay);
        Assert.Null(loaded.ActionOverlayLeft);
        Assert.Null(loaded.ActionOverlayTop);
    }

    [Fact]
    public async Task SaveAsync_EcritureAtomique_FichierOriginalSurvitSiTmpPresent()
    {
        // Sauvegarder un état initial
        await _service.SaveAsync(new AppState { ActiveProfileName = "Initial" });

        // Vérifier que le fichier .tmp n'existe PAS après un save réussi
        Assert.False(File.Exists(_tempFile + ".tmp"));

        // Vérifier que le fichier principal contient le bon état
        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Equal("Initial", loaded.ActiveProfileName);
    }

    [Fact]
    public async Task SaveAsync_FichierTmpResiduel_EstEcraseParProchainSave()
    {
        // Simuler un .tmp résiduel d'un crash précédent
        await File.WriteAllTextAsync(_tempFile + ".tmp", "garbage");

        await _service.SaveAsync(new AppState { ActiveProfileName = "Fresh" });

        // Le .tmp résiduel a été écrasé puis renommé
        Assert.False(File.Exists(_tempFile + ".tmp"));
        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Equal("Fresh", loaded.ActiveProfileName);
    }

    [Fact]
    public async Task LoadAsync_FichierVide_RetourneNull()
    {
        // Simuler un fichier tronqué (crash pendant écriture sans atomic write)
        await File.WriteAllTextAsync(_tempFile, "");

        var result = await _service.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_FichierLocke_RetourneNull()
    {
        // Écrire un état valide d'abord
        await _service.SaveAsync(new AppState { ActiveProfileName = "Test" });

        // Locker le fichier en écriture exclusive
        using var lockStream = new FileStream(_tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // LoadAsync doit retourner null (IOException) sans crash
        var result = await _service.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_Concurrent_PasDeCorruption()
    {
        // Lancer 10 saves concurrents
        var tasks = Enumerable.Range(0, 10)
            .Select(i => _service.SaveAsync(new AppState { ActiveProfileName = $"Save{i}" }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Le fichier doit contenir un état valide (l'un des 10)
        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.StartsWith("Save", loaded.ActiveProfileName);
    }
}
