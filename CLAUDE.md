# FS24 Pilot Flying — instructions for Claude Code

This file is read automatically by Claude Code at the start of every session in this repository.
Personal, machine-specific instructions go in `CLAUDE.local.md` (not committed).

## The project

FS24 Pilot Flying is an open-source (MIT) external Windows application that **flies the aircraft** in
Microsoft Flight Simulator 2024: taxi, take-off, flight, landing. It fills the gap left by the "AI Pilot" of
MSFS 2020, which no longer exists in MSFS 2024 and is especially missed by simmers with disabilities.

**Current phase: feasibility study.** No application code yet. We first establish what is possible, through
documentation reading and small throw-away experiments.

Read first, in this order:
1. `README.md` (overview)
2. `Documentation/English/06-feasibility.md` (open questions, their status, the experiments)
3. `Documentation/English/decision-log.md` (what is decided and what is only proposed)
4. The other documents in `Documentation/English/` when the task needs them.

## Rules (mandatory)

### Language
- **Everything in the repository is in English**: code, identifiers, comments, console output, file and folder
  names, commit messages.
- **Only exception: documentation.** Every document in `Documentation/English/` has a full French counterpart in
  `Documentation/French/` with **the same file name**. Any change to one must be applied to the other in the same
  task. Each page links to its translation on line 3.

### Documentation
- Feasibility questions are identified (A1, B3, D2…) in `06-feasibility.md`; update their status
  (✅ confirmed · 🟡 partly · ❓ to be checked · ❌ impossible or dropped) when a test or a reading settles them.
- Every decision goes into `decision-log.md` (both languages), with its date (day/month/year) and its reason.
  Proposals stay under "Proposals awaiting validation" until the maintainer validates them.
- Facts about MSFS or the SDK must be checked against the **official MSFS 2024 SDK documentation**
  (https://docs.flightsimulator.com/msfs2024/html/…). The community `msfs-sdk` MCP server is approximate and may
  return MSFS 2020 pages: use it to find a page, then verify the 2024 version.
- Cite sources (URLs) for any claim about third-party tools, licences or community needs.

### Experiments
- Feasibility experiments are **throw-away consoles** in `Experiments/<question id>-<Name>/`
  (e.g. `Experiments/D2-TaxiLayout/`), each with its own `README.md` explaining how to run it and what it checks.
- Experiments that need the simulator cannot be validated without it: build them, test the analysis offline on
  synthetic or saved data, then ask the maintainer to run them in MSFS and report back. Never claim a SimConnect
  behaviour is confirmed until it has run in the simulator.
- Results go into `results/` (git-ignored).
- **Consistency checks**: each experiment compares its measurements with what is expected (e.g. offset ≈ 0 when the
  aircraft is placed on the line) and stops or asks for confirmation when they disagree, instead of going on silently.
- **No conclusion from a single interpretation**: write the observation, the possible explanations and the test that
  separates them; only what is shown goes into the documentation. When Hugues's observation disagrees with a
  measurement, check our own computation (conversions, units, signs, reference points) first.
- **Safe state**: whatever an experiment sends to the aircraft is undone by a state that has been checked, not assumed.

### Code
- .NET 10, C#, x64. SimConnect through **P/Invoke on the native `SimConnect.dll`** (the SDK's managed DLL targets
  .NET Framework 4.6.1 and does not load in .NET 10). Structures are `#pragma pack(1)`.
- `SimConnect.dll` is copied at build time from the developer's own MSFS 2024 SDK (`MsfsSdk` MSBuild property,
  default `C:\MSFS 2024 SDK`). **Never commit it.**
- Licence: MIT. **Do not copy GPL or LGPL code** (e.g. MSFS-Tools, node-simconnect): take ideas only. Check the
  licence of any other code before reusing it, and record it in `05-ecosystem.md`.
- Ask the maintainer before any architecture or scope decision; propose one recommendation, not a catalogue.

## Known SimConnect / MSFS 2024 facts and pitfalls

Every fact carries its source: **[SDK]** official 2024 documentation or `SimConnect.h`, **[test X]** an experiment
of this repository (date in `06-feasibility.md`), **[old]** the previous project only, *not re-verified*: check it
before relying on it. Do not add a fact without a source.

- **Facilities API** works from the main menu and returns the layout as MSFS loads it, payware sceneries included;
  data arrives packed, field by field in definition order [test D2]. Messages `FACILITY_DATA` 28, `FACILITY_DATA_END`
  29; element types 0 airport, 1 runway, 2 start, 14 taxi point, 15 parking, 16 taxi path, 17 taxi name [SDK header,
  test D2].
- `TAXI_PATH.TYPE`: 1 TAXI, 2 RUNWAY, 3 PARKING (END = parking index), 4 PATH, 5 CLOSED, 6 VEHICLE, 7 ROAD,
  8 PAINTEDLINE. `TAXI_POINT.TYPE`: 1 NORMAL, 2 HOLD_SHORT, 4 ILS_HOLD_SHORT, 5/6 the same without markings;
  `ORIENTATION` 0 forward, 1 reverse [SDK]. Parking `HEADING` is true [SDK]; runway `HEADING` behaves as true
  [test D2: runway paths 0.13 m from the centreline computed with it].
