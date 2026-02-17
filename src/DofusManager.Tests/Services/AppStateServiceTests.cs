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
        var state = new AppState
        {
            ActiveProfileName = null,
            LastHotkeyConfig = GlobalHotkeyConfig.CreateDefault()
        };

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Null(loaded.ActiveProfileName);
        Assert.NotNull(loaded.LastHotkeyConfig);
        Assert.Equal("Ctrl+Tab", loaded.LastHotkeyConfig.NextWindow.DisplayName);
        Assert.Equal("Alt", loaded.LastHotkeyConfig.BroadcastKey.DisplayName);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_RoundTrip_AvecProfile()
    {
        var state = new AppState
        {
            ActiveProfileName = "Team Donjon",
            LastHotkeyConfig = GlobalHotkeyConfig.CreateDefault()
        };

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("Team Donjon", loaded.ActiveProfileName);
    }

    [Fact]
    public async Task SaveAsync_PuisLoadAsync_ConfigCustom_PreserveValeurs()
    {
        var customConfig = new GlobalHotkeyConfig
        {
            NextWindow = new HotkeyBindingConfig
            {
                DisplayName = "Ctrl+Space",
                Modifiers = 2, // MOD_CONTROL
                VirtualKeyCode = 0x20 // VK_SPACE
            },
            PreviousWindow = GlobalHotkeyConfig.CreateDefault().PreviousWindow,
            LastWindow = GlobalHotkeyConfig.CreateDefault().LastWindow,
            FocusLeader = GlobalHotkeyConfig.CreateDefault().FocusLeader,
            BroadcastKey = new HotkeyBindingConfig
            {
                DisplayName = "Ctrl",
                VirtualKeyCode = 0xA2 // VK_LCONTROL
            }
        };

        var state = new AppState
        {
            ActiveProfileName = null,
            LastHotkeyConfig = customConfig
        };

        await _service.SaveAsync(state);
        var loaded = await _service.LoadAsync();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.LastHotkeyConfig);
        Assert.Equal("Ctrl+Space", loaded.LastHotkeyConfig.NextWindow.DisplayName);
        Assert.Equal(0x20u, loaded.LastHotkeyConfig.NextWindow.VirtualKeyCode);
        Assert.Equal("Ctrl", loaded.LastHotkeyConfig.BroadcastKey.DisplayName);
        Assert.Equal(0xA2u, loaded.LastHotkeyConfig.BroadcastKey.VirtualKeyCode);
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
    public async Task LoadAsync_SansLastHotkeyConfig_RetourneNull()
    {
        var state = new AppState { ActiveProfileName = "Team", LastHotkeyConfig = null };
        await _service.SaveAsync(state);

        var loaded = await _service.LoadAsync();
        Assert.NotNull(loaded);
        Assert.Null(loaded.LastHotkeyConfig);
    }
}
