using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class PushToBroadcastServiceTests : IDisposable
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly PushToBroadcastService _service;

    public PushToBroadcastServiceTests()
    {
        _mockHelper = new Mock<IWin32WindowHelper>();
        _mockHelper.Setup(h => h.IsWindowValid(It.IsAny<nint>())).Returns(true);
        _mockHelper.Setup(h => h.SetCursorPos(It.IsAny<int>(), It.IsAny<int>())).Returns(true);
        _mockHelper.Setup(h => h.SendMouseClick()).Returns(true);
        _mockHelper.Setup(h => h.SendMouseUp()).Returns(true);
        _mockHelper.Setup(h => h.GetCursorPos()).Returns((500, 300));

        // Simuler que FocusWindow change le foreground : GetForegroundWindow
        // retourne le dernier handle passé à FocusWindow
        nint lastFocusedHandle = 0;
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>()))
            .Callback<nint>(handle => lastFocusedHandle = handle)
            .Returns(true);
        _mockHelper.Setup(h => h.GetForegroundWindow())
            .Returns(() => lastFocusedHandle);

        _service = new PushToBroadcastService(_mockHelper.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
        GC.SuppressFinalize(this);
    }

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

    // --- Arm / Disarm ---

    [Fact]
    public void Arm_WithWindows_SetsIsArmedTrue()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);

        Assert.True(_service.IsArmed);
    }

    [Fact]
    public void Arm_NoWindows_DoesNotArm()
    {
        _service.Arm(new List<DofusWindow>());

        Assert.False(_service.IsArmed);
    }

    [Fact]
    public void Arm_AlreadyArmed_IgnoresSecondCall()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);
        _service.Arm(windows);

        Assert.True(_service.IsArmed);
    }

    [Fact]
    public void Disarm_WhenArmed_SetsIsArmedFalse()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);

        _service.Disarm();

        Assert.False(_service.IsArmed);
    }

    [Fact]
    public void Disarm_WhenNotArmed_NoOp()
    {
        _service.Disarm();
        Assert.False(_service.IsArmed);
    }

    // --- ProcessMouseClick ---

    [Fact]
    public void ProcessMouseClick_WhenNotArmed_ReturnsZero()
    {
        var result = _service.ProcessMouseClick(100, 200, (nint)100);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ProcessMouseClick_ForegroundNotDofus_ReturnsZero()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);

        // sourceHandle 999 ne correspond à aucune fenêtre Dofus
        var result = _service.ProcessMouseClick(100, 200, (nint)999);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ProcessMouseClick_BroadcastsToOtherWindows()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);

        // La fenêtre source est la première (handle 100)
        _mockHelper.Setup(h => h.ScreenToClient((nint)100, 500, 300)).Returns((50, 30));
        // ClientToScreen pour chaque cible
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        // 2 fenêtres cibles (200, 300) doivent recevoir le clic
        Assert.Equal(2, result);

        // Vérifier FocusWindow pour chaque cible + restauration source
        _mockHelper.Verify(h => h.FocusWindow((nint)200), Times.Once);
        _mockHelper.Verify(h => h.FocusWindow((nint)300), Times.Once);
        _mockHelper.Verify(h => h.FocusWindow((nint)100), Times.Once); // restauration

        // Vérifier SetCursorPos pour chaque cible
        _mockHelper.Verify(h => h.SetCursorPos(550, 330), Times.Once);
        _mockHelper.Verify(h => h.SetCursorPos(600, 360), Times.Once);

        // Vérifier SendMouseClick pour chaque cible
        _mockHelper.Verify(h => h.SendMouseClick(), Times.Exactly(2));

        // La source NE reçoit PAS de SendMouseClick (seulement FocusWindow pour restauration)
        _mockHelper.Verify(
            h => h.PostMessage((nint)100, It.IsAny<uint>(), It.IsAny<nint>(), It.IsAny<nint>()),
            Times.Never);
    }

    [Fact]
    public void ProcessMouseClick_RestoresCursorPosition()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, 500, 300)).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.GetCursorPos()).Returns((500, 300));

        _service.ProcessMouseClick(500, 300, (nint)100);

        // Le curseur est restauré à sa position d'origine
        _mockHelper.Verify(h => h.SetCursorPos(500, 300), Times.Once);
    }

    [Fact]
    public void ProcessMouseClick_ScreenToClientFails_ReturnsZero()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient(It.IsAny<nint>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(((int, int)?)null);

        var result = _service.ProcessMouseClick(500, 300, (nint)100);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ProcessMouseClick_SkipsInvalidWindows()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        // La fenêtre 200 a disparu
        _mockHelper.Setup(h => h.IsWindowValid((nint)200)).Returns(false);

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(1, result);

        // Pas de FocusWindow pour la fenêtre disparue
        _mockHelper.Verify(h => h.FocusWindow((nint)200), Times.Never);
    }

    [Fact]
    public void ProcessMouseClick_SingleWindow_ReturnsZero()
    {
        var windows = CreateWindows(1);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));

        var result = _service.ProcessMouseClick(500, 300, (nint)100);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ProcessMouseClick_ClientToScreenFails_SkipsWindow()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        // ClientToScreen échoue pour fenêtre 200
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns(((int, int)?)null);
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(1, result);
    }

    // --- BroadcastPerformed event ---

    [Fact]
    public void ProcessMouseClick_FiresBroadcastPerformedEvent()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        int? eventWindowCount = null;
        _service.BroadcastPerformed += (_, count) => eventWindowCount = count;

        _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(2, eventWindowCount);
    }

    [Fact]
    public void ProcessMouseClick_NoWindowsReached_DoesNotFireEvent()
    {
        var windows = CreateWindows(1);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));

        var eventFired = false;
        _service.BroadcastPerformed += (_, _) => eventFired = true;

        _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.False(eventFired);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_DisarmsIfArmed()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);
        Assert.True(_service.IsArmed);

        _service.Dispose();

        Assert.False(_service.IsArmed);
    }

    // --- SendMouseClick failure ---

    [Fact]
    public void ProcessMouseClick_SendMouseClickFails_WindowNotCounted()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        // SendMouseClick échoue
        _mockHelper.SetupSequence(h => h.SendMouseClick())
            .Returns(false)  // fenêtre 200 échoue
            .Returns(true);  // fenêtre 300 réussit

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(1, result);
    }

    // --- ReturnToLeaderAfterBroadcast ---

    [Fact]
    public void ProcessMouseClick_ReturnToLeader_FocusesLeaderInsteadOfSource()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);
        _service.ReturnToLeaderAfterBroadcast = true;
        _service.LeaderHandle = (nint)300; // fenêtre 3 = leader

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        _service.ProcessMouseClick(500, 300, (nint)100);

        // Le dernier FocusWindow doit être le leader (300), pas la source (100)
        var focusCalls = new List<nint>();
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>()))
            .Callback<nint>(handle => focusCalls.Add(handle))
            .Returns(true);

        // Re-vérifier : le focus final doit être sur le leader
        // On vérifie que FocusWindow(300) a été appelé en dernier (restauration)
        // via GetForegroundWindow qui retourne le dernier handle focusé
        var foreground = _mockHelper.Object.GetForegroundWindow();
        Assert.Equal((nint)300, foreground);
    }

    [Fact]
    public void ProcessMouseClick_ReturnToLeader_NoLeaderHandle_FallsBackToSource()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);
        _service.ReturnToLeaderAfterBroadcast = true;
        _service.LeaderHandle = null;

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));

        _service.ProcessMouseClick(500, 300, (nint)100);

        // Le focus final doit être sur la source (100) car pas de leader
        var foreground = _mockHelper.Object.GetForegroundWindow();
        Assert.Equal((nint)100, foreground);
    }

    [Fact]
    public void ProcessMouseClick_ReturnToLeader_InvalidLeader_FallsBackToSource()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);
        _service.ReturnToLeaderAfterBroadcast = true;
        _service.LeaderHandle = (nint)999; // handle pas dans la liste

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));

        _service.ProcessMouseClick(500, 300, (nint)100);

        // Le focus final doit être sur la source (100) car leader invalide
        var foreground = _mockHelper.Object.GetForegroundWindow();
        Assert.Equal((nint)100, foreground);
    }

    [Fact]
    public void ProcessMouseClick_ReturnToLeaderDisabled_FocusesSourceEvenWithLeader()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);
        _service.ReturnToLeaderAfterBroadcast = false;
        _service.LeaderHandle = (nint)300;

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        _service.ProcessMouseClick(500, 300, (nint)100);

        // Le focus final doit être sur la source (100) car option désactivée
        var foreground = _mockHelper.Object.GetForegroundWindow();
        Assert.Equal((nint)100, foreground);
    }

    // --- BroadcastDelayMs ---

    [Fact]
    public void BroadcastDelayMs_DefaultIs65()
    {
        Assert.Equal(65, _service.BroadcastDelayMs);
    }

    [Fact]
    public void BroadcastDelayMs_CanBeSet()
    {
        _service.BroadcastDelayMs = 200;
        Assert.Equal(200, _service.BroadcastDelayMs);
    }

    [Fact]
    public void ProcessMouseClick_WithZeroDelay_StillBroadcasts()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);
        _service.BroadcastDelayMs = 0;

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(1, result);
    }

    // --- BroadcastDelayRandomMs ---

    [Fact]
    public void BroadcastDelayRandomMs_DefaultIs25()
    {
        Assert.Equal(25, _service.BroadcastDelayRandomMs);
    }

    [Fact]
    public void BroadcastDelayRandomMs_CanBeSet()
    {
        _service.BroadcastDelayRandomMs = 50;
        Assert.Equal(50, _service.BroadcastDelayRandomMs);
    }

    [Fact]
    public void ProcessMouseClick_WithZeroRandomDelay_StillBroadcasts()
    {
        var windows = CreateWindows(2);
        _service.Arm(windows);
        _service.BroadcastDelayRandomMs = 0;

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(1, result);
    }

    [Fact]
    public void ProcessMouseClick_ReturnToLeader_SourceIsLeader_SameBehavior()
    {
        var windows = CreateWindows(3);
        _service.Arm(windows);
        _service.ReturnToLeaderAfterBroadcast = true;
        _service.LeaderHandle = (nint)100; // source = leader

        _mockHelper.Setup(h => h.ScreenToClient((nint)100, It.IsAny<int>(), It.IsAny<int>())).Returns((50, 30));
        _mockHelper.Setup(h => h.ClientToScreen((nint)200, 50, 30)).Returns((550, 330));
        _mockHelper.Setup(h => h.ClientToScreen((nint)300, 50, 30)).Returns((600, 360));

        var result = _service.ProcessMouseClick(500, 300, (nint)100);

        Assert.Equal(2, result);
        // Le focus final doit être sur le leader/source (100)
        var foreground = _mockHelper.Object.GetForegroundWindow();
        Assert.Equal((nint)100, foreground);
    }
}
