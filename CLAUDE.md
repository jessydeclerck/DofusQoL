# DofusManager — Multi-Account Window Manager for Dofus

## About
Application Windows (.NET 8 / WPF) pour gérer les fenêtres Dofus en multi-compte.
Gestion fenêtres OS uniquement. Zéro automation gameplay, zéro injection, zéro lecture mémoire client.

## Architecture
- **DofusManager.Core** : Logique métier, zéro dépendance UI. Models + Services + Win32 helpers.
- **DofusManager.UI** : Application WPF, pattern MVVM via CommunityToolkit.Mvvm.
- **DofusManager.Tests** : Tests unitaires xUnit.

## Stack
- .NET 10, WPF, CommunityToolkit.Mvvm
- Win32 via Microsoft.Windows.CsWin32 (source generator P/Invoke)
- Microsoft.Extensions.DependencyInjection
- System.Text.Json pour la persistance
- Serilog pour le logging
- xUnit + Moq pour les tests

## Documentation
- Specs complètes : `docs/specs.md`
- Suivi d'avancement : `docs/specs-iteration-status.md`
Mets à jour le suivi après chaque tâche complétée.

## Itérations
Le projet se construit itérativement (8 itérations). Chaque itération produit un incrément fonctionnel testable.
État actuel : **Itération 3 terminée** — Profils (F3)

## Conventions de code
- Noms de classes/méthodes en anglais, commentaires en français OK
- Async/await pour tout ce qui est I/O ou Win32 long
- Chaque service implémente une interface (IWindowDetectionService, etc.)
- DI via constructor injection partout
- Logs structurés Serilog : Log.Information("Detected {Count} windows", count)
- Pas de `Task.Result` ni `.Wait()` — async tout du long

## Commandes
```bash
dotnet build src/DofusManager.sln          # Build
dotnet test src/DofusManager.Tests/        # Tests
dotnet run --project src/DofusManager.UI/  # Lancer l'app
```

## Règles importantes
- TOUJOURS écrire les tests AVANT ou EN MÊME TEMPS que le code
- Commit atomique par feature/fix avec message descriptif
- Ne jamais modifier plusieurs services dans un même commit
- Vérifier que `dotnet build` passe avant chaque commit
- Utiliser CsWin32 (NativeMethods.txt) au lieu de P/Invoke manuel
- TOUJOURS préférer CsWin32 (source generator) pour les appels Win32 : ajouter l'API dans `NativeMethods.txt` et utiliser la classe `PInvoke` générée. Ne jamais écrire de `[DllImport]` manuel.
