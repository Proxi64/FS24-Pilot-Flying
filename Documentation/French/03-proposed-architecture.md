# 03 — Architecture envisagée

*English: [../English/03-proposed-architecture.md](../English/03-proposed-architecture.md)*

> **Rien n'est figé.** Ce document rassemble les pistes actuelles pour servir de base de discussion. Les choix définitifs viendront après l'étude de faisabilité ([06](06-feasibility.md)) et avec les contributeurs.

## Vue d'ensemble

```
┌──────────────────────────── Application Windows (.NET) ────────────────────────────┐
│                                                                                    │
│  Interface (WinUI 3)      Chef de vol (machine à états des phases)                │
│        │                     │        │         │          │                       │
│        │               Roulage   Décollage   Vol / approche   Atterrissage         │
│        │                     │        │         │          │                       │
│        └──────────── Profils d'avion (actions abstraites → commandes réelles) ─────┤
│                              │                                                     │
│   Plans d'aérodromes ────────┤        Couche simulateur                            │
│   (bibliothèque commune)     │        ├── SimConnect natif (P/Invoke)              │
│                              │        └── client du module WASM (LVars, H-events)  │
└──────────────────────────────┼─────────────────────────────────────────────────────┘
                               │
┌──────────────────────────────┼──────────── MSFS 2024 ──────────────────────────────┐
│   SimConnect      Module WASM MobiFlight (ou équivalent)      [option] notre module│
│                                                               WASM pour les boucles │
│                                                               rapides               │
└────────────────────────────────────────────────────────────────────────────────────┘
```

## Composants

| Composant | Rôle | Remarques |
|---|---|---|
| **Couche simulateur** | Connexion SimConnect, lecture d'état, envoi de commandes, client du module WASM | P/Invoke sur la DLL SimConnect **native** : la DLL « managed » du SDK cible .NET Framework 4.6.1 et ne se charge pas en .NET moderne (déjà éprouvé) |
| **Plans d'aérodromes** | Lecture par l'API Facilities, copie locale, graphe de roulage | Code existant à extraire dans une **bibliothèque commune** (voir [04](04-airports-and-taxiing.md)) |
| **Profils d'avion** | Table « action abstraite → commande » + caractéristiques (vitesses, empattement, crans de volets, capacités : autoland, autopoussée…) | Fichiers texte (JSON ou YAML) écrits et partagés par la communauté ; commandes tirées de HubHop |
| **Chef de vol** | Machine à états : parking → roulage → alignement → décollage → montée → croisière → descente → approche → atterrissage → dégagement | Gère aussi les sorties de secours (remise de gaz, arrêt) |
| **Contrôleurs** | Boucles de régulation : suivi de trajectoire au sol, vitesse de roulage, tenue d'axe, rotation, arrondi | Dans l'application si la latence le permet, sinon dans un module WASM |
| **Interface** | Choix du vol, démarrage, suivi, reprise en main | WinUI 3 ; éventuellement un panneau dans le simulateur (barre d'outils ou EFB) |

## Pile technique envisagée

- **.NET 10, C#**, processus x64.
- **SimConnect natif** en P/Invoke (structures `#pragma pack(1)`), déjà au point dans un projet précédent.
- **Module WASM MobiFlight** (licence MIT) pour les LVars et H-events ; FSUIPC seulement en option, s'il est déjà installé.
- **WinUI 3** + CommunityToolkit.Mvvm pour l'interface.
- **Option** : module WASM maison (C/C++, chaîne du SDK MSFS 2024) si une boucle doit tourner au rythme du simulateur.

## Le module WASM maison, si nécessaire

D'après la documentation WebAssembly du SDK MSFS 2024 :

- C/C++ compilé en WebAssembly par la chaîne du SDK (extension Visual Studio), puis **converti en code natif** à l'avance ;
- un **module autonome** (dossier `modules` d'un paquet) est chargé automatiquement et reçoit **un appel à chaque image**, sans passer par SimConnect ;
- chaque module a **son propre fil d'exécution** (en 2020, tout tournait sur le fil principal) ;
- API disponibles : Vars, Event, IO, **CommBus** (dialogue avec l'application externe), Network, Planned Route… ; l'ancienne API Gauge est dépréciée ;
- interdits : API Windows, exceptions et fils C++. Fichiers : lecture seule dans le paquet, lecture/écriture dans `\work`.

## Profil d'avion : à quoi il pourrait ressembler

Exemple **indicatif** (format non décidé) :

```yaml
avion: Fenix A320
titres: ["FenixA320*"]            # titres SimConnect reconnus
caracteristiques:
  empattement_m: 12.6             # pour le suivi de trajectoire au sol
  vitesse_roulage_kt: 20
  autoland: true
  autopoussee: true
actions:
  train_sortir:   { hubhop: "<preset HubHop>" }
  volets_cran:    { hubhop: "<preset HubHop>", parametre: cran }
  ap1:            { hubhop: "<preset HubHop>" }
  direction_sol:  { lvar: "<LVar du tiller>", plage: [-1, 1] }
etat:
  phase_fma:      { lvar: "<LVar>" }
```

Des projets existants ont des formats proches dont on peut s'inspirer : les définitions YAML de FS Copilot et les presets HubHop (voir [05](05-ecosystem.md)).

## Questions d'architecture ouvertes

- Boucles rapides dans l'application ou dans un module WASM ? (dépend des mesures A3, E1, E2 de [06](06-feasibility.md))
- Format des profils (JSON ou YAML) et manière de les partager.
- Interface : application seule, ou aussi un panneau dans le simulateur ?
- Coexistence avec l'ATC tiers (BeyondATC) : lire ses clairances de roulage ?
