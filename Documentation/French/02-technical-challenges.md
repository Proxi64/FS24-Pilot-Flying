# 02 — Défis techniques

*English: [../English/02-technical-challenges.md](../English/02-technical-challenges.md)*

Un pilote virtuel doit remplacer **les mains, les yeux et le jugement** du pilote, sur des avions très différents, **depuis l'extérieur du simulateur**. Ce document décrit chaque difficulté et la piste envisagée pour la traiter.

## 1. Un vol, ce sont six problèmes

| Phase | Ce que l'avion sait déjà faire | Difficulté | Cas du Fenix A320 |
|---|---|---|---|
| Montée, croisière, descente | Pilote automatique (PA) | Faible | Vol managé par le FMS |
| Début de descente | Rien sur la plupart des avions légers | Moyenne | Calculé par le FMS |
| Approche | Mode APPR / ILS si l'avion l'a | Moyenne | APPR + deux PA |
| Atterrissage (arrondi, toucher) | Pas d'autoland sur l'immense majorité des avions | **Élevée** | **Autoland ILS** : arrondi et roulement faits par l'avion |
| Décollage | Rien : tenue d'axe, rotation | Moyenne à élevée | Pas de décollage automatique sur Airbus : à faire jusqu'à l'engagement du PA (vers 100 ft) |
| Roulage | Rien | Moyenne grâce aux plans d'aérodromes (voir [04](04-airports-and-taxiing.md)) | Commande de direction (tiller), freins |

Le décollage, l'arrondi (hors autoland) et le roulage demandent de vraies **boucles de régulation** qui agissent en continu sur les gouvernes, le palonnier, les gaz et les freins selon l'état de l'avion. Il ne suffit pas d'envoyer des ordres ponctuels.

## 2. Le roulage

Les données sont là : les plans d'aérodromes fournis par le simulateur décrivent les voies de circulation, les points d'attente, les places et les pistes (détail dans [04](04-airports-and-taxiing.md)). Reste à :

1. **Calculer l'itinéraire** dans le graphe des voies (A*), de la place au point d'attente de la piste, en excluant les voies réservées aux véhicules. Idéalement, suivre la **clairance de l'ATC** (« via A, B, attente piste 31 ») plutôt que le plus court chemin.
2. **Adoucir les virages** aux croisements. Les courbes sont déjà découpées en tronçons courts (médiane d'environ 12 m à Pau).
3. **Suivre la trajectoire** avec un algorithme classique de robotique mobile (*pure pursuit*, *Stanley*). Deux particularités aéronautiques :
   - **le sur-virage des gros porteurs** : il faut faire suivre la ligne au train principal, pas à la roulette de nez, sinon les roues principales coupent le virage. Le décalage dépend de l'empattement, une donnée du profil d'avion ;
   - **la commande de direction** varie selon l'avion : palonnier, tiller, freins différentiels ;
   - **l'avion ne roule pas droit tout seul** : commandes au neutre, le C172 est parti à gauche dès qu'il a roulé, à cause des effets de l'hélice (essai A1, 09/10/2026 : cap −4,5° et 1,6 m d'écart à la ligne de départ après 28 m). Suivre une ligne, c'est donc corriger en permanence l'écart latéral et l'écart de cap par rapport à l'axe de la voie ou de la piste, jamais tenir une position de commande fixe.
4. **Réguler la vitesse** : vitesse cible selon la courbure, freinage anticipé avant les virages. Sur un jet, le ralenti suffit souvent à accélérer, d'où un freinage par à-coups. Même chose sur le C172 : au ralenti, il roule déjà à 5,7 kt (essai A1).
5. **S'arrêter** aux points d'attente et derrière les autres avions (positions lues par SimConnect).

Le roulage est lent : une boucle externe à 20-30 Hz devrait suffire (à mesurer, question A3 de [06](06-feasibility.md)).

## 3. Le décollage et l'arrondi

- **Décollage** : tenir l'axe de piste au palonnier pendant la course, puis faire la rotation à la bonne vitesse et à la bonne assiette, et monter jusqu'à l'altitude d'engagement du PA.
- **Arrondi** (avions sans autoland) : réduire la vitesse verticale juste avant le toucher, réduire les gaz, garder l'axe, puis freiner.

Un précédent encourageant : le tutoriel « Flying planes with JavaScript » de Pomax réalise un **décollage et un atterrissage automatiques** sur huit avions légers, depuis un programme externe relié par SimConnect (MSFS 2020). Voir [05](05-ecosystem.md).

## 4. La diversité des avions

- Les **avions d'Asobo** répondent aux événements SimConnect standard (`AP_ALT_VAR_SET`, `FLAPS_INCR`, `GEAR_UP`…).
- Les **avions tiers complexes** (Fenix, PMDG, iniBuilds…) utilisent souvent leurs propres variables internes (**LVars**) et événements (**H-events**), différents d'un produit à l'autre.
- Les vitesses de référence (Vr, Vref), les crans de volets et la logique du PA (G1000, FCU Airbus, MCP Boeing) sont propres à chaque avion.

Piste retenue : un **profil par avion**, qui associe des actions abstraites (« sortir le train », « afficher l'altitude 5000 ») aux commandes réelles de l'avion, et donne ses caractéristiques (vitesses, empattement…). Les commandes viendraient en grande partie de la base communautaire **HubHop** (voir [05](05-ecosystem.md)).

## 5. Piloter depuis l'extérieur du simulateur

L'AI Pilot de 2020 tournait **dans** le moteur du simulateur. Une application externe passe par SimConnect, ce qui pose trois questions :

- **latence et irrégularité** des échanges, gênantes pour une boucle d'arrondi ;
- **conflit avec les commandes du joueur** (joystick, manette des gaz) qui envoient elles aussi des valeurs ;
- **accès aux variables internes** des avions tiers, que SimConnect seul ne voit pas.

Pistes :
- pour les variables internes : un **module WASM** chargé dans le simulateur (celui de MobiFlight existe, licence MIT) ;
- pour la latence, si les essais montrent que c'est nécessaire : notre **propre module WASM**, appelé à chaque image du simulateur, pour les boucles rapides, l'application .NET gardant l'orchestration (voir [03](03-proposed-architecture.md)).

## 6. Le jugement et la fiabilité

Vent de travers, relief, approche instable (remise de gaz), carburant, trafic au sol : un pilote virtuel doit savoir **renoncer et se mettre en sécurité**, pas seulement exécuter. Comme une partie du public ne peut pas reprendre la main, l'exigence de fiabilité est plus élevée que pour un outil de confort. Le projet devra définir des **limites d'emploi claires** (conditions, avions et terrains pris en charge) et des **sorties de secours** (remise de gaz, mise en attente, arrêt sur la piste).
