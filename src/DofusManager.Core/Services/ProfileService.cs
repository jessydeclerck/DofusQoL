using System.Text.Json;
using DofusManager.Core.Models;
using Serilog;

namespace DofusManager.Core.Services;

/// <summary>
/// Implémentation du service de profils avec persistance JSON.
/// </summary>
public class ProfileService : IProfileService
{
    private static readonly ILogger Logger = Log.ForContext<ProfileService>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Profile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public string DefaultFilePath { get; }

    public event EventHandler? ProfilesChanged;

    public ProfileService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DofusManager");
        DefaultFilePath = Path.Combine(dir, "profiles.json");
    }

    /// <summary>
    /// Constructeur pour les tests, permettant de spécifier le chemin du fichier.
    /// </summary>
    public ProfileService(string defaultFilePath)
    {
        DefaultFilePath = defaultFilePath;
    }

    public IReadOnlyList<Profile> GetAllProfiles()
    {
        lock (_lock)
        {
            return _profiles.Values.ToList().AsReadOnly();
        }
    }

    public Profile? GetProfile(string profileName)
    {
        lock (_lock)
        {
            return _profiles.GetValueOrDefault(profileName);
        }
    }

    public void CreateProfile(Profile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.ProfileName, nameof(profile.ProfileName));

        lock (_lock)
        {
            if (_profiles.ContainsKey(profile.ProfileName))
            {
                throw new InvalidOperationException($"Un profil nommé '{profile.ProfileName}' existe déjà.");
            }

            profile.CreatedAt = DateTime.UtcNow;
            profile.LastModifiedAt = DateTime.UtcNow;
            _profiles[profile.ProfileName] = profile;
        }

        Logger.Information("Profil créé : {ProfileName} ({SlotCount} slots)", profile.ProfileName, profile.Slots.Count);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProfile(Profile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.ProfileName, nameof(profile.ProfileName));

        lock (_lock)
        {
            if (!_profiles.ContainsKey(profile.ProfileName))
            {
                throw new InvalidOperationException($"Profil '{profile.ProfileName}' introuvable.");
            }

            profile.LastModifiedAt = DateTime.UtcNow;
            _profiles[profile.ProfileName] = profile;
        }

        Logger.Information("Profil mis à jour : {ProfileName}", profile.ProfileName);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteProfile(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName, nameof(profileName));

        lock (_lock)
        {
            if (!_profiles.Remove(profileName))
            {
                throw new InvalidOperationException($"Profil '{profileName}' introuvable.");
            }
        }

        Logger.Information("Profil supprimé : {ProfileName}", profileName);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAsync(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;

        List<Profile> snapshot;
        lock (_lock)
        {
            snapshot = _profiles.Values.ToList();
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions);

        Logger.Information("Profils sauvegardés dans {FilePath} ({Count} profils)", path, snapshot.Count);
    }

    public async Task LoadAsync(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;

        if (!File.Exists(path))
        {
            Logger.Information("Fichier de profils inexistant : {FilePath}, démarrage avec liste vide", path);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var profiles = await JsonSerializer.DeserializeAsync<List<Profile>>(stream, JsonOptions);

            if (profiles != null)
            {
                lock (_lock)
                {
                    _profiles.Clear();
                    foreach (var profile in profiles)
                    {
                        _profiles[profile.ProfileName] = profile;
                    }
                }

                Logger.Information("Profils chargés depuis {FilePath} ({Count} profils)", path, profiles.Count);
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (JsonException ex)
        {
            Logger.Error(ex, "Erreur de désérialisation du fichier de profils : {FilePath}", path);
            throw;
        }
    }
}
