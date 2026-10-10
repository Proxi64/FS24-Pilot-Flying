# 01 — Vision and need

*Français : [../French/01-vision.md](../French/01-vision.md)*

## The need

### What disappeared

MSFS 2020 offered an **"AI Pilot"** (assistance panel, Alt+C shortcut). It could take over the aircraft end to end: taxi, takeoff, flight, landing. Its flaws were well known (hesitant taxiing, approximate approaches), but it existed.

**MSFS 2024 no longer has it.** In 2024 the "AI copilot" only handles radio communications. Asobo has never explained the removal nor announced its return (as of 9 October 2026).

### Who is asking for it

The official forum thread "AI pilot is missing from 2024 or ALT + C does nothing" has been one of the most active since 2024 was released (more than 8,600 views, still active in April 2026). It includes:

- **users with disabilities** (paralysis, cerebral palsy, chronic pain) for whom it was the only way to fly. At least one virtual airline of disabled pilots and several live streamers relied on it;
- **casual users**, who do not know how to start up an aircraft or just want to "do the flight";
- **users who want to be passengers**, watch the scenery or film;
- users who say they stay on MSFS 2020 because of this gap, and at least one who says he would pay up to $75 for a reliable solution.

A related request also comes up: the **assisted or automatic checklist** of 2020, also missing from 2024.

### What the community tried

A mod ("AI Pilot for MSFS 2024", flightsim.to) reused the 2020 panel. It worked poorly (unwanted climbs on final, crashes in terrain) and **has stopped working since update SU5** (comments from May 2026).

## What already exists

Many tools surround the pilot; **none flies the aircraft end to end**:

| Category | Examples | Does the tool fly? |
|---|---|---|
| Crew copilot (callouts, checklists, flows) | FS2Crew, FS First Officer, FlightFabric, CrewMate A350, SimVoice Copilot, Checklist Reader | No: the user remains pilot flying |
| AI air traffic control | BeyondATC, SayIntentions.AI | No |
| Shared cockpit between two humans | FS Copilot, YourControls | No |
| Landing analysis, logbook | TouchdownFX, BeatMyLanding, Volanta | No |
| Replay | MSFS 2024 built-in replay, Sky Dolly, FlightControlReplay | No |
| Pushback, ground services | Toolbar Pushback, Ground Services EFB | No |
| Missions | MissionGen, Liberty Fly | No |

These tools, and what we can reuse from them, are detailed in [05 — Ecosystem](05-ecosystem.md).

## Positioning

- **FS24 Pilot Flying flies the aircraft.** That is the empty slot.
- **Complementary, not competing** with crew copilots. A user can let FS24 Pilot Flying fly while FS2Crew makes the callouts and BeyondATC handles the radio.
- **Open**: a generic engine plus **aircraft profiles** that the community can write and share, rather than a paid product per aircraft.
- **PC only.** An external application cannot run on Xbox or PlayStation.

## Target audience, in order of priority

1. Users with disabilities.
2. Casual users and "passenger" users.
3. Experienced users who want to delegate part of the flight (long haul, video, scenery testing).

Consequence: **reliability** comes before realism. Part of the audience cannot take over if something goes wrong.

## Current objective (decision of 10 October 2026)

One complete flight, flown autonomously: **Cirrus SF50 Vision Jet** (default MSFS 2024 aircraft), from **Toulouse-Blagnac (LFBO), stand F10, engine off**, to **Biarritz (LFBZ), stand 5, engine shut down**. The feasibility study is organised around this flight (see [06](06-feasibility.md)); the scope below remains a proposal for afterwards.

## Planned scope (proposal, to be validated with contributors)

| Step | Content | Aircraft |
|---|---|---|
| V1 "assisted flight" | Aircraft ready at the gate, flight plan loaded → taxi → line-up → takeoff → managed flight → **automatic ILS landing** → runway exit | MSFS 2024's default airliner, Asobo's A320neo (autoland to be checked, question F3); add-ons such as the Fenix A320 later |
| V2 | Automatic takeoff and flare for aircraft **without** autoland (general aviation) | Asobo base aircraft (C172, TBM…) |
| V3 | Taxi to the arrival gate, following ATC clearances | All |
| Later | Start-up from cold & dark, FMS programming, checklists, more profiles | — |

The order may change depending on the feasibility study. Taxiing, for example, is easier to test than landing, and it is the first phase shown to work: on 9 October 2026 a Cessna 172 taxied on its own from its stand to the runway hold-short point at Pau (see [06](06-feasibility.md)).

**Why the default aircraft first** (decision of 10 October 2026: no tests on add-ons for now): every user has them, they need no add-on, they are commanded through the simulator's own controls, and the bases (taxi, takeoff, landing) must be established before adapting to each add-on. Asobo's A320neo is also the aircraft used by the audience that asks most for this feature.

**The Fenix A320 later:** it already does a lot on its own (ILS autoland with flare and rollout, autothrust, managed flight), and a first test showed it can be commanded through native SimConnect (test B1). But it is an add-on: its profile will come once the bases work on the default aircraft.

## Out of scope

- Replacing ATC (BeyondATC and SayIntentions do it) or crew copilots.
- Consoles (Xbox, PlayStation).
- Any use outside the simulator.

## Sources

- Official forum, "AI pilot is missing from 2024 or ALT + C does nothing": https://forums.flightsimulator.com/t/ai-pilot-is-missing-from-2024-or-alt-c-does-nothing/664986
- Official forum, "Where is A.I. Pilot in 2024?": https://forums.flightsimulator.com/t/where-is-a-i-pilot-in-2024/672602
- "AI Pilot for MSFS 2024" mod: https://flightsim.to/file/85165/ai-pilot-for-msfs-2024
- Official forum, "Auto / assist checklist": https://forums.flightsimulator.com/t/auto-assist-checklist-any-dev-here/743225
- Official forum, "Third-Party EFB Apps": https://forums.flightsimulator.com/t/third-party-efb-apps/739415
