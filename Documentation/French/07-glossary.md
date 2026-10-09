# 07 — Glossaire

*English: [../English/07-glossary.md](../English/07-glossary.md)*

| Terme | Définition |
|---|---|
| **A\* / Dijkstra** | Algorithmes classiques de recherche du plus court chemin dans un graphe ; l'essai E1 utilise Dijkstra pour calculer l'itinéraire de roulage qui suit une clairance. |
| **AI Pilot** | Fonction de MSFS 2020 qui pilotait l'avion à la place du joueur ; absente de MSFS 2024. |
| **API Facilities** | Partie de SimConnect qui fournit les données des installations : aérodromes, pistes, voies, places, fréquences, procédures. |
| **Autoland** | Atterrissage automatique sur ILS, arrondi et roulement compris, géré par l'avion lui-même (ex. A320). |
| **Autopoussée (A/THR)** | Gestion automatique des gaz pour tenir une vitesse. |
| **CommBus** | Mécanisme de messages de MSFS entre modules WASM, panneaux HTML/JS et applications externes. |
| **Code calculateur (RPN)** | Petits programmes en notation polonaise inverse que le simulateur sait exécuter (`(L:VAR) 1 + (>L:VAR)`). |
| **Clairance (roulage)** | Instruction de l'ATC qui donne les voies à suivre et où s'arrêter (« roulez via C, NG, NW, attente piste 31 »). |
| **Empattement** | Distance entre la roulette de nez et le train principal ; détermine le décalage à appliquer en virage. |
| **Écart latéral** | Distance latérale entre l'avion et la ligne qu'il doit suivre ; la principale mesure des essais de roulage. |
| **FCU / MCP** | Panneau de commande du pilote automatique (Airbus / Boeing). |
| **FMA** | Bandeau des modes actifs du pilote automatique, en haut de l'écran principal de vol. |
| **FMS / MCDU** | Ordinateur de gestion du vol et son clavier-écran. |
| **GSX** | Add-on commercial de services au sol (FSDreamTeam) ; lit les mêmes plans d'aérodromes et place l'avion sur sa place. |
| **H-event** | Événement propre à un avion (« HTML event »), déclenché par nom. |
| **HubHop** | Base communautaire de MobiFlight qui recense, avion par avion, les variables et événements à utiliser. |
| **ILS** | Système d'atterrissage aux instruments (guidage en axe et en pente). |
| **Input event (variable B:)** | Commande d'un cockpit MSFS 2024 (interrupteur, bouton…) que SimConnect sait lister, lire, régler et surveiller. |
| **LVar** | Variable locale définie par un avion ou un module (`L:NOM`) ; SimConnect de MSFS 2024 la lit et l'écrit nativement (essai C1). |
| **Module WASM** | Programme C/C++ compilé en WebAssembly et chargé **dans** le simulateur, avec accès aux variables internes. |
| **Point d'attente (hold short)** | Point où l'avion doit s'arrêter avant d'entrer sur une piste. |
| **Profil d'avion** | Fichier FS24 Pilot Flying qui décrit un avion : caractéristiques et correspondance entre actions abstraites et commandes réelles. |
| **Pure pursuit / Stanley** | Algorithmes de suivi de trajectoire issus de la robotique mobile : viser un point du chemin quelques mètres devant / corriger l'écart de cap et l'écart latéral. Tous deux utilisés dans l'essai E1. |
| **Pushback (repoussage)** | Sortie de la place en marche arrière, poussé par un tracteur. |
| **SimConnect** | Interface officielle de communication entre MSFS et les programmes externes. |
| **SimVar** | Variable du simulateur lisible (et parfois modifiable) par SimConnect (`PLANE ALTITUDE`, `GROUND VELOCITY`…). |
| **Tiller** | Volant de direction de la roulette de nez, utilisé au sol sur les avions de ligne. |
| **Vr / Vref** | Vitesse de rotation au décollage / vitesse de référence en approche. |
| **WGS84** | Ellipsoïde de référence du GPS et du simulateur ; ses échelles servent à convertir latitude / longitude en mètres. |
