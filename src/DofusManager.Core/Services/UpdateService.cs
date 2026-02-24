using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using DofusManager.Core.Models;
using Serilog;
using Windows.Win32;

namespace DofusManager.Core.Services;

public class UpdateService : IUpdateService
{
    private static readonly ILogger Logger = Log.ForContext<UpdateService>();

    private const string GitHubApiUrl = "https://api.github.com/repos/jessydeclerck/DofusQoL/releases/latest";
    private const string ZipAssetPrefix = "DofusQoL-";

    private static string RuntimeIdentifier => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.X86 => "win-x86",
        Architecture.Arm64 => "win-arm64",
        _ => "win-x64"
    };

    private readonly HttpClient _httpClient;
    private readonly Version _currentVersion;

    public UpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _currentVersion = GetCurrentVersion();
    }

    /// <summary>Constructeur interne pour les tests (version explicite).</summary>
    internal UpdateService(HttpClient httpClient, Version currentVersion)
    {
        _httpClient = httpClient;
        _currentVersion = currentVersion;
    }

    private static Version GetCurrentVersion()
    {
        var infoVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (infoVersion is null)
            return new Version(0, 0, 0);

        // Strip build metadata après '+' (ex: "1.2.0+abc123")
        var plusIndex = infoVersion.IndexOf('+');
        if (plusIndex > 0)
            infoVersion = infoVersion[..plusIndex];

        // Strip suffixe prerelease après '-' (ex: "0.0.0-dev")
        var dashIndex = infoVersion.IndexOf('-');
        if (dashIndex > 0)
            infoVersion = infoVersion[..dashIndex];

        return Version.TryParse(infoVersion, out var version) ? version : new Version(0, 0, 0);
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.UserAgent.ParseAdd("DofusQoL-UpdateChecker");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                Logger.Warning("GitHub API rate limit atteint ({StatusCode})", response.StatusCode);
                return UpdateCheckResult.Error("Limite de requêtes GitHub atteinte. Réessayez plus tard.");
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var versionStr = tagName.TrimStart('v');

            if (!Version.TryParse(versionStr, out var remoteVersion))
            {
                Logger.Warning("Tag version non parsable : {Tag}", tagName);
                return UpdateCheckResult.Error($"Format de version invalide : {tagName}");
            }

            if (remoteVersion <= _currentVersion)
            {
                Logger.Information("Application à jour ({Current} >= {Remote})", _currentVersion, remoteVersion);
                return UpdateCheckResult.UpToDate();
            }

            // Chercher l'asset zip correspondant à l'architecture courante
            var rid = RuntimeIdentifier;
            var expectedSuffix = $"-{rid}.zip";
            string? downloadUrl = null;
            long sizeBytes = 0;

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.StartsWith(ZipAssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    sizeBytes = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                    break;
                }
            }

            if (downloadUrl is null)
            {
                Logger.Warning("Aucun asset zip trouvé pour {Rid} dans la release {Tag}", rid, tagName);
                return UpdateCheckResult.Error($"Aucun fichier de mise à jour trouvé pour {rid}.");
            }

            var releaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
            var publishedAt = root.TryGetProperty("published_at", out var pubProp) && pubProp.GetString() is { } pubStr
                ? DateTime.Parse(pubStr)
                : DateTime.UtcNow;

            var updateInfo = new UpdateInfo
            {
                TagName = tagName,
                Version = remoteVersion,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes,
                PublishedAt = publishedAt,
                SizeBytes = sizeBytes
            };

            Logger.Information("Mise à jour disponible : {Current} -> {Remote}", _currentVersion, remoteVersion);
            return UpdateCheckResult.Available(updateInfo);
        }
        catch (HttpRequestException ex)
        {
            Logger.Warning(ex, "Erreur réseau lors de la vérification de mise à jour");
            return UpdateCheckResult.Error("Impossible de contacter le serveur de mise à jour.");
        }
        catch (TaskCanceledException)
        {
            return UpdateCheckResult.Error("Vérification annulée.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erreur inattendue lors de la vérification de mise à jour");
            return UpdateCheckResult.Error($"Erreur : {ex.Message}");
        }
    }

    public async Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DofusQoL_Update");
        Directory.CreateDirectory(tempDir);

        var zipPath = Path.Combine(tempDir, $"DofusQoL-{update.TagName}.zip");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
        request.Headers.UserAgent.ParseAdd("DofusQoL-UpdateChecker");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.SizeBytes;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(zipPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (totalBytes > 0)
                progress?.Report((double)totalRead / totalBytes);
        }

        progress?.Report(1.0);
        Logger.Information("Mise à jour téléchargée : {Path} ({Bytes} octets)", zipPath, totalRead);
        return zipPath;
    }

    public void LaunchUpdaterAndExit(string zipPath, string installDirectory)
    {
        var currentPid = Environment.ProcessId;
        var updaterPath = Path.Combine(installDirectory, "DofusManager.Updater.exe");

        if (!File.Exists(updaterPath))
        {
            Logger.Error("Updater introuvable : {Path}", updaterPath);
            throw new FileNotFoundException("Updater introuvable", updaterPath);
        }

        // TrimEnd '\' pour éviter que le backslash final échappe le guillemet dans les arguments
        var installDir = installDirectory.TrimEnd('\\');

        Logger.Information("Lancement updater : PID={Pid}, Zip={Zip}, Dir={Dir}", currentPid, zipPath, installDir);

        // Supprimer le Mark of the Web pour éviter le blocage SmartScreen
        RemoveMarkOfTheWeb(updaterPath);

        // UseShellExecute = true pour détacher du Job Object (sinon Rider/VS tuent le processus enfant)
        Process.Start(new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = $"\"{currentPid}\" \"{zipPath}\" \"{installDir}\"",
            UseShellExecute = true
        });

        Environment.Exit(0);
    }

    private static void RemoveMarkOfTheWeb(string filePath)
    {
        try
        {
            PInvoke.DeleteFile(filePath + ":Zone.Identifier");
            Logger.Debug("MOTW supprimé de {Path}", filePath);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Impossible de supprimer le MOTW de {Path}", filePath);
        }
    }
}