- `TAXI_PATH.NAME_INDEX` = index in the `TAXI_NAME` list (0 = unnamed), but on RUNWAY paths = index of the runway.
  Sceneries differ: Asobo uses PATH (4) for taxiways, France VFR uses TAXI (1); `WIDTH` / `CENTER_LINE` / `WEIGHT`
  may be constant placeholders (LFBP: 30 m / 0 / 0) [test D2, LFBP and LFPG].
- `BIAS_X` = east, `BIAS_Z` = north, metres from the airport reference point [test D2]. Convert with **WGS84** metres
  per degree at the reference latitude: `mLat = a(1−e²)/w^1.5 · π/180`, `mLon = a/√w · cos φ0 · π/180`,
  `w = 1 − e² sin² φ0` (a = 6378137, e² = 0.00669438) [tests D4, E1, GSX ground map]. The sphere `111320` m/degree
  (previous project) gives ~1.5 m of lateral error 1 km from the reference point.
- Layout centrelines match the painted lines within ~0.5 m at LFBP (France VFR) [test D4].
- An aircraft on a parking spot is not always on its point: at LFBP 8A (radius 14 m) the C172 stood on the stand axis
  14.6 m ahead of it, placed by GSX for the nose wheel of a larger aircraft [test E1 route, Hugues]. A stand can have a
  PARKING path behind the aircraft (pushback) and one ahead [test E1 route, LFBP 8A to 8C].
- `SimConnect_RequestAllFacilities` (airports) answers with `SIMCONNECT_RECV_AIRPORT_LIST` (message 18) [SDK header],
  entries of 36 bytes: Ident char[9], Region char[3], 3 doubles [SDK header].
- User aircraft object ID 0: name it `SIMCONNECT_OBJECT_ID_USER_AIRCRAFT`; `SIMCONNECT_OBJECT_ID_USER` (same value) is
  marked deprecated [SDK header].
- Axis events on the C172 [test A1]: a positive `AXIS_ELEVATOR/AILERONS/RUDDER_SET` value gives a negative
  `*_POSITION` (nose down, roll left, yaw left). `AXIS_*_BRAKE_SET` is non-linear (-16383 → 5 %, 0 → 31 %,
  +16383 → 100 %). A moved joystick overrides the program between two sends; the last value sent stays until something
  else sends. Nose wheel: turns only while rolling; rudder and `AXIS_STEERING_SET` both steer it, 20° per full command,
  positive = left (tiller: opposite of the docs); read it with `GEAR CENTER STEER ANGLE`, not
  `CONTACT POINT STEER ANGLE:0`. `ROTATION VELOCITY BODY Y` positive = turning right. At idle the C172 rolls at ~5.7 kt
  and drifts left with the controls centred.
- MSFS 2024 SimConnect reads/writes LVars natively ("L:NAME" in `SimConnect_AddToDataDefinition`, FLOAT64) [SDK,
  test C1] and drives cockpit input events (B: vars: `SimConnect_EnumerateInputEvents` / `SetInputEvent` /
  `SubscribeInputEvent`; RECV ids 34–37) [SDK header, test C1]. On the 2024 C172, `0 (>K:TOGGLE_BEACON_LIGHTS)`
  through the MobiFlight module worked once in four, the input event every time; cause unknown [test C1].
- MobiFlight WASM module (MIT, v1.0.1): works with a third-party client; lists at most 1000 LVars (hard limit in its
  source; on Hugues's PC GSX and another add-on fill them) [test C1, source code].
- Fenix A320 [test B1]: cockpit = plain LVars, written natively. Push buttons are counters (+1 press, +1 release),
  knobs are encoders `E_FCU_*` (+1/−1 per detent), FCU windows `N_FCU_*`, lights `I_*` (names from HubHop, no
  licence: reference only). 220 input events (audio panels, FCU knobs, radio, standby), no AP buttons [test C1].
  `THROTTLE_SET 0` is NOT idle (levers left at ~50 %; mapping not understood): put the levers back through
  `A_FC_THROTTLE_LEFT/RIGHT_INPUT` (2 at the start, presumably idle). Tiller: `N_FC_CAPT_TILLER` (±75) or
  `AXIS_STEERING_SET`. Many buttons do nothing on the ground with engines off: normal aircraft behaviour.
- Camera states [SDK]: 2 cockpit, 3 chase drone, 4 fixed on plane, 5 environment, 6 gameplay, 7 showcase, 8 drone
  plane, 24 first view, 26 third view, 29 idle (the doc stops at 33). "34–35 = menus / loading" [old].
- [old] `SimConnect_FlightPlanLoad` has no effect in MSFS 2024; a hand-written flight plan inside a `.FLT` file
  crashed MSFS. `PLANE TOUCHDOWN *` SimVars give the exact touchdown even when read at 1 Hz. SimConnect weather
  functions are deprecated in MSFS 2024 (they are still declared in the 2024 header).
- Third-party aircraft access: proposed (not decided) through the MobiFlight WASM module and HubHop; tests C1 and B1
  show native SimConnect may be enough for LVars and input events; FSUIPC only as an option.
