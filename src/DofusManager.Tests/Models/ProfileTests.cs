using DofusManager.Core.Models;
using Xunit;

namespace DofusManager.Tests.Models;

public class ProfileSlotTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var slot = new ProfileSlot
        {
            Index = 0,
            CharacterName = "Panda-Main",
            WindowTitlePattern = "*Panda*",
            IsLeader = true,
            FocusHotkey = "F1"
        };

        Assert.Equal(0, slot.Index);
        Assert.Equal("Panda-Main", slot.CharacterName);
        Assert.Equal("*Panda*", slot.WindowTitlePattern);
        Assert.True(slot.IsLeader);
        Assert.Equal("F1", slot.FocusHotkey);
    }

    [Fact]
    public void Defaults_WindowTitlePatternIsStar()
    {
        var slot = new ProfileSlot { Index = 0, CharacterName = "Test" };

        Assert.Equal("*", slot.WindowTitlePattern);
    }

    [Fact]
    public void Defaults_IsLeaderIsFalse()
    {
        var slot = new ProfileSlot { Index = 0, CharacterName = "Test" };

        Assert.False(slot.IsLeader);
    }

    [Fact]
    public void Defaults_FocusHotkeyIsNull()
    {
        var slot = new ProfileSlot { Index = 0, CharacterName = "Test" };

        Assert.Null(slot.FocusHotkey);
    }
}

public class BroadcastPresetTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var preset = new BroadcastPreset
        {
            Name = "Valider quête",
            InputType = "key",
            Key = "Enter",
            Hotkey = "Ctrl+Enter"
        };

        Assert.Equal("Valider quête", preset.Name);
        Assert.Equal("key", preset.InputType);
        Assert.Equal("Enter", preset.Key);
        Assert.Equal("Ctrl+Enter", preset.Hotkey);
    }

    [Fact]
    public void Defaults_HaveCorrectValues()
    {
        var preset = new BroadcastPreset { Name = "Test", InputType = "key" };

        Assert.Equal("left", preset.ClickButton);
        Assert.Equal("all", preset.Targets);
        Assert.Equal(80, preset.DelayMin);
        Assert.Equal(300, preset.DelayMax);
        Assert.Equal("profile", preset.OrderMode);
        Assert.Null(preset.Hotkey);
        Assert.Null(preset.Key);
        Assert.Null(preset.ClickX);
        Assert.Null(preset.ClickY);
        Assert.Null(preset.CustomTargetSlotIndices);
    }

    [Fact]
    public void ClickAtPosition_SetsCoordinates()
    {
        var preset = new BroadcastPreset
        {
            Name = "Clic prêt",
            InputType = "clickAtPosition",
            ClickX = 450,
            ClickY = 620,
            ClickButton = "left"
        };

        Assert.Equal(450, preset.ClickX);
        Assert.Equal(620, preset.ClickY);
        Assert.Equal("clickAtPosition", preset.InputType);
    }

    // --- Validation ---

    [Fact]
    public void Validate_ValidKeyPreset_ReturnsNull()
    {
        var preset = new BroadcastPreset { Name = "Test", InputType = "key", Key = "Enter" };
        Assert.Null(preset.Validate());
    }

    [Fact]
    public void Validate_ValidClickPreset_ReturnsNull()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "clickAtPosition",
            ClickX = 100,
            ClickY = 200
        };
        Assert.Null(preset.Validate());
    }

    [Fact]
    public void Validate_EmptyName_ReturnsError()
    {
        var preset = new BroadcastPreset { Name = "", InputType = "key", Key = "Enter" };
        Assert.NotNull(preset.Validate());
        Assert.Contains("nom", preset.Validate()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_InvalidInputType_ReturnsError()
    {
        var preset = new BroadcastPreset { Name = "Test", InputType = "invalid" };
        var error = preset.Validate();
        Assert.NotNull(error);
        Assert.Contains("InputType invalide", error);
    }

    [Fact]
    public void Validate_KeyTypeWithoutKey_ReturnsError()
    {
        var preset = new BroadcastPreset { Name = "Test", InputType = "key" };
        Assert.NotNull(preset.Validate());
    }

    [Fact]
    public void Validate_ClickWithoutCoordinates_ReturnsError()
    {
        var preset = new BroadcastPreset { Name = "Test", InputType = "clickAtPosition" };
        Assert.NotNull(preset.Validate());
    }

    [Fact]
    public void Validate_DelayMinGreaterThanMax_ReturnsError()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "key",
            Key = "Enter",
            DelayMin = 300,
            DelayMax = 80
        };
        Assert.NotNull(preset.Validate());
    }

    [Fact]
    public void Validate_NegativeDelayMin_ReturnsError()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "key",
            Key = "Enter",
            DelayMin = -1
        };
        Assert.NotNull(preset.Validate());
    }

    [Fact]
    public void Validate_CustomTargetsWithoutIndices_ReturnsError()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "key",
            Key = "Enter",
            Targets = "custom"
        };
        Assert.NotNull(preset.Validate());
    }

    [Fact]
    public void Validate_CustomTargetsWithIndices_ReturnsNull()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "key",
            Key = "Enter",
            Targets = "custom",
            CustomTargetSlotIndices = [0, 1]
        };
        Assert.Null(preset.Validate());
    }

    [Fact]
    public void Validate_InvalidClickButton_ReturnsError()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "clickAtPosition",
            ClickX = 10,
            ClickY = 20,
            ClickButton = "middle"
        };
        Assert.NotNull(preset.Validate());
    }

    [Fact]
    public void Validate_InvalidOrderMode_ReturnsError()
    {
        var preset = new BroadcastPreset
        {
            Name = "Test",
            InputType = "key",
            Key = "Enter",
            OrderMode = "invalid"
        };
        Assert.NotNull(preset.Validate());
    }
}

