using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class GroupInviteServiceTests
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly GroupInviteService _service;

    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_W = 0x57;

    public GroupInviteServiceTests()
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
        _mockHelper.Setup(h => h.SendKeyCombination(It.IsAny<ushort>(), It.IsAny<ushort>())).Returns(true);

        _service = new GroupInviteService(_mockHelper.Object);
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

    // --- ExtractCharacterName ---

    [Theory]
    [InlineData("Cuckoolo - Pandawa - 3.4.18.19 - Release", "Cuckoolo")]
    [InlineData("MonPerso - Cra - 3.4.18.19 - Release", "MonPerso")]
    [InlineData("Simple", "Simple")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void ExtractCharacterName_ReturnsFirstToken(string title, string expected)
    {
        var result = IGroupInviteService.ExtractCharacterName(title);
        Assert.Equal(expected, result);
    }

    // --- InviteAllAsync ---

    [Fact]
    public async Task InviteAllAsync_InvitesAllExceptLeader()
    {
        var windows = CreateWindows("Leader", "Perso2", "Perso3");
        var leader = windows[0];

        var result = await _service.InviteAllAsync(windows, leader);

        Assert.True(result.Success);
        Assert.Equal(2, result.Invited);
    }

    [Fact]
    public async Task InviteAllAsync_FocusesLeaderWindow()
    {
        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        await _service.InviteAllAsync(windows, leader);

        _mockHelper.Verify(h => h.FocusWindow(leader.Handle), Times.Once);
    }

    [Fact]
    public async Task InviteAllAsync_SendsCorrectCommands()
    {
        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        var callOrder = new List<string>();

        _mockHelper.Setup(h => h.SendKeyPress(VK_SPACE))
            .Callback(() => callOrder.Add("SPACE"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => callOrder.Add("ENTER"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendText(It.IsAny<string>()))
            .Callback<string>(text => callOrder.Add($"TEXT:{text}"))
            .Returns(true);

        await _service.InviteAllAsync(windows, leader);

        // Séquence attendue : SPACE (ouvrir chat), TEXT:/invite Perso2, ENTER (envoyer)
        Assert.Equal(3, callOrder.Count);
        Assert.Equal("SPACE", callOrder[0]);
        Assert.Equal("TEXT:/invite Perso2", callOrder[1]);
        Assert.Equal("ENTER", callOrder[2]);
    }

    [Fact]
    public async Task InviteAllAsync_MultipleCharacters_SendsAllInvites()
    {
        var windows = CreateWindows("Leader", "Perso2", "Perso3", "Perso4");
        var leader = windows[0];

        var inviteTexts = new List<string>();
        _mockHelper.Setup(h => h.SendText(It.IsAny<string>()))
            .Callback<string>(text => inviteTexts.Add(text))
            .Returns(true);

        var result = await _service.InviteAllAsync(windows, leader);

        Assert.Equal(3, result.Invited);
        Assert.Equal(3, inviteTexts.Count);
        Assert.Contains("/invite Perso2", inviteTexts);
        Assert.Contains("/invite Perso3", inviteTexts);
        Assert.Contains("/invite Perso4", inviteTexts);
    }

    [Fact]
    public async Task InviteAllAsync_SingleWindow_ReturnsZeroInvited()
    {
        var windows = CreateWindows("Leader");
        var leader = windows[0];

        var result = await _service.InviteAllAsync(windows, leader);

        Assert.True(result.Success);
        Assert.Equal(0, result.Invited);
        _mockHelper.Verify(h => h.FocusWindow(It.IsAny<nint>()), Times.Never);
    }

    [Fact]
    public async Task InviteAllAsync_EmptyWindows_ReturnsZeroInvited()
    {
        var leader = new DofusWindow
        {
            Handle = 100, ProcessId = 1, Title = "Leader - Cra",
            IsVisible = true, IsMinimized = false
        };

        var result = await _service.InviteAllAsync(new List<DofusWindow>(), leader);

        Assert.True(result.Success);
        Assert.Equal(0, result.Invited);
    }

    [Fact]
    public async Task InviteAllAsync_ForegroundMismatch_RetriesAndFails()
    {
        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        // FocusWindow réussit mais GetForegroundWindow retourne toujours un autre handle
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>())).Returns(true);
        _mockHelper.Setup(h => h.GetForegroundWindow()).Returns((nint)999);

        var result = await _service.InviteAllAsync(windows, leader);

        Assert.False(result.Success);
        Assert.Equal(0, result.Invited);
        // Vérifie qu'il a réessayé 3 fois
        _mockHelper.Verify(h => h.FocusWindow(leader.Handle), Times.Exactly(3));
    }

    [Fact]
    public async Task InviteAllAsync_LeaderEmptyTitle_ReturnsError()
    {
        var leader = new DofusWindow
        {
            Handle = 100, ProcessId = 1, Title = "",
            IsVisible = true, IsMinimized = false
        };
        var windows = new List<DofusWindow>
        {
            leader,
            new DofusWindow
            {
                Handle = 200, ProcessId = 2, Title = "Perso2 - Cra",
                IsVisible = true, IsMinimized = false
            }
        };

        var result = await _service.InviteAllAsync(windows, leader);

        Assert.False(result.Success);
        Assert.Contains("nom du leader", result.ErrorMessage!);
    }

    [Fact]
    public async Task InviteAllAsync_SkipsWindowsWithEmptyName()
    {
        var windows = new List<DofusWindow>
        {
            new() { Handle = 100, ProcessId = 1, Title = "Leader - Cra", IsVisible = true, IsMinimized = false },
            new() { Handle = 200, ProcessId = 2, Title = "", IsVisible = true, IsMinimized = false },
            new() { Handle = 300, ProcessId = 3, Title = "Perso3 - Enu", IsVisible = true, IsMinimized = false }
        };
        var leader = windows[0];

        var result = await _service.InviteAllAsync(windows, leader);

        Assert.True(result.Success);
        Assert.Equal(1, result.Invited);

        // Seul /invite Perso3 doit être envoyé
        _mockHelper.Verify(h => h.SendText("/invite Perso3"), Times.Once);
        _mockHelper.Verify(h => h.SendText(It.Is<string>(s => s.Contains("invite"))), Times.Once);
    }

    // --- ToggleAutoFollowAsync ---

    [Fact]
    public async Task ToggleAutoFollow_SendsCtrlW_ToAllExceptLeader()
    {
        var windows = CreateWindows("Leader", "Perso2", "Perso3");
        var leader = windows[0];

        var result = await _service.ToggleAutoFollowAsync(windows, leader);

        Assert.True(result.Success);
        Assert.Equal(2, result.Invited);
        _mockHelper.Verify(h => h.SendKeyCombination(VK_CONTROL, VK_W), Times.Exactly(2));
    }

    [Fact]
    public async Task ToggleAutoFollow_FocusesEachNonLeaderWindow()
    {
        var windows = CreateWindows("Leader", "Perso2", "Perso3");
        var leader = windows[0];

        await _service.ToggleAutoFollowAsync(windows, leader);

        // Focus sur chaque suiveur + restauration leader
        _mockHelper.Verify(h => h.FocusWindow((nint)200), Times.Once);
        _mockHelper.Verify(h => h.FocusWindow((nint)300), Times.Once);
        _mockHelper.Verify(h => h.FocusWindow(leader.Handle), Times.Once); // restauration
    }

    [Fact]
    public async Task ToggleAutoFollow_RestoresFocusToLeader()
    {
        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        await _service.ToggleAutoFollowAsync(windows, leader);

        // Le dernier appel FocusWindow doit être sur le leader
        var calls = _mockHelper.Invocations
            .Where(i => i.Method.Name == "FocusWindow")
            .Select(i => (nint)i.Arguments[0])
            .ToList();

        Assert.Equal(leader.Handle, calls[^1]);
    }

    [Fact]
    public async Task ToggleAutoFollow_SingleWindow_ReturnsZero()
    {
        var windows = CreateWindows("Leader");
        var leader = windows[0];

        var result = await _service.ToggleAutoFollowAsync(windows, leader);

        Assert.True(result.Success);
        Assert.Equal(0, result.Invited);
        _mockHelper.Verify(h => h.SendKeyCombination(It.IsAny<ushort>(), It.IsAny<ushort>()), Times.Never);
    }

    [Fact]
    public async Task ToggleAutoFollow_SkipsWindowIfFocusFails()
    {
        var windows = CreateWindows("Leader", "Perso2", "Perso3");
        var leader = windows[0];

        // Le focus échoue pour Perso2 (handle 200) mais réussit pour Perso3 (handle 300)
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>()))
            .Returns(true);
        _mockHelper.Setup(h => h.GetForegroundWindow())
            .Returns((nint)0); // échoue par défaut

        // Seul le focus sur 300 réussit (et la restauration leader)
        _mockHelper.Setup(h => h.FocusWindow((nint)300))
            .Callback(() =>
            {
                _mockHelper.Setup(h => h.GetForegroundWindow()).Returns((nint)300);
            })
            .Returns(true);

        var result = await _service.ToggleAutoFollowAsync(windows, leader);

        Assert.Equal(1, result.Invited);
        _mockHelper.Verify(h => h.SendKeyCombination(VK_CONTROL, VK_W), Times.Once);
    }

    // --- PasteToChatAsync ---

    [Fact]
    public async Task PasteToChatAsync_PastesToAllWindows()
    {
        var windows = CreateWindows("Perso1", "Perso2", "Perso3");

        var result = await _service.PasteToChatAsync(windows, windows[0]);

        Assert.True(result.Success);
        Assert.Equal(3, result.Invited);
    }

    [Fact]
    public async Task PasteToChatAsync_SendsCorrectKeySequence()
    {
        var windows = CreateWindows("Perso1");

        var callOrder = new List<string>();
        _mockHelper.Setup(h => h.SendKeyPress(VK_SPACE))
            .Callback(() => callOrder.Add("SPACE"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyCombination(VK_CONTROL, VK_V))
            .Callback(() => callOrder.Add("CTRL+V"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => callOrder.Add("ENTER"))
            .Returns(true);

        await _service.PasteToChatAsync(windows, null);

        Assert.Equal(3, callOrder.Count);
        Assert.Equal("SPACE", callOrder[0]);
        Assert.Equal("CTRL+V", callOrder[1]);
        Assert.Equal("ENTER", callOrder[2]);
    }

    [Fact]
    public async Task PasteToChatAsync_EmptyWindows_ReturnsFailure()
    {
        var result = await _service.PasteToChatAsync(new List<DofusWindow>(), null);

        Assert.False(result.Success);
        Assert.Contains("Aucune fenêtre", result.ErrorMessage!);
    }

    [Fact]
    public async Task PasteToChatAsync_RestoresFocusToLeader()
    {
        var windows = CreateWindows("Perso1", "Perso2");
        var leader = windows[0];

        await _service.PasteToChatAsync(windows, leader);

        // Le dernier FocusWindow doit être le leader
        var calls = _mockHelper.Invocations
            .Where(i => i.Method.Name == "FocusWindow")
            .Select(i => (nint)i.Arguments[0])
            .ToList();

        Assert.Equal(leader.Handle, calls[^1]);
    }

    [Fact]
    public async Task PasteToChatAsync_NullLeader_SkipsRestore()
    {
        var windows = CreateWindows("Perso1");

        var result = await _service.PasteToChatAsync(windows, null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Invited);
        // FocusWindow called only for the window, not for restore
        _mockHelper.Verify(h => h.FocusWindow((nint)100), Times.Once);
    }

    [Fact]
    public async Task PasteToChatAsync_DoubleEnter_SendsTwoEnterKeys()
    {
        var windows = CreateWindows("Perso1");

        var enterCount = 0;
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => enterCount++)
            .Returns(true);

        await _service.PasteToChatAsync(windows, null, doubleEnter: true);

        // 2 ENTER : un pour envoyer, un pour confirmer
        Assert.Equal(2, enterCount);
    }

    [Fact]
    public async Task PasteToChatAsync_NoDoubleEnter_SendsOneEnterKey()
    {
        var windows = CreateWindows("Perso1");

        var enterCount = 0;
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => enterCount++)
            .Returns(true);

        await _service.PasteToChatAsync(windows, null, doubleEnter: false);

        Assert.Equal(1, enterCount);
    }

    [Fact]
    public async Task PasteToChatAsync_SkipsWindowIfFocusFails()
    {
        var windows = CreateWindows("Perso1", "Perso2");

        // Focus échoue pour Perso1 (100), réussit pour Perso2 (200)
        _mockHelper.Setup(h => h.FocusWindow(It.IsAny<nint>())).Returns(true);
        _mockHelper.Setup(h => h.GetForegroundWindow()).Returns((nint)0);

        _mockHelper.Setup(h => h.FocusWindow((nint)200))
            .Callback(() =>
            {
                _mockHelper.Setup(h => h.GetForegroundWindow()).Returns((nint)200);
            })
            .Returns(true);

        var result = await _service.PasteToChatAsync(windows, null);

        Assert.True(result.Success);
        Assert.Equal(1, result.Invited);
    }

    // --- ChatOpenKeyCode configurable ---

    [Fact]
    public async Task InviteAllAsync_UsesCustomChatOpenKey()
    {
        const ushort customKey = 0x54; // 'T'
        _service.ChatOpenKeyCode = customKey;

        var windows = CreateWindows("Leader", "Perso2");
        var leader = windows[0];

        var callOrder = new List<string>();
        _mockHelper.Setup(h => h.SendKeyPress(customKey))
            .Callback(() => callOrder.Add("CUSTOM"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_SPACE))
            .Callback(() => callOrder.Add("SPACE"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => callOrder.Add("ENTER"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendText(It.IsAny<string>()))
            .Callback<string>(text => callOrder.Add($"TEXT:{text}"))
            .Returns(true);

        await _service.InviteAllAsync(windows, leader);

        // La touche custom doit être utilisée, pas SPACE
        Assert.Equal("CUSTOM", callOrder[0]);
        Assert.DoesNotContain("SPACE", callOrder);
    }

    [Fact]
    public async Task PasteToChatAsync_UsesCustomChatOpenKey()
    {
        const ushort customKey = 0x54; // 'T'
        _service.ChatOpenKeyCode = customKey;

        var windows = CreateWindows("Perso1");

        var callOrder = new List<string>();
        _mockHelper.Setup(h => h.SendKeyPress(customKey))
            .Callback(() => callOrder.Add("CUSTOM"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_SPACE))
            .Callback(() => callOrder.Add("SPACE"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyCombination(VK_CONTROL, VK_V))
            .Callback(() => callOrder.Add("CTRL+V"))
            .Returns(true);
        _mockHelper.Setup(h => h.SendKeyPress(VK_RETURN))
            .Callback(() => callOrder.Add("ENTER"))
            .Returns(true);

        await _service.PasteToChatAsync(windows, null);

        // La touche custom doit être utilisée, pas SPACE
        Assert.Equal("CUSTOM", callOrder[0]);
        Assert.Equal("CTRL+V", callOrder[1]);
        Assert.Equal("ENTER", callOrder[2]);
        Assert.DoesNotContain("SPACE", callOrder);
    }
}
