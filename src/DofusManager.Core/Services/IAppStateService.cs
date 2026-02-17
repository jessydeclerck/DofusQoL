using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de persistance de l'état applicatif entre les sessions.
/// </summary>
public interface IAppStateService
{
    /// <summary>Sauvegarde l'état dans le fichier JSON.</summary>
    Task SaveAsync(AppState state);

    /// <summary>Charge l'état depuis le fichier JSON. Retourne null si inexistant.</summary>
    Task<AppState?> LoadAsync();
}
