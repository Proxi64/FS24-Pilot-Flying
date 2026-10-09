# 06 — Faisabilité : ce qu'il faut établir avant de coder

*English: [../English/06-feasibility.md](../English/06-feasibility.md)*

**Principe :** on établit ce qui est possible avant d'écrire l'application. Chaque question se tranche par une lecture de documentation ou par un **petit essai** : une console jetable rangée dans `Experiments/`, hors du futur projet.

Statuts : ✅ confirmé · 🟡 en partie · ❓ à vérifier · ❌ impossible ou abandonné.

## A. Commander l'avion depuis l'extérieur

| # | Question | Comment vérifier | Statut |
|---|---|---|---|
| A1 | Peut-on envoyer en continu (20 à 50 Hz) profondeur, ailerons, palonnier, gaz et freins par SimConnect (`AXIS_*_SET`, `THROTTLE_SET`, `AXIS_LEFT/RIGHT_BRAKE_SET`) ? | Essai sur C172, puis A320 d'Asobo | ❓ |
| A2 | Que se passe-t-il avec le joystick et la manette des gaz du joueur branchés ? Comment prendre et rendre la main proprement ? | Essai avec matériel branché | ❓ |
| A3 | Quelle fréquence de lecture et d'écriture atteint-on réellement, avec quelle irrégularité ? | Mesure pendant l'essai A1 | ❓ |
| A4 | Au sol, le palonnier oriente-t-il la roulette de nez ? Y a-t-il un tiller séparé ? | Essai au parking | ❓ |

## B. Fenix A320

