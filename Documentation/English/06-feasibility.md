# 06 — Feasibility: what must be established before coding

*Français : [../French/06-feasibility.md](../French/06-feasibility.md)*

**Principle:** we establish what is possible before writing the application. Each question is settled by reading documentation or by a **small test**: a throw-away console stored in `Experiments/` ("tests"), outside the future project.

Status: ✅ confirmed · 🟡 partly · ❓ to be checked · ❌ impossible or dropped.

## A. Controlling the aircraft from outside

| # | Question | How to check | Status |
|---|---|---|---|
| A1 | Can we continuously send (20 to 50 Hz) elevator, ailerons, rudder, throttle and brakes through SimConnect (`AXIS_*_SET`, `THROTTLE_SET`, `AXIS_LEFT/RIGHT_BRAKE_SET`)? | Test on C172, then Asobo A320 | 🟡 Yes on the C172 at a standstill (test A1, 09/10/2026): elevator, ailerons, rudder, throttle and brakes follow events sent at 30 and 60 Hz. A positive `AXIS_ELEVATOR/AILERONS/RUDDER_SET` value gives a negative `*_POSITION` (nose down, roll left, yaw left). Brakes: non-linear scale, -16383 → 5 %, -8191 → 13 %, 0 → 31 %, +8191 → 55 %, +16383 → 100 %. Remain: in flight, Asobo A320 |
| A2 | What happens with the user's joystick and throttle plugged in? How to take and hand back control cleanly? | Test with hardware plugged in | 🟡 Test A1 (09/10/2026), T.16000M joystick: untouched, it never interferes; moved, it overrides the program between two sends (program value only 37 % of the time); when the program stops sending, the last value stays until the joystick moves, and then the joystick takes over at once. So a user input can be detected as a gap between command and position. Remains: throttle lever, clean handover design |
| A3 | What read and write rate do we actually reach, with how much jitter? | Measured during test A1 | ✅ Test A1 (09/10/2026), C172 at a standstill: reading at every simulation frame = 41 Hz on this PC (interval 25 ms, p95 26.4 ms, one 81 ms gap in 20 s); sending 30 and 60 Hz held (p95 34.7 / 17.9 ms); throttle and brakes respond at the next frame (≤ 10 ms), control surfaces with about 20 ms of delay (sine wave) and settle in about 0.1 s. The reading rate follows the simulator frame rate |
| A4 | On the ground, does the rudder steer the nose wheel? Is there a separate tiller? | Test at the gate | ✅ C172 (test A1, 09/10/2026): the nose wheel only turns while rolling (at a standstill its angle stays at 0). Rolling at 3 to 5 kt, the rudder **and** the tiller event `AXIS_STEERING_SET` both steer it, linearly, 20° per full command, no measurable delay; the aircraft yaws about 0.22 s later. A positive value turns left for both (for the tiller, the opposite of the documentation). Angle read with `GEAR CENTER STEER ANGLE` or `GEAR STEER ANGLE:0`; `CONTACT POINT STEER ANGLE:0` stays at 0. Airliners: see B2 |

## B. Fenix A320

| # | Question | How to check | Status |
|---|---|---|---|
| B1 | Does the Fenix accept standard SimConnect axes (sidestick, rudder, brakes, thrust levers), or do we need LVars? | Test A1 on the Fenix | ❓ |
| B2 | Which control for the tiller? | HubHop + test | ❓ |
| B3 | Do FCU controls (speed, heading, altitude, VS, AP1/2, A/THR, APPR, LOC), flaps, gear, spoilers and autobrake exist in HubHop and work under 2024? | HubHop + test through the MobiFlight WASM module | ❓ |
| B4 | Can we read the state: FMA modes, FMS phase, warnings? | HubHop, LVar list, test | ❓ |
| B5 | Does full autoland (flare, rollout, autobrake) work on ILS when our program arms it? | Manual test flight, then automated | ❓ |
| B6 | What does `Fenix.GqlGateway` provide? Do Fenix's terms of use allow a third-party tool? | Exploration + reading the terms, possibly asking Fenix | ❓ |
| B7 | Flight plan in the MCDU: in V1 we assume the user loads it (SimBrief import from the Fenix EFB). Can it be automated later? | Later | ❓ |

## C. Access to third-party aircraft variables

| # | Question | How to check | Status |
|---|---|---|---|
| C1 | Does the MobiFlight WASM module work under MSFS 2024 with a registered third-party client? | Test | ❓ |
| C2 | Licences: MobiFlight WASM module, HubHop data, FS Copilot, WASimCommander | Reading the licences | 🟡 MobiFlight module: MIT |
| C3 | Do we need FSUIPC? (not wanted) | Follows from C1 | ❓ |
| C4 | Do the values sent "on change" by the MobiFlight module arrive fast enough for a control loop? | Measured during C1 | ❓ |

## D. Airports and taxiing

