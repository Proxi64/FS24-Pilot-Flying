# 03 — Proposed architecture

*Français : [../French/03-proposed-architecture.md](../French/03-proposed-architecture.md)*

> **Nothing is final.** This document gathers the current ideas as a basis for discussion. Final choices will come after the feasibility study ([06](06-feasibility.md)) and with the contributors.

## Overview

```
┌──────────────────────────── Windows application (.NET) ────────────────────────────┐
│                                                                                    │
│  User interface (WinUI 3)   Flight manager (state machine of flight phases)        │
│        │                     │        │         │          │                       │
│        │                   Taxi    Takeoff   Flight / approach   Landing           │
│        │                     │        │         │          │                       │
│        └──────────── Aircraft profiles (abstract actions → actual controls) ───────┤
│                              │                                                     │
│   Airport layouts ───────────┤        Simulator layer                              │
│   (shared library)           │        ├── native SimConnect (P/Invoke)             │
│                              │        └── WASM module client (LVars, H-events)     │
└──────────────────────────────┼─────────────────────────────────────────────────────┘
                               │
┌──────────────────────────────┼──────────── MSFS 2024 ──────────────────────────────┐
│   SimConnect      MobiFlight WASM module (or equivalent)      [option] our own WASM │
│                                                               module for fast loops │
└────────────────────────────────────────────────────────────────────────────────────┘
```

## Components

| Component | Role | Notes |
|---|---|---|
| **Simulator layer** | SimConnect connection, state reading, sending commands, LVars and input events, optionally a WASM module client | P/Invoke on the **native** SimConnect DLL: the SDK's managed DLL targets .NET Framework 4.6.1 and does not load in modern .NET (proven again by every test). LVars and input events are native in MSFS 2024 (tests C1, B1) |
| **Airport layouts** | Reading through the Facilities API, local copy, taxi graph | Existing code to be extracted into a **shared library** (see [04](04-airports-and-taxiing.md)) |
| **Aircraft profiles** | "abstract action → control" table + characteristics (speeds, wheelbase, flap settings, capabilities: autoland, autothrust…) | Text files (JSON or YAML) written and shared by the community; controls taken from HubHop |
| **Flight manager** | State machine: gate → taxi → line-up → takeoff → climb → cruise → descent → approach → landing → runway exit | Also handles safe exits (go-around, stop) |
| **Controllers** | Control loops: ground path following, taxi speed, centreline tracking, rotation, flare | In the application if latency allows, otherwise in a WASM module. Taxiing: in the application, shown by test E1 |
| **User interface** | Flight selection, start, monitoring, taking back control | WinUI 3; possibly a panel inside the simulator (toolbar or EFB) |

## Proposed tech stack

- **.NET 10, C#**, x64 process.
- **Native SimConnect** through P/Invoke (`#pragma pack(1)` structures), already working in a previous project.
- Third-party aircraft: **native SimConnect** for LVars and input events (enough for the Fenix so far, tests C1 and B1); the **MobiFlight WASM module** (MIT licence) for what native SimConnect cannot do (H-events, calculator code), if needed; FSUIPC only as an option, if already installed.
- **WinUI 3** + CommunityToolkit.Mvvm for the user interface.
- **Option**: our own WASM module (C/C++, MSFS 2024 SDK toolchain) if a loop must run at the simulator's pace.

## Our own WASM module, if needed

According to the MSFS 2024 SDK WebAssembly documentation:

- C/C++ compiled to WebAssembly by the SDK toolchain (Visual Studio extension), then **converted to native code** ahead of time;
- a **standalone module** (`modules` folder of a package) is loaded automatically and receives **a call on every frame**, without going through SimConnect;
- each module has **its own thread** (in 2020, everything ran on the main thread);
- available APIs: Vars, Event, IO, **CommBus** (dialogue with the external application), Network, Planned Route…; the old Gauge API is deprecated;
- not allowed: Windows API, C++ exceptions and threads. Files: read-only inside the package, read/write in `\work`.

## What an aircraft profile could look like

**Indicative** example (format not decided):

```yaml
aircraft: Fenix A320
titles: ["FenixA320*"]            # recognised SimConnect titles
characteristics:
  wheelbase_m: 12.6               # for ground path following
  taxi_speed_kt: 20
  autoland: true
  autothrust: true
actions:                          # LVar names seen in test B1 (from HubHop, reference only)
  gear_down:      { hubhop: "<HubHop preset>" }
  flaps_setting:  { lvar: "S_FC_FLAPS", values: [0, 1, 2, 3, 4] }
  ap1:            { lvar_counter: "S_FCU_AP1" }   # push button: +1 on press, +1 on release
  speed_knob:     { lvar_encoder: "E_FCU_SPEED" } # +1 / -1 per detent
  ground_steering: { lvar: "N_FC_CAPT_TILLER", range: [-75, 75] }
state:
  ap1_engaged:    { lvar: "I_FCU_AP1" }
  fcu_speed:      { lvar: "N_FCU_SPEED" }
  fma_mode:       { lvar: "<not found yet>" }
```

Existing projects have similar formats we can draw on: FS Copilot's YAML definitions and HubHop presets (see [05](05-ecosystem.md)).

## Open architecture questions

- Fast loops in the application or in a WASM module? Taxiing works from the application (A3, E1 in [06](06-feasibility.md)); takeoff and flare (E2) remain to be measured.
- Profile format (JSON or YAML) and how to share profiles.
- User interface: standalone application only, or also a panel inside the simulator?
- Coexistence with third-party ATC (BeyondATC): read its taxi clearances?
