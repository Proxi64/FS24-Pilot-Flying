# Journal des décisions

*English: [../English/decision-log.md](../English/decision-log.md)*

## Décisions prises

| Date | Décision | Raison |
|---|---|---|
| 09/10/2026 | Projet retenu : un **pilote virtuel** pour MSFS 2024 (l'application pilote l'avion) | Demande la plus forte de la communauté, sans solution existante (voir [01](01-vision.md)) |
| 09/10/2026 | **Étude de faisabilité avant tout code applicatif** | On établit d'abord ce qui est possible |
| 09/10/2026 | Code commun avec le projet précédent (plans d'aérodromes, SimConnect) : **extraire une bibliothèque commune** plutôt que dépendre de l'ancien projet | Les deux projets en ont besoin |
| 09/10/2026 | Roulage : s'appuyer sur les **plans d'aérodromes de l'API Facilities** | Déjà lus pour ~85 000 terrains, scènes payware comprises |
| 09/10/2026 | Essais de faisabilité = **consoles jetables** dans `Experiments/<question>/` | Séparer l'exploration du futur code |
| 09/10/2026 | Documentation **en français et en anglais**, même structure et mêmes noms de fichiers, toujours tenue à jour en même temps | Ouvrir le projet à d'autres développeurs de la communauté |
| 09/10/2026 | **Licence MIT** pour le projet | Simple et attirante pour les contributeurs ; un passage ultérieur vers une licence plus protectrice (GPL) reste possible, l'inverse serait très difficile. Conséquence : pas de reprise de code GPL ou LGPL, seulement des idées |
| 09/10/2026 | Nom du projet : **FS24 Pilot Flying** (anciennement « Virtual Pilot ») | « Pilot Flying » = le pilote aux commandes, ce que fait le logiciel ; « FS24 » situe le simulateur. Nom à revoir si le projet suit une version suivante de MSFS |
| 09/10/2026 | **Tout le projet en anglais** : code, identifiants, textes, noms de dossiers et de fichiers. Seule exception : la documentation existe aussi en français complet, dans `Documentation/French/`, avec les mêmes noms de fichiers que `Documentation/English/` | Projet ouvert à une communauté internationale |
| 09/10/2026 | Hébergement sur **GitHub** : https://github.com/Proxi64/FS24-Pilot-Flying | Plateforme habituelle de la communauté MSFS |
| 09/10/2026 | Wassette écarté | Sans rapport avec les modules WASM de MSFS |
| 09/10/2026 | Pendant l'étude de faisabilité, **commit sur `main` à chaque étape utile**, sans demander ; le mainteneur fait les push | Phase de collecte d'informations et de documentation |
| 10/10/2026 | **Pas d'essais sur les add-ons pour l'instant** (avions tiers comme le Fenix A320) : l'étude de faisabilité continue avec les avions par défaut de MSFS 2024 | Bien trop prématuré : il faut d'abord établir les bases (roulage, décollage, atterrissage) sur les avions du simulateur. Les résultats sur add-ons du 9 octobre 2026 sont conservés |

Les dates sont écrites jour/mois/année.

## Propositions en attente de validation

- Moteur générique + profils d'avion ouverts ; premiers profils sur les **avions par défaut de MSFS 2024** (A320neo d'Asobo pour les phases d'avion de ligne, C172 pour les essais et l'aviation générale) ; profils d'add-ons (Fenix A320…) plus tard. *(Mis à jour le 10 octobre 2026 après la décision sur les add-ons ; auparavant : premier profil Fenix A320, deuxième A320neo d'Asobo.)*
- Accès aux variables des avions tiers : **SimConnect natif** (LVars et input events, suffisant pour le Fenix dans les essais C1 et B1) ; le **module WASM MobiFlight** seulement pour ce que SimConnect natif ne sait pas faire (H-events, code calculateur) ; HubHop comme référence pour les noms de variables (aucune licence indiquée) ; FSUIPC seulement en option. *(Mis à jour le 9 octobre 2026 après les essais C1 et B1 ; auparavant : module WASM MobiFlight + HubHop. Reporté avec les essais sur add-ons, décision du 10 octobre 2026.)*
- Pile : .NET 10, SimConnect natif en P/Invoke, interface WinUI 3 ; module WASM maison seulement si les mesures l'exigent.

## Questions à décider avec les contributeurs

- **Licence des profils d'avion et des données** (par exemple CC BY 4.0), distincte de celle du code.
- Accord de contribution (CLA) ou simple engagement à publier sous MIT ?
- Organisation (tickets, revue de code).
- Gratuit, ou modèle permettant de financer le travail ?
