using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class FocusServiceTests
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly FocusService _service;

    public FocusServiceTests()
    {
        _mockHelper = new Mock<IWin32WindowHelper>();
        // Par défaut, toutes les fenêtres sont valides et le focus réussit
        _mockHelper.Setup(h => h.IsWindowValid(It.IsAny<nint>())).Returns(true);
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>())).Returns(true);
        _service = new FocusService(_mockHelper.Object);
    }

    private static DofusWindow CreateWindow(nint handle, string title = "Dofus")
    {
        return new DofusWindow
        {
            Handle = handle,
            ProcessId = (int)handle,
            Title = title,
            IsVisible = true,
            IsMinimized = false
        };
    }

    private void SetupSlots(params DofusWindow[] windows)
    {
        _service.UpdateSlots(windows.ToList());
    }

    // --- Slots & State ---

    [Fact]
    public void Slots_InitiallyEmpty()
    {
        Assert.Empty(_service.Slots);
    }

    [Fact]
    public void UpdateSlots_SetsSlots()
    {
        var w1 = CreateWindow(1, "Dofus - Panda");
        var w2 = CreateWindow(2, "Dofus - Iop");

        SetupSlots(w1, w2);

        Assert.Equal(2, _service.Slots.Count);
        Assert.Equal("Dofus - Panda", _service.Slots[0].Title);
    }

    [Fact]
    public void CurrentSlotIndex_InitiallyNull()
    {
        Assert.Null(_service.CurrentSlotIndex);
    }

    // --- FocusSlot ---

    [Fact]
    public void FocusSlot_ValidIndex_Success()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2));

        var result = _service.FocusSlot(0);

        Assert.True(result.Success);
        Assert.Equal(0, _service.CurrentSlotIndex);
        _mockHelper.Verify(h => h.FocusWindow(1), Times.Once);
    }

    [Fact]
    public void FocusSlot_EmptySlots_ReturnsError()
    {
        var result = _service.FocusSlot(0);

        Assert.False(result.Success);
        Assert.Contains("Aucune fenêtre", result.ErrorMessage);
    }

    [Fact]
    public void FocusSlot_OutOfBounds_ReturnsError()
    {
        SetupSlots(CreateWindow(1));

        var result = _service.FocusSlot(5);

        Assert.False(result.Success);
        Assert.Contains("hors bornes", result.ErrorMessage);
    }

    [Fact]
    public void FocusSlot_NegativeIndex_ReturnsError()
    {
        SetupSlots(CreateWindow(1));

        var result = _service.FocusSlot(-1);

        Assert.False(result.Success);
    }

    [Fact]
    public void FocusSlot_WindowInvalid_ReturnsError()
    {
        var window = CreateWindow(1, "Dofus - Disparu");
        SetupSlots(window);
        _mockHelper.Setup(h => h.IsWindowValid(1)).Returns(false);

        var result = _service.FocusSlot(0);

        Assert.False(result.Success);
        Assert.Contains("n'existe plus", result.ErrorMessage);
    }

    [Fact]
    public void FocusSlot_FocusFails_ReturnsError()
    {
        SetupSlots(CreateWindow(1));
        _mockHelper.Setup(h => h.FocusWindow(1)).Returns(false);

        var result = _service.FocusSlot(0);

        Assert.False(result.Success);
        Assert.Contains("Impossible de focus", result.ErrorMessage);
    }

    // --- FocusNext ---

    [Fact]
    public void FocusNext_FromSlot0_GoesToSlot1()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2), CreateWindow(3));
        _service.FocusSlot(0);

        var result = _service.FocusNext();

        Assert.True(result.Success);
        Assert.Equal(1, _service.CurrentSlotIndex);
    }

    [Fact]
    public void FocusNext_FromLastSlot_WrapsToSlot0()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2));
        _service.FocusSlot(1);

        var result = _service.FocusNext();

        Assert.True(result.Success);
        Assert.Equal(0, _service.CurrentSlotIndex);
    }

    [Fact]
    public void FocusNext_NoCurrent_GoesToSlot0()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2));

        var result = _service.FocusNext();

        Assert.True(result.Success);
        Assert.Equal(0, _service.CurrentSlotIndex);
    }

    [Fact]
    public void FocusNext_EmptySlots_ReturnsError()
    {
        var result = _service.FocusNext();

        Assert.False(result.Success);
    }

    // --- FocusPrevious ---

    [Fact]
    public void FocusPrevious_FromSlot1_GoesToSlot0()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2), CreateWindow(3));
        _service.FocusSlot(1);

        var result = _service.FocusPrevious();

        Assert.True(result.Success);
        Assert.Equal(0, _service.CurrentSlotIndex);
    }

    [Fact]
    public void FocusPrevious_FromSlot0_WrapsToLastSlot()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2), CreateWindow(3));
        _service.FocusSlot(0);

        var result = _service.FocusPrevious();

        Assert.True(result.Success);
        Assert.Equal(2, _service.CurrentSlotIndex);
    }

    [Fact]
    public void FocusPrevious_NoCurrent_GoesToLastSlot()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2), CreateWindow(3));

        var result = _service.FocusPrevious();

        Assert.True(result.Success);
        Assert.Equal(2, _service.CurrentSlotIndex);
    }

    // --- FocusLast ---

    [Fact]
    public void FocusLast_NoHistory_ReturnsError()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2));

        var result = _service.FocusLast();

        Assert.False(result.Success);
        Assert.Contains("précédemment", result.ErrorMessage);
    }

    [Fact]
    public void FocusLast_AfterTwoFocus_ReturnsToPrevious()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2), CreateWindow(3));
        _service.FocusSlot(0);
        _service.FocusSlot(2);

        var result = _service.FocusLast();

        Assert.True(result.Success);
        Assert.Equal(0, _service.CurrentSlotIndex);
    }

    [Fact]
    public void FocusLast_SlotOutOfRange_ReturnsError()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2), CreateWindow(3));
        _service.FocusSlot(1);
        _service.FocusSlot(2);

        // Réduire les slots pour que _lastSlotIndex (1) devienne hors bornes
        _service.UpdateSlots([CreateWindow(1)]);

        var result = _service.FocusLast();

        Assert.False(result.Success);
        Assert.Contains("n'existe plus", result.ErrorMessage);
    }

    // --- FocusLeader ---

    [Fact]
    public void FocusLeader_DefaultsToSlot0()
    {
        SetupSlots(CreateWindow(1, "Dofus - Leader"), CreateWindow(2));

        var result = _service.FocusLeader();

        Assert.True(result.Success);
        Assert.Equal(0, _service.CurrentSlotIndex);
        _mockHelper.Verify(h => h.FocusWindow(1), Times.Once);
    }

    [Fact]
    public void FocusLeader_WithDesignatedLeader_FocusesCorrectWindow()
    {
        SetupSlots(CreateWindow(1), CreateWindow(2, "Dofus - Leader"));
        _service.SetLeader(2);

        var result = _service.FocusLeader();

        Assert.True(result.Success);
        Assert.Equal(1, _service.CurrentSlotIndex);
        _mockHelper.Verify(h => h.FocusWindow(2), Times.Once);
    }

    [Fact]
    public void FocusLeader_NoWindows_ReturnsError()
    {
        var result = _service.FocusLeader();

        Assert.False(result.Success);
    }

    // --- SetLeader ---

    [Fact]
    public void SetLeader_UpdatesCurrentLeader()
    {
        var leader = CreateWindow(42, "Dofus - Boss");
        SetupSlots(CreateWindow(1), leader);
        _service.SetLeader(42);

        Assert.NotNull(_service.CurrentLeader);
        Assert.Equal(42, _service.CurrentLeader!.Handle);
    }

    [Fact]
    public void CurrentLeader_DefaultsToFirstSlot()
    {
        var first = CreateWindow(1, "Dofus - First");
        SetupSlots(first, CreateWindow(2));

        Assert.NotNull(_service.CurrentLeader);
        Assert.Equal(1, _service.CurrentLeader!.Handle);
    }

    [Fact]
    public void CurrentLeader_NoSlots_ReturnsNull()
    {
        Assert.Null(_service.CurrentLeader);
    }
}
