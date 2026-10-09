# Experiment A1 — controlling the aircraft from outside

Throw-away console of the feasibility study (see `Documentation/English/06-feasibility.md`, questions A1 to A4;
French: `Documentation/French/06-feasibility.md`).
With the aircraft **stopped at a parking spot, engine off**, it moves the controls through SimConnect key events and
records every simulation frame. Nothing can roll or take off; the worst case is a control left deflected, which the
program puts back to neutral when it stops (Esc at any time).

## Running it

1. Start MSFS 2024 and load a free flight: **Asobo Cessna 172**, at a parking spot, **engine off**. Joystick and
   throttle plugged in as usual.
2. In this folder:
   ```
   dotnet run
   ```
3. The console runs 15 short phases (5 to 20 s each). Before each one: Enter = run, S = skip, Esc = stop.
   Follow the instruction shown for each phase: "do not touch any control", except in two joystick phases
   ("move the joystick fore and aft").
4. Results in `results\`: `A1-<date>-report.txt`, plus the raw data (`-samples.csv`, `-sends.csv`, `-notes.txt`).

Replay the analysis of a saved run: `dotnet run -- --analyse results\A1-<date>-samples.csv`.
Test the analysis without MSFS: `dotnet run -- --synthetic` (fake data from a simple model).

Requirements: .NET 10 SDK, MSFS 2024 SDK (for the native `SimConnect.dll`, copied at build time).

## The phases

| Phases | Question | What is sent |
|---|---|---|
| `read-only` | A3 | Nothing for 20 s: reading rate and jitter |
| `elevator-steps`, `ailerons-steps`, `rudder-steps` | A1 | `AXIS_ELEVATOR_SET` / `AXIS_AILERONS_SET` / `AXIS_RUDDER_SET` at 30 Hz: 0, +0.5, −0.5, +1, −1, 0 |
| `throttle-steps` | A1 | `THROTTLE1_SET` at 30 Hz: 0, 0.5, 1, 0 |
| `left-brake-steps`, `right-brake-steps` | A1 | `AXIS_LEFT/RIGHT_BRAKE_SET` at 30 Hz: 0, 25, 50, 75, 100 %, 0 (the documented scale is non-linear) |
| `elevator-sine-30hz`, `elevator-sine-60hz` | A3 | Elevator sine wave sent at 30 then 60 Hz: delay between command and response |
| `rudder-steering` | A4 | Rudder sine wave: does the nose wheel turn (`GEAR CENTER STEER ANGLE`)? |
| `tiller-steering` | A4 | `AXIS_STEERING_SET` sine wave: does the tiller event work on this aircraft? |
| `joystick-hands-off` | A2 | Elevator held at +0.5, joystick not touched |
| `joystick-hands-on` | A2 | Elevator held at +0.5 while Hugues moves the joystick: who wins? |
| `joystick-stop-sending` | A2 | +0.5 for 2 s, then nothing: does the value stay? |
| `joystick-take-back` | A2 | Nothing sent, Hugues moves the joystick: does it take control back? |

## Second script: rolling slowly (A4)

At a standstill the nose wheel angle stays at 0 with the rudder and with the tiller, so A4 is repeated while rolling:

1. Asobo Cessna 172 on a **straight, clear taxiway with at least 150 m ahead**, engine running at idle,
   **parking brake set**, hands and feet off the controls.
2. In this folder: `dotnet run -- --rolling`, then Enter. The 7 phases follow each other without stopping (48 s,
   about 75 m):

| Phase | What the program does |
|---|---|
| `roll-release` | Toe brakes full, parking brake released (`PARKING_BRAKE_SET 0`) |
| `roll-accelerate` | Holds 4 kt with the throttle (PI, throttle capped at 45 %, brakes only above 5.5 kt) |
| `roll-rudder` | Rudder sine wave ±0.5, period 4 s, speed still held |
| `roll-straight-1` | Rudder centred |
| `roll-tiller` | `AXIS_STEERING_SET` sine wave ±0.5, period 4 s |
| `roll-straight-2` | Tiller centred |
| `roll-stop` | Throttle idle, brakes 50 %, then full brakes and parking brake once stopped |

**Emergency stop** (throttle idle, full brakes, parking brake, controls centred): Esc at any time, or automatically
above 8 kt, after a heading change of more than 60°, or if the aircraft leaves the ground. The same safe state is
sent when the program ends.

The report gives, for each phase, the nose wheel angle under three SimVars (`GEAR CENTER STEER ANGLE`,
`GEAR STEER ANGLE:0`, `CONTACT POINT STEER ANGLE:0`), the yaw rate and the heading change, so that we know both
which control steers and which SimVar tells it.

Event and SimVar names and ranges were checked against the MSFS 2024 SDK documentation (Key Events and SimVars
pages) on 9 October 2026.

Run in MSFS 2024 on 9 October 2026 (Asobo C172): see the A1 to A4 rows of `06-feasibility.md` for the results.
