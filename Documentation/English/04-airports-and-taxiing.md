# 04 — Airports and taxiing

*Français : [../French/04-airports-and-taxiing.md](../French/04-airports-and-taxiing.md)*

Automatic taxiing builds on good news: **the simulator provides the complete layout of every airport**, and this layout is already read and stored by a previous project of the initiator (a mission generator for MSFS 2024, in C# .NET 10).

## The SimConnect Facilities API

This is the API GSX uses. With `SimConnect_AddToFacilityDefinition` and `SimConnect_RequestFacilityData`, you get the airport layout **as MSFS loads it**, active payware sceneries included.

### What has been validated in practice (September 2026)

- It works **from the main menu**, without being in flight.
- **List of all airports** (`SimConnect_RequestAllFacilities`): 85,791 airports received in 0.1 s. The response uses `SIMCONNECT_RECV_AIRPORT_LIST` (message no. 18, 36-byte entries), not `FACILITY_MINIMAL_LIST` as one might expect.
- **Reading all layouts**: 85,764 layouts in 7.8 minutes, 32 simultaneous requests, local JSON copy (one file per airport), automatic re-read when the format changes.
- **Payware scenery taken into account**: Biarritz (LFBZ) with the Flightbeam scenery returns its 27 parking spots.
- **Axes**: `BIAS_X` = **east**, `BIAS_Z` = **north**, in metres from the airport reference point (verified on site). Conversion to latitude / longitude: **with WGS84 metres per degree at the reference latitude** (meridian and parallel radii of curvature). The spherical `111,320 m per degree` of the previous project is about 1.5 m off 1 km from the reference point (corrected on 9 October 2026, tests D4 and E1). The documentation speaks of "longitudinal / latitudinal" axes; test D2 confirmed east / north.
- Data arrives **packed, without alignment** (read field by field in definition order); each element arrives in a `SIMCONNECT_RECV_FACILITY_DATA` message (no. 28) whose `Type` field gives its kind (0 airport, 1 runway, 14 taxi point, 15 parking, 16 taxi path); the end is signalled by `FACILITY_DATA_END` (no. 29).

### What a stored layout contains

Reference point and altitude; **runways** (centre, heading, length, width, surface, designations); **parking spots** (type, name, number, heading, radius, position); helipads; **taxi paths** (end points, width, type); radio frequencies.

Example of Pau (LFBP), as stored by the previous project: 2 runways, 22 parking spots, 489 taxi paths, 469 nodes (read again with every field by test D2, France VFR scenery, 9 October 2026: 509 paths, 458 taxi points, 16 taxiway names, 8 hold-short points). The network is well connected (383 pass-through nodes, 60 intersections, 26 dead ends) and curves are already finely split (segments from 2.5 to 729 m, median 12 m).

## What the 2024 SDK documentation says (read on 9 October 2026)

Source: [SimConnect_AddToFacilityDefinition](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/API_Reference/Facilities/SimConnect_AddToFacilityDefinition.htm).

### TAXI_POINT: taxi points

| Field | Values |
|---|---|
| `TYPE` | 0 NONE, 1 NORMAL, **2 HOLD_SHORT**, **4 ILS_HOLD_SHORT**, 5 HOLD_SHORT_NO_DRAW, 6 ILS_HOLD_SHORT_NO_DRAW (no 3) |
| `ORIENTATION` | 0 FORWARD, 1 REVERSE (direction of the hold-short point) |
| `BIAS_X`, `BIAS_Z` | position in metres |

→ **Runway hold-short points** are provided, including ILS holds (CAT II/III).

### TAXI_PATH: taxi paths

| Field | Content |
|---|---|
| `TYPE` | 0 NONE, **1 TAXI**, **2 RUNWAY**, **3 PARKING**, **4 PATH**, 5 CLOSED, 6 VEHICLE, 7 ROAD, 8 PAINTEDLINE → for an aircraft: 1 to 4; exclude 5, 6, 7 |
| `WIDTH`, `LEFT_HALF_WIDTH`, `RIGHT_HALF_WIDTH` | width (asymmetric taxiways included) |
| `WEIGHT` | weight limit (lb): rule out taxiways that are too weak |
| `RUNWAY_NUMBER`, `RUNWAY_DESIGNATOR` | runway the segment belongs to (1-36, then N, NE…; L, R, C…) |
| `CENTER_LINE`, `CENTER_LINE_LIGHTED` | painted centreline present, lit |
| `LEFT_EDGE`, `RIGHT_EDGE` (+ lighting) | edge markings |
| `START`, `END` | index of the start and end point (for type 3, `END` is the parking spot index) |
| `NAME_INDEX` | index in the `TAXI_NAME` list (0 = unnamed); on RUNWAY paths, index of the runway instead (test D2) |

### Other useful elements

- **TAXI_NAME**: `NAME` (32 characters); their count is given by `AIRPORT.N_TAXI_NAMES`.
- **START**: runway start positions (position, heading, number, designator, type) → line-up point.
- **RUNWAY**: besides geometry, `PRIMARY/SECONDARY_ILS_ICAO` (ILS for each direction, hence autoland possible or not), displaced thresholds, overruns, directions open for takeoff and landing.
- **TAXI_PARKING**: besides the fields already read, `SUFFIX`, `ORIENTATION`, `TAXI_POINT_TYPE` and assigned airlines (`AIRLINE`).

## What remains to be added to the existing reader

1. Keep the point `TYPE` (it was read then ignored) and add `ORIENTATION`.
2. Keep the `START` / `END` indices (they were converted to coordinates then lost).
3. Stop ignoring **runway** segments (type 2), essential for runway entry and crossings.
4. Add `NAME_INDEX`, `RUNWAY_NUMBER`, `RUNWAY_DESIGNATOR`, `CENTER_LINE`, `WEIGHT` and the half-widths.
5. Add the **TAXI_NAME** and **START** lists, and runway ILS.
6. Move to a new file format; the existing mechanism then re-reads all layouts.

Test `Experiments/D2-TaxiLayout` already reads all of this for one airport and automatically checks the uncertain points (see [06](06-feasibility.md)).

## The taxi chain

```
airport layout (Facilities API; positions converted with WGS84 scales)
  → graph (nodes = taxi points and parking spots; edges = paths of type 1 to 4)
  → route following the ATC clearance: parking spot (exit ahead of the aircraft) → runway hold-short point
  → path following: pure pursuit + drift correction (main gear on the line for large aircraft)
  → speed regulation: cruise speed, slower before turns, braking
  → stop before the hold-short point, wait for clearance
```

**Validated with the C172 at Pau (test E1, 9 October 2026)**: from stand 8A to the runway 31 hold-short point via C, NG, NW (1.2 km): offset ≤ 0.11 m on straight parts and ≤ 0.76 m in turns, stop 4.8 m before the hold-short point. The layout centrelines match the painted lines within about 0.5 m (test D4). Not done yet: the main gear in turns (large aircraft), the A320, other aircraft on the ground.

And the other way round, after landing: runway exit → arrival parking spot chosen according to aircraft size (the previous project can already choose a suitable spot).
