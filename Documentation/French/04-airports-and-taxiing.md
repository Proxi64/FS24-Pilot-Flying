# 04 — Aérodromes et roulage

*English: [../English/04-airports-and-taxiing.md](../English/04-airports-and-taxiing.md)*

Le roulage automatique repose sur une bonne nouvelle : **le simulateur fournit le plan complet de chaque aérodrome**, et ce plan est déjà lu et stocké par un projet précédent de l'initiateur (un générateur de missions pour MSFS 2024, en C# .NET 10).

## L'API Facilities de SimConnect

C'est l'API qu'utilise GSX. Avec `SimConnect_AddToFacilityDefinition` et `SimConnect_RequestFacilityData`, on obtient le plan de l'aérodrome **tel que MSFS le charge**, scènes payware activées comprises.

### Ce qui a été validé en pratique (septembre 2026)

- Ça fonctionne **dès le menu principal**, sans être en vol.
- **Liste de tous les aérodromes** (`SimConnect_RequestAllFacilities`) : 85 791 terrains reçus en 0,1 s. Réponse au format `SIMCONNECT_RECV_AIRPORT_LIST` (message n° 18, entrées de 36 octets), et non `FACILITY_MINIMAL_LIST` comme on pouvait le croire.
- **Lecture de tous les plans** : 85 764 plans en 7,8 minutes, 32 demandes simultanées, copie locale en JSON (un fichier par terrain), relecture automatique quand le format change.
- **Scène payware prise en compte** : Biarritz (LFBZ) avec la scène Flightbeam renvoie bien ses 27 places.
- **Axes** : `BIAS_X` = **est**, `BIAS_Z` = **nord**, en mètres depuis le point de référence de l'aérodrome (vérifié sur le terrain). Conversion : `lat = lat0 + z / 111 320` ; `lon = lon0 + x / (111 320 · cos lat0)`. La doc parle d'axes « longitudinal / latitudinal », d'où l'intérêt de la vérification automatique prévue dans l'essai D2.
- Les données arrivent **à la suite, sans alignement** (lecture champ par champ dans l'ordre de la définition) ; chaque élément arrive dans un message `SIMCONNECT_RECV_FACILITY_DATA` (n° 28) dont le champ `Type` indique la nature (0 aérodrome, 1 piste, 14 point de voie, 15 place, 16 tronçon) ; fin par `FACILITY_DATA_END` (n° 29).

### Ce que contient un plan déjà stocké

Référence et altitude ; **pistes** (centre, cap, longueur, largeur, revêtement, désignations) ; **places** (type, nom, numéro, cap, rayon, position) ; hélisurfaces ; **tronçons de voies** (extrémités, largeur, type) ; fréquences radio.

Exemple de Pau (LFBP) : 2 pistes, 22 places, 489 tronçons, 469 nœuds. Le réseau est bien connecté (383 nœuds de passage, 60 croisements, 26 culs-de-sac) et les courbes sont déjà découpées finement (tronçons de 2,5 à 729 m, médiane 12 m).

## Ce que dit la doc SDK 2024 (lue le 9 octobre 2026)

Source : [SimConnect_AddToFacilityDefinition](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/API_Reference/Facilities/SimConnect_AddToFacilityDefinition.htm).

### TAXI_POINT : les points de voie

| Champ | Valeurs |
|---|---|
| `TYPE` | 0 NONE, 1 NORMAL, **2 HOLD_SHORT**, **4 ILS_HOLD_SHORT**, 5 HOLD_SHORT_NO_DRAW, 6 ILS_HOLD_SHORT_NO_DRAW (pas de 3) |
| `ORIENTATION` | 0 FORWARD, 1 REVERSE (sens du point d'attente) |
| `BIAS_X`, `BIAS_Z` | position en mètres |

→ Les **points d'attente avant piste** sont fournis, y compris les attentes ILS (CAT II/III).

### TAXI_PATH : les tronçons

| Champ | Contenu |
|---|---|
| `TYPE` | 0 NONE, **1 TAXI**, **2 RUNWAY**, **3 PARKING**, **4 PATH**, 5 CLOSED, 6 VEHICLE, 7 ROAD, 8 PAINTEDLINE → pour un avion : 1 à 4 ; exclure 5, 6, 7 |
| `WIDTH`, `LEFT_HALF_WIDTH`, `RIGHT_HALF_WIDTH` | largeur (voies asymétriques comprises) |
| `WEIGHT` | limite de masse (lb) : écarter les voies trop faibles |
| `RUNWAY_NUMBER`, `RUNWAY_DESIGNATOR` | piste à laquelle appartient le tronçon (1-36, puis N, NE… ; L, R, C…) |
| `CENTER_LINE`, `CENTER_LINE_LIGHTED` | présence d'une ligne axiale peinte, éclairée |
| `LEFT_EDGE`, `RIGHT_EDGE` (+ éclairage) | marquage des bords |
| `START`, `END` | rang du point de départ et d'arrivée (pour le type 3, `END` est le rang de la place) |
| `NAME_INDEX` | rang du nom de la voie (la doc ne précise pas la liste visée : sans doute `TAXI_NAME`, à confirmer) |

### Autres éléments utiles

- **TAXI_NAME** : `NAME` (32 caractères) ; leur nombre est donné par `AIRPORT.N_TAXI_NAMES`.
- **START** : positions de départ sur piste (position, cap, numéro, désignateur, type) → point d'alignement.
- **RUNWAY** : en plus de la géométrie, `PRIMARY/SECONDARY_ILS_ICAO` (ILS de chaque sens, donc autoland possible ou non), seuils décalés, prolongements, sens ouverts au décollage et à l'atterrissage.
- **TAXI_PARKING** : en plus des champs déjà lus, `SUFFIX`, `ORIENTATION`, `TAXI_POINT_TYPE` et les compagnies affectées (`AIRLINE`).

## Ce qu'il reste à ajouter au lecteur existant

1. Garder le `TYPE` des points (il était lu puis ignoré) et ajouter `ORIENTATION`.
2. Garder les rangs `START` / `END` (ils étaient convertis en coordonnées puis perdus).
3. Ne plus ignorer les tronçons de **piste** (type 2), indispensables pour l'entrée en piste et les traversées.
4. Ajouter `NAME_INDEX`, `RUNWAY_NUMBER`, `RUNWAY_DESIGNATOR`, `CENTER_LINE`, `WEIGHT` et les demi-largeurs.
5. Ajouter les listes **TAXI_NAME** et **START**, et les ILS des pistes.
6. Passer au nouveau format de fichier ; le mécanisme existant relit alors tous les plans.

L'essai `Experiments/D2-TaxiLayout` lit déjà tout cela pour un aérodrome et vérifie automatiquement les points incertains (voir [06](06-feasibility.md)).

## La chaîne de roulage visée

```
plan de l'aérodrome
  → graphe (nœuds = points de voie et places ; arêtes = tronçons de type 1 à 4)
  → itinéraire A* : place → point d'attente de la piste (ou selon la clairance ATC)
  → lissage des virages
  → suivi de trajectoire (train principal sur la ligne)
  → régulation de vitesse
  → arrêt au point d'attente, attente de l'autorisation
```

Et dans l'autre sens, après l'atterrissage : sortie de piste → place d'arrivée choisie selon la taille de l'avion (le projet précédent sait déjà choisir une place adaptée).
