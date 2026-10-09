# FS24 Pilot Flying

**Un pilote virtuel pour Microsoft Flight Simulator 2024 : une application qui pilote l'avion à la place du joueur, du parking au parking.**

*English version: [../English/README.md](../English/README.md)*

---

## En deux mots

MSFS 2020 avait un « AI Pilot » capable d'emmener l'avion de la porte de départ à la porte d'arrivée. **MSFS 2024 ne l'a plus**, et aucun outil ne le remplace. Une partie de la communauté en a pourtant vraiment besoin, en particulier les joueurs en situation de handicap pour qui c'était la seule façon de voler.

FS24 Pilot Flying veut combler ce manque : une application Windows externe, reliée au simulateur par SimConnect, qui **roule, décolle, vole et atterrit** à la place du joueur, sur des avions décrits par des **profils ouverts** que la communauté peut écrire.

## Où en est le projet

| | |
|---|---|
| Phase | **Étude de faisabilité.** Pas encore de code applicatif : on établit d'abord ce qui est possible. |
| Déjà acquis | Plans d'aérodromes complets lus dans le simulateur (≈ 85 000 terrains, scènes payware comprises), lecture SimConnect native en .NET, doc SDK 2024 analysée pour le roulage |
| Premier essai | Console de lecture du plan de roulage complet (`Experiments/D2-TaxiLayout`), prête, à valider dans le simulateur |
| Recherché | Développeurs MSFS intéressés pour construire le projet en commun (voir « Participer ») |

## Lire la documentation

| Document | Contenu |
|---|---|
| [01 — Vision et besoin](01-vision.md) | Le besoin, le public, ce qui existe déjà, le positionnement, le périmètre envisagé |
| [02 — Défis techniques](02-technical-challenges.md) | Pourquoi c'est difficile, phase par phase, et comment on compte s'y prendre |
| [03 — Architecture envisagée](03-proposed-architecture.md) | Composants, pile technique, profils d'avion, options (rien n'est figé) |
| [04 — Aérodromes et roulage](04-airports-and-taxiing.md) | Les données de l'API Facilities, ce qui est déjà lu, ce qu'il reste à faire pour rouler seul |
| [05 — Écosystème](05-ecosystem.md) | Outils, bibliothèques et projets existants étudiés, avec leurs licences |
| [06 — Faisabilité](06-feasibility.md) | **Les questions à trancher avant de coder**, leur statut, les essais |
| [07 — Glossaire](07-glossary.md) | LVar, H-event, HubHop, Facilities, WASM… |
| [Journal des décisions](decision-log.md) | Ce qui a été décidé, quand et pourquoi |

Chaque document a son équivalent exact en anglais, sous le même nom de fichier ; chaque page renvoie à sa traduction en tête.

## Participer

Le projet cherche des contributeurs à l'aise avec au moins un de ces sujets :

- **SimConnect / SDK MSFS 2024** (C# ou C++), API Facilities, modules WASM ;
- **lois de pilotage** : suivi de trajectoire au sol, décollage, arrondi, régulation (PID ou autre) ;
- **avions tiers** : LVars et événements du Fenix A320, des PMDG, iniBuilds… (profils d'avion) ;
- **WinUI 3 / .NET** pour l'application ;
- **tests en vol**, sur des avions et des terrains variés.

Le projet est publié sous licence MIT (voir `LICENSE` à la racine du dépôt). Les questions encore ouvertes (organisation) sont listées dans le [journal des décisions](decision-log.md) et se décideront avec les premiers contributeurs.

Contact : ouvrez une *issue* sur le dépôt GitHub : https://github.com/Proxi64/FS24-Pilot-Flying/issues

## Organisation des dossiers

```
FS24 Pilot Flying/
├── Documentation/
│   ├── French/       ← cette documentation
│   └── English/      ← la même, en anglais (toujours tenue à jour en même temps)
└── Experiments/      ← consoles d'essai jetables de l'étude de faisabilité
```

Règle : **tout document existe dans les deux langues**, avec le même nom de fichier et le même contenu.
