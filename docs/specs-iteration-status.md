# Suivi des itérations

## Itération 1 — Fondations + Détection (F1)
- [x] Solution .NET 10 créée (Core, UI, Tests)
- [x] NativeMethods.txt (CsWin32) — EnumWindows, GetWindowText, GetWindowThreadProcessId, IsWindowVisible, IsIconic, MonitorFromWindow, GetMonitorInfo, GetClassName
- [x] WindowDetectionService + IWindowDetectionService (polling configurable, événements de changement)
- [x] DofusWindow model (Handle, PID, Title, IsVisible, IsMinimized, ScreenName)
- [x] IWin32WindowHelper + WindowHelper (abstraction pour tests)
- [x] MainWindow avec DataGrid (PID, Titre, État, Écran) + boutons Rafraîchir/Polling
- [x] MainViewModel avec ObservableCollection + RelayCommands
- [x] App.xaml.cs avec DI (ServiceCollection) + Serilog
- [x] Tests unitaires (15 tests : DofusWindowTests + WindowDetectionServiceTests)

## Itération 2 — Hotkeys de focus (F2)
- [x] NativeMethods.txt — RegisterHotKey, UnregisterHotKey, SetForegroundWindow, ShowWindow, IsWindow
- [x] HotkeyBinding model + HotkeyAction enum + HotkeyModifiers flags + FocusResult record
- [x] IWin32WindowHelper étendu — FocusWindow, IsWindowValid, RegisterHotKey, UnregisterHotKey
- [x] WindowHelper — implémentation CsWin32 des nouvelles méthodes
- [x] IHotkeyService + HotkeyService (enregistrement global, dispatch WM_HOTKEY, événement HotkeyPressed)
- [x] HotkeyPressedEventArgs
- [x] IFocusService + FocusService (FocusSlot, Next, Previous, Last, Leader, slot management)
- [x] HotkeyViewModel (auto-assign slots, toggle hotkeys, leader designation, dispatch actions)
- [x] HotkeyView.xaml (DataGrid slots, boutons activer/leader, raccourcis navigation)
- [x] MainWindow refacto — TabControl (Fenêtres + Raccourcis) + hook WM_HOTKEY via HwndSource
- [x] MainViewModel mis à jour — intègre HotkeyViewModel, sync slots sur scan manuel
- [x] App.xaml.cs — DI pour IHotkeyService, IFocusService, HotkeyViewModel
- [x] Tests unitaires (45 nouveaux : HotkeyBindingTests + HotkeyServiceTests + FocusServiceTests) — total 60

## Itération 3 — Profils (F3)
- [x] ProfileSlot model (Index, CharacterName, WindowTitlePattern, IsLeader, FocusHotkey)
- [x] BroadcastPreset model stub (sérialisable, pas de logique — pour F4)
- [x] Profile model (ProfileName, Slots, BroadcastPresets, CreatedAt, LastModifiedAt)
- [x] GlobMatcher helper (matching patterns glob * et ? pour les titres de fenêtre)
- [x] IProfileService + ProfileService (CRUD, persistance JSON via System.Text.Json, événements)
- [x] ProfileViewModel (Create/Load/Save/Delete, auto-matching fenêtres par pattern, sync hotkeys/focus)
- [x] ProfileView.xaml (3e onglet : création, liste profils, DataGrid slots, boutons CRUD)
- [x] MainWindow.xaml — ajout onglet Profils
- [x] MainViewModel — intègre ProfileViewModel
- [x] App.xaml.cs — DI pour IProfileService, ProfileViewModel
- [x] MainWindow.xaml.cs — initialisation async des profils au démarrage
- [x] Tests unitaires (33 nouveaux : ProfileSlotTests + BroadcastPresetTests + ProfileTests + GlobMatcherTests + ProfileServiceTests) — total 99

## Itération 4 — Broadcast d'input (F4)
- À planifier

## Itération 5 — Session manager (F5)
- À planifier

## Itération 6 — Tray + Switcher (F6, F7)
- À planifier

## Itération 7 — Combat assist + Audio + Multi-écrans (F8, F9, F10)
- À planifier
