using DofusManager.Core.Models;
using Xunit;

namespace DofusManager.Tests.Models;

public class HotkeyDefaultsTests
{
    [Theory]
    [InlineData(0, "F1", 0x70u)]
    [InlineData(1, "F2", 0x71u)]
    [InlineData(2, "F3", 0x72u)]
    [InlineData(3, "F4", 0x73u)]
    [InlineData(4, "F5", 0x74u)]
    [InlineData(5, "F6", 0x75u)]
    [InlineData(6, "F7", 0x76u)]
    [InlineData(7, "F8", 0x77u)]
    public void GetDefaultSlotHotkey_ValidIndex_ReturnsFKey(int index, string expectedDisplay, uint expectedVk)
    {
        var result = HotkeyDefaults.GetDefaultSlotHotkey(index);

        Assert.NotNull(result);
        Assert.Equal(HotkeyModifiers.None, result.Value.Modifiers);
        Assert.Equal(expectedVk, result.Value.VirtualKeyCode);
        Assert.Equal(expectedDisplay, result.Value.DisplayName);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(100)]
    public void GetDefaultSlotHotkey_IndexTooHigh_ReturnsNull(int index)
    {
        var result = HotkeyDefaults.GetDefaultSlotHotkey(index);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public void GetDefaultSlotHotkey_NegativeIndex_ReturnsNull(int index)
    {
        var result = HotkeyDefaults.GetDefaultSlotHotkey(index);

        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultSlotHotkey_AllValidSlots_HaveNoModifiers()
    {
        for (var i = 0; i < 8; i++)
        {
            var result = HotkeyDefaults.GetDefaultSlotHotkey(i);
            Assert.NotNull(result);
            Assert.Equal(HotkeyModifiers.None, result.Value.Modifiers);
        }
    }
}
