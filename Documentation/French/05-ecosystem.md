# 05 — Écosystème : outils et projets étudiés

*English: [../English/05-ecosystem.md](../English/05-ecosystem.md)*

Ce qu'on a regardé (octobre 2026), ce qu'on peut en tirer et à quelles conditions. Les licences sont celles constatées ; « à vérifier » signifie qu'on ne l'a pas encore lue.

## Accès aux variables des avions tiers

Ces outils règlent le **câblage**, c'est-à-dire commander les systèmes d'un avion tiers. Ils ne disent rien du **pilotage** : quand agir, boucles de régulation, roulage, jugement.

### Module WASM MobiFlight + base HubHop — candidat principal

- **Licence MIT** (module).
- Module WASM autonome, placé dans le dossier Community, qui exécute des événements et du code calculateur **dans le contexte des jauges de l'avion** : accès aux LVars et H-events que SimConnect seul ne voit pas.
- **Plusieurs clients possibles** : un programme externe s'enregistre (`MF.Clients.Add.<Nom>`, réponse `.Finished`) et reçoit ses propres canaux de mémoire partagée (`<Nom>.LVars`, `<Nom>.Command`, `<Nom>.Response`). Il peut donc coexister avec le MobiFlight Connector du joueur.
- Commandes : `MF.SimVars.Add.(code)` déclare une variable à lire (4 octets par valeur, dans l'ordre d'ajout ; identifiants conseillés à partir de 1000), `MF.SimVars.AddString` (128 octets, 64 chaînes au plus), `MF.SimVars.Set.(code)` pour écrire ou exécuter, `MF.LVars.List`, `MF.Config.MAX_VARS_PER_FRAME.Set`.
- Les valeurs sont transmises **quand elles changent**, sans fréquence fixe annoncée : à mesurer pour une boucle de pilotage.
- Pièges : la première commande après le démarrage peut être ignorée (en envoyer une factice) ; liste des LVars plafonnée à 1000 noms.
- **MSFS 2024** : le README parle de 2020, mais la doc MobiFlight explique comment réactiver le module sous 2024 (le simulateur le désactive parfois). À confirmer par essai.
- **HubHop** : base communautaire de commandes prêtes à l'emploi, avion par avion (« sortir le train du Fenix »…). Idéal pour écrire les profils d'avion. Licence des données : à vérifier.
- https://github.com/MobiFlight/MobiFlight-WASM-Module · https://docs.mobiflight.com/guides/wasm-module/enable-in-msfs2024/ · https://hubhop.mobiflight.com/

### FSUIPC7 — option si le joueur l'a déjà

- Gère MSFS 2024 ; module WASM (LVars, H-vars, presets, code calculateur) et interface programme (WAPI).
- Son WASM a connu des plantages sous 2024, nettement moins probables depuis la version 7.5.6 (janvier 2026) selon son auteur.
- **Produit commercial** (version enregistrée payante) : ne pas l'imposer.
- Le copilote FS First Officer s'appuie dessus, ce qui montre que l'approche « application externe + module WASM » est éprouvée.

### WASimCommander — alternative

- Module WASM + client, du même auteur que MSFS-Tools ; accès à la Gauge API, aux LVars et au code calculateur.
- Support de MSFS 2024 et licence : à vérifier.
- https://flightsim.to/addon/36474/wasimcommander

## Projets qui pilotent ou qui l'ont tenté

### « Flying planes with JavaScript » (Pomax) — référence d'algorithmes

- Pilote automatique **externe** par SimConnect : ailes à plat, altitude, cap, autopoussée, points de route, suivi du relief, **décollage automatique** (course, rotation) et **atterrissage automatique** (approche, plan de descente, toucher, freinage).
- Testé sur huit avions légers (Beaver, C310R, Arrow III, Beech 18, Kodiak 100…). MSFS 2020. Pas de roulage.
- Preuve qu'on peut décoller et atterrir depuis l'extérieur du simulateur ; base d'idées pour nos contrôleurs.
- **Licence du dépôt à vérifier** : en attendant, on s'inspire des idées sans reprendre de code.
- https://pomax.github.io/are-we-flying/

### Mod « AI Pilot for MSFS 2024 »

Réutilisait le panneau de 2020. Médiocre, puis cassé par la SU5 (mai 2026). Confirme le besoin et l'absence de solution.

## Copilotes d'équipage

