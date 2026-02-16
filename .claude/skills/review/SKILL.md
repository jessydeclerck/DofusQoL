---
name: review
description: Revue de code de l'itération courante
allowed-tools: Read, Grep, Glob, Bash(dotnet build:*), Bash(dotnet test:*)
---
Fais une revue complète du code actuel :
1. Vérifie la cohérence avec le cahier des charges (docs/specs.md)
2. Vérifie les conventions CLAUDE.md
3. Lance dotnet build et dotnet test
4. Vérifie que chaque service a ses tests
5. Vérifie l'injection de dépendances
6. Identifie le code mort ou dupliqué
7. Propose des améliorations
