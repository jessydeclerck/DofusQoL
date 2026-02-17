using DofusManager.Core.Models;
using DofusManager.Core.Services;
using Xunit;

namespace DofusManager.Tests.Services;

public class ProfileServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DofusManagerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "profiles.json");
        _service = new ProfileService(_tempFile);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static Profile CreateTestProfile(string name = "Team Donjon", int slotCount = 2)
    {
        var profile = new Profile { ProfileName = name };
        for (int i = 0; i < slotCount; i++)
        {
            profile.Slots.Add(new ProfileSlot
            {
                Index = i,
                CharacterName = $"Perso{i}",
                WindowTitlePattern = $"*Perso{i}*",
                IsLeader = i == 0,
                FocusHotkey = $"F{i + 1}"
            });
        }
        return profile;
    }

    // --- CRUD ---

    [Fact]
    public void CreateProfile_AddsToList()
    {
        var profile = CreateTestProfile();

        _service.CreateProfile(profile);

        var all = _service.GetAllProfiles();
        Assert.Single(all);
        Assert.Equal("Team Donjon", all[0].ProfileName);
    }

    [Fact]
    public void CreateProfile_DuplicateName_Throws()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));

        Assert.Throws<InvalidOperationException>(() =>
            _service.CreateProfile(CreateTestProfile("Team A")));
    }

    [Fact]
    public void CreateProfile_EmptyName_Throws()
    {
        var profile = new Profile { ProfileName = "" };

        Assert.Throws<ArgumentException>(() => _service.CreateProfile(profile));
    }

    [Fact]
    public void CreateProfile_WhitespaceName_Throws()
    {
        var profile = new Profile { ProfileName = "   " };

        Assert.Throws<ArgumentException>(() => _service.CreateProfile(profile));
    }

    [Fact]
    public void GetProfile_ExistingName_ReturnsProfile()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));

        var result = _service.GetProfile("Team A");

        Assert.NotNull(result);
        Assert.Equal("Team A", result.ProfileName);
    }

    [Fact]
    public void GetProfile_NonExistingName_ReturnsNull()
    {
        var result = _service.GetProfile("Inexistant");

        Assert.Null(result);
    }

    [Fact]
    public void GetProfile_CaseInsensitive()
    {
        _service.CreateProfile(CreateTestProfile("Team Donjon"));

        var result = _service.GetProfile("team donjon");

        Assert.NotNull(result);
    }

    [Fact]
    public void GetAllProfiles_ReturnsAllCreated()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));
        _service.CreateProfile(CreateTestProfile("Team B"));
        _service.CreateProfile(CreateTestProfile("Team C"));

        var all = _service.GetAllProfiles();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void UpdateProfile_ModifiesExisting()
    {
        _service.CreateProfile(CreateTestProfile("Team A", slotCount: 2));

        var updated = CreateTestProfile("Team A", slotCount: 4);
        _service.UpdateProfile(updated);

        var result = _service.GetProfile("Team A");
        Assert.NotNull(result);
        Assert.Equal(4, result.Slots.Count);
    }

    [Fact]
    public void UpdateProfile_NonExisting_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.UpdateProfile(CreateTestProfile("Inexistant")));
    }

    [Fact]
    public void UpdateProfile_SetsLastModifiedAt()
    {
        var profile = CreateTestProfile("Team A");
        _service.CreateProfile(profile);
        var createdAt = profile.LastModifiedAt;

        // Petit délai pour que le timestamp change
        var updated = CreateTestProfile("Team A");
        _service.UpdateProfile(updated);

        var result = _service.GetProfile("Team A");
        Assert.NotNull(result);
        Assert.True(result.LastModifiedAt >= createdAt);
    }

    [Fact]
    public void DeleteProfile_RemovesFromList()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));

        _service.DeleteProfile("Team A");

        Assert.Empty(_service.GetAllProfiles());
        Assert.Null(_service.GetProfile("Team A"));
    }

    [Fact]
    public void DeleteProfile_NonExisting_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.DeleteProfile("Inexistant"));
    }

    // --- Events ---

    [Fact]
    public void CreateProfile_RaisesProfilesChanged()
    {
        var raised = false;
        _service.ProfilesChanged += (_, _) => raised = true;

        _service.CreateProfile(CreateTestProfile());

        Assert.True(raised);
    }

    [Fact]
    public void UpdateProfile_RaisesProfilesChanged()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));
        var raised = false;
        _service.ProfilesChanged += (_, _) => raised = true;

        _service.UpdateProfile(CreateTestProfile("Team A"));

        Assert.True(raised);
    }

    [Fact]
    public void DeleteProfile_RaisesProfilesChanged()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));
        var raised = false;
        _service.ProfilesChanged += (_, _) => raised = true;

        _service.DeleteProfile("Team A");

        Assert.True(raised);
    }

    // --- Serialization ---

    [Fact]
    public async Task SaveAsync_CreatesJsonFile()
    {
        _service.CreateProfile(CreateTestProfile());

        await _service.SaveAsync();

        Assert.True(File.Exists(_tempFile));
        var content = await File.ReadAllTextAsync(_tempFile);
        Assert.Contains("Team Donjon", content);
        Assert.Contains("Perso0", content);
    }

    [Fact]
    public async Task LoadAsync_FileNotFound_NoException()
    {
        // Ne doit pas lever d'exception
        await _service.LoadAsync(Path.Combine(_tempDir, "nonexistent.json"));

        Assert.Empty(_service.GetAllProfiles());
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var original = CreateTestProfile("Team Donjon", slotCount: 3);
        original.BroadcastPresets.Add(new BroadcastPreset
        {
            Name = "Valider quête",
            InputType = "key",
            Key = "Enter",
            Hotkey = "Ctrl+Enter",
            Targets = "allExceptLeader",
            DelayMin = 80,
            DelayMax = 300
        });
        _service.CreateProfile(original);

        await _service.SaveAsync();

        // Charger dans un nouveau service
        var newService = new ProfileService(_tempFile);
        await newService.LoadAsync();

        var loaded = newService.GetProfile("Team Donjon");
        Assert.NotNull(loaded);
        Assert.Equal("Team Donjon", loaded.ProfileName);
        Assert.Equal(3, loaded.Slots.Count);
        Assert.Equal("Perso0", loaded.Slots[0].CharacterName);
        Assert.True(loaded.Slots[0].IsLeader);
        Assert.Equal("F1", loaded.Slots[0].FocusHotkey);
        Assert.Equal("*Perso0*", loaded.Slots[0].WindowTitlePattern);

        // Broadcast preset
        Assert.Single(loaded.BroadcastPresets);
        Assert.Equal("Valider quête", loaded.BroadcastPresets[0].Name);
        Assert.Equal("key", loaded.BroadcastPresets[0].InputType);
        Assert.Equal("Enter", loaded.BroadcastPresets[0].Key);
        Assert.Equal("allExceptLeader", loaded.BroadcastPresets[0].Targets);
    }

    [Fact]
    public async Task SaveAndLoad_MultipleProfiles()
    {
        _service.CreateProfile(CreateTestProfile("Team A"));
        _service.CreateProfile(CreateTestProfile("Team B"));

        await _service.SaveAsync();

        var newService = new ProfileService(_tempFile);
        await newService.LoadAsync();

        Assert.Equal(2, newService.GetAllProfiles().Count);
        Assert.NotNull(newService.GetProfile("Team A"));
        Assert.NotNull(newService.GetProfile("Team B"));
    }

    [Fact]
    public async Task LoadAsync_RaisesProfilesChanged()
    {
        _service.CreateProfile(CreateTestProfile());
        await _service.SaveAsync();

        var newService = new ProfileService(_tempFile);
        var raised = false;
        newService.ProfilesChanged += (_, _) => raised = true;

        await newService.LoadAsync();

        Assert.True(raised);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "profiles.json");
        var service = new ProfileService(nestedPath);
        service.CreateProfile(CreateTestProfile());

        await service.SaveAsync();

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_WithGlobalHotkeys()
    {
        var profile = CreateTestProfile("Team Custom", slotCount: 2);
        profile.Slots[0].FocusHotkeyModifiers = (uint)HotkeyModifiers.None;
        profile.Slots[0].FocusHotkeyVirtualKeyCode = 0x70; // F1
        profile.Slots[1].FocusHotkeyModifiers = (uint)HotkeyModifiers.Control;
        profile.Slots[1].FocusHotkeyVirtualKeyCode = 0x42; // B

        profile.GlobalHotkeys = new GlobalHotkeyConfig
        {
            NextWindow = new HotkeyBindingConfig { DisplayName = "Alt+N", Modifiers = (uint)HotkeyModifiers.Alt, VirtualKeyCode = 0x4E },
            PreviousWindow = new HotkeyBindingConfig { DisplayName = "Alt+P", Modifiers = (uint)HotkeyModifiers.Alt, VirtualKeyCode = 0x50 },
            LastWindow = new HotkeyBindingConfig { DisplayName = "Ctrl+`", Modifiers = (uint)HotkeyModifiers.Control, VirtualKeyCode = 0xC0 },
            FocusLeader = new HotkeyBindingConfig { DisplayName = "Ctrl+F1", Modifiers = (uint)HotkeyModifiers.Control, VirtualKeyCode = 0x70 }
        };

        _service.CreateProfile(profile);
        await _service.SaveAsync();

        var newService = new ProfileService(_tempFile);
        await newService.LoadAsync();
        var loaded = newService.GetProfile("Team Custom");

        Assert.NotNull(loaded);

        // Slot hotkeys
        Assert.Equal(0x70u, loaded.Slots[0].FocusHotkeyVirtualKeyCode);
        Assert.Equal((uint)HotkeyModifiers.None, loaded.Slots[0].FocusHotkeyModifiers);
        Assert.Equal(0x42u, loaded.Slots[1].FocusHotkeyVirtualKeyCode);
        Assert.Equal((uint)HotkeyModifiers.Control, loaded.Slots[1].FocusHotkeyModifiers);

        // Global hotkeys
        Assert.Equal("Alt+N", loaded.GlobalHotkeys.NextWindow.DisplayName);
        Assert.Equal((uint)HotkeyModifiers.Alt, loaded.GlobalHotkeys.NextWindow.Modifiers);
        Assert.Equal(0x4Eu, loaded.GlobalHotkeys.NextWindow.VirtualKeyCode);
        Assert.Equal("Alt+P", loaded.GlobalHotkeys.PreviousWindow.DisplayName);
    }

    [Fact]
    public async Task SaveAndLoad_LegacyProfile_WithoutNewFields_LoadsCorrectly()
    {
        // Simule un profil legacy (sans FocusHotkeyModifiers/VirtualKeyCode/GlobalHotkeys)
        var legacyJson = """
        [
            {
                "ProfileName": "Legacy Team",
                "Slots": [
                    { "Index": 0, "CharacterName": "Perso0", "WindowTitlePattern": "*Perso0*", "IsLeader": true, "FocusHotkey": "F1" },
                    { "Index": 1, "CharacterName": "Perso1", "WindowTitlePattern": "*Perso1*", "IsLeader": false, "FocusHotkey": "F2" }
                ],
                "BroadcastPresets": [],
                "CreatedAt": "2024-01-01T00:00:00Z",
                "LastModifiedAt": "2024-01-01T00:00:00Z"
            }
        ]
        """;
        await File.WriteAllTextAsync(_tempFile, legacyJson);

        var newService = new ProfileService(_tempFile);
        await newService.LoadAsync();

        var loaded = newService.GetProfile("Legacy Team");
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Slots.Count);

        // Les nouveaux champs ont les valeurs par défaut (0)
        Assert.Equal(0u, loaded.Slots[0].FocusHotkeyModifiers);
        Assert.Equal(0u, loaded.Slots[0].FocusHotkeyVirtualKeyCode);

        // GlobalHotkeys est initialisé avec les defaults
        Assert.NotNull(loaded.GlobalHotkeys);
        Assert.Equal("Ctrl+Tab", loaded.GlobalHotkeys.NextWindow.DisplayName);
    }
}