| Outil | Ce qu'il fait | Prix | Pour nous |
|---|---|---|---|
| FS2Crew | Copilote « pilote surveillant » : checklists, flows, annonces, volets et train sur ordre ; versions par avion (Fenix, PMDG…) | Fenix : 29,99 $ + 14,99 € (copilote animé) | Complémentaire ; montre que le Fenix est très demandé |
| FS First Officer | Copilote par commandes vocales ou automatique ; édition générique pour tous les liners | Payant | S'appuie sur FSUIPC7 |
| FlightFabric | Commandes vocales directes pour les liners populaires | Gratuit, open source (alpha) | Liste d'avions et méthode de commande à regarder |
| CrewMate A350 | Copilote vocal pour l'A350 d'iniBuilds | Gratuit, open source | Idem |

Aucun ne roule, ne décolle ni n'atterrit : le joueur reste aux commandes.

## Cockpit partagé

### FS Copilot

- Synchronise position, gouvernes et interrupteurs entre **deux pilotes humains** (alternative à YourControls). Gratuit, open source. **Ne pilote pas.**
- Intérêt : ses **définitions d'avions en YAML** (variables et événements de chaque interrupteur), proches de nos futurs profils, et son code d'interface avec MSFS 2024. Licence à vérifier avant toute reprise.
- https://fscopilot.com/ · https://github.com/yury-sch/FsCopilot

## Bibliothèques SimConnect

| Projet | Langage / licence | Intérêt |
|---|---|---|
| node-simconnect (EvenAR) | TypeScript, LGPL-3.0 | Réimplémente le **protocole SimConnect sans DLL** (TCP, tubes nommés), gère MSFS 2024. Référence si une structure 2024 pose problème. https://github.com/EvenAR/node-simconnect |
| msfs-simconnect-api-wrapper (Pomax) | JavaScript | Surcouche de node-simconnect ; base d'aérodromes, pistes, ILS ; rien sur les voies de circulation. https://github.com/Pomax/msfs-simconnect-api-wrapper |
| MSFS-Tools (mpaperno) | C++, surtout GPL v3 | **DocImport** : base structurée de tous les événements, SimVars et unités tirée de la doc SDK (utile pour valider les profils). **SimConnect-Request-Tracker** : relie une erreur SimConnect à l'appel fautif. Code GPL, incompatible avec la licence MIT du projet : on s'inspire des idées, on ne reprend pas le code. https://github.com/mpaperno/MSFS-Tools |
| simconnect-sdk-rs | Rust, MIT | Archivé en février 2026. Écarté. https://github.com/mihai-dinculescu/simconnect-sdk-rs |

## Avion de référence : Fenix A320

- Autoland ILS (arrondi + roulement), autopoussée, vol managé : le meilleur candidat pour une première version complète.
- Composants installés : FenixSystem, FenixDisplay, **Fenix.GqlGateway**. Ce dernier laisse penser à une interface de données (GraphQL ?) à explorer. Il faudra aussi **vérifier les conditions d'utilisation de Fenix** pour un outil tiers.
- Piège signalé (forum FSUIPC, février 2025) : des LVars Fenix restées à 0 sous 2024, à cause d'un plantage du module WASM FSUIPC.

## Outils de développement

- **Serveur MCP « MSFS SDK »** (communautaire, open source) : permet à un assistant IA de chercher dans la doc du SDK. Il lit le site officiel en direct. Sa recherche est approximative et la version visée (2020 ou 2024) n'est pas précisée : on vérifie toujours dans la doc 2024 officielle. https://github.com/90barricade93/MSFS-SDK-MCP
- **Wassette** (Microsoft) : moteur de composants WebAssembly pour donner des outils aux agents IA. Sans rapport avec les modules WASM de MSFS (autre format, ne tourne pas dans le simulateur). Écarté.

## Le projet précédent de l'initiateur

Générateur de missions pour MSFS 2024 (C# .NET 10, non publié). Réutilisable :

- P/Invoke sur SimConnect natif, structures 2024 éprouvées ;
- lecteur de plans d'aérodromes par l'API Facilities, copie locale de ~85 000 plans, calculs géographiques ;
- détection des avions installés (`SimConnect_EnumerateSimObjectsAndLiveries`) et regroupement par famille ;
- pièges déjà rencontrés :
  - `SimConnect_FlightPlanLoad` est sans effet sous 2024 ;
  - un plan de vol écrit à la main dans un `.FLT` a fait planter MSFS ;
  - `PLANE TOUCHDOWN *` donne le toucher exact même lu à 1 Hz ;
  - états de caméra : 2 à 8, 24, 26 et 29 = aux commandes ; 34 et 35 = menus ou chargement ;
  - les fonctions météo de SimConnect sont dépréciées en 2024.

Décision : extraire ce code dans une **bibliothèque commune** plutôt que de dépendre de l'ancien projet.
