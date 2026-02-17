using System.Diagnostics;
using DofusManager.Core.Models;
using DofusManager.Core.Win32;
using Serilog;

namespace DofusManager.Core.Services;

public class WindowDetectionService : IWindowDetectionService
{
    private static readonly ILogger Logger = Log.ForContext<WindowDetectionService>();

    private const string DofusProcessName = "Dofus";

    private readonly IWin32WindowHelper _windowHelper;
    private readonly object _lock = new();
    private List<DofusWindow> _detectedWindows = [];
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public WindowDetectionService(IWin32WindowHelper windowHelper)
    {
        _windowHelper = windowHelper;
    }

    public IReadOnlyList<DofusWindow> DetectedWindows
    {
        get
        {
            lock (_lock)
            {
                return _detectedWindows.AsReadOnly();
            }
        }
    }

    public event EventHandler<WindowsChangedEventArgs>? WindowsChanged;

    public bool IsPolling => _pollingCts is { IsCancellationRequested: false };

    public IReadOnlyList<DofusWindow> DetectOnce()
    {
        var allWindows = _windowHelper.EnumerateAllWindows();
        var dofusWindows = FilterDofusWindows(allWindows);

        UpdateAndNotify(dofusWindows);

        return DetectedWindows;
    }

    public void StartPolling(int intervalMs = 500)
    {
        if (IsPolling)
        {
            Logger.Warning("Le polling est déjà actif");
            return;
        }

        Logger.Information("Démarrage du polling toutes les {IntervalMs}ms", intervalMs);

        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        _pollingTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
            try
            {
                // Premier scan immédiat
                DetectOnce();

                while (await timer.WaitForNextTickAsync(token))
                {
                    DetectOnce();
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Erreur dans la boucle de polling");
            }
        }, token);
    }

    public void StopPolling()
    {
        if (!IsPolling)
            return;

        Logger.Information("Arrêt du polling");
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    public void Dispose()
    {
        StopPolling();
        GC.SuppressFinalize(this);
    }

    private static List<DofusWindow> FilterDofusWindows(IReadOnlyList<DofusWindow> allWindows)
    {
        return allWindows
            .Where(w => IsDofusWindow(w))
            .ToList();
    }

    private static bool IsDofusWindow(DofusWindow window)
    {
        try
        {
            var process = Process.GetProcessById(window.ProcessId);
            // Comparaison exacte pour éviter de matcher "DofusManager" et autres
            return process.ProcessName.Equals(DofusProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            // Le process n'existe plus
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void UpdateAndNotify(List<DofusWindow> newWindows)
    {
        List<DofusWindow> added;
        List<DofusWindow> removed;

        bool titlesChanged;

        lock (_lock)
        {
            added = newWindows.Where(w => !_detectedWindows.Contains(w)).ToList();
            removed = _detectedWindows.Where(w => !newWindows.Contains(w)).ToList();

            if (added.Count == 0 && removed.Count == 0)
            {
                // Mettre à jour les propriétés mutables et détecter les changements de titre
                titlesChanged = false;
                foreach (var existing in _detectedWindows)
                {
                    var updated = newWindows.FirstOrDefault(w => w.Handle == existing.Handle);
                    if (updated is not null)
                    {
                        if (existing.Title != updated.Title)
                            titlesChanged = true;
                        existing.Title = updated.Title;
                        existing.IsVisible = updated.IsVisible;
                        existing.IsMinimized = updated.IsMinimized;
                        existing.ScreenName = updated.ScreenName;
                    }
                }

                if (!titlesChanged)
                    return;
            }
            else
            {
                _detectedWindows = newWindows;
            }
        }

        if (added.Count > 0)
            Logger.Information("Nouvelles fenêtres Dofus détectées : {Count}", added.Count);
        if (removed.Count > 0)
            Logger.Information("Fenêtres Dofus disparues : {Count}", removed.Count);

        WindowsChanged?.Invoke(this, new WindowsChangedEventArgs
        {
            Added = added,
            Removed = removed,
            Current = DetectedWindows
        });
    }
}
