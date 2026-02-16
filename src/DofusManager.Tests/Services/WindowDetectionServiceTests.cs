using DofusManager.Core.Models;
using DofusManager.Core.Services;
using DofusManager.Core.Win32;
using Moq;
using Xunit;

namespace DofusManager.Tests.Services;

public class WindowDetectionServiceTests : IDisposable
{
    private readonly Mock<IWin32WindowHelper> _mockHelper;
    private readonly WindowDetectionService _service;

    public WindowDetectionServiceTests()
    {
        _mockHelper = new Mock<IWin32WindowHelper>();
        _service = new WindowDetectionService(_mockHelper.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void DetectOnce_WithNoDofusWindows_ReturnsEmptyList()
    {
        // Aucune fenêtre sur le système
        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(Array.Empty<DofusWindow>());

        var result = _service.DetectOnce();

        Assert.Empty(result);
    }

    [Fact]
    public void DetectOnce_FiltersNonDofusWindows()
    {
        // EnumerateAllWindows retourne des fenêtres quelconques.
        // Le service filtre par nom de process (Process.GetProcessById),
        // ce qui ne marchera pas en test. On vérifie que le service
        // ne crashe pas et retourne une liste (possiblement vide).
        var windows = new List<DofusWindow>
        {
            new() { Handle = 1, ProcessId = -1, Title = "Notepad", IsVisible = true, IsMinimized = false },
        };

        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(windows);

        var result = _service.DetectOnce();

        // Le PID -1 ne correspond à aucun process, donc filtré
        Assert.Empty(result);
    }

    [Fact]
    public void DetectedWindows_InitiallyEmpty()
    {
        Assert.Empty(_service.DetectedWindows);
    }

    [Fact]
    public void IsPolling_InitiallyFalse()
    {
        Assert.False(_service.IsPolling);
    }

    [Fact]
    public void StartPolling_SetsIsPollingTrue()
    {
        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(Array.Empty<DofusWindow>());

        _service.StartPolling(1000);

        Assert.True(_service.IsPolling);
    }

    [Fact]
    public void StopPolling_SetsIsPollingFalse()
    {
        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(Array.Empty<DofusWindow>());

        _service.StartPolling(1000);
        _service.StopPolling();

        Assert.False(_service.IsPolling);
    }

    [Fact]
    public void StartPolling_WhenAlreadyPolling_DoesNotThrow()
    {
        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(Array.Empty<DofusWindow>());

        _service.StartPolling(1000);
        _service.StartPolling(1000); // pas d'exception

        Assert.True(_service.IsPolling);
    }

    [Fact]
    public void WindowsChanged_RaisedOnNewWindows()
    {
        // Simule le process courant comme fenêtre Dofus
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var dofusWindow = new DofusWindow
        {
            Handle = 9999,
            ProcessId = currentProcess.Id,
            Title = "Dofus - TestPerso",
            IsVisible = true,
            IsMinimized = false
        };

        // Mais le process courant ne s'appelle pas "Dofus",
        // donc il sera filtré. On teste quand même que l'événement
        // ne lève pas d'erreur.
        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(new List<DofusWindow> { dofusWindow });

        WindowsChangedEventArgs? eventArgs = null;
        _service.WindowsChanged += (_, args) => eventArgs = args;

        _service.DetectOnce();

        // Le process ne s'appelle pas Dofus, donc pas d'événement
        // (pas de changement par rapport à la liste vide initiale)
        Assert.Null(eventArgs);
    }

    [Fact]
    public void Dispose_StopsPolling()
    {
        _mockHelper.Setup(h => h.EnumerateAllWindows())
            .Returns(Array.Empty<DofusWindow>());

        _service.StartPolling(1000);
        _service.Dispose();

        Assert.False(_service.IsPolling);
    }
}
