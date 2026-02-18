using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class HotkeyServiceTests : IDisposable
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly HotkeyService _service;
    private const nint TestHwnd = 0xABCD;

    public HotkeyServiceTests()
    {
        _mockHelper = new Mock<IWin32WindowHelper>();
        _service = new HotkeyService(_mockHelper.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    private HotkeyBinding CreateBinding(int id = 1, uint vk = 0x70, HotkeyAction action = HotkeyAction.FocusSlot)
    {
        return new HotkeyBinding
        {
            Id = id,
            Modifiers = HotkeyModifiers.None,
            VirtualKeyCode = vk,
            DisplayName = $"F{id}",
            Action = action,
            SlotIndex = action == HotkeyAction.FocusSlot ? id - 1 : null
        };
    }

    [Fact]
    public void IsInitialized_InitiallyFalse()
    {
        Assert.False(_service.IsInitialized);
    }

    [Fact]
    public void Initialize_SetsIsInitialized()
    {
        _service.Initialize(TestHwnd);

        Assert.True(_service.IsInitialized);
    }

    [Fact]
    public void Initialize_WithZeroHandle_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.Initialize(0));
    }

    [Fact]
    public void Register_WithoutInitialize_Throws()
    {
        var binding = CreateBinding();

        Assert.Throws<InvalidOperationException>(() => _service.Register(binding));
    }

    [Fact]
    public void Register_CallsWin32RegisterHotKey()
    {
        _service.Initialize(TestHwnd);
        // MOD_NOREPEAT (0x4000) est ajouté automatiquement par le service
        _mockHelper.Setup(h => h.RegisterHotKey(TestHwnd, 1, 0x4000, 0x70)).Returns(true);

        var result = _service.Register(CreateBinding());

        Assert.True(result);
        _mockHelper.Verify(h => h.RegisterHotKey(TestHwnd, 1, 0x4000, 0x70), Times.Once);
    }

    [Fact]
    public void Register_Failure_ReturnsFalse()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(false);

        var result = _service.Register(CreateBinding());

        Assert.False(result);
        Assert.Empty(_service.RegisteredHotkeys);
    }

    [Fact]
    public void Register_Success_AddsToRegisteredHotkeys()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);

        _service.Register(CreateBinding());

        Assert.Single(_service.RegisteredHotkeys);
    }

    [Fact]
    public void Register_DuplicateId_UnregistersFirst()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);

        _service.Register(CreateBinding(id: 1, vk: 0x70));
        _service.Register(CreateBinding(id: 1, vk: 0x71));

        _mockHelper.Verify(h => h.UnregisterHotKey(TestHwnd, 1), Times.Once);
        Assert.Single(_service.RegisteredHotkeys);
        Assert.Equal(0x71u, _service.RegisteredHotkeys[0].VirtualKeyCode);
    }

    [Fact]
    public void Unregister_RemovesBinding()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);

        _service.Register(CreateBinding());
        _service.Unregister(1);

        Assert.Empty(_service.RegisteredHotkeys);
        _mockHelper.Verify(h => h.UnregisterHotKey(TestHwnd, 1), Times.Once);
    }

    [Fact]
    public void Unregister_UnknownId_DoesNotThrow()
    {
        _service.Initialize(TestHwnd);

        _service.Unregister(999);

        _mockHelper.Verify(h => h.UnregisterHotKey(It.IsAny<nint>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void UnregisterAll_RemovesAllBindings()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);

        _service.Register(CreateBinding(id: 1, vk: 0x70));
        _service.Register(CreateBinding(id: 2, vk: 0x71));
        _service.UnregisterAll();

        Assert.Empty(_service.RegisteredHotkeys);
        _mockHelper.Verify(h => h.UnregisterHotKey(TestHwnd, 1), Times.Once);
        _mockHelper.Verify(h => h.UnregisterHotKey(TestHwnd, 2), Times.Once);
    }

    [Fact]
    public void ProcessMessage_KnownId_RaisesHotkeyPressed()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);
        _service.Register(CreateBinding());

        HotkeyPressedEventArgs? eventArgs = null;
        _service.HotkeyPressed += (_, args) => eventArgs = args;

        var handled = _service.ProcessMessage(1, 0);

        Assert.True(handled);
        Assert.NotNull(eventArgs);
        Assert.Equal(1, eventArgs.HotkeyId);
        Assert.Equal(HotkeyAction.FocusSlot, eventArgs.Binding.Action);
    }

    [Fact]
    public void ProcessMessage_UnknownId_ReturnsFalse()
    {
        _service.Initialize(TestHwnd);

        HotkeyPressedEventArgs? eventArgs = null;
        _service.HotkeyPressed += (_, args) => eventArgs = args;

        var handled = _service.ProcessMessage(999, 0);

        Assert.False(handled);
        Assert.Null(eventArgs);
    }

    [Fact]
    public void Dispose_UnregistersAll()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);

        _service.Register(CreateBinding(id: 1));
        _service.Register(CreateBinding(id: 2, vk: 0x71));
        _service.Dispose();

        Assert.Empty(_service.RegisteredHotkeys);
    }

    // --- Mouse button bindings ---

    [Fact]
    public void Register_MouseBinding_DoesNotCallRegisterHotKey()
    {
        _service.Initialize(TestHwnd);

        var binding = new HotkeyBinding
        {
            Id = 10,
            Modifiers = HotkeyModifiers.None,
            VirtualKeyCode = 0x05, // VK_XBUTTON1
            DisplayName = "Souris 4",
            Action = HotkeyAction.NextWindow
        };

        var result = _service.Register(binding);

        Assert.True(result);
        _mockHelper.Verify(
            h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()),
            Times.Never);
    }

    [Fact]
    public void Register_MouseBinding_AppearsInRegisteredHotkeys()
    {
        _service.Initialize(TestHwnd);

        var binding = new HotkeyBinding
        {
            Id = 10,
            Modifiers = HotkeyModifiers.None,
            VirtualKeyCode = 0x06, // VK_XBUTTON2
            DisplayName = "Souris 5",
            Action = HotkeyAction.PreviousWindow
        };

        _service.Register(binding);

        Assert.Single(_service.RegisteredHotkeys);
        Assert.Equal(0x06u, _service.RegisteredHotkeys[0].VirtualKeyCode);
    }

    [Fact]
    public void UnregisterAll_ClearsBothKeyboardAndMouseBindings()
    {
        _service.Initialize(TestHwnd);
        _mockHelper.Setup(h => h.RegisterHotKey(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
            .Returns(true);

        // Register keyboard binding
        _service.Register(CreateBinding(id: 1, vk: 0x70));

        // Register mouse binding
        _service.Register(new HotkeyBinding
        {
            Id = 10,
            Modifiers = HotkeyModifiers.None,
            VirtualKeyCode = 0x05,
            DisplayName = "Souris 4",
            Action = HotkeyAction.NextWindow
        });

        Assert.Equal(2, _service.RegisteredHotkeys.Count);

        _service.UnregisterAll();

        Assert.Empty(_service.RegisteredHotkeys);
    }

    [Theory]
    [InlineData(0x04u, true)]   // VK_MBUTTON
    [InlineData(0x05u, true)]   // VK_XBUTTON1
    [InlineData(0x06u, true)]   // VK_XBUTTON2
    [InlineData(0x70u, false)]  // VK_F1
    [InlineData(0x09u, false)]  // VK_TAB
    [InlineData(0x12u, false)]  // VK_MENU
    public void HotkeyBinding_IsMouseButton_Theory(uint vk, bool expected)
    {
        var binding = new HotkeyBinding
        {
            Id = 1,
            Modifiers = HotkeyModifiers.None,
            VirtualKeyCode = vk,
            DisplayName = "Test",
            Action = HotkeyAction.FocusSlot
        };

        Assert.Equal(expected, binding.IsMouseButton);
    }
}