public class ProfileTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var profile = new Profile { ProfileName = "Team Donjon" };

        Assert.Equal("Team Donjon", profile.ProfileName);
        Assert.NotNull(profile.Slots);
        Assert.Empty(profile.Slots);
        Assert.NotNull(profile.BroadcastPresets);
        Assert.Empty(profile.BroadcastPresets);
        Assert.True(profile.CreatedAt <= DateTime.UtcNow);
        Assert.True(profile.LastModifiedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Slots_CanBePopulated()
    {
        var profile = new Profile
        {
            ProfileName = "Team Donjon",
            Slots = new List<ProfileSlot>
            {
                new() { Index = 0, CharacterName = "Panda-Main", IsLeader = true, FocusHotkey = "F1" },
                new() { Index = 1, CharacterName = "Iop-DPS", FocusHotkey = "F2" }
            }
        };

        Assert.Equal(2, profile.Slots.Count);
        Assert.Equal("Panda-Main", profile.Slots[0].CharacterName);
        Assert.True(profile.Slots[0].IsLeader);
        Assert.Equal("Iop-DPS", profile.Slots[1].CharacterName);
        Assert.False(profile.Slots[1].IsLeader);
    }

    [Fact]
    public void BroadcastPresets_CanBePopulated()
    {
        var profile = new Profile
        {
            ProfileName = "Team Donjon",
            BroadcastPresets = new List<BroadcastPreset>
            {
                new() { Name = "Valider quête", InputType = "key", Key = "Enter" }
            }
        };

        Assert.Single(profile.BroadcastPresets);
        Assert.Equal("Valider quête", profile.BroadcastPresets[0].Name);
    }

    [Fact]
    public void FullProfile_MatchesSpecStructure()
    {
        var profile = new Profile
        {
            ProfileName = "Team Donjon",
            Slots = new List<ProfileSlot>
            {
                new()
                {
                    Index = 0,
                    CharacterName = "Panda-Main",
                    WindowTitlePattern = "*Panda*",
                    IsLeader = true,
                    FocusHotkey = "F1"
                },
                new()
                {
                    Index = 1,
                    CharacterName = "Iop-DPS",
                    WindowTitlePattern = "*Iop*",
                    IsLeader = false,
                    FocusHotkey = "F2"
                }
            },
            BroadcastPresets = new List<BroadcastPreset>
            {
                new()
                {
                    Name = "Valider quête",
                    Hotkey = "Ctrl+Enter",
                    InputType = "key",
                    Key = "Enter",
                    Targets = "allExceptLeader",
                    DelayMin = 80,
                    DelayMax = 300,
                    OrderMode = "profile"
                }
            }
        };

        Assert.Equal("Team Donjon", profile.ProfileName);
        Assert.Equal(2, profile.Slots.Count);
        Assert.Single(profile.BroadcastPresets);
        Assert.Equal("allExceptLeader", profile.BroadcastPresets[0].Targets);
    }
}
