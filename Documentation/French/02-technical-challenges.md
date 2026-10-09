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

1. **Calculer l'itinéraire** dans le graphe des voies, de la place au point d'attente de la piste, en excluant les voies réservées aux véhicules, et **en suivant la clairance de l'ATC** (« via C, NG, NW, attente piste 31 ») plutôt que le plus court chemin. Fait dans l'essai E1 : plus court chemin (Dijkstra) sur (nœud, position dans la clairance) ; tronçons de liaison sans nom permis mais pénalisés ; jamais de piste ; jamais d'entrée dans la voie qui mène à la piste.
2. **Sortir de la place dans le bon sens** : une place peut avoir une sortie derrière l'avion (pushback) et une devant, et l'avion n'est pas toujours sur le point de la place (à Pau, GSX a placé un C172 14,6 m devant, là où serait la roue avant d'un avion plus grand). Les courbes sont déjà découpées en tronçons courts (médiane d'environ 12 m à Pau).
3. **Suivre la trajectoire** avec un algorithme classique de robotique mobile. Dans l'essai E1, une loi de type *Stanley* a tenu une ligne droite à 1 cm RMS près, et la poursuite (*pure pursuit* : viser le point de l'itinéraire quelques mètres devant) a fait passer le C172 dans tous les virages d'un itinéraire de 1,2 km à 0,76 m près. Particularités aéronautiques :
   - **le sur-virage des gros porteurs** : il faut faire suivre la ligne au train principal, pas à la roulette de nez, sinon les roues principales coupent le virage. Le décalage dépend de l'empattement, une donnée du profil d'avion (à essayer avec l'A320) ;
   - **la commande de direction** varie selon l'avion : palonnier, tiller, freins différentiels (C172 : palonnier et tiller braquent tous deux la roulette de nez, seulement en roulant ; Fenix : LVar du tiller ou `AXIS_STEERING_SET`) ;
   - **l'avion ne roule pas droit tout seul** : commandes au neutre, le C172 est parti à gauche dès qu'il a roulé, à cause des effets de l'hélice (essai A1, 09/10/2026 : cap −4,5° et 1,6 m d'écart à la ligne de départ après 28 m). Suivre une ligne, c'est donc corriger en permanence l'écart latéral et l'écart de cap par rapport à l'axe de la voie ou de la piste (un terme intégral tient la dérive), jamais tenir une position de commande fixe ;
   - **les positions doivent être converties précisément** : latitude / longitude en mètres avec les échelles WGS84 ; une Terre sphérique mettait l'avion à 1,5 m de la ligne à 1 km du point de référence de l'aérodrome (essais D4, E1).
4. **Réguler la vitesse** : vitesse cible selon la courbure, freinage anticipé avant les virages. Sur un jet, le ralenti suffit souvent à accélérer, d'où un freinage par à-coups. Même chose sur le C172 : au ralenti, il roule déjà à 5,7 kt (essai A1) ; l'essai E1 a tenu 5 kt avec les gaz et les freins, 3 kt en virage.
5. **S'arrêter** aux points d'attente (essai E1 : 4,8 m avant, jamais au-delà) et derrière les autres avions (positions lues par SimConnect, question D5).

Le roulage est lent : la boucle externe de l'essai E1 (commandes à 30 Hz, positions lues à chaque image, environ 40 Hz) a suffi ; pas besoin de module WASM pour le roulage.

## 3. Le décollage et l'arrondi

- **Décollage** : tenir l'axe de piste au palonnier pendant la course, puis faire la rotation à la bonne vitesse et à la bonne assiette, et monter jusqu'à l'altitude d'engagement du PA.
- **Arrondi** (avions sans autoland) : réduire la vitesse verticale juste avant le toucher, réduire les gaz, garder l'axe, puis freiner.

Un précédent encourageant : le tutoriel « Flying planes with JavaScript » de Pomax réalise un **décollage et un atterrissage automatiques** sur huit avions légers, depuis un programme externe relié par SimConnect (MSFS 2020). Voir [05](05-ecosystem.md).

## 4. La diversité des avions

- Les **avions d'Asobo** répondent aux événements SimConnect standard (`AP_ALT_VAR_SET`, `FLAPS_INCR`, `GEAR_UP`…).
- Les **avions tiers complexes** (Fenix, PMDG, iniBuilds…) utilisent souvent leurs propres variables internes (**LVars**) et événements (**H-events**), différents d'un produit à l'autre.
- Les vitesses de référence (Vr, Vref), les crans de volets et la logique du PA (G1000, FCU Airbus, MCP Boeing) sont propres à chaque avion.

Piste retenue : un **profil par avion**, qui associe des actions abstraites (« sortir le train », « afficher l'altitude 5000 ») aux commandes réelles de l'avion, et donne ses caractéristiques (vitesses, empattement…). Les commandes viendraient en grande partie de la base communautaire **HubHop** (voir [05](05-ecosystem.md)). Les essais C1 et B1 ont montré que les interrupteurs, boutons et voyants du Fenix sont de simples LVars que SimConnect lit et écrit nativement, et que les cockpits de MSFS 2024 exposent aussi des **input events** ; HubHop est une bonne source de noms, mais n'indique aucune licence (référence seulement).

## 5. Piloter depuis l'extérieur du simulateur

L'AI Pilot de 2020 tournait **dans** le moteur du simulateur. Une application externe passe par SimConnect, ce qui pose trois questions :

- **latence et irrégularité** des échanges, gênantes pour une boucle d'arrondi ;
- **conflit avec les commandes du joueur** (joystick, manette des gaz) qui envoient elles aussi des valeurs ;
- **accès aux variables internes** des avions tiers, que SimConnect seul ne voit pas.

Ce que les essais ont montré (octobre 2026, voir [06](06-feasibility.md)) :
- **latence** : SimConnect donne l'état de l'avion à chaque image (environ 40 Hz ici, régulier) et les commandes agissent à l'image suivante ; suffisant pour le roulage (essai E1). Décollage et arrondi restent à mesurer ;
- **conflit avec le joystick** : un joystick immobile n'interfère jamais ; bougé, il prend le dessus entre deux envois, ce qui donne aussi un moyen de détecter que le joueur reprend la main (essai A1) ;
- **variables internes** : SimConnect 2024 lit et écrit lui-même les LVars et pilote les input events du cockpit ; le module WASM MobiFlight fonctionne aussi (licence MIT), mais n'a pas été nécessaire pour le Fenix (essais C1, B1).

Notre **propre module WASM**, appelé à chaque image du simulateur, reste une option pour les boucles rapides si le décollage ou l'arrondi l'exigent, l'application .NET gardant l'orchestration (voir [03](03-proposed-architecture.md)).

## 6. Le jugement et la fiabilité

Vent de travers, relief, approche instable (remise de gaz), carburant, trafic au sol : un pilote virtuel doit savoir **renoncer et se mettre en sécurité**, pas seulement exécuter. Comme une partie du public ne peut pas reprendre la main, l'exigence de fiabilité est plus élevée que pour un outil de confort. Le projet devra définir des **limites d'emploi claires** (conditions, avions et terrains pris en charge) et des **sorties de secours** (remise de gaz, mise en attente, arrêt sur la piste).
