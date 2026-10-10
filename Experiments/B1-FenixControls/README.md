# Experiment B1 — commanding and reading the Fenix A320 from outside

Throw-away console of the feasibility study (see `Documentation/English/06-feasibility.md`, questions B1 to B4;
French: `Documentation/French/06-feasibility.md`).

> **Postponed**: no tests on add-ons for now (decision of 10 October 2026). The results of 9 October 2026 are kept.

Experiment C1 showed that MSFS 2024 SimConnect reads and writes LVars natively, and that the Fenix keeps its switches
and annunciators in plain LVars. This console uses **native SimConnect only** (LVars, key events, input events; no
WASM module) to act on the Fenix cockpit and log every watched variable that reacts. LVar names come from the HubHop
presets for the Fenix A320 (https://hubhop.mobiflight.com), used as a reference only (no licence stated).

## Running it

1. MSFS 2024: **Fenix A320 at a parking spot, batteries 1 and 2 ON and EXT PWR ON**, engines and hydraulics off.
   The program refuses to start if the main bus is not powered or if an engine is running.
2. In this folder:
   ```
   dotnet run
   ```
   then Enter. About 2 min 30 s; Esc stops at any time (axes centred, thrust levers back to their initial position).
3. **Watch the cockpit**: the console says, for each action, what to look at (FCU buttons and windows, autobrake,
   flaps lever, beacon switch, sidestick, thrust levers, tiller). Tell what you saw.
4. Results in `results\`: `B1-<date>-report.txt` and the raw log `B1-<date>-log.csv`.

Replay the analysis: `dotnet run -- --analyse results\B1-<date>-log.csv`. Without MSFS: `dotnet run -- --synthetic`.

## Actions

Each action is undone by the next one (second press, knob back, lever back).

| Question | Actions | How |
|---|---|---|
| B4 | `baseline` (5 s) | Read 47 Fenix LVars and 23 SimVars |
| B3 | AP1, A/THR, LOC, APPR, each pressed twice | Push button counters `S_FCU_…` (+1 on press, +1 on release) |
| B3 | SPD +5/−5, HDG +10/−10, ALT +5/−5 | Encoders `E_FCU_…` (+1/−1 per detent) |
| B3 | SPD knob pulled then pushed | `S_FCU_SPEED` +1 / −1 |
| B3 | SPD knob through the input event `FNX320_INPUT_KNOB_PUSHPULL_E_FCU_SPEED_KNOB` | `SimConnect_SetInputEvent` |
| B3 | Autobrake LO and MED, each pressed twice | `S_MIP_AUTOBRAKE_…` = 1 then 0 |
| B3 | Flaps lever to 1 and back; beacon switch and back | `S_FC_FLAPS`, `S_OH_EXT_LT_BEACON` |
| B1 | Sidestick pitch and roll, rudder: ±0.5 then 0; thrust levers: `THROTTLE_SET` 30 % then 0 (not idle on the Fenix: about 50 %), then back to their initial position through their LVars | `AXIS_ELEVATOR_SET`, `AXIS_AILERONS_SET`, `AXIS_RUDDER_SET`, `THROTTLE_SET` |
| B2 | Tiller ±0.5 then 0, then ±30 through the Fenix LVar | `AXIS_STEERING_SET`, `N_FC_CAPT_TILLER` |

The landing gear lever is never touched.

The report lists, for each action, the commands sent and every watched variable that changed before the next action.

Run in MSFS 2024 on 9 October 2026: see the B1 to B4 rows of `06-feasibility.md` for the results.
