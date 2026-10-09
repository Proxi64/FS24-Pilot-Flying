# Experiment E1 — first automatic taxi on a straight centreline

Throw-away console of the feasibility study (see `Documentation/English/06-feasibility.md`, question E1;
French: `Documentation/French/06-feasibility.md`).

The C172 is stopped on a straight taxiway centreline. The program releases the brakes, taxis at 5 kt for up to 150 m
along the centreline of the airport layout (read by experiment D2), correcting the lateral offset and the heading
error at every frame through the rudder (which steers the nose wheel, test A1), then stops and sets the parking brake.

## Running it

Requirement: `results\layout-LFBP.json` written by experiment D2.

1. MSFS 2024: Asobo Cessna 172 at Pau (LFBP), **stopped on a straight taxiway with at least 70 m of straight line
   ahead** (taxiway NE is 332 m long), nose wheel on the yellow line, aligned with it, **engine at idle, parking brake
   set**. Joystick, throttle and pedals at rest.
2. From the repository root:
   ```
   dotnet run --project Experiments/E1-GroundTracking -- LFBP
   ```
   The console checks the aircraft, finds the line and says how far it will taxi; Enter starts.
3. Hands and feet off the controls. The console shows the speed, the offset, the heading error and the steer command
   live. **Esc = emergency stop** (idle, full brakes, parking brake).
4. Results in `results\`: `E1-LFBP-<date>-report.txt`, the frames `-frames.csv`, `-notes.txt`.

Replay: `dotnet run --project Experiments/E1-GroundTracking -- --analyse results\E1-LFBP-<date>-frames.csv`.
Without MSFS: `dotnet run --project Experiments/E1-GroundTracking -- --synthetic` (the same law on a kinematic model of
the C172 built from test A1: nose wheel 20° for a full command, yaw 0.2 s later, left drift −0.35°/s).

## Route mode: from the parking spot to a hold-short point

The program computes a route in the layout that follows a clearance (taxiway names in order; unnamed connecting
pieces allowed at three times their length; runways never), from the nearest parking spot to the first hold-short
point of the last taxiway of the clearance, which is never entered. It then follows it:

- lateral law: pure pursuit (aim at the route point max(5 m, 1.5 s × speed) ahead, wheelbase 2.1 m) + the same
  integral of the offset for the drift, ±20° (±10° for the first 3 s);
- speed: 5 kt, 3 kt when the route turns by more than 20° within the next 25 m, then down to a stop 4 m before the
  hold-short point (aircraft reference point);
- checks before starting: aircraft within 5 m of a parking spot, and the route leaving less than 90° from its heading
  (otherwise a pushback would be needed: refused);
- emergency stop: Esc, above 10 kt, more than 4 m off the route, route lost, **less than 1 m from the hold-short
  point**, or off the ground.

```
dotnet run --project Experiments/E1-GroundTracking -- LFBP --route C,NG,NW,N5
```
Simulation (no MSFS): `dotnet run --project Experiments/E1-GroundTracking -- --route-synthetic LFBP 0 C,NG,NW,N5`.
Results: `E1-route-LFBP-<date>-report.txt`, `-frames.csv`, `-notes.txt`, `-track.geojson` (route in blue, track in red).

Only the stand exits **ahead of the aircraft** are used: stands 8A/8B/8C at LFBP have two PARKING paths, one 2.5 to
15 m behind the aircraft (for a pushback) and one about 17 m ahead; the first version took the shorter one, behind,
and refused the start. From stand 8A (GATE_A 8, heading 35°, confirmed by Hugues): 17 m ahead, then a 180° right
turn in three steps along the apron, C 106 m, NG 111 m, NW 874 m, hold-short point 30 (entrance of N5 towards runway
31), 1,290 m in all; this matches the path Hugues drew. Simulated: offset ≤ 0.13 m on straight parts, ≤ 0.41 m in
turns, stop 4.5 m before the hold-short point.

## The law (straight-line mode)

- Point controlled: 2 m ahead of the aircraft reference point (towards the nose wheel).
- Nose wheel angle = −(heading error + atan(0.6 · offset / max(speed, 2 m/s)) + 0.5 · ∫offset) − 0.3 · yaw rate,
  limited to ±20° (full rudder), the cross-track term to ±10° and the steering to ±10° for the first 3 s of rolling
  (run 1 captured a 1.5 m offset with full lock at 0.7 kt). The integral term removes the steady offset of the drift.
- Speed held at 5 kt by the throttle; at idle the C172 already rolls at about 5.7 kt (test A1), so the brakes trim it.
- Consistency check: before starting, if the measured offset from the line is above 0.75 m, the console warns that
  the measurement is probably wrong (the aircraft is supposed to be on the yellow line) and waits for Esc or C.
- Emergency stop: Esc, above 10 kt, more than 4 m off the line, more than 25° off its axis, or off the ground. Whatever
  happens, the program ends with idle, full brakes, parking brake and rudder centred.

Runs 1 and 2 in MSFS 2024 on 9 October 2026 (taxiway NE at LFBP): see the E1 row of `06-feasibility.md`. They used a
spherical lat/lon conversion that put the aircraft 1.5 m off the line it was really on; positions are now converted
with WGS84 scales (`Layout.cs`). Run 3 (WGS84): offset RMS 0.01 m, max 0.07 m; the aircraft stayed on the yellow line.
