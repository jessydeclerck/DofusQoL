using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class BroadcastServiceTests : IDisposable
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly BroadcastService _service;

    public BroadcastServiceTests()
    {
        _mockHelper = new Mock<IWin32WindowHelper>();
        _mockHelper.Setup(h => h.IsWindowValid(It.IsAny<nint>())).Returns(true);
        _mockHelper.Setup(h => h.PostMessage(It.IsAny<nint>(), It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>())).Returns(true);
        _service = new BroadcastService(_mockHelper.Object);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static BroadcastPreset CreateKeyPreset(string targets = "all") => new()
    {
        Name = "Test Key",
        InputType = "key",
        Key = "Enter",
        Targets = targets,
        DelayMin = 0,
        DelayMax = 0,
        OrderMode = "profile"
    };

    private static BroadcastPreset CreateClickPreset(string button = "left") => new()
    {
        Name = "Test Click",
        InputType = "clickAtPosition",
        ClickX = 100,
        ClickY = 200,
        ClickButton = button,
        Targets = "all",
        DelayMin = 0,
        DelayMax = 0,
        OrderMode = "profile"
    };

    private static List<DofusWindow> CreateWindows(int count)
    {
        return Enumerable.Range(1, count).Select(i => new DofusWindow
        {
            Handle = (nint)(i * 100),
            ProcessId = i,
            Title = $"Dofus - Perso{i}",
            IsVisible = true,
            IsMinimized = false
        }).ToList();
    }

    // --- Envoi touche ---

    [Fact]
    public async Task ExecuteBroadcast_KeyTo4Windows_Sends4KeyDownAnd4KeyUp()
    {
        var windows = CreateWindows(4);
        var preset = CreateKeyPreset();

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.True(result.Success);
        Assert.Equal(4, result.WindowsTargeted);
        Assert.Equal(4, result.WindowsReached);

        // 4 fenêtres × 2 messages (keydown + keyup) = 8 appels PostMessage
        _mockHelper.Verify(
            h => h.PostMessage(It.IsAny<nint>(), 0x0100, It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Exactly(4));
        _mockHelper.Verify(
            h => h.PostMessage(It.IsAny<nint>(), 0x0101, It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task ExecuteBroadcast_KeySendsCorrectVirtualKey()
    {
        var windows = CreateWindows(1);
        var preset = CreateKeyPreset();
        preset.Key = "A";

        await _service.ExecuteBroadcastAsync(preset, windows, null);

        // 'A' = 0x41
        _mockHelper.Verify(
            h => h.PostMessage(windows[0].Handle, 0x0100, (nint)0x41, 0),
            Times.Once);
    }

    // --- Envoi clic ---

    [Fact]
    public async Task ExecuteBroadcast_ClickSendsCorrectCoordinates()
    {
        var windows = CreateWindows(1);
        var preset = CreateClickPreset();

        await _service.ExecuteBroadcastAsync(preset, windows, null);

        // lParam = (200 << 16) | 100 = 13107300
        var expectedLParam = (nint)((200 << 16) | 100);
        _mockHelper.Verify(
            h => h.PostMessage(windows[0].Handle, 0x0201, (nint)0x0001, expectedLParam),
            Times.Once);
        _mockHelper.Verify(
            h => h.PostMessage(windows[0].Handle, 0x0202, (nint)0x0001, expectedLParam),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteBroadcast_RightClickSendsRightButtonMessages()
    {
        var windows = CreateWindows(1);
        var preset = CreateClickPreset("right");

        await _service.ExecuteBroadcastAsync(preset, windows, null);

        _mockHelper.Verify(
            h => h.PostMessage(windows[0].Handle, 0x0204, It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Once);
        _mockHelper.Verify(
            h => h.PostMessage(windows[0].Handle, 0x0205, It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Once);
    }

    // --- Cibles ---

    [Fact]
    public async Task ExecuteBroadcast_AllTargets_SendsToAllWindows()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset("all");

        var result = await _service.ExecuteBroadcastAsync(preset, windows, (nint)100);

        Assert.Equal(3, result.WindowsTargeted);
        Assert.Equal(3, result.WindowsReached);
    }

    [Fact]
    public async Task ExecuteBroadcast_AllExceptLeader_ExcludesLeader()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset("allExceptLeader");
        var leaderHandle = windows[0].Handle; // handle 100

        var result = await _service.ExecuteBroadcastAsync(preset, windows, leaderHandle);

        Assert.Equal(2, result.WindowsTargeted);
        Assert.Equal(2, result.WindowsReached);

        // Le leader (handle 100) ne doit pas recevoir de message
        _mockHelper.Verify(
            h => h.PostMessage(leaderHandle, It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteBroadcast_CustomTargets_SendsOnlyToSelectedSlots()
    {
        var windows = CreateWindows(4);
        var preset = CreateKeyPreset("custom");
        preset.CustomTargetSlotIndices = [1, 3]; // fenêtres aux indices 1 et 3

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.Equal(2, result.WindowsTargeted);
        Assert.Equal(2, result.WindowsReached);

        // Seuls les handles 200 et 400 reçoivent des messages
        _mockHelper.Verify(
            h => h.PostMessage((nint)200, It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Exactly(2)); // keydown + keyup
        _mockHelper.Verify(
            h => h.PostMessage((nint)400, It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteBroadcast_AllExceptLeader_NoLeader_SendsToAll()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset("allExceptLeader");

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.Equal(3, result.WindowsTargeted);
    }

    // --- Fenêtre disparue ---

    [Fact]
    public async Task ExecuteBroadcast_WindowDisappeared_SkipsAndContinues()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset();

        // La 2e fenêtre (handle 200) a disparu
        _mockHelper.Setup(h => h.IsWindowValid((nint)200)).Returns(false);

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.True(result.Success);
        Assert.Equal(3, result.WindowsTargeted);
        Assert.Equal(2, result.WindowsReached);

        // PostMessage n'est pas appelé pour la fenêtre disparue
        _mockHelper.Verify(
            h => h.PostMessage((nint)200, It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Never);
    }

    // --- Cooldown ---

    [Fact]
    public async Task ExecuteBroadcast_CooldownActive_ReturnsError()
    {
        var windows = CreateWindows(2);
        var preset = CreateKeyPreset();
        _service.CooldownMs = 5000; // 5 secondes de cooldown

        // Premier broadcast → OK
        var result1 = await _service.ExecuteBroadcastAsync(preset, windows, null);
        Assert.True(result1.Success);

        // Deuxième broadcast immédiat → refusé
        var result2 = await _service.ExecuteBroadcastAsync(preset, windows, null);
        Assert.False(result2.Success);
        Assert.Contains("Cooldown", result2.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteBroadcast_CooldownExpired_AllowsNewBroadcast()
    {
        var windows = CreateWindows(1);
        var preset = CreateKeyPreset();
        _service.CooldownMs = 50; // 50ms de cooldown

        await _service.ExecuteBroadcastAsync(preset, windows, null);
        await Task.Delay(100); // attendre l'expiration

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);
        Assert.True(result.Success);
    }

    // --- Pause ---

    [Fact]
    public async Task ExecuteBroadcast_Paused_ReturnsError()
    {
        var windows = CreateWindows(2);
        var preset = CreateKeyPreset();
        _service.IsPaused = true;

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.False(result.Success);
        Assert.Contains("pause", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Aucun PostMessage envoyé
        _mockHelper.Verify(
            h => h.PostMessage(It.IsAny<nint>(), It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteBroadcast_UnpausedAfterPause_Works()
    {
        var windows = CreateWindows(1);
        var preset = CreateKeyPreset();

        _service.IsPaused = true;
        var result1 = await _service.ExecuteBroadcastAsync(preset, windows, null);
        Assert.False(result1.Success);

        _service.IsPaused = false;
        var result2 = await _service.ExecuteBroadcastAsync(preset, windows, null);
        Assert.True(result2.Success);
    }

    // --- Délais ---

    [Fact]
    public async Task ExecuteBroadcast_WithDelays_TakesAtLeastMinDelay()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset();
        preset.DelayMin = 20;
        preset.DelayMax = 30;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _service.ExecuteBroadcastAsync(preset, windows, null);
        sw.Stop();

        // 2 délais (entre 3 fenêtres) × 20ms minimum = 40ms minimum
        Assert.True(sw.ElapsedMilliseconds >= 30, $"Temps écoulé : {sw.ElapsedMilliseconds}ms, attendu >= 30ms");
    }

    // --- Ordre ---

    [Fact]
    public async Task ExecuteBroadcast_ProfileOrder_SendsInSlotOrder()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset();
        preset.OrderMode = "profile";

        var sentOrder = new List<nint>();
        _mockHelper.Setup(h => h.PostMessage(It.IsAny<nint>(), 0x0100, It.IsAny<nint>(), It.IsAny<nint>()))
            .Callback<nint, uint, nint, nint>((handle, _, _, _) => sentOrder.Add(handle))
            .Returns(true);

        await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.Equal([(nint)100, (nint)200, (nint)300], sentOrder);
    }

    // --- Validation ---

    [Fact]
    public async Task ExecuteBroadcast_InvalidPreset_ReturnsError()
    {
        var windows = CreateWindows(2);
        var preset = new BroadcastPreset
        {
            Name = "Bad",
            InputType = "invalid"
        };

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.False(result.Success);
        Assert.Contains("InputType invalide", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteBroadcast_NoTargets_ReturnsError()
    {
        var preset = CreateKeyPreset();
        var emptyWindows = new List<DofusWindow>();

        var result = await _service.ExecuteBroadcastAsync(preset, emptyWindows, null);

        Assert.False(result.Success);
        Assert.Contains("Aucune fenêtre cible", result.ErrorMessage);
    }

    // --- MakeLParam ---

    [Fact]
    public void MakeLParam_CorrectlyEncodesCoordinates()
    {
        var result = BroadcastService.MakeLParam(100, 200);
        // (200 << 16) | 100 = 13107300
        Assert.Equal((nint)((200 << 16) | 100), result);
    }

    [Fact]
    public void MakeLParam_ZeroCoordinates()
    {
        var result = BroadcastService.MakeLParam(0, 0);
        Assert.Equal((nint)0, result);
    }

    // --- KeyNameToVirtualKey ---

    [Theory]
    [InlineData("Enter", 0x0Du)]
    [InlineData("ENTER", 0x0Du)]
    [InlineData("Return", 0x0Du)]
    [InlineData("Escape", 0x1Bu)]
    [InlineData("Space", 0x20u)]
    [InlineData("Tab", 0x09u)]
    [InlineData("A", 0x41u)]
    [InlineData("Z", 0x5Au)]
    [InlineData("0", 0x30u)]
    [InlineData("9", 0x39u)]
    [InlineData("F1", 0x70u)]
    [InlineData("F12", 0x7Bu)]
    public void KeyNameToVirtualKey_ReturnsCorrectCode(string keyName, uint expected)
    {
        Assert.Equal(expected, BroadcastService.KeyNameToVirtualKey(keyName));
    }

    [Fact]
    public void KeyNameToVirtualKey_UnknownKey_ReturnsZero()
    {
        Assert.Equal(0u, BroadcastService.KeyNameToVirtualKey("UnknownKey"));
    }

    // --- ResolveTargets ---

    [Fact]
    public void ResolveTargets_Custom_OutOfBoundsIndicesIgnored()
    {
        var windows = CreateWindows(3);
        var preset = CreateKeyPreset("custom");
        preset.CustomTargetSlotIndices = [0, 5, -1, 2]; // 5 et -1 hors bornes

        var targets = _service.ResolveTargets(preset, windows, null);

        Assert.Equal(2, targets.Count);
        Assert.Equal((nint)100, targets[0].Handle);
        Assert.Equal((nint)300, targets[1].Handle);
    }

    // --- Cancellation ---

    [Fact]
    public async Task ExecuteBroadcast_Cancelled_ThrowsOperationCanceled()
    {
        var windows = CreateWindows(5);
        var preset = CreateKeyPreset();
        preset.DelayMin = 100;
        preset.DelayMax = 200;

        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ExecuteBroadcastAsync(preset, windows, null, cts.Token));
    }

    // --- Clic avec PostMessage qui échoue ---

    [Fact]
    public async Task ExecuteBroadcast_PostMessageFails_WindowNotReached()
    {
        var windows = CreateWindows(2);
        var preset = CreateKeyPreset();

        // PostMessage échoue pour la 1ère fenêtre
        _mockHelper.Setup(h => h.PostMessage((nint)100, It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()))
            .Returns(false);

        var result = await _service.ExecuteBroadcastAsync(preset, windows, null);

        Assert.True(result.Success);
        Assert.Equal(2, result.WindowsTargeted);
        Assert.Equal(1, result.WindowsReached); // seulement la 2e fenêtre
    }
}
