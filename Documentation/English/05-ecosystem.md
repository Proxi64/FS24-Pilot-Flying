# 05 — Ecosystem: tools and projects reviewed

*Français : [../French/05-ecosystem.md](../French/05-ecosystem.md)*

What we looked at (October 2026), what we can take from it and under which conditions. Licences are as observed; "to be checked" means we have not read it yet.

## Access to third-party aircraft variables

These tools solve the **wiring**, that is, commanding the systems of a third-party aircraft. They say nothing about **flying**: when to act, control loops, taxiing, judgement.

### MobiFlight WASM module + HubHop database — tested, works, optional

- **MIT licence** (module).
- Standalone WASM module, placed in the Community folder, that runs events and calculator code **in the context of the aircraft gauges**: access to LVars and H-events that SimConnect alone cannot see.
- **Several clients possible**: an external program registers (`MF.Clients.Add.<Name>`, reply `.Finished`) and gets its own shared memory channels (`<Name>.LVars`, `<Name>.Command`, `<Name>.Response`). It can therefore coexist with the user's MobiFlight Connector.
- Commands: `MF.SimVars.Add.(code)` declares a variable to read (4 bytes per value, in order of addition; IDs recommended from 1000), `MF.SimVars.AddString` (128 bytes, 64 strings max), `MF.SimVars.Set.(code)` to write or execute, `MF.LVars.List`, `MF.Config.MAX_VARS_PER_FRAME.Set`.
- Values are sent **when they change**; measured in test C1: a value changing at every frame arrives at every frame (40 per second here), an LVar written through the module is read back in about 73 ms.
- Pitfalls: the first command after start-up may be ignored (send a dummy one); the LVar list is capped at 1,000 names (hard limit in its source; on the maintainer's PC other add-ons fill it and no Fenix LVar appears).
- **MSFS 2024**: works (test C1, version 1.0.1, the latest release, on the C172 and the Fenix A320, with our own registered client). The MobiFlight documentation explains how to re-enable the module if the simulator disables it.
- **HubHop**: community database of ready-to-use controls, aircraft by aircraft (2,285 presets for the Fenix). **No licence stated** (site and repository, checked 9 October 2026): use it as a reference for variable names, do not redistribute its data without asking.
- https://github.com/MobiFlight/MobiFlight-WASM-Module · https://docs.mobiflight.com/guides/wasm-module/enable-in-msfs2024/ · https://hubhop.mobiflight.com/

### Native SimConnect in MSFS 2024 — enough so far

- `SimConnect_AddToDataDefinition` accepts LVars (`L:NAME`, read and write), and the SDK provides **input events**, the `B:` variables of MSFS 2024 cockpits (`SimConnect_EnumerateInputEvents`, `GetInputEvent`, `SetInputEvent`, `SubscribeInputEvent`). Sources: official SDK documentation and `SimConnect.h`; MobiFlight guide https://docs.mobiflight.com/guides/input-events-2024/.
- Tests C1 and B1: faster than through the module (LVar read back in 5 to 19 ms, input event applied in 15 to 17 ms); the Fenix is commanded entirely through its LVars (FCU, AP1, flaps, beacon, tiller). The C172 has 233 input events, the Fenix 220 (audio panels, FCU knobs, radio, standby).
- Not covered: H-events and calculator code, which need a WASM module (MobiFlight) if an aircraft requires them.

### FSUIPC7 — option if the user already has it

- Supports MSFS 2024; WASM module (LVars, H-vars, presets, calculator code) and a programming interface (WAPI).
- Its WASM crashed at times under 2024; much less likely since version 7.5.6 (January 2026) according to its author.
- **Commercial product** (paid registered version): we should not require it.
- The FS First Officer copilot relies on it, which shows the "external application + WASM module" approach is proven.

### WASimCommander — alternative

- WASM module + client, by the author of MSFS-Tools; access to the Gauge API, LVars and calculator code.
- MSFS 2024 support and licence: to be checked.
- https://flightsim.to/addon/36474/wasimcommander

## Projects that fly, or tried to

### "Flying planes with JavaScript" (Pomax) — algorithm reference

- **External** autopilot through SimConnect: wing leveller, altitude, heading, autothrottle, waypoints, terrain following, **automatic takeoff** (roll, rotation) and **automatic landing** (approach, glide slope, touchdown, braking).
- Tested on eight light aircraft (Beaver, C310R, Arrow III, Beech 18, Kodiak 100…). MSFS 2020. No taxiing.
- Proof that takeoff and landing can be done from outside the simulator; a source of ideas for our controllers.
- **Repository licence to be checked**: until then, we take ideas but do not reuse code.
- https://pomax.github.io/are-we-flying/

### "AI Pilot for MSFS 2024" mod

Reused the 2020 panel. Mediocre, then broken by SU5 (May 2026). Confirms both the need and the lack of a solution.

## Crew copilots

| Tool | What it does | Price | For us |
|---|---|---|---|
| FS2Crew | "Pilot monitoring" copilot: checklists, flows, callouts, flaps and gear on command; per-aircraft editions (Fenix, PMDG…) | Fenix: $29.99 + €14.99 (animated copilot) | Complementary; shows strong demand for the Fenix |
| FS First Officer | Voice-command or automatic copilot; generic edition for all airliners | Paid | Relies on FSUIPC7 |
| FlightFabric | Direct voice commands for popular airliners | Free, open source (alpha) | Aircraft list and command method worth a look |
| CrewMate A350 | Voice copilot for the iniBuilds A350 | Free, open source | Same |

None of them taxis, takes off or lands: the user stays at the controls.

## Shared cockpit

### FS Copilot

- Synchronises position, control surfaces and switches between **two human pilots** (alternative to YourControls). Free, open source. **Does not fly.**
- Of interest: its **YAML aircraft definitions** (variables and events of each switch), close to our future profiles, and its MSFS 2024 interface code. Licence to be checked before any reuse.
- https://fscopilot.com/ · https://github.com/yury-sch/FsCopilot

## SimConnect libraries

| Project | Language / licence | Of interest |
|---|---|---|
| node-simconnect (EvenAR) | TypeScript, LGPL-3.0 | Reimplements the **SimConnect protocol without the DLL** (TCP, named pipes), supports MSFS 2024. Reference if a 2024 structure causes trouble. https://github.com/EvenAR/node-simconnect |
| msfs-simconnect-api-wrapper (Pomax) | JavaScript | Layer over node-simconnect; airport database, runways, ILS; nothing about taxiways. https://github.com/Pomax/msfs-simconnect-api-wrapper |
| MSFS-Tools (mpaperno) | C++, mostly GPL v3 | **DocImport**: structured database of all events, SimVars and units extracted from the SDK docs (useful to validate profiles). **SimConnect-Request-Tracker**: links a SimConnect error to the faulty call. GPL code, incompatible with the project's MIT licence: we take ideas, not code. https://github.com/mpaperno/MSFS-Tools |
| simconnect-sdk-rs | Rust, MIT | Archived in February 2026. Ruled out. https://github.com/mihai-dinculescu/simconnect-sdk-rs |

## Fenix A320 (add-on, postponed)

- ILS autoland (flare + rollout), autothrust, managed flight: a strong candidate once add-ons are back in the plan (no add-on tests for now, decision of 10 October 2026; the first version targets the default A320neo).
- Installed components: FenixSystem, FenixDisplay, **Fenix.GqlGateway**. The latter suggests a data interface (GraphQL?) worth exploring. We will also need to **check Fenix's terms of use** for a third-party tool.
- Reported pitfall (FSUIPC forum, February 2025): Fenix LVars stuck at 0 under 2024, caused by a crash of the FSUIPC WASM module.

## Development tools

- **"MSFS SDK" MCP server** (community, open source): lets an AI assistant search the SDK documentation. It reads the official site live. Its search is approximate and the targeted version (2020 or 2024) is not stated: we always check against the official 2024 documentation. https://github.com/90barricade93/MSFS-SDK-MCP
- **simconnect-mcp** (mrlm-net, version 0.14.2): MCP server giving an AI assistant live access to the running simulator (SimVars, events, airport data); used to read the aircraft position during test E1. **Business Source License**, not open source: a development tool only, no code reused. https://github.com/mrlm-net/simconnect-mcp
- **GSX** (FSDreamTeam, commercial) and **Navigraph Charts**: their ground map and airport charts were used by the maintainer to check the tests; GSX reads the same Facilities layout and places the aircraft on its stand.
- **Wassette** (Microsoft): WebAssembly component runtime that gives tools to AI agents. Unrelated to MSFS WASM modules (different format, does not run in the simulator). Ruled out.

## The initiator's previous project

Mission generator for MSFS 2024 (C# .NET 10, unpublished). Reusable:

- P/Invoke on native SimConnect, proven 2024 structures;
- airport layout reader through the Facilities API, local copy of ~85,000 layouts, geographic calculations;
- detection of installed aircraft (`SimConnect_EnumerateSimObjectsAndLiveries`) and grouping by family;
- pitfalls already met:
  - `SimConnect_FlightPlanLoad` has no effect under 2024;
  - a flight plan hand-written into a `.FLT` file crashed MSFS;
  - `PLANE TOUCHDOWN *` gives the exact touchdown even when read at 1 Hz;
  - camera states: 2 to 8, 24, 26 and 29 = at the controls; 34 and 35 = menus or loading;
  - SimConnect weather functions are deprecated in 2024;
  - its latitude / longitude conversion used a spherical Earth (111,320 m per degree), about 1.5 m off 1 km from the airport reference point: corrected here with WGS84 scales (tests D4, E1).

These pitfalls come from the previous project; until a test of this project confirms them, `CLAUDE.md` marks them as not re-verified.

Decision: extract this code into a **shared library** rather than depend on the old project.
