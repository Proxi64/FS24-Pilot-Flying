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
| **Simulator layer** | SimConnect connection, state reading, sending commands, WASM module client | P/Invoke on the **native** SimConnect DLL: the SDK's managed DLL targets .NET Framework 4.6.1 and does not load in modern .NET (already proven) |
| **Airport layouts** | Reading through the Facilities API, local copy, taxi graph | Existing code to be extracted into a **shared library** (see [04](04-airports-and-taxiing.md)) |
| **Aircraft profiles** | "abstract action → control" table + characteristics (speeds, wheelbase, flap settings, capabilities: autoland, autothrust…) | Text files (JSON or YAML) written and shared by the community; controls taken from HubHop |
| **Flight manager** | State machine: gate → taxi → line-up → takeoff → climb → cruise → descent → approach → landing → runway exit | Also handles safe exits (go-around, stop) |
| **Controllers** | Control loops: ground path following, taxi speed, centreline tracking, rotation, flare | In the application if latency allows, otherwise in a WASM module |
| **User interface** | Flight selection, start, monitoring, taking back control | WinUI 3; possibly a panel inside the simulator (toolbar or EFB) |

## Proposed tech stack

- **.NET 10, C#**, x64 process.
- **Native SimConnect** through P/Invoke (`#pragma pack(1)` structures), already working in a previous project.
- **MobiFlight WASM module** (MIT licence) for LVars and H-events; FSUIPC only as an option, if already installed.
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
actions:
  gear_down:      { hubhop: "<HubHop preset>" }
  flaps_setting:  { hubhop: "<HubHop preset>", parameter: setting }
  ap1:            { hubhop: "<HubHop preset>" }
  ground_steering: { lvar: "<tiller LVar>", range: [-1, 1] }
state:
  fma_mode:       { lvar: "<LVar>" }
```

Existing projects have similar formats we can draw on: FS Copilot's YAML definitions and HubHop presets (see [05](05-ecosystem.md)).

## Open architecture questions

- Fast loops in the application or in a WASM module? (depends on measurements A3, E1, E2 in [06](06-feasibility.md))
- Profile format (JSON or YAML) and how to share profiles.
- User interface: standalone application only, or also a panel inside the simulator?
- Coexistence with third-party ATC (BeyondATC): read its taxi clearances?
