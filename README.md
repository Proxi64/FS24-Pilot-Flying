# FS24 Pilot Flying

**A virtual pilot for Microsoft Flight Simulator 2024: an open, community-driven application that flies the aircraft for you, from gate to gate.**

*[Version française plus bas](#version-française)*

> **Status: feasibility study.** There is no usable application yet. This repository currently holds the research, the documentation and the first technical tests. Contributors are welcome.

---

## Why this project exists

Microsoft Flight Simulator 2020 had an **"AI Pilot"**: one keystroke, and the simulator took over the aircraft. It taxied, took off, flew the route and landed. It was far from perfect, but it worked.

**Microsoft Flight Simulator 2024 does not have it.** The "AI copilot" in 2024 only handles the radio. The feature has not come back, Asobo has not said why, and nothing in the community replaces it. The only mod that tried stopped working with Sim Update 5.

### People are waiting for it, starting with disabled simmers

Since the release of MSFS 2024, the official forum has been full of requests to bring the AI Pilot back. The strongest voices come from **simmers with disabilities**:

- pilots living with **paralysis, cerebral palsy or chronic pain**, for whom the AI Pilot was **the only way to fly at all**;
- a **virtual airline run by and for disabled pilots**, whose activity depended on it;
- **live streamers** who fly for their audience and cannot hold a yoke for hours;
- many who say they **stay on MSFS 2020**, or have stopped buying add-ons, because this one feature is missing.

They are joined by casual players who just want to enjoy the flight, people who want to sit back as a passenger and watch the world go by, and experienced simmers who would like to hand over part of a long flight.

Today, these people have nothing. Plenty of excellent tools surround the pilot (crew copilots such as FS2Crew, AI air traffic control such as BeyondATC, shared cockpits, landing analysers), but **none of them actually flies the aircraft**.

**FS24 Pilot Flying wants to give them their wings back.**

## What FS24 Pilot Flying aims to be

- An **external Windows application**, connected to MSFS 2024 through SimConnect, that **taxis, takes off, flies and lands** the aircraft.
- **Reliability first**: some of its users cannot take over if something goes wrong. The aircraft must stay safe: go-around, holding, stopping, within clearly stated limits.
- **Open aircraft profiles**: a generic engine plus small description files, one per aircraft, that the community can write and share, instead of one paid product per aircraft.
- **Complementary**, not competing: FS24 Pilot Flying flies, while your favourite crew copilot makes the callouts and your favourite ATC handles the radio.
- **PC only**: an external application cannot run on Xbox or PlayStation.

### Planned steps (subject to the feasibility study)

| Step | Content |
|---|---|
| V1 | From the gate to the runway exit on an airliner that can autoland (Fenix A320 first, then Asobo's A320neo): taxi, take-off, managed flight, automatic ILS landing |
| V2 | Automatic take-off and landing for general aviation aircraft without autoland |
| V3 | Taxi to the arrival gate, following ATC clearances |
| Later | Cold & dark start-up, flight plan programming, checklists, more aircraft |

## What is already in hand

- **Complete airport layouts read from the simulator**: taxiways, hold-short points, parking spots, runways, for about 85,000 airports, payware sceneries included, through the SimConnect Facilities API.
- **Native SimConnect access from .NET**, proven on MSFS 2024.
- A detailed review of the **MSFS 2024 SDK** and of the existing tools (MobiFlight WASM module and HubHop, FSUIPC, FS Copilot, node-simconnect…), with their licences.
- A first test console that reads and checks a complete taxi layout.

## Documentation

The full documentation exists in both languages:

- 🇬🇧 [Documentation/English](Documentation/English/README.md)
- 🇫🇷 [Documentation/French](Documentation/French/README.md)

Good places to start: the **vision**, the **technical challenges**, and the **feasibility** list of questions that must be answered before writing the application.

## How you can help

We are looking for people comfortable with at least one of these:

- **SimConnect / MSFS 2024 SDK** (C# or C++), Facilities API, WASM modules;
- **control laws**: ground path following, take-off, flare (PID or other);
- **third-party aircraft**: LVars and events of the Fenix, PMDG, iniBuilds… to write aircraft profiles;
- **WinUI 3 / .NET** for the application;
- **testing** on many aircraft and airports;
- and, just as important, **simmers with disabilities** who are willing to tell us what they need and to test.

Everything in the repository is in English (code, identifiers, file and folder names); the documentation also exists in full in French.

Open an issue to introduce yourself, ask a question or share an idea. The way we organise ourselves will be decided with the first contributors.

## Repository layout

```
Documentation/
  English/      full documentation in English
  French/       the same in French
Experiments/    throw-away feasibility test consoles
```

## Licence

FS24 Pilot Flying is released under the **MIT licence** (see [LICENSE](LICENSE)). By contributing, you agree that your contribution is published under the same licence.

## Disclaimer

FS24 Pilot Flying is an independent community project. It is not affiliated with, endorsed by or supported by Microsoft, Asobo Studio or any aircraft developer. It is meant for flight simulation only.

---

## Version française

**Un pilote virtuel pour Microsoft Flight Simulator 2024 : une application ouverte, portée par la communauté, qui pilote l'avion pour vous, du parking au parking.**

> **État : étude de faisabilité.** Il n'existe pas encore d'application utilisable. Ce dépôt contient pour l'instant les recherches, la documentation et les premiers essais techniques. Les contributeurs sont les bienvenus.

### Pourquoi ce projet

Microsoft Flight Simulator 2020 avait un **« AI Pilot »** : une touche, et le simulateur prenait l'avion en charge. Il roulait, décollait, suivait la route et atterrissait. C'était loin d'être parfait, mais ça marchait.

**Microsoft Flight Simulator 2024 ne l'a pas.** Le « copilote IA » de 2024 ne gère que la radio. La fonction n'est pas revenue, Asobo n'a pas dit pourquoi, et rien dans la communauté ne la remplace. Le seul mod qui avait essayé ne fonctionne plus depuis la mise à jour SU5.

#### Une attente forte, d'abord chez les joueurs en situation de handicap

Depuis la sortie de MSFS 2024, le forum officiel regorge de demandes de retour de l'AI Pilot. Les voix les plus fortes viennent de **joueurs en situation de handicap** :

- des pilotes atteints de **paralysie, de paralysie cérébrale ou de douleurs chroniques**, pour qui l'AI Pilot était **la seule façon de voler** ;
- une **compagnie virtuelle créée par et pour des pilotes handicapés**, dont l'activité en dépendait ;
- des **diffuseurs en direct** qui volent pour leur public et ne peuvent pas tenir un manche pendant des heures ;
- beaucoup qui disent **rester sur MSFS 2020**, ou ne plus acheter d'add-ons, à cause de cette seule absence.

S'y ajoutent les joueurs occasionnels qui veulent simplement profiter du vol, ceux qui veulent se laisser porter comme passagers et regarder le paysage, et les joueurs confirmés qui aimeraient déléguer une partie d'un long vol.

Aujourd'hui, ils n'ont rien. D'excellents outils entourent le pilote (copilotes d'équipage comme FS2Crew, contrôle aérien IA comme BeyondATC, cockpits partagés, analyseurs d'atterrissage), mais **aucun ne pilote réellement l'avion**.

**FS24 Pilot Flying veut leur rendre leurs ailes.**

### Ce que FS24 Pilot Flying veut être

- Une **application Windows externe**, reliée à MSFS 2024 par SimConnect, qui **roule, décolle, vole et atterrit**.
- **La fiabilité d'abord** : une partie des utilisateurs ne peut pas reprendre la main en cas de problème. L'avion doit rester en sécurité (remise de gaz, attente, arrêt), dans des limites clairement annoncées.
- **Des profils d'avion ouverts** : un moteur générique et de petits fichiers de description, un par avion, que la communauté peut écrire et partager, plutôt qu'un produit payant par avion.
- **Complémentaire**, pas concurrent : FS24 Pilot Flying pilote, votre copilote d'équipage préféré fait les annonces et votre ATC préféré gère la radio.
- **PC uniquement** : une application externe ne peut pas tourner sur Xbox ou PlayStation.

#### Étapes envisagées (selon les résultats de l'étude de faisabilité)

| Étape | Contenu |
|---|---|
| V1 | Du parking à la sortie de piste sur un avion de ligne capable d'autoland (Fenix A320 d'abord, puis A320neo d'Asobo) : roulage, décollage, vol managé, atterrissage automatique ILS |
| V2 | Décollage et atterrissage automatiques pour les avions légers sans autoland |
| V3 | Roulage jusqu'à la place d'arrivée, en suivant les clairances de l'ATC |
| Plus tard | Mise en route depuis « cold & dark », programmation du plan de vol, checklists, autres avions |

### Ce qui est déjà acquis

- **Les plans complets des aérodromes lus dans le simulateur** (voies de circulation, points d'attente, places, pistes), pour environ 85 000 terrains, scènes payware comprises, grâce à l'API Facilities de SimConnect.
- **L'accès SimConnect natif depuis .NET**, éprouvé sous MSFS 2024.
- Une étude détaillée du **SDK MSFS 2024** et des outils existants (module WASM MobiFlight et HubHop, FSUIPC, FS Copilot, node-simconnect…), avec leurs licences.
- Une première console d'essai qui lit et vérifie un plan de roulage complet.

### Documentation

La documentation complète existe dans les deux langues :

- 🇫🇷 [Documentation/French](Documentation/French/README.md)
- 🇬🇧 [Documentation/English](Documentation/English/README.md)

Pour commencer : la **vision**, les **défis techniques** et la liste de **faisabilité**, c'est-à-dire les questions à trancher avant d'écrire l'application.

### Comment aider

Nous cherchons des personnes à l'aise avec au moins un de ces sujets :

- **SimConnect / SDK MSFS 2024** (C# ou C++), API Facilities, modules WASM ;
- **lois de pilotage** : suivi de trajectoire au sol, décollage, arrondi (PID ou autre) ;
- **avions tiers** : LVars et événements du Fenix, des PMDG, iniBuilds… pour écrire les profils d'avion ;
- **WinUI 3 / .NET** pour l'application ;
- **tests** sur de nombreux avions et terrains ;
- et, tout aussi important, des **joueurs en situation de handicap** prêts à nous dire ce dont ils ont besoin et à tester.

Tout le dépôt est en anglais (code, identifiants, noms de fichiers et de dossiers) ; la documentation existe aussi intégralement en français.

Ouvrez une *issue* pour vous présenter, poser une question ou proposer une idée. Notre organisation sera décidée avec les premiers contributeurs.

### Organisation du dépôt

```
Documentation/
  English/      documentation complète en anglais
  French/       la même en français
Experiments/    consoles d'essai jetables de l'étude de faisabilité
```

### Licence

FS24 Pilot Flying est publié sous **licence MIT** (voir [LICENSE](LICENSE)). En contribuant, vous acceptez que votre contribution soit publiée sous la même licence.

### Avertissement

FS24 Pilot Flying est un projet communautaire indépendant. Il n'est ni affilié à Microsoft, Asobo Studio ou un quelconque développeur d'avions, ni approuvé ou soutenu par eux. Il est destiné uniquement à la simulation de vol.
