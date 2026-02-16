---
name: test
description: Lance les tests du projet
allowed-tools: Bash(dotnet test:*), Read, Grep, Glob
---
Lance les tests du projet DofusManager :
1. Exécute `dotnet test src/DofusManager.Tests/` avec la sortie détaillée
2. Analyse les résultats
3. Si des tests échouent, identifie la cause et propose un correctif
4. Vérifie la couverture : chaque service public doit avoir des tests correspondants
