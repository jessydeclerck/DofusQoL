using DofusManager.Core.Models;

namespace DofusManager.Core.Services;

/// <summary>
/// Service de gestion des profils de team (CRUD + persistance JSON).
/// </summary>
public interface IProfileService
{
    /// <summary>Retourne tous les profils chargés.</summary>
    IReadOnlyList<Profile> GetAllProfiles();

    /// <summary>Retourne un profil par nom, ou null si inexistant.</summary>
    Profile? GetProfile(string profileName);

    /// <summary>Crée un nouveau profil. Lève si le nom existe déjà.</summary>
    void CreateProfile(Profile profile);

    /// <summary>Met à jour un profil existant. Lève si inexistant.</summary>
    void UpdateProfile(Profile profile);

    /// <summary>Supprime un profil par nom. Lève si inexistant.</summary>
    void DeleteProfile(string profileName);

    /// <summary>Sauvegarde tous les profils vers un fichier JSON.</summary>
    Task SaveAsync(string? filePath = null);

    /// <summary>Charge les profils depuis un fichier JSON.</summary>
    Task LoadAsync(string? filePath = null);

    /// <summary>Déclenché quand la liste des profils change.</summary>
    event EventHandler? ProfilesChanged;

    /// <summary>Chemin par défaut du fichier de profils.</summary>
    string DefaultFilePath { get; }
}
