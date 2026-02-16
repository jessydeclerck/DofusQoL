# Dofus Multi-Account Manager — Cahier des charges

> **Document de référence pour implémentation itérative avec Claude Code.**
> Chaque fonctionnalité (F1–F10) correspond à un incrément livrable et testable.

---

## 1. Objectif du produit

Application Windows qui améliore le confort de jeu multi-compte Dofus en :

- Accélérant le changement de fenêtre.
- Fournissant des profils de team et un session manager.
- Permettant le broadcast d'inputs entre fenêtres.
- Ajoutant un HUD externe optionnel (sans lire le client).

**Principe fondamental** : l'outil gère l'OS (fenêtres, raccourcis, audio, envoi d'inputs simples). Il ne joue pas à la place du joueur. Zéro automation de gameplay, zéro lecture mémoire du client.

---

## 2. Périmètre

| Inclus | Exclus |
|---|---|
| Windows 10 / 11 | Autres OS |
| Dofus via Ankama Launcher, multi-instances | Dofus Retro (non testé, peut fonctionner) |
| Gestion fenêtres via Win32 | Injection, hooks bas niveau, drivers |
| Raccourcis clavier globaux | OCR / lecture écran pour piloter le jeu |
| Broadcast d'inputs simples (touche / clic) | Macros enchaînées, boucles, séquences |
| Icône tray + UI de configuration | — |

---

## 3. Personas & scénarios

| Persona | Besoin principal |
|---|---|
| Joueur 2 comptes | 2 fenêtres stables + switch instantané |
| Joueur 4 comptes | Hotkeys + broadcast quêtes |
| Joueur 6–8 comptes | Profils, multi-écrans, recovery crash, broadcast |

**Scénarios clés :**

1. "En combat → je passe au bon perso sans alt-tab chaotique."
2. "Un client crash → je relance et je retrouve la config."
3. "Je valide une quête sur tous mes persos en un seul raccourci."

---

## 4. Stack technique

| Composant | Choix |
|---|---|
| Runtime | .NET 8 |
| UI | WPF |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Win32 | P/Invoke : `EnumWindows`, `GetWindowText`, `GetWindowThreadProcessId`, `ShowWindow`, `RegisterHotKey`, `SendMessage`, `PostMessage`, `GetClientRect` |
| Persistance | JSON (System.Text.Json) |
| Logging | Serilog (fichiers locaux) |

---

## 5. Exigences non-fonctionnelles

| Catégorie | Exigence |
|---|---|
| Performance | Polling fenêtres ≤ 2 Hz par défaut (configurable). CPU < 1–2 % en idle. Focus en < 150 ms. |
| Fiabilité | Si une API Win32 échoue → retry contrôlé + log. Fenêtre disparue → fallback propre + notification. |
| Sécurité | Pas de driver, pas d'injection, pas de hook clavier global low-level (préférer `RegisterHotKey`). Envoi d'inputs via `SendMessage`/`PostMessage` uniquement. |
| UX | UI simple : onglets "Profil", "Fenêtres", "Raccourcis", "Broadcast", "Options". Presets "2 / 4 / 8 comptes". Onboarding 3 étapes. |

---

## 6. Fonctionnalités détaillées

### F1. Détection des fenêtres Dofus

**Priorité : MVP (v0.1)**

**Description :** Détecter toutes les fenêtres associées à Dofus (process + title). Afficher la liste dans l'UI : PID, titre, état (actif/minimisé), écran.

**Implémentation :**

- Utiliser `EnumWindows` + `GetWindowThreadProcessId` + `GetWindowText`.
- Filtrer par nom de process (`Dofus`) et/ou pattern de titre de fenêtre.
- Polling configurable (défaut : toutes les 500 ms).

**Critères d'acceptation :**

- [ ] Si 2+ clients ouverts, ils apparaissent tous en < 2 s.
- [ ] Mise à jour auto à intervalle configurable.
- [ ] La liste affiche PID, titre, état, écran.

---

### F2. Raccourcis de focus (hotkeys)

**Priorité : MVP (v0.1)**

**Description :** Hotkeys globales (F1..F8) pour focus direct sur une fenêtre assignée. Touches "Suivant/Précédent" et "Dernière fenêtre". Raccourci "Panic leader" pour ramener le leader au premier plan.

**Implémentation :**

- `RegisterHotKey` / `UnregisterHotKey` pour les raccourcis globaux.
- `SetForegroundWindow` + `ShowWindow` pour le focus.
- Gestion du cas où la fenêtre n'existe plus (fallback + notification).

**Critères d'acceptation :**

- [ ] Focus en < 150 ms.
- [ ] Si la fenêtre n'existe plus → notification, pas de crash.
- [ ] Hotkeys configurables par l'utilisateur.

---

### F3. Profils (team presets)

**Priorité : MVP (v0.1)**

**Description :** Enregistrer et charger des profils contenant : mapping persos → fenêtres, hotkeys, configuration broadcast.

**Structure JSON :**

```json
{
  "profileName": "Team Donjon",
  "slots": [
    {
      "index": 0,
      "characterName": "Panda-Main",
      "windowTitlePattern": "*Panda*",
      "isLeader": true,
      "hotkey": "F1"
    }
  ],
  "broadcastPresets": []
}
```

**Critères d'acceptation :**

- [ ] Sauvegarder un profil écrit un fichier JSON valide.
- [ ] Charger un profil réapplique hotkeys + broadcast.
- [ ] Plusieurs profils gérables (créer, modifier, supprimer).

---

### F4. Broadcast d'input (raccourci / clic)

**Priorité : Haute (v0.2)**

**Description :** Envoyer un même input (touche clavier ou clic souris) à plusieurs fenêtres Dofus en une seule action, avec un délai aléatoire entre chaque envoi. Cas d'usage : valider une quête, cliquer "Prêt", accepter une invitation, lancer un craft.

#### Modes de broadcast

| Mode | Description |
|---|---|
| **Touche** | Envoie une frappe clavier identique à toutes les cibles (ex : Entrée). |
| **Clic à position fixe** | Envoie un clic aux coordonnées relatives (x, y) pré-enregistrées dans chaque fenêtre. |
| **Clic à position actuelle** | Envoie un clic aux coordonnées actuelles du curseur, transposées relativement dans chaque fenêtre (requiert taille identique). |

#### Configuration

**Cibles :**

- Toutes les fenêtres détectées.
- Toutes sauf le leader.
- Sélection manuelle (cases à cocher).
- Groupe personnalisé (ex : "team donjon", "team craft").

**Délai aléatoire :**

- Plage configurable `[délai_min ; délai_max]` en ms.
- Défaut : 80–300 ms.
- Tirage indépendant pour chaque paire de fenêtres consécutives.
- Utiliser `Random` ou `RandomNumberGenerator`.

**Ordre d'envoi :** Ordre du profil (slot 1 → 2 → …) ou aléatoire.

**Hotkeys de broadcast :** Jusqu'à 4 raccourcis globaux configurables, chacun associé à un input différent. Distincts des hotkeys de focus (F2).

#### Outil "Pointer & Pick" (capture de position)

1. Le joueur active le mode capture depuis l'UI.
2. Il clique sur l'élément voulu dans n'importe quelle fenêtre Dofus.
3. L'app enregistre les coordonnées relatives à la zone client (`GetClientRect`).
4. Ces coordonnées sont associées au raccourci de broadcast.

#### Sécurités et garde-fous

| Garde-fou | Détail |
|---|---|
| Anti-rafale | Cooldown configurable (défaut 500 ms) entre deux broadcasts. |
| Confirmation optionnelle | Toast d'1 s avec annulation avant envoi. |
| Pause globale | Le bouton "Pause hotkeys" (F7 / tray) suspend aussi les broadcasts. |
| Fenêtre disparue | Ignorée silencieusement + log. Les autres reçoivent l'input. Notification optionnelle. |
| Scope limité | Un seul input par broadcast (une touche OU un clic). Pas de séquences, pas de macros, pas de boucles. |

#### Implémentation

- `SendMessage` / `PostMessage` avec `WM_KEYDOWN` / `WM_KEYUP` / `WM_LBUTTONDOWN` / `WM_LBUTTONUP` pour l'envoi en arrière-plan (sans focus).
- `GetClientRect` pour le calcul des coordonnées relatives.
- Pas de `SendInput` (nécessite le focus).
- Thread dédié (`Task.Run`) pour ne pas bloquer l'UI pendant les délais.

#### Structure JSON du preset broadcast

```json
{
  "broadcastPresets": [
    {
      "name": "Valider quête",
      "hotkey": "Ctrl+Enter",
      "inputType": "key",
      "key": "Enter",
      "targets": "allExceptLeader",
      "delayMin": 80,
      "delayMax": 300,
      "orderMode": "profile"
    },
    {
      "name": "Clic prêt combat",
      "hotkey": "Ctrl+Shift+R",
      "inputType": "clickAtPosition",
      "clickX": 450,
      "clickY": 620,
      "clickButton": "left",
      "targets": "all",
      "delayMin": 100,
      "delayMax": 350,
      "orderMode": "random"
    }
  ]
}
```

**Critères d'acceptation :**

- [ ] Broadcast touche vers 4 fenêtres s'exécute en < `délai_max × 3 + 200 ms`.
- [ ] Le délai entre deux envois respecte la plage `[min ; max]` configurée.
- [ ] L'input est reçu par chaque fenêtre cible, même en arrière-plan.
- [ ] Clic broadcast touche le même endroit relatif dans chaque fenêtre (tolérance ±2 px).
- [ ] Pointer & Pick capture la bonne position (±2 px).
- [ ] Aucun input envoyé à des fenêtres non-Dofus.

---

### F5. Session manager (lancement orchestré)

**Priorité : v1**

**Description :** Bouton "Démarrer la team" : ouvrir le launcher (optionnel), attendre l'apparition des fenêtres, appliquer hotkeys + broadcast. Mode "recovery" : si une fenêtre disparaît → notification + bouton "relancer et remettre".

> L'outil ne connecte pas à la place du joueur ; il orchestre l'ouverture, puis laisse l'utilisateur se log.

**Critères d'acceptation :**

- [ ] "Démarrer la team" lance le workflow complet.
- [ ] Attente des fenêtres avec timeout configurable.
- [ ] Recovery détecte la disparition d'une fenêtre et propose la relance.

---

### F6. Tray + commandes rapides

**Priorité : v1**

**Description :** Icône zone de notification avec menu contextuel.

**Actions disponibles :**

- Appliquer Profil X.
- Focus leader.
- Basculer mode combat (F8).
- Pause/Reprise hotkeys (inclut broadcast).

**Critères d'acceptation :**

- [ ] L'icône tray apparaît au lancement.
- [ ] Chaque action du menu fonctionne.
- [ ] Pause hotkeys suspend focus ET broadcast.

---

### F7. Switcher visuel

**Priorité : v1**

**Description :** Overlay ou listing non intrusif montrant : noms des persos, numéro hotkey, état (actif/minimisé).

**Critères d'acceptation :**

- [ ] Le switcher affiche toutes les fenêtres détectées.
- [ ] Cliquer sur un élément focus la fenêtre correspondante.
- [ ] L'overlay ne bloque pas les interactions avec le jeu.

---

### F8. Mode "combat assist" manuel

**Priorité : v2 (nice-to-have)**

**Description :** Aide au suivi d'un ordre de tour défini par le joueur (ex : Panda → Iop → Enu → …). Touche "Next turn" → focus le perso suivant. Alerte sonore optionnelle.

> Ne dépend d'aucune donnée du jeu. Zéro automation de clic.

**Critères d'acceptation :**

- [ ] L'utilisateur définit l'ordre manuellement.
- [ ] "Next turn" focus le perso suivant dans l'ordre.
- [ ] Alerte sonore optionnelle fonctionne.

---

### F9. Audio par fenêtre

**Priorité : v2 (nice-to-have)**

**Description :** Mute/volume différent par process Dofus. Leader audible, secondaires atténués.

**Implémentation :** Windows Core Audio API (`IAudioSessionManager2`).

**Critères d'acceptation :**

- [ ] Le volume de chaque fenêtre Dofus peut être réglé indépendamment.
- [ ] Le profil sauvegarde les réglages audio.

---

### F10. Multi-écrans avancé

**Priorité : v2 (nice-to-have)**

**Description :** Détection hotplug écrans. Profils par configuration d'écrans (portable + dock).

**Critères d'acceptation :**

- [ ] Détection dynamique de branchement/débranchement d'écran.
- [ ] Le profil s'adapte automatiquement à la configuration détectée.

---

## 7. Architecture du projet

```
DofusManager/
├── DofusManager.sln
├── src/
│   ├── DofusManager.Core/              # Logique métier (pas de dépendance UI)
│   │   ├── Models/
│   │   │   ├── DofusWindow.cs          # Modèle d'une fenêtre détectée
│   │   │   ├── Profile.cs              # Profil de team
│   │   │   ├── BroadcastPreset.cs      # Preset de broadcast
│   │   │   └── HotkeyBinding.cs        # Binding hotkey
│   │   ├── Services/
│   │   │   ├── WindowDetectionService.cs
│   │   │   ├── HotkeyService.cs
│   │   │   ├── BroadcastService.cs
│   │   │   ├── ProfileService.cs
│   │   │   └── SessionService.cs
│   │   └── Win32/
│   │       ├── NativeMethods.cs        # P/Invoke declarations
│   │       └── WindowHelper.cs         # Wrappers haut niveau
│   ├── DofusManager.UI/               # Application WPF
│   │   ├── App.xaml
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── WindowListViewModel.cs
│   │   │   ├── HotkeyViewModel.cs
│   │   │   ├── BroadcastViewModel.cs
│   │   │   └── ProfileViewModel.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml
│   │   │   ├── WindowListView.xaml
│   │   │   ├── HotkeyView.xaml
│   │   │   ├── BroadcastView.xaml
│   │   │   └── ProfileView.xaml
│   │   └── Resources/
│   └── DofusManager.Tests/            # Tests unitaires
│       ├── Services/
│       └── Models/
├── config/
│   └── default-profiles.json
└── docs/
    ├── installation.md
    ├── guide-utilisateur.md
    └── depannage.md
```

---

## 8. Plan d'implémentation itératif

Chaque itération produit un incrément fonctionnel testable.

### Itération 1 — Fondations + Détection (F1)

**Objectif :** Projet compilable avec détection des fenêtres Dofus.

**Tâches :**

1. Créer la solution .NET 8 + projets (Core, UI, Tests).
2. Implémenter `NativeMethods.cs` (P/Invoke : `EnumWindows`, `GetWindowText`, `GetWindowThreadProcessId`, `IsWindowVisible`).
3. Implémenter `WindowDetectionService` avec polling configurable.
4. Implémenter le modèle `DofusWindow`.
5. Créer `MainWindow.xaml` avec une liste affichant les fenêtres détectées (PID, titre, état).
6. Écrire les tests unitaires pour la détection.

**Livrable :** L'app se lance, détecte et liste les fenêtres Dofus ouvertes.

---

### Itération 2 — Hotkeys de focus (F2)

**Objectif :** Naviguer entre fenêtres par raccourcis clavier.

**Tâches :**

1. Implémenter `HotkeyService` (`RegisterHotKey`, `UnregisterHotKey`, message loop).
2. Implémenter `SetForegroundWindow` + `ShowWindow` dans `WindowHelper`.
3. Gestion Suivant/Précédent/Dernière fenêtre/Panic leader.
4. Fallback si fenêtre disparue (notification toast).
5. `HotkeyView.xaml` : configuration des raccourcis.
6. Tests.

**Livrable :** F1–F8 focus la fenêtre assignée en < 150 ms.

---

### Itération 3 — Profils (F3)

**Objectif :** Sauvegarder et restaurer des configurations complètes.

**Tâches :**

1. Implémenter `Profile` model (slots, hotkeys, broadcast presets).
2. Implémenter `ProfileService` (CRUD, sérialisation JSON).
3. `ProfileView.xaml` : créer, charger, modifier, supprimer des profils.
4. Au chargement d'un profil → réappliquer hotkeys.
5. Tests de sérialisation/désérialisation.

**Livrable :** L'utilisateur crée un profil, ferme l'app, relance, charge le profil.

---

### Itération 4 — Broadcast d'input (F4)

**Objectif :** Envoyer des inputs à plusieurs fenêtres avec délai aléatoire.

**Tâches :**

1. Implémenter `BroadcastPreset` model.
2. Implémenter `BroadcastService` :
    - Envoi touche via `PostMessage` (`WM_KEYDOWN` / `WM_KEYUP`).
    - Envoi clic via `PostMessage` (`WM_LBUTTONDOWN` / `WM_LBUTTONUP`) avec `lParam` = coordonnées.
    - Délai aléatoire `[min ; max]` entre chaque fenêtre.
    - Thread dédié (`Task.Run` + `Task.Delay`).
    - Sélection des cibles (all, allExceptLeader, custom group).
    - Ordre d'envoi (profil ou random).
3. Implémenter le "Pointer & Pick" :
    - Mode capture global.
    - `GetClientRect` + calcul coordonnées relatives.
    - Enregistrement dans le preset.
4. Garde-fous : cooldown anti-rafale, pause globale, gestion fenêtre disparue.
5. Hotkeys broadcast via `RegisterHotKey` (jusqu'à 4).
6. `BroadcastView.xaml` : config des presets, capture de position, test de broadcast.
7. Intégration dans les profils (F3).
8. Tests.

**Livrable :** Un raccourci envoie Entrée à toutes les fenêtres avec délai aléatoire visible dans les logs.

---

### Itération 5 — Session manager (F5)

**Objectif :** Lancement orchestré + recovery.

**Tâches :**

1. Implémenter `SessionService` : lancement launcher, attente fenêtres, application profil.
2. Mode recovery : détection disparition → notification + relance.
3. UI pour "Démarrer la team".
4. Tests.

**Livrable :** "Démarrer la team" lance, attend, configure automatiquement.

---

### Itération 6 — Tray + Switcher (F6, F7)

**Objectif :** Accès rapide depuis la zone de notification + switcher visuel.

**Tâches :**

1. Icône tray (`NotifyIcon`) avec menu contextuel.
2. Actions : appliquer profil, focus leader, pause hotkeys/broadcast, mode combat.
3. Switcher visuel : overlay ou fenêtre toujours visible avec liste des persos.
4. Tests.

**Livrable :** L'app vit dans le tray, le switcher montre l'état de la team.

---

### Itération 7 — Combat assist + Audio + Multi-écrans (F8, F9, F10)

**Objectif :** Fonctionnalités avancées nice-to-have.

**Tâches :**

1. Combat assist : ordre manuel, "Next turn", alerte sonore.
2. Audio par fenêtre : Windows Core Audio API.
3. Multi-écrans avancé : détection hotplug, profils par config écrans.
4. Tests.

**Livrable :** Version complète v2.

---

## 9. Format de configuration JSON global

```json
{
  "settings": {
    "pollingIntervalMs": 500,
    "focusLatencyTarget": 150,
    "broadcastCooldownMs": 500,
    "broadcastConfirmation": false,
    "logLevel": "Information",
    "logPath": "./logs"
  },
  "profiles": [
    {
      "profileName": "Team Donjon",
      "slots": [
        {
          "index": 0,
          "characterName": "Panda-Main",
          "windowTitlePattern": "*Panda*",
          "isLeader": true,
          "focusHotkey": "F1"
        },
        {
          "index": 1,
          "characterName": "Iop-DPS",
          "windowTitlePattern": "*Iop*",
          "isLeader": false,
          "focusHotkey": "F2"
        }
      ],
      "broadcastPresets": [
        {
          "name": "Valider quête",
          "hotkey": "Ctrl+Enter",
          "inputType": "key",
          "key": "Enter",
          "targets": "allExceptLeader",
          "delayMin": 80,
          "delayMax": 300,
          "orderMode": "profile"
        }
      ],
      "combatOrder": ["Panda-Main", "Iop-DPS"],
      "audioSettings": {
        "leaderVolume": 100,
        "secondaryVolume": 20
      }
    }
  ]
}
```

---

## 10. Livrables finaux

- Application Windows (`.exe` + installateur).
- Fichier de config JSON (profils).
- Documentation :
    - `installation.md` : prérequis, installation.
    - `guide-utilisateur.md` : création de profil, raccourcis, broadcast.
    - `depannage.md` : problèmes courants, logs.

---

## 11. Récapitulatif du backlog priorisé

| Ordre | ID | Fonctionnalité | Itération | Priorité |
|---|---|---|---|---|
| 1 | F1 | Détection fenêtres | 1 | MVP |
| 2 | F2 | Hotkeys focus | 2 | MVP |
| 3 | F3 | Profils | 3 | MVP |
| 4 | F4 | Broadcast input | 4 | Haute |
| 5 | F5 | Session manager | 5 | v1 |
| 6 | F6 | Tray | 6 | v1 |
| 7 | F7 | Switcher visuel | 6 | v1 |
| 8 | F8 | Combat assist | 7 | v2 |
| 9 | F9 | Audio par fenêtre | 7 | v2 |
| 10 | F10 | Multi-écrans avancé | 7 | v2 |