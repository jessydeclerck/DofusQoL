using System.Text.Json;
using DofusManager.Core.Models;
using Serilog;

namespace DofusManager.Core.Services;

/// <summary>
/// Persistance de l'état applicatif (dernier profil, config hotkeys) dans un fichier JSON.
/// </summary>
public class AppStateService : IAppStateService
{
    private static readonly ILogger Logger = Log.ForContext<AppStateService>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AppStateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _filePath = Path.Combine(appData, "DofusManager", "appstate.json");
    }

    /// <summary>
    /// Constructeur pour les tests, permettant de spécifier le chemin du fichier.
    /// </summary>
    public AppStateService(string filePath)
    {
        _filePath = filePath;
    }

    public async Task SaveAsync(AppState state)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await _writeLock.WaitAsync();
        try
        {
            // Écriture atomique : write-to-temp-then-rename pour éviter la corruption
            var tmpPath = _filePath + ".tmp";
            await using (var stream = File.Create(tmpPath))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions);
                await stream.FlushAsync();
            }

            File.Move(tmpPath, _filePath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }

        Logger.Information("État applicatif sauvegardé (profil actif : {ProfileName})",
            state.ActiveProfileName ?? "(aucun)");
    }

    public async Task<AppState?> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            Logger.Information("Fichier d'état inexistant : {FilePath}, premier lancement", _filePath);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<AppState>(stream, JsonOptions);
            Logger.Information("État applicatif chargé (profil actif : {ProfileName})",
                state?.ActiveProfileName ?? "(aucun)");
            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Logger.Warning(ex, "Fichier d'état illisible, ignoré : {FilePath}", _filePath);
            return null;
        }
    }
}
