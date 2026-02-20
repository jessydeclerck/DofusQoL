using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class ZaapTravelServiceTests
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly ZaapTravelService _service;

    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_H = 0x48;

    public ZaapTravelServiceTests()
    {
        _mockHelper = new Mock<IWin32WindowHelper>();

        // Par défaut, FocusWindow réussit et GetForegroundWindow retourne le dernier handle focusé
        nint lastFocusedHandle = 0;
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>()))
            .Callback<nint>(handle => lastFocusedHandle = handle)
            .Returns(true);
        _mockHelper.Setup(h => h.GetForegroundWindow())
            .Returns(() => lastFocusedHandle);

        _mockHelper.Setup(h => h.SendKeyPress(It.IsAny<ushort>())).Returns(true);
        _mockHelper.Setup(h => h.SendText(It.IsAny<string>())).Returns(true);
        _mockHelper.Setup(h => h.SetCursorPos(It.IsAny<int>(), It.IsAny<int>())).Returns(true);
        _mockHelper.Setup(h => h.SendMouseClick()).Returns(true);
        _mockHelper.Setup(h => h.ClientToScreen(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((nint _, int x, int y) => (x + 100, y + 100));

        _service = new ZaapTravelService(_mockHelper.Object);
        _service.ZaapClickX = 500;
        _service.ZaapClickY = 300;
    }

    private static List<DofusWindow> CreateWindows(params string[] names)
    {
        return names.Select((name, i) => new DofusWindow
        {
            Handle = (nint)((i + 1) * 100),
            ProcessId = i + 1,
            Title = $"{name} - Pandawa - 3.4.18.19 - Release",
            IsVisible = true,
            IsMinimized = false
        }).ToList();
    }

    [Fact]
    public async Task TravelToZaapAsync_EmptyWindows_ReturnsFailure()
    {
        var leader = CreateWindows("Leader")[0];

        var result = await _service.TravelToZaapAsync(new List<DofusWindow>(), leader, "Amakna");

        Assert.False(result.Success);
        Assert.Contains("Aucune fenêtre", result.ErrorMessage!);
    }

    [Fact]
    public async Task TravelToZaapAsync_ZeroCoordinates_ReturnsConfigError()
    {
        _service.ZaapClickX = 0;
        _service.ZaapClickY = 0;

        var windows = CreateWindows("Perso1");
        var leader = windows[0];

        var result = await _service.TravelToZaapAsync(windows, leader, "Amakna");

        Assert.False(result.Success);
        Assert.Contains("Coordonnées", result.ErrorMessage!);
    }

    [Fact]
    public async Task TravelToZaapAsync_SendsCorrectSequence()
    {
        var windows = CreateWindows("Perso1");
        var leader = windows[0];

        var callOrder = new List<string>();

        _mockHelper.Setup(h => h.SendKeyPress(VK_H))
            .Callback(() => callOrder.Add("HAVRESAC"))
            .Returns(true);
        _mockHelper.Setup(h => h.SetCursorPos(It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int, int>((x, y) => callOrder.Add($"CURSOR:{x},{y}"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendMouseClick())
            .Callback(() => callOrder.Add("CLICK"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendText(It.IsAny<string>()))
            .Callback<string>(text => callOrder.Add($"TEXT:{text}"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => callOrder.Add("ENTER"))
            .Returns(true);

        await _service.TravelToZaapAsync(windows, leader, "Amakna");

        // Séquence attendue : HAVRESAC, CURSOR, CLICK, TEXT, ENTER
        Assert.Equal("HAVRESAC", callOrder[0]);
        Assert.StartsWith("CURSOR:", callOrder[1]);
        Assert.Equal("CLICK", callOrder[2]);
        Assert.Equal("TEXT:Amakna", callOrder[3]);
        Assert.Equal("ENTER", callOrder[4]);
    }

    [Fact]
    public async Task TravelToZaapAsync_MultipleWindows_ProcessesAll()
    {
        var windows = CreateWindows("Leader", "Perso2", "Perso3");
        var leader = windows[0];

        var result = await _service.TravelToZaapAsync(windows, leader, "Bonta");

        Assert.True(result.Success);
        Assert.Equal(3, result.Invited);

        // Vérifie que le havre-sac a été envoyé 3 fois
        _mockHelper.Verify(h => h.SendKeyPress(VK_H), Times.Exactly(3));
        _mockHelper.Verify(h => h.SendText("Bonta"), Times.Exactly(3));
    }

    [Fact]
    public async Task TravelToZaapAsync_SkipsFocusFailedWindows()
    {
        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        // Perso2 (handle 200) ne peut pas être focusé
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>())).Returns(true);
        _mockHelper.Setup(h => h.GetForegroundWindow()).Returns((nint)0);

        // Seul le leader réussit (handle 100)
        _mockHelper.Setup(h => h.FocusWindow((nint)100))
            .Callback(() =>
            {
                _mockHelper.Setup(h => h.GetForegroundWindow()).Returns((nint)100);
            })
            .Returns(true);

        var result = await _service.TravelToZaapAsync(windows, leader, "Amakna");

        Assert.True(result.Success);
        Assert.Equal(1, result.Invited);
    }

    [Fact]
    public async Task TravelToZaapAsync_RestoresFocusToLeader()
    {
        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        await _service.TravelToZaapAsync(windows, leader, "Amakna");

        // Le dernier appel à FocusWindow doit être pour le leader
        var calls = _mockHelper.Invocations
            .Where(i => i.Method.Name == nameof(IWin32WindowHelper.FocusWindow))
            .Select(i => (nint)i.Arguments[0])
            .ToList();

        Assert.Equal(leader.Handle, calls.Last());
    }

    [Fact]
    public async Task TravelToZaapAsync_UsesCustomHavreSacKey()
    {
        const ushort customKey = 0x54; // 'T'
        _service.HavreSacKeyCode = customKey;

        var windows = CreateWindows("Perso1");
        var leader = windows[0];

        await _service.TravelToZaapAsync(windows, leader, "Amakna");

        _mockHelper.Verify(h => h.SendKeyPress(customKey), Times.Once);
        _mockHelper.Verify(h => h.SendKeyPress(VK_H), Times.Never);
    }

    [Fact]
    public async Task TravelToZaapAsync_UsesConfiguredCoordinates()
    {
        _service.ZaapClickX = 800;
        _service.ZaapClickY = 450;

        var windows = CreateWindows("Perso1");
        var leader = windows[0];

        await _service.TravelToZaapAsync(windows, leader, "Bonta");

        // ClientToScreen ajoute +100 dans le mock, donc 800→900, 450→550
        _mockHelper.Verify(h => h.ClientToScreen(windows[0].Handle, 800, 450), Times.Once);
        _mockHelper.Verify(h => h.SetCursorPos(900, 550), Times.AtLeastOnce);
    }
}