| # | Question | How to check | Status |
|---|---|---|---|
| D1 | Are runway hold-short points provided? | SDK 2024 docs | ✅ `TAXI_POINT.TYPE`: 2 HOLD_SHORT, 4 ILS_HOLD_SHORT (+ 5 and 6 without markings), with `ORIENTATION`. Seen at LFBP (test D2, 09/10/2026): 8 points, all type 5 (no markings), 147 to 167 m from the runway centreline, all on the main network. LFPG (default scenery): 147 points, types 5 and 6 (ILS), 71 to 310 m from the centreline, all on the main network |
| D2 | Are taxiway names available and linked to the paths? | SDK docs + test D2 | ✅ `NAME_INDEX` is the index in the `TAXI_NAME` list (0 = unnamed). LFBP (test D2, 09/10/2026): 16 names, each forming 1 or 2 continuous pieces; 12 of them also exist in OpenStreetMap (https://www.openstreetmap.org, ODbL) at the same place, 6 to 85 m apart for short taxiways (E, G, N, N1, N3, N5, S2). C, M and NG exist only in MSFS, B, BA and BC only in OSM. LFPG: 225 names, 217 of them in a single piece. On RUNWAY paths (type 2), `NAME_INDEX` is instead the index of the runway in the airport's runway list |
| D3 | What do the path types mean? | SDK 2024 docs | ✅ 1 TAXI, 2 RUNWAY, 3 PARKING, 4 PATH, 5 CLOSED, 6 VEHICLE, 7 ROAD, 8 PAINTEDLINE. Sceneries do not use them the same way: LFBP (France VFR) mostly TAXI, LFPG (Asobo) almost only PATH (4,667 PATH against 3 TAXI): both types must be treated as taxiways |
| D4 | Does the layout centreline match the painted line of the scenery (default and payware sceneries)? | Taxi manually and compare the aircraft position with the graph | ❓ |
| D5 | Can we know the position of other aircraft on the ground (AI traffic, BeyondATC) to stop behind them? | `SimConnect_RequestDataOnSimObjectType` | ❓ |
| D6 | Can we retrieve the BeyondATC taxi clearance? | Study of an existing BeyondATC-related tool | ❓ |

## E. Control loops

| # | Question | How to check | Status |
|---|---|---|---|
| E1 | Is ground path following stable at the rate measured in A3 (C172, then A320)? | Test at Pau (LFBP) | ❓ |
| E2 | A320 takeoff: centreline tracking and rotation with the sidestick until AP at 100 ft | Test | ❓ |
| E3 | Do we need a WASM module for latency? | Follows from A3, E1, E2 | ❓ (good sign: Pomax takes off and lands light aircraft from outside, under MSFS 2020) |
| E4 | Under which licence is Pomax's tutorial code? | Reading the repository | ❓ |

## Tests

| Test | Folder | Questions | State |
|---|---|---|---|
| D2 — complete taxi layout | `Experiments/D2-TaxiLayout/` | D1, D2, D3, axis convention, network connectivity | Run in MSFS at LFBP, France VFR payware scenery from the Community folder (9 October 2026). Confirmed: element types 2 START and 17 TAXI_NAME, BIAS_X = east / BIAS_Z = north (median 0.4 m against 297 m), network connected to all parking spots and hold-short points. Run again at LFPG, default Asobo scenery (9 October 2026): same conclusions (axes: median 1.3 m against 544 m; 4,313-node main network, 374 parking spots all connected). `WIDTH`, `CENTER_LINE` and `WEIGHT` are transmitted (LFPG: widths 5 to 60 m, painted centreline on 3,252 paths, weight 500,000 lb) but depend on the scenery: LFBP gives 30 m, 0 and 0 everywhere. They cannot be relied on without a fallback |
| A1 — controlling the aircraft from outside | `Experiments/A1-AxisControl/` | A1, A2, A3, A4 (Cessna 172 at a parking spot, engine off) | Run in MSFS on 9 October 2026 (Asobo C172 at a parking spot, engine off): no SimConnect exception; results in the A1 to A4 rows. Second script `--rolling` run the same day (70 m on a taxiway, no emergency stop): settles A4. Also seen: `PARKING_BRAKE_SET` works both ways; at idle the C172 already rolls at 5.7 kt, so holding 4 kt needs the brakes (useful for E1); brakes at 50 % stop it from 3.5 kt in 1.4 s and 1.4 m |

### What test D2 does

It reads the complete layout of one airport, with every field useful for taxiing (points with type and orientation, paths with name, runway, painted centreline and weight, taxiway names, start positions, runway ILS), then:

- counts points and paths by type;
- for each taxiway name, counts its paths and its pieces: a real taxiway name forms a continuous track;
- lists hold-short points with their taxiway and their distance to the runway centreline;
- **checks the axis convention** by comparing runway paths with the runway centrelines, under both possible interpretations;
- checks that the aircraft network forms a single set connected to parking spots and hold-short points;
- writes a report, the raw data (JSON) and a GeoJSON file to display over a map (https://geojson.io).

## Proposed order

1. **D2** in the simulator (read-only, no risk).
2. **A1 to A4** on an Asobo aircraft: a console that reads the state and sends axes.
3. **C1**, then **B1 to B4** on the Fenix.
4. **D4 + E1**: first automatic taxi at Pau.
5. **B5, E2**: autoland and takeoff.

Afterwards: set the V1 scope and the architecture.
