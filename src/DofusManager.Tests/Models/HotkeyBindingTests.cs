using DofusManager.Core.Models;
using Xunit;

namespace DofusManager.Tests.Models;

public class HotkeyBindingTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var binding = new HotkeyBinding
        {
            Id = 1,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKeyCode = 0x70, // VK_F1
            DisplayName = "Ctrl+Shift+F1",
            Action = HotkeyAction.FocusSlot,
            SlotIndex = 0
        };

        Assert.Equal(1, binding.Id);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, binding.Modifiers);
        Assert.Equal(0x70u, binding.VirtualKeyCode);
        Assert.Equal("Ctrl+Shift+F1", binding.DisplayName);
        Assert.Equal(HotkeyAction.FocusSlot, binding.Action);
        Assert.Equal(0, binding.SlotIndex);
    }

    [Fact]
    public void SlotIndex_DefaultsToNull()
    {
        var binding = new HotkeyBinding
        {
            Id = 1,
            Modifiers = HotkeyModifiers.None,
            VirtualKeyCode = 0x09, // VK_TAB
            DisplayName = "Tab",
            Action = HotkeyAction.NextWindow
        };

        Assert.Null(binding.SlotIndex);
    }

    [Fact]
    public void HotkeyModifiers_FlagsCombination()
    {
        var mods = HotkeyModifiers.Control | HotkeyModifiers.Alt;

        Assert.True(mods.HasFlag(HotkeyModifiers.Control));
        Assert.True(mods.HasFlag(HotkeyModifiers.Alt));
        Assert.False(mods.HasFlag(HotkeyModifiers.Shift));
        Assert.Equal(0x0003u, (uint)mods);
    }

    [Fact]
    public void HotkeyAction_CoversAllCases()
    {
        var values = Enum.GetValues<HotkeyAction>();

        Assert.Contains(HotkeyAction.FocusSlot, values);
        Assert.Contains(HotkeyAction.NextWindow, values);
        Assert.Contains(HotkeyAction.PreviousWindow, values);
        Assert.Contains(HotkeyAction.LastWindow, values);
        Assert.Contains(HotkeyAction.PanicLeader, values);
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void FocusResult_Ok_IsSuccess()
    {
        var result = FocusResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void FocusResult_Error_HasMessage()
    {
        var result = FocusResult.Error("Fenêtre disparue");

        Assert.False(result.Success);
        Assert.Equal("Fenêtre disparue", result.ErrorMessage);
    }
}
