---
name: wpf-win32-conventions
description: Conventions pour le développement WPF avec interop Win32 via CsWin32.
  Utiliser quand on crée des services Win32, des ViewModels WPF, ou des P/Invoke.
---

# WPF + Win32 Conventions

## CsWin32 (P/Invoke)
- Ajouter les noms de fonctions Win32 dans `src/DofusManager.Core/NativeMethods.txt`
- CsWin32 génère automatiquement les signatures P/Invoke au build
- NE JAMAIS écrire de [DllImport] manuellement
- Wrapper les appels natifs dans `Win32/WindowHelper.cs`

## MVVM
- ViewModels héritent de ObservableObject (CommunityToolkit.Mvvm)
- Commands via RelayCommand / AsyncRelayCommand
- Pas de code-behind dans les Views (sauf InitializeComponent)
- Binding via {Binding} en XAML, DataContext injecté via DI

## Services
- Interface dans Core (ex: IWindowDetectionService)
- Implémentation dans Core (ex: WindowDetectionService)
- Enregistrement DI dans App.xaml.cs
- Services thread-safe si utilisés avec polling/timers

## Tests
- Mocker les interfaces Win32 pour les tests unitaires
- Tester la logique métier, pas les appels Win32 directs
- Nommer : [Méthode]_[Scénario]_[Résultat attendu]
