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

## The law

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
with WGS84 scales (`Layout.cs`).
