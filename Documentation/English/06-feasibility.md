# 06 — Feasibility: what must be established before coding🟡 Test B1 (09/10/2026, parking, powered, engines and hydraulics off): rudder yes (`AXIS_RUDDER_SET` ±0.5 gives ±0.32 rudder, and also moves the tiller LVar); thrust levers move, but `THROTTLE_SET` 0 is not idle: the levers stayed at about 50 % (seen in the cockpit), their LVars `A_FC_THROTTLE_LEFT/RIGHT_INPUT` going from 2 (value at the start, levers presumably at idle) to 2.99 for `THROTTLE_SET` 30 %, then 2.76 for 0. How the Fenix maps this event is not understood yet (unverified hypothesis: the full axis range, idle at −16383); the LVars can be written. Sidestick (`AXIS_ELEVATOR/AILERONS_SET`): no watched variable moved; to be checked with hydraulics on or in flight. Brakes: not tested |✅ Test B1: `AXIS_STEERING_SET` (±0.5 gives ±37) and a direct write of the LVar `N_FC_CAPT_TILLER` (range ±75) both move the Fenix tiller; the rudder moves it too |🟡 Test B1, native SimConnect only (no WASM module): FCU SPD / HDG / ALT knobs follow exactly (encoders `E_FCU_…`, +1 kt / +1° / +1000 ft per detent, about 0.1 s), also through the FCU input events; AP1 engaged and disengaged on the ground (counter `S_FCU_AP1`, light `I_FCU_AP1`); flaps lever (`S_FC_FLAPS` 1 gives `FLAPS HANDLE INDEX` 2; possibly CONF 1+F on the ground, not checked); beacon switch (`S_OH_EXT_LT_BEACON`, light in 36 to 58 ms). A/THR, LOC, APPR and autobrake presses are registered but give no light on the ground with engines off, which is the aircraft's normal behaviour (same when pressed by hand): to be confirmed in flight. Gear and spoilers: not tested |🟡 Test B1: annunciators (`I_…`), FCU windows (`N_FCU_SPEED/HEADING/ALTITUDE/VS`), dashes (`B_FCU_…`), levers and switches (`S_…`) are readable LVars, updated within about 0.1 s. FMA modes, FMS phase and ECAM warnings: not found yet (not in the HubHop presets checked) |🟡 First automatic taxi (test E1, 09/10/2026, C172, LFBP taxiway NE, 150 m at 5 kt, commands at 30 Hz, frames at 40 Hz): after the capture the aircraft held the layout centreline within a few centimetres (offset +0.03 m, heading error 0.1° at the end), without oscillation, then stopped 2 m after braking. The capture was too brutal (full lock at 0.7 kt, 16° of heading error): law softened; run 2 (same place) captured the 1.6 m offset with 10° of steering, overshot by 0.36 m, then converged to +0.04 m without oscillation. The aircraft is not on the paint there because the layout itself is off the paint (see D4). Taxiing along a route with turns, and with the A320: to do |

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
| C1 | Does the MobiFlight WASM module work under MSFS 2024 with a registered third-party client? | Test | ✅ Yes on the C172 and the Fenix A320 (test C1, 09/10/2026): ping, own client registered in about 44 ms, variables read, LVar written. But its LVar list stops at 1000 (hard limit in its source): on this PC GSX (510) and another add-on ("p42", 431) fill it, and no Fenix LVar appears. Also found: MSFS 2024 SimConnect reads and writes LVars natively and drives the cockpit input events (B: variables) without any module, on both aircraft |
| C2 | Licences: MobiFlight WASM module, HubHop data, FS Copilot, WASimCommander | Reading the licences | 🟡 MobiFlight module: MIT. HubHop: no licence stated, neither for the presets (site footer: "© HubHop and its contributors") nor for the website repository (https://github.com/MobiFlight/HubHop-Website, no LICENSE file), checked 09/10/2026: usable as a reference for variable names, not to be redistributed without asking. It holds 2,285 Fenix presets (Fenix switches and indicators are plain LVars: `S_…`, `I_…`, `E_…`, `N_…`) |
| C3 | Do we need FSUIPC? (not wanted) | Follows from C1 | 🟡 Not for LVars, input events and K events (test C1). H-events remain to be tried (through the MobiFlight module's calculator code if needed) |
| C4 | Do the values sent "on change" by the MobiFlight module arrive fast enough for a control loop? | Measured during C1 | ✅ C172 (test C1): a value changing at every frame arrives 40 times per second (every simulation frame, interval 25 ms, p95 26.3 ms); an LVar written through the module is read back in about 73 ms. Native SimConnect is faster: LVar read back in 5 to 19 ms, input event applied in 15 to 17 ms |

## D. Airports and taxiing

| # | Question | How to check | Status |
|---|---|---|---|
| D1 | Are runway hold-short points provided? | SDK 2024 docs | ✅ `TAXI_POINT.TYPE`: 2 HOLD_SHORT, 4 ILS_HOLD_SHORT (+ 5 and 6 without markings), with `ORIENTATION`. Seen at LFBP (test D2, 09/10/2026): 8 points, all type 5 (no markings), 147 to 167 m from the runway centreline, all on the main network. LFPG (default scenery): 147 points, types 5 and 6 (ILS), 71 to 310 m from the centreline, all on the main network |
| D2 | Are taxiway names available and linked to the paths? | SDK docs + test D2 | ✅ `NAME_INDEX` is the index in the `TAXI_NAME` list (0 = unnamed). LFBP (test D2, 09/10/2026): 16 names, each forming 1 or 2 continuous pieces; 12 of them also exist in OpenStreetMap (https://www.openstreetmap.org, ODbL) at the same place, 6 to 85 m apart for short taxiways (E, G, N, N1, N3, N5, S2). C, M and NG exist only in MSFS, B, BA and BC only in OSM. LFPG: 225 names, 217 of them in a single piece. On RUNWAY paths (type 2), `NAME_INDEX` is instead the index of the runway in the airport's runway list |
| D3 | What do the path types mean? | SDK 2024 docs | ✅ 1 TAXI, 2 RUNWAY, 3 PARKING, 4 PATH, 5 CLOSED, 6 VEHICLE, 7 ROAD, 8 PAINTEDLINE. Sceneries do not use them the same way: LFBP (France VFR) mostly TAXI, LFPG (Asobo) almost only PATH (4,667 PATH against 3 TAXI): both types must be treated as taxiways |
| D4 | Does the layout centreline match the painted line of the scenery (default and payware sceneries)? | Taxi manually and compare the aircraft position with the graph | ✅ LFBP, France VFR scenery (test D4, 09/10/2026, C172 taxied by hand for 8 min, 8,700 usable fixes on straight pieces): offset from the layout centreline median +0.11 m, 90 % within 0.50 m, max 1.05 m; by taxiway between −0.16 m (N) and +0.33 m (M). The layout matches the paint within what a pilot can hold. **Positions must be converted with WGS84 scales**: with a sphere of 111,320 m per degree, the same data showed up to 1.5 m of false offset about 1 km from the reference point (seen in test E1, confirmed with the GSX ground map). Default scenery not tested |
| D5 | Can we know the position of other aircraft on the ground (AI traffic, BeyondATC) to stop behind them? | `SimConnect_RequestDataOnSimObjectType` | ❓ |
| D6 | Can we retrieve the BeyondATC taxi clearance? | Study of an existing BeyondATC-related tool | ❓ |

## E. Control loops

| # | Question | How to check | Status |
|---|---|---|---|
| E1 | Is ground path following stable at the rate measured in A3 (C172, then A320)? | Test at Pau (LFBP) | 🟡 First automatic taxi (test E1, 09/10/2026, C172, LFBP taxiway NE, 150 m at 5 kt, commands at 30 Hz, frames at 40 Hz): the law holds the line within a few centimetres without oscillation, then stops 2 m after braking. Runs 1 and 2 used the spherical conversion: the aircraft, actually on the paint, was measured 1.5 m off, so the program steered 1.5 m right of the yellow line (Hugues saw it in the cockpit and on the GSX map). Conversion corrected to WGS84 (the start position then reads 0.07 to 0.12 m from the line); the capture was also softened after run 1 (full lock at 0.7 kt). Run 3 with the corrected conversion, a route with turns, and the A320: to do |
| E2 | A320 takeoff: centreline tracking and rotation with the sidestick until AP at 100 ft | Test | ❓ |
| E3 | Do we need a WASM module for latency? | Follows from A3, E1, E2 | ❓ (good sign: Pomax takes off and lands light aircraft from outside, under MSFS 2020) |
| E4 | Under which licence is Pomax's tutorial code? | Reading the repository | ❓ |

## Tests

| Test | Folder | Questions | State |
|---|---|---|---|
| D2 — complete taxi layout | `Experiments/D2-TaxiLayout/` | D1, D2, D3, axis convention, network connectivity | Run in MSFS at LFBP, France VFR payware scenery from the Community folder (9 October 2026). Confirmed: element types 2 START and 17 TAXI_NAME, BIAS_X = east / BIAS_Z = north (median 0.4 m against 297 m; 0.13 m with the WGS84 conversion adopted later), network connected to all parking spots and hold-short points. Run again at LFPG, default Asobo scenery (9 October 2026): same conclusions (axes: median 1.3 m against 544 m; 4,313-node main network, 374 parking spots all connected). `WIDTH`, `CENTER_LINE` and `WEIGHT` are transmitted (LFPG: widths 5 to 60 m, painted centreline on 3,252 paths, weight 500,000 lb) but depend on the scenery: LFBP gives 30 m, 0 and 0 everywhere. They cannot be relied on without a fallback |
| A1 — controlling the aircraft from outside | `Experiments/A1-AxisControl/` | A1, A2, A3, A4 (Cessna 172 at a parking spot, engine off) | Run in MSFS on 9 October 2026 (Asobo C172 at a parking spot, engine off): no SimConnect exception; results in the A1 to A4 rows. Second script `--rolling` run the same day (70 m on a taxiway, no emergency stop): settles A4. Also seen: `PARKING_BRAKE_SET` works both ways; at idle the C172 already rolls at 5.7 kt, so holding 4 kt needs the brakes (useful for E1); brakes at 50 % stop it from 3.5 kt in 1.4 s and 1.4 m |
| C1 — MobiFlight WASM module | `Experiments/C1-MobiFlight/` | C1, C4 (+ LVar list for B1 to B4), and what native SimConnect does without the module: LVars and input events (B: variables) | Run on the C172 on 9 October 2026 (results in the C1 and C4 rows). Also seen: `K:TOGGLE_BEACON_LIGHTS` (sent as `0 (>K:TOGGLE_BEACON_LIGHTS)` through the module) worked once out of four times on the 2024 C172, while the input event `LIGHTING_BEACON_1` worked every time with its echo. Cause not known (the parameter 0? the module?): one case only, not a general rule; to re-test with a key event sent directly by SimConnect. The C172 has 233 input events. Run on the Fenix A320 the same day (batteries and external power on): same behaviour for the module and native LVars; `K:TOGGLE_BEACON_LIGHTS` again worked only the first time (the beacon came on and stayed on). The Fenix exposes 220 input events: 180 for the audio panels, 18 for the FCU knobs (speed, heading, altitude, V/S and baro: turn, push, pull), the rest for radio panels and standby instruments; no autopilot button, light or switch. Its LVars must therefore be found elsewhere (B3, B4) |
| B1 — Fenix controls | `Experiments/B1-FenixControls/` | B1 to B4 (Fenix at a parking spot, powered, engines off; native SimConnect only) | Run in MSFS on 9 October 2026 (Fenix at a parking spot, powered, engines off): no SimConnect exception; results in the B1 to B4 rows. The program now puts the thrust levers back through their LVars |
| D4 — layout centrelines vs. painted lines | `Experiments/D4-CentrelineMatch/` | D4 (C172 taxied by hand at LFBP) | Run in MSFS on 9 October 2026; analysis replayed with WGS84 scales: results in the D4 row |
| E1 — first automatic taxi | `Experiments/E1-GroundTracking/` | E1 (C172 on a straight taxiway at LFBP, 5 kt, up to 150 m) | Runs 1 and 2 in MSFS on 9 October 2026 (spherical conversion, now corrected to WGS84; capture softened after run 1): results in the E1 row. Run 3 to do |

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
