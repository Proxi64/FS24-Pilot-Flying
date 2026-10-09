# 02 — Technical challenges

*Français : [../French/02-technical-challenges.md](../French/02-technical-challenges.md)*

A virtual pilot has to replace the pilot's **hands, eyes and judgement**, on very different aircraft, **from outside the simulator**. This document describes each difficulty and the approach we have in mind.

## 1. A flight is six problems

| Phase | What the aircraft can already do | Difficulty | Fenix A320 case |
|---|---|---|---|
| Climb, cruise, descent | Autopilot (AP) | Low | Managed flight by the FMS |
| Top of descent | Nothing on most light aircraft | Medium | Computed by the FMS |
| Approach | APPR / ILS mode if available | Medium | APPR + both APs |
| Landing (flare, touchdown) | No autoland on the vast majority of aircraft | **High** | **ILS autoland**: flare and rollout done by the aircraft |
| Takeoff | Nothing: centreline tracking, rotation | Medium to high | No automatic takeoff on Airbus: to be done until AP engagement (around 100 ft) |
| Taxi | Nothing | Medium thanks to the airport layouts (see [04](04-airports-and-taxiing.md)) | Steering (tiller), brakes |

Takeoff, flare (without autoland) and taxiing require real **control loops** acting continuously on control surfaces, rudder, throttle and brakes according to the aircraft state. Sending one-off commands is not enough.

## 2. Taxiing

The data is there: the airport layouts provided by the simulator describe taxiways, hold-short points, parking spots and runways (details in [04](04-airports-and-taxiing.md)). What remains:

1. **Compute the route** in the taxiway graph (A*), from the parking spot to the runway hold-short point, excluding vehicle-only roads. Ideally, follow the **ATC clearance** ("via A, B, hold short runway 31") rather than the shortest path.
2. **Smooth the turns** at intersections. Curves are already split into short segments (median about 12 m at Pau).
3. **Follow the path** with a classic mobile-robotics algorithm (*pure pursuit*, *Stanley*). Two aviation specifics:
   - **oversteering on large aircraft**: the main gear, not the nose wheel, must follow the line, otherwise the main wheels cut the corner. The offset depends on the wheelbase, a profile parameter;
   - **steering control** differs per aircraft: rudder pedals, tiller, differential braking.
4. **Regulate speed**: target speed depending on curvature, braking before turns. On a jet, idle thrust is often enough to accelerate, hence braking in short applications.
5. **Stop** at hold-short points and behind other aircraft (positions read through SimConnect).

Taxiing is slow: an external loop at 20-30 Hz should be enough (to be measured, question A3 in [06](06-feasibility.md)).

## 3. Takeoff and flare

- **Takeoff**: keep the runway centreline with the rudder during the roll, rotate at the right speed and pitch, then climb to the AP engagement altitude.
- **Flare** (aircraft without autoland): reduce vertical speed just before touchdown, reduce thrust, keep the centreline, then brake.

An encouraging precedent: Pomax's tutorial "Flying planes with JavaScript" performs **automatic takeoff and landing** on eight light aircraft, from an external program connected through SimConnect (MSFS 2020). See [05](05-ecosystem.md).

## 4. Aircraft diversity

- **Asobo aircraft** respond to standard SimConnect events (`AP_ALT_VAR_SET`, `FLAPS_INCR`, `GEAR_UP`…).
- **Complex third-party aircraft** (Fenix, PMDG, iniBuilds…) often use their own internal variables (**LVars**) and events (**H-events**), different from one product to another.
- Reference speeds (Vr, Vref), flap settings and autopilot logic (G1000, Airbus FCU, Boeing MCP) are specific to each aircraft.

Chosen approach: **one profile per aircraft**, mapping abstract actions ("gear down", "set altitude 5000") to the aircraft's actual controls, and giving its characteristics (speeds, wheelbase…). Most controls would come from the community database **HubHop** (see [05](05-ecosystem.md)).

## 5. Flying from outside the simulator

The 2020 AI Pilot ran **inside** the simulator engine. An external application goes through SimConnect, which raises three questions:

- **latency and jitter** of the exchanges, a problem for a flare loop;
- **conflict with the user's controls** (joystick, throttle) which also send values;
- **access to internal variables** of third-party aircraft, which SimConnect alone cannot see.

Approaches:
- for internal variables: a **WASM module** loaded in the simulator (MobiFlight's exists, MIT licence);
- for latency, if tests show it is needed: our **own WASM module**, called on every simulator frame, for the fast loops, with the .NET application keeping the orchestration (see [03](03-proposed-architecture.md)).

## 6. Judgement and reliability

Crosswind, terrain, unstable approach (go-around), fuel, ground traffic: a virtual pilot must know how to **give up and get to safety**, not just execute. Since part of the audience cannot take over, the reliability requirement is higher than for a comfort tool. The project will need **clear operating limits** (conditions, aircraft and airports supported) and **safe exits** (go-around, holding, stopping on the runway).
