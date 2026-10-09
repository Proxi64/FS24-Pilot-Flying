# 01 — Vision et besoin

*English: [../English/01-vision.md](../English/01-vision.md)*

## Le besoin

### Ce qui a disparu

MSFS 2020 proposait un **« AI Pilot »** (panneau d'assistance, raccourci Alt+C). Il pouvait prendre l'avion en charge de bout en bout : roulage, décollage, vol, atterrissage. Ses défauts étaient connus (roulage hésitant, approches approximatives), mais il existait.

**MSFS 2024 ne l'a plus.** Dans 2024, le « copilote IA » ne gère que les communications radio. Asobo n'a jamais expliqué ce retrait ni annoncé de retour (constat au 9 octobre 2026).

### Qui le réclame

Le sujet « AI pilot is missing from 2024 or ALT + C does nothing » du forum officiel est l'un des plus vifs depuis la sortie de 2024 (plus de 8 600 vues, toujours actif en avril 2026). On y trouve :

- **des joueurs en situation de handicap** (paralysie, paralysie cérébrale, douleurs chroniques) pour qui c'était la seule façon de voler. Au moins une compagnie virtuelle de pilotes handicapés et des diffuseurs en direct s'appuyaient dessus ;
- **des joueurs occasionnels**, qui ne savent pas mettre un avion en route ou veulent simplement « faire le vol » ;
- **des joueurs qui veulent être passagers**, regarder le paysage ou filmer ;
- des joueurs qui disent rester sur MSFS 2020 à cause de cette absence, et au moins un qui se dit prêt à payer jusqu'à 75 $ pour une solution fiable.

Une demande voisine revient aussi : la **checklist assistée ou automatique** de 2020, absente de 2024.

### Ce que la communauté a tenté

Un mod (« AI Pilot for MSFS 2024 », flightsim.to) réutilisait le panneau de 2020. Il marchait mal (montées intempestives en finale, crashs en relief) et **ne fonctionne plus depuis la mise à jour SU5** (commentaires de mai 2026).

## Ce qui existe déjà

Beaucoup d'outils entourent le pilote, **aucun ne pilote l'avion de bout en bout** :

| Catégorie | Exemples | L'outil pilote-t-il ? |
|---|---|---|
| Copilote d'équipage (annonces, checklists, flows) | FS2Crew, FS First Officer, FlightFabric, CrewMate A350, SimVoice Copilot, Checklist Reader | Non : le joueur reste pilote aux commandes |
| Contrôle aérien IA | BeyondATC, SayIntentions.AI | Non |
| Cockpit partagé entre deux humains | FS Copilot, YourControls | Non |
| Analyse d'atterrissage, carnet de vol | TouchdownFX, BeatMyLanding, Volanta | Non |
| Replay | Replay intégré à MSFS 2024, Sky Dolly, FlightControlReplay | Non |
| Pushback, services au sol | Toolbar Pushback, Ground Services EFB | Non |
| Missions | MissionGen, Liberty Fly | Non |

Le détail de ces outils, et ce qu'on peut en réutiliser, se trouve dans [05 — Écosystème](05-ecosystem.md).

## Positionnement

- **FS24 Pilot Flying pilote l'avion.** C'est la place vide.
- **Complémentaire, pas concurrent** des copilotes d'équipage. Un joueur peut laisser FS24 Pilot Flying voler pendant que FS2Crew fait les annonces, et laisser BeyondATC gérer la radio.
- **Ouvert** : un moteur générique, et des **profils d'avion** que la communauté peut écrire et partager, plutôt qu'un produit payant par avion.
- **PC uniquement.** Une application externe ne peut pas tourner sur Xbox ou PlayStation.

## Public visé, par ordre de priorité

1. Joueurs en situation de handicap.
2. Joueurs occasionnels et joueurs « passagers ».
3. Joueurs confirmés qui veulent déléguer une partie du vol (long courrier, vidéo, test de scène).

Conséquence : la **fiabilité** passe avant le réalisme. Une partie du public ne peut pas reprendre la main si quelque chose se passe mal.

## Périmètre envisagé (proposition, à valider avec les contributeurs)

| Étape | Contenu | Avions |
|---|---|---|
| V1 « vol assisté » | Avion prêt au parking, plan de vol chargé → roulage → alignement → décollage → vol managé → **atterrissage automatique ILS** → dégagement de piste | Fenix A320 (sait faire l'autoland), puis A320neo d'Asobo |
| V2 | Décollage et arrondi automatiques pour les avions **sans** autoland (aviation générale) | Avions Asobo de base (C172, TBM…) |
| V3 | Roulage jusqu'à la place d'arrivée, suivi des clairances de l'ATC | Tous |
| Plus tard | Mise en route depuis « cold & dark », programmation du FMS, checklists, profils supplémentaires | — |

L'ordre peut changer selon les résultats de l'étude de faisabilité. Le roulage, par exemple, est plus facile à tester que l'atterrissage (voir [06](06-feasibility.md)).

**Pourquoi le Fenix A320 en premier :** il sait déjà faire beaucoup de choses seul (autoland ILS avec arrondi et roulement, autopoussée, vol managé). Il donne donc vite un vol complet, ce qui permet de concentrer l'effort sur le roulage et le décollage. C'est aussi un avion très demandé, et il est disponible pour les tests.

**Pourquoi l'A320neo d'Asobo juste après :** même logique Airbus, commandes SimConnect standard, et c'est l'avion du public qui réclame le plus cette fonction.

## Hors périmètre

- Remplacer l'ATC (BeyondATC et SayIntentions le font) ou les copilotes d'équipage.
- Consoles (Xbox, PlayStation).
- Toute utilisation en dehors du simulateur.

## Sources

- Forum officiel, « AI pilot is missing from 2024 or ALT + C does nothing » : https://forums.flightsimulator.com/t/ai-pilot-is-missing-from-2024-or-alt-c-does-nothing/664986
- Forum officiel, « Where is A.I. Pilot in 2024? » : https://forums.flightsimulator.com/t/where-is-a-i-pilot-in-2024/672602
- Mod « AI Pilot for MSFS 2024 » : https://flightsim.to/file/85165/ai-pilot-for-msfs-2024
- Forum officiel, « Auto / assist checklist » : https://forums.flightsimulator.com/t/auto-assist-checklist-any-dev-here/743225
- Forum officiel, « Third-Party EFB Apps » : https://forums.flightsimulator.com/t/third-party-efb-apps/739415
