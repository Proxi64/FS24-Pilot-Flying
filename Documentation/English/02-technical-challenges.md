# 02 — Technical challenges

*Français : [../French/02-technical-challenges.md](../French/02-technical-challenges.md)*

A virtual pilot has to replace the pilot's **hands, eyes and judgement**, on very different aircraft, **from outside the simulator**. This document describes each difficulty and the approach we have in mind.

## 1. A flight is six problems

| Phase | What the aircraft can already do | Difficulty | Airliner case (A320) |
|---|---|---|---|
| Climb, cruise, descent | Autopilot (AP) | Low | Managed flight by the FMS |
| Top of descent | Nothing on most light aircraft | Medium | Computed by the FMS |
| Approach | APPR / ILS mode if available | Medium | APPR + both APs |
| Landing (flare, touchdown) | No autoland on the vast majority of aircraft | **High** | **ILS autoland**: flare and rollout done by the aircraft (Fenix; to be checked on the default A320neo, question F3) |
| Takeoff | Nothing: centreline tracking, rotation | Medium to high | No automatic takeoff on Airbus: to be done until AP engagement (around 100 ft) |
| Taxi | Nothing | Medium thanks to the airport layouts (see [04](04-airports-and-taxiing.md)) | Steering (tiller), brakes |

Takeoff, flare (without autoland) and taxiing require real **control loops** acting continuously on control surfaces, rudder, throttle and brakes according to the aircraft state. Sending one-off commands is not enough.

## 2. Taxiing

The data is there: the airport layouts provided by the simulator describe taxiways, hold-short points, parking spots and runways (details in [04](04-airports-and-taxiing.md)). What remains:

1. **Compute the route** in the taxiway graph, from the parking spot to the runway hold-short point, excluding vehicle-only roads, and **following the ATC clearance** ("via C, NG, NW, hold short runway 31") rather than the shortest path. Done in test E1: shortest path (Dijkstra) on (node, position in the clearance); unnamed connecting pieces allowed but penalised; runways never; the taxiway leading onto the runway never entered.
2. **Leave the stand the right way**: a stand can have one exit behind the aircraft (pushback) and one ahead, and the aircraft is not always on the stand point (at Pau, GSX placed a C172 14.6 m ahead of it, where the nose wheel of a larger aircraft would be). Curves are already split into short segments (median about 12 m at Pau).
3. **Follow the path** with a classic mobile-robotics algorithm. In test E1, a *Stanley*-like law held a straight line within 1 cm RMS, and *pure pursuit* (aim at the route point a few metres ahead) took the C172 through every turn of a 1.2 km route within 0.76 m. Aviation specifics:
   - **oversteering on large aircraft**: the main gear, not the nose wheel, must follow the line, otherwise the main wheels cut the corner. The offset depends on the wheelbase, a profile parameter (to be tested with the A320);
   - **steering control** differs per aircraft: rudder pedals, tiller, differential braking (C172: rudder and tiller both steer the nose wheel, only while rolling; Fenix: tiller LVar or `AXIS_STEERING_SET`);
   - **the aircraft does not roll straight by itself**: with the controls centred, the C172 veered left as soon as it moved, because of the propeller effects (test A1, 09/10/2026: heading −4.5° and 1.6 m off the initial line after 28 m). Following a line therefore means correcting, all the time, the lateral offset and the heading error relative to the taxiway or runway centreline (an integral term holds the drift), never holding a fixed control position;
   - **positions must be converted precisely**: latitude / longitude to metres with WGS84 scales; a spherical Earth put the aircraft 1.5 m off the line 1 km from the airport reference point (tests D4, E1).
4. **Regulate speed**: target speed depending on curvature, braking before turns. On a jet, idle thrust is often enough to accelerate, hence braking in short applications. Same on the C172: at idle it already rolls at 5.7 kt (test A1); test E1 held 5 kt with throttle and brakes, 3 kt in turns.
5. **Stop** at hold-short points (test E1: 4.8 m before it, never beyond) and behind other aircraft (positions read through SimConnect, question D5).

Taxiing is slow: the external loop of test E1 (commands at 30 Hz, positions read at every frame, about 40 Hz) was enough; no WASM module is needed for taxiing.

## 3. Takeoff and flare

- **Takeoff**: keep the runway centreline with the rudder during the roll, rotate at the right speed and pitch, then climb to the AP engagement altitude.
- **Flare** (aircraft without autoland): reduce vertical speed just before touchdown, reduce thrust, keep the centreline, then brake.

An encouraging precedent: Pomax's tutorial "Flying planes with JavaScript" performs **automatic takeoff and landing** on eight light aircraft, from an external program connected through SimConnect (MSFS 2020). See [05](05-ecosystem.md).

## 4. Aircraft diversity

- **Asobo aircraft** respond to standard SimConnect events (`AP_ALT_VAR_SET`, `FLAPS_INCR`, `GEAR_UP`…).
- **Complex third-party aircraft** (Fenix, PMDG, iniBuilds…) often use their own internal variables (**LVars**) and events (**H-events**), different from one product to another.
- Reference speeds (Vr, Vref), flap settings and autopilot logic (G1000, Airbus FCU, Boeing MCP) are specific to each aircraft.

Chosen approach: **one profile per aircraft**, mapping abstract actions ("gear down", "set altitude 5000") to the aircraft's actual controls, and giving its characteristics (speeds, wheelbase…). Most controls would come from the community database **HubHop** (see [05](05-ecosystem.md)). Tests C1 and B1 showed that the Fenix's switches, buttons and lights are plain LVars that native SimConnect reads and writes, and that MSFS 2024 cockpits also expose **input events**; HubHop is a good source of names, but states no licence (reference only).

## 5. Flying from outside the simulator

The 2020 AI Pilot ran **inside** the simulator engine. An external application goes through SimConnect, which raises three questions:

- **latency and jitter** of the exchanges, a problem for a flare loop;
- **conflict with the user's controls** (joystick, throttle) which also send values;
- **access to internal variables** of third-party aircraft, which SimConnect alone cannot see.

What the tests showed (October 2026, see [06](06-feasibility.md)):
- **latency**: SimConnect gives the aircraft state at every frame (about 40 Hz here, regular) and commands act at the next frame; enough for taxiing (test E1). Takeoff and flare remain to be measured;
- **joystick conflict**: an untouched joystick never interferes; a moved one overrides the program between two sends, which also gives a way to detect that the user takes over (test A1);
- **internal variables**: MSFS 2024 SimConnect reads and writes LVars itself and drives cockpit input events; the MobiFlight WASM module also works (MIT licence), but was not needed for the Fenix (tests C1, B1).

Our **own WASM module**, called on every simulator frame, remains an option for the fast loops if takeoff or flare need it, with the .NET application keeping the orchestration (see [03](03-proposed-architecture.md)).

## 6. Judgement and reliability

Crosswind, terrain, unstable approach (go-around), fuel, ground traffic: a virtual pilot must know how to **give up and get to safety**, not just execute. Since part of the audience cannot take over, the reliability requirement is higher than for a comfort tool. The project will need **clear operating limits** (conditions, aircraft and airports supported) and **safe exits** (go-around, holding, stopping on the runway).