| # | Question | Comment vérifier | Statut |
|---|---|---|---|
| B1 | Le Fenix accepte-t-il les axes SimConnect standard (mini-manche, palonnier, freins, manettes), ou faut-il passer par des LVars ? | Essai A1 sur le Fenix | ❓ |
| B2 | Quelle commande pour le tiller ? | HubHop + essai | ❓ |
| B3 | Les commandes du FCU (vitesse, cap, altitude, VS, AP1/2, A/THR, APPR, LOC), des volets, du train, des spoilers et de l'autobrake existent-elles dans HubHop et fonctionnent-elles sous 2024 ? | HubHop + essai par le module WASM MobiFlight | ❓ |
| B4 | Peut-on lire l'état : modes FMA, phase du FMS, alarmes ? | HubHop, liste des LVars, essai | ❓ |
| B5 | L'autoland complet (arrondi, roulement, freinage automatique) fonctionne-t-il sur ILS quand c'est notre programme qui l'arme ? | Vol d'essai manuel, puis piloté | ❓ |
| B6 | Que fournit `Fenix.GqlGateway` ? Les conditions d'utilisation de Fenix autorisent-elles un outil tiers ? | Exploration + lecture des CGU, voire demande à Fenix | ❓ |
| B7 | Plan de vol dans le MCDU : en V1, on suppose que le joueur le charge (import SimBrief de l'EFB Fenix). Automatisable plus tard ? | Plus tard | ❓ |

## C. Accès aux variables des avions tiers

| # | Question | Comment vérifier | Statut |
|---|---|---|---|
| C1 | Le module WASM MobiFlight fonctionne-t-il sous MSFS 2024, avec un client tiers enregistré ? | Essai | ❓ |
| C2 | Licences : module WASM MobiFlight, données HubHop, FS Copilot, WASimCommander | Lecture des licences | 🟡 module MobiFlight : MIT |
| C3 | Faut-il FSUIPC ? (non souhaité) | Découle de C1 | ❓ |
| C4 | Les valeurs envoyées « au changement » par le module MobiFlight arrivent-elles assez vite pour une boucle ? | Mesure pendant C1 | ❓ |

## D. Aérodromes et roulage

| # | Question | Comment vérifier | Statut |
|---|---|---|---|
| D1 | Les points d'attente avant piste sont-ils fournis ? | Doc SDK 2024 | ✅ `TAXI_POINT.TYPE` : 2 HOLD_SHORT, 4 ILS_HOLD_SHORT (+ 5 et 6 sans marquage), avec `ORIENTATION`. Vu à LFBP (essai D2, 09/10/2026) : 8 points, tous de type 5 (sans marquage), à 147–167 m de l'axe de piste, tous sur le réseau principal. LFPG (scène par défaut) : 147 points, types 5 et 6 (ILS), à 71–310 m de l'axe, tous sur le réseau principal |
| D2 | Les noms des voies sont-ils disponibles et reliés aux tronçons ? | Doc SDK + essai D2 | ✅ `NAME_INDEX` est l'indice dans la liste `TAXI_NAME` (0 = sans nom). LFBP (essai D2, 09/10/2026) : 16 noms, chacun formant 1 ou 2 morceaux continus ; 12 existent aussi dans OpenStreetMap (https://www.openstreetmap.org, ODbL) au même endroit, à 6–85 m près pour les voies courtes (E, G, N, N1, N3, N5, S2). C, M et NG n'existent que dans MSFS, B, BA et BC que dans OSM. LFPG : 225 noms, dont 217 d'un seul morceau. Sur les tronçons RUNWAY (type 2), `NAME_INDEX` est en revanche l'indice de la piste dans la liste des pistes du terrain |
| D3 | Que signifient les types de tronçons ? | Doc SDK 2024 | ✅ 1 TAXI, 2 RUNWAY, 3 PARKING, 4 PATH, 5 CLOSED, 6 VEHICLE, 7 ROAD, 8 PAINTEDLINE. Les scènes ne s'en servent pas de la même façon : LFBP (France VFR) surtout TAXI, LFPG (Asobo) presque uniquement PATH (4 667 PATH contre 3 TAXI) : les deux types doivent être traités comme des voies de circulation |
| D4 | L'axe des voies du plan coïncide-t-il avec la ligne peinte de la scène (scène de base et payware) ? | Rouler à la main et comparer la position de l'avion au graphe | ❓ |
| D5 | Peut-on connaître la position des autres avions au sol (trafic IA, BeyondATC) pour s'arrêter derrière eux ? | `SimConnect_RequestDataOnSimObjectType` | ❓ |
| D6 | Peut-on récupérer la clairance de roulage de BeyondATC ? | Étude d'un outil existant autour de BeyondATC | ❓ |

## E. Boucles de pilotage

| # | Question | Comment vérifier | Statut |
|---|---|---|---|
| E1 | Le suivi de trajectoire au sol est-il stable à la fréquence mesurée en A3 (C172, puis A320) ? | Essai à Pau (LFBP) | ❓ |
| E2 | Décollage A320 : tenue d'axe et rotation au mini-manche jusqu'au PA à 100 ft | Essai | ❓ |
| E3 | Faut-il un module WASM pour la latence ? | Découle de A3, E1, E2 | ❓ (indice favorable : Pomax décolle et atterrit des avions légers depuis l'extérieur, sous MSFS 2020) |
| E4 | Sous quelle licence est le code du tutoriel de Pomax ? | Lecture du dépôt | ❓ |

## Essais

| Essai | Dossier | Questions | État |
|---|---|---|---|
| D2 — plan de roulage complet | `Experiments/D2-TaxiLayout/` | D1, D2, D3, convention des axes, connexité du réseau | Lancé dans MSFS à LFBP, scène payware France VFR du dossier Community (9 octobre 2026). Confirmé : types d'éléments 2 START et 17 TAXI_NAME, BIAS_X = est / BIAS_Z = nord (médiane 0,4 m contre 297 m), réseau relié à toutes les places et à tous les points d'attente. Relancé à LFPG, scène Asobo par défaut (9 octobre 2026) : mêmes conclusions (axes : médiane 1,3 m contre 544 m ; réseau principal de 4 313 nœuds, 374 places toutes reliées). `WIDTH`, `CENTER_LINE` et `WEIGHT` sont bien transmis (LFPG : largeurs de 5 à 60 m, ligne peinte sur 3 252 tronçons, masse 500 000 lb) mais dépendent de la scène : LFBP donne 30 m, 0 et 0 partout. On ne peut pas s'y fier sans valeur de repli |

### Ce que fait l'essai D2

Il lit le plan complet d'un aérodrome, avec tous les champs utiles au roulage (points avec type et orientation, tronçons avec nom, piste, axe peint et masse, noms des voies, positions de départ, ILS des pistes), puis :

- compte les points et les tronçons par type ;
- pour chaque nom de voie, compte ses tronçons et ses morceaux : un vrai nom de voie forme un tracé continu ;
- liste les points d'attente avec leur voie et leur distance à l'axe de piste ;
- **vérifie la convention des axes** en comparant les tronçons de piste à l'axe des pistes, dans les deux interprétations possibles ;
- vérifie que le réseau avion forme un seul ensemble relié aux places et aux points d'attente ;
- écrit un rapport, les données brutes (JSON) et un fichier GeoJSON à afficher sur fond de carte (https://geojson.io).

## Ordre proposé

1. **D2** dans le simulateur (lecture seule, sans risque).
2. **A1 à A4** sur un avion d'Asobo : une console qui lit l'état et envoie des axes.
3. **C1**, puis **B1 à B4** sur le Fenix.
4. **D4 + E1** : premier roulage automatique à Pau.
5. **B5, E2** : autoland et décollage.

À l'issue : fixer le périmètre de la V1 et l'architecture.
