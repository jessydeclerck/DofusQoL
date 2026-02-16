using DofusManager.Core.Models;
using Xunit;

namespace DofusManager.Tests.Models;

public class DofusWindowTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var window = new DofusWindow
        {
            Handle = 0x1234,
            ProcessId = 42,
            Title = "Dofus - MonPerso",
            IsVisible = true,
            IsMinimized = false,
            ScreenName = @"\\.\DISPLAY1"
        };

        Assert.Equal(0x1234, window.Handle);
        Assert.Equal(42, window.ProcessId);
        Assert.Equal("Dofus - MonPerso", window.Title);
        Assert.True(window.IsVisible);
        Assert.False(window.IsMinimized);
        Assert.Equal(@"\\.\DISPLAY1", window.ScreenName);
        Assert.True(window.DetectedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Equals_SameHandle_ReturnsTrue()
    {
        var a = new DofusWindow { Handle = 100, ProcessId = 1, Title = "A", IsVisible = true, IsMinimized = false };
        var b = new DofusWindow { Handle = 100, ProcessId = 2, Title = "B", IsVisible = false, IsMinimized = true };

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentHandle_ReturnsFalse()
    {
        var a = new DofusWindow { Handle = 100, ProcessId = 1, Title = "A", IsVisible = true, IsMinimized = false };
        var b = new DofusWindow { Handle = 200, ProcessId = 1, Title = "A", IsVisible = true, IsMinimized = false };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var window = new DofusWindow { Handle = 100, ProcessId = 1, Title = "A", IsVisible = true, IsMinimized = false };

        Assert.False(window.Equals(null));
    }

    [Fact]
    public void GetHashCode_SameHandle_SameHash()
    {
        var a = new DofusWindow { Handle = 100, ProcessId = 1, Title = "A", IsVisible = true, IsMinimized = false };
        var b = new DofusWindow { Handle = 100, ProcessId = 2, Title = "B", IsVisible = false, IsMinimized = true };

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsPidAndTitle()
    {
        var window = new DofusWindow { Handle = 100, ProcessId = 42, Title = "Dofus - Panda", IsVisible = true, IsMinimized = false };

        var result = window.ToString();

        Assert.Contains("42", result);
        Assert.Contains("Dofus - Panda", result);
    }
}
