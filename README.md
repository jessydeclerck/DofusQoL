# DofusQoL

Application Windows pour gérer les fenêtres Dofus en multi-compte. Gestion fenêtres OS uniquement — zéro automation gameplay, zéro injection, zéro lecture mémoire client.

## Fonctionnalités

- **Détection automatique** des fenêtres Dofus ouvertes
- **Raccourcis clavier globaux** pour switcher instantanément entre les fenêtres
- **Profils d'équipe** pour sauvegarder et restaurer des configurations de slots
- **Broadcast d'inputs** (clics et touches) vers toutes les fenêtres en un raccourci
- **Push-to-Broadcast** maintenir une touche pour broadcaster les clics souris

## Prérequis

- Windows 10 / 11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Installation

Télécharger le dernier `.zip` depuis la page [Releases](../../releases), extraire et lancer `DofusManager.UI.exe`.

## Build depuis les sources

```bash
dotnet build src/DofusManager.sln
```

### Lancer l'application

```bash
dotnet run --project src/DofusManager.UI/
```

### Lancer les tests

```bash
dotnet test src/DofusManager.Tests/
```

## Stack technique

| Composant | Choix |
|---|---|
| Runtime | .NET 10 |
| UI | WPF |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Win32 | CsWin32 (source generator P/Invoke) |
| Persistance | JSON (System.Text.Json) |
| Logging | Serilog |
| Tests | xUnit + Moq |

## Propriété intellectuelle

Les images et assets graphiques liés à Dofus sont la propriété d'[Ankama Games](https://www.ankama.com/). Ce projet n'est pas affilié à Ankama.

## Licence

Usage personnel.
