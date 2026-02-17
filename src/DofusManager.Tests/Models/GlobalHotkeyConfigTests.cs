using System.Text.Json;
using DofusManager.Core.Models;
using Xunit;

namespace DofusManager.Tests.Models;

public class GlobalHotkeyConfigTests
{
    [Fact]
    public void CreateDefault_ReturnsNonNullConfig()
    {
        var config = GlobalHotkeyConfig.CreateDefault();

        Assert.NotNull(config);
        Assert.NotNull(config.NextWindow);
        Assert.NotNull(config.PreviousWindow);
        Assert.NotNull(config.LastWindow);
        Assert.NotNull(config.FocusLeader);
        Assert.NotNull(config.BroadcastKey);
    }

    [Fact]
    public void CreateDefault_NextWindow_IsCtrlTab()
    {
        var config = GlobalHotkeyConfig.CreateDefault();

        Assert.Equal("Ctrl+Tab", config.NextWindow.DisplayName);
        Assert.Equal((uint)HotkeyModifiers.Control, config.NextWindow.Modifiers);
        Assert.Equal(0x09u, config.NextWindow.VirtualKeyCode); // VK_TAB
    }

    [Fact]
    public void CreateDefault_PreviousWindow_IsCtrlShiftTab()
    {
        var config = GlobalHotkeyConfig.CreateDefault();

        Assert.Equal("Ctrl+Shift+Tab", config.PreviousWindow.DisplayName);
        Assert.Equal((uint)(HotkeyModifiers.Control | HotkeyModifiers.Shift), config.PreviousWindow.Modifiers);
        Assert.Equal(0x09u, config.PreviousWindow.VirtualKeyCode);
    }

    [Fact]
    public void CreateDefault_LastWindow_IsCtrlBacktick()
    {
        var config = GlobalHotkeyConfig.CreateDefault();

        Assert.Equal("Ctrl+`", config.LastWindow.DisplayName);
        Assert.Equal((uint)HotkeyModifiers.Control, config.LastWindow.Modifiers);
        Assert.Equal(0xC0u, config.LastWindow.VirtualKeyCode); // VK_OEM_3
    }

    [Fact]
    public void CreateDefault_FocusLeader_IsCtrlF1()
    {
        var config = GlobalHotkeyConfig.CreateDefault();

        Assert.Equal("Ctrl+F1", config.FocusLeader.DisplayName);
        Assert.Equal((uint)HotkeyModifiers.Control, config.FocusLeader.Modifiers);
        Assert.Equal(0x70u, config.FocusLeader.VirtualKeyCode); // VK_F1
    }

    [Fact]
    public void CreateDefault_BroadcastKey_IsAlt()
    {
        var config = GlobalHotkeyConfig.CreateDefault();

        Assert.Equal("Alt", config.BroadcastKey.DisplayName);
        Assert.Equal(0u, config.BroadcastKey.Modifiers);
        Assert.Equal(0x12u, config.BroadcastKey.VirtualKeyCode); // VK_MENU
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllValues()
    {
        var original = GlobalHotkeyConfig.CreateDefault();
        original.NextWindow.DisplayName = "Alt+N";
        original.NextWindow.Modifiers = (uint)HotkeyModifiers.Alt;
        original.NextWindow.VirtualKeyCode = 0x4E; // VK_N
        original.BroadcastKey.DisplayName = "F9";
        original.BroadcastKey.VirtualKeyCode = 0x78; // VK_F9

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<GlobalHotkeyConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Alt+N", deserialized.NextWindow.DisplayName);
        Assert.Equal((uint)HotkeyModifiers.Alt, deserialized.NextWindow.Modifiers);
        Assert.Equal(0x4Eu, deserialized.NextWindow.VirtualKeyCode);
        // Other hotkeys preserved
        Assert.Equal("Ctrl+Shift+Tab", deserialized.PreviousWindow.DisplayName);
        Assert.Equal("Ctrl+`", deserialized.LastWindow.DisplayName);
        Assert.Equal("Ctrl+F1", deserialized.FocusLeader.DisplayName);
        // Broadcast key preserved
        Assert.Equal("F9", deserialized.BroadcastKey.DisplayName);
        Assert.Equal(0x78u, deserialized.BroadcastKey.VirtualKeyCode);
    }

    [Fact]
    public void HotkeyBindingConfig_DefaultsAreEmpty()
    {
        var config = new HotkeyBindingConfig();

        Assert.Equal(string.Empty, config.DisplayName);
        Assert.Equal(0u, config.Modifiers);
        Assert.Equal(0u, config.VirtualKeyCode);
    }

    [Fact]
    public void JsonDeserialization_MissingGlobalHotkeys_UsesDefaults()
    {
        // Simule un ancien profil sans GlobalHotkeys
        var json = """{"ProfileName":"Old","Slots":[],"CreatedAt":"2024-01-01T00:00:00Z","LastModifiedAt":"2024-01-01T00:00:00Z"}""";
        var profile = JsonSerializer.Deserialize<Profile>(json);

        Assert.NotNull(profile);
        Assert.NotNull(profile.GlobalHotkeys);
        // Les valeurs par défaut du constructeur sont appliquées
        Assert.Equal("Ctrl+Tab", profile.GlobalHotkeys.NextWindow.DisplayName);
        Assert.Equal("Alt", profile.GlobalHotkeys.BroadcastKey.DisplayName);
        Assert.Equal(0x12u, profile.GlobalHotkeys.BroadcastKey.VirtualKeyCode);
    }

    [Fact]
    public void JsonDeserialization_GlobalHotkeys_WithoutBroadcastKey_DefaultsToAlt()
    {
        // Simule un profil avec GlobalHotkeys mais sans BroadcastKey (profils pré-configurable)
        var json = """
        {
            "NextWindow": {"DisplayName":"Ctrl+Tab","Modifiers":2,"VirtualKeyCode":9},
            "PreviousWindow": {"DisplayName":"Ctrl+Shift+Tab","Modifiers":6,"VirtualKeyCode":9},
            "LastWindow": {"DisplayName":"Ctrl+`","Modifiers":2,"VirtualKeyCode":192},
            "FocusLeader": {"DisplayName":"Ctrl+F1","Modifiers":2,"VirtualKeyCode":112}
        }
        """;
        var config = JsonSerializer.Deserialize<GlobalHotkeyConfig>(json);

        Assert.NotNull(config);
        Assert.NotNull(config.BroadcastKey);
        // BroadcastKey utilise l'initialisation par défaut de la propriété (Alt)
        Assert.Equal("Alt", config.BroadcastKey.DisplayName);
        Assert.Equal(0x12u, config.BroadcastKey.VirtualKeyCode);
    }
}
