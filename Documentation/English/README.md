# FS24 Pilot Flying

**A virtual pilot for Microsoft Flight Simulator 2024: an application that flies the aircraft for the user, from gate to gate.**

*Version française : [../French/README.md](../French/README.md)*

---

## In short

MSFS 2020 had an "AI Pilot" that could take the aircraft from the departure gate to the arrival gate. **MSFS 2024 no longer has it**, and no tool replaces it. Yet part of the community really needs it, especially users with disabilities, for whom it was the only way to fly.

FS24 Pilot Flying aims to fill that gap: an external Windows application, connected to the simulator through SimConnect, that **taxis, takes off, flies and lands** for the user, on aircraft described by **open profiles** that the community can write.

## Project status

| | |
|---|---|
| Phase | **Feasibility study.** No application code yet: we first establish what is possible. |
| Current objective | Fly autonomously a Cirrus SF50 Vision Jet from LFBO (Toulouse-Blagnac), stand F10, engine off, to LFBZ (Biarritz), stand 5, engine shut down (decision of 10 October 2026) |
| Already in hand | Complete airport layouts read from the simulator (≈ 85,000 airports, payware sceneries included), native SimConnect access from .NET, SDK 2024 documentation analysed for taxiing |
| Tests run (9 October 2026) | Six throw-away consoles run in MSFS 2024 (`Experiments/`): taxi layouts checked against the painted lines; the C172 commanded from outside (axes, brakes, nose wheel); the Fenix A320 commanded and read through native SimConnect (FCU, AP1, flaps, tiller); **first automatic taxi: a C172 from its stand at Pau to the runway 31 hold-short point, following the clearance "C NG NW N5"**. Details and open points: [06](06-feasibility.md) |
| Looking for | MSFS developers interested in building the project together (see "Getting involved") |

## Reading the documentation

| Document | Content |
|---|---|
| [01 — Vision and need](01-vision.md) | The need, the audience, what already exists, positioning, planned scope |
| [02 — Technical challenges](02-technical-challenges.md) | Why it is hard, phase by phase, and how we intend to tackle it |
| [03 — Proposed architecture](03-proposed-architecture.md) | Components, tech stack, aircraft profiles, options (nothing is final) |
| [04 — Airports and taxiing](04-airports-and-taxiing.md) | Facilities API data, what is already read, what remains to taxi automatically |
| [05 — Ecosystem](05-ecosystem.md) | Existing tools, libraries and projects reviewed, with their licences |
| [06 — Feasibility](06-feasibility.md) | **The questions to settle before coding**, their status, the tests |
| [07 — Glossary](07-glossary.md) | LVar, H-event, HubHop, Facilities, WASM… |
| [Decision log](decision-log.md) | What was decided, when and why |

Each document has its exact counterpart in French, under the same file name; each page links to its translation at the top.

## Getting involved

The project is looking for contributors comfortable with at least one of these topics:

- **SimConnect / MSFS 2024 SDK** (C# or C++), Facilities API, WASM modules;
- **control laws**: ground path following, takeoff, flare, regulation (PID or other);
- **third-party aircraft**: LVars and events of the Fenix A320, PMDG, iniBuilds… (aircraft profiles);
- **WinUI 3 / .NET** for the application;
- **flight testing** on a variety of aircraft and airports.

The project is released under the MIT licence (see `LICENSE` at the root of the repository). Open questions (organisation) are listed in the [decision log](decision-log.md) and will be settled with the first contributors.

Contact: open an issue on the GitHub repository: https://github.com/Proxi64/FS24-Pilot-Flying/issues

## Folder layout

```
FS24 Pilot Flying/
├── Documentation/
│   ├── French/       ← French documentation
│   └── English/      ← this documentation, in English (always updated at the same time)
└── Experiments/      ← throw-away test consoles of the feasibility study
```

Rule: **every document exists in both languages**, with the same file name and the same content.
