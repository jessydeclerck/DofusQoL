# DofusQoL

Application Windows de quality of life pour le multi-compte Dofus.
Gestion fenêtres OS uniquement — zero automation gameplay, zero injection, zero lecture memoire client.

![DofusManager UI](UI%20dofus%20maanger.png)

## Fonctionnalites

### Navigation ultra-rapide entre les fenetres

Fini le Alt+Tab chaotique. Chaque personnage est assigne a un raccourci clavier (F1-F8 par defaut), un appui et la bonne fenetre est au premier plan en moins de 150 ms. Des raccourcis globaux permettent aussi de passer a la fenetre suivante/precedente, revenir a la derniere fenetre utilisee ou rappeler le leader d'un seul geste.

### Push-to-Broadcast : un clic, toutes les fenetres

Maintenez la touche broadcast (Alt par defaut) et cliquez : le clic est reproduit aux memes coordonnees dans toutes les fenetres Dofus. Ideal pour valider une quete, cliquer "Pret" en combat ou accepter une invitation sur tous les comptes en meme temps. Le delai entre chaque envoi et sa variation aleatoire sont reglables.

### Coller dans le chat en un clic

Appuyez sur le raccourci (clic molette par defaut) et le contenu du presse-papier est colle dans le chat de la fenetre visee : ouverture du chat, Ctrl+V, Entree, le tout automatiquement. Option double Entree pour les actions qui demandent confirmation. Vous pouvez choisir de toujours cibler le leader ou la fenetre sous le curseur.

### Invitations de groupe instantanees

Un bouton envoie `/invite` a tous les personnages depuis le leader. Un autre active l'autofollow (Ctrl+W) sur tous les suiveurs. Un troisieme ouvre le havre-sac sur tous les comptes. Plus besoin de taper les commandes a la main sur chaque fenetre.

### Voyage Zaap en un clic

L'onglet Zaap liste les 45 territoires classes par region. Selectionnez une destination, cliquez voyager : l'application ouvre le havre-sac, clique sur le Zaap, tape le nom du territoire et valide, sur chaque fenetre. Les destinations favorites sont marquees d'une etoile pour un acces rapide. Option autofollow automatique apres le voyage.

### Profils d'equipe

Sauvegardez votre configuration complete (ordre des personnages, raccourcis, reglages broadcast, coordonnees Zaap, favoris) dans des profils nommes. Changez d'equipe en un clic sans tout reconfigurer. La session en cours est sauvegardee automatiquement.

### Mises a jour automatiques

L'application verifie les nouvelles versions sur GitHub au demarrage et affiche un bandeau discret quand une mise a jour est disponible. Un clic pour telecharger et installer.

## Prerequis

- Windows 10 / 11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Installation

Telecharger le dernier `.zip` depuis la page [Releases](../../releases), extraire et lancer `DofusManager.UI.exe`.

## Build depuis les sources

```bash
dotnet build DofusQoL.sln
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

## Propriete intellectuelle

Les images et assets graphiques lies a Dofus sont la propriete d'[Ankama Games](https://www.ankama.com/). Ce projet n'est pas affilie a Ankama.

## Licence

Usage personnel.
