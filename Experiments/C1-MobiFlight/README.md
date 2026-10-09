# Experiment C1 — the MobiFlight WASM module as a third-party client

Throw-away console of the feasibility study (see `Documentation/English/06-feasibility.md`, questions C1 and C4;
French: `Documentation/French/06-feasibility.md`).

Third-party aircraft (Fenix, PMDG…) keep their state in local variables (LVars) and react to their own events, which
SimConnect cannot reach directly. The MobiFlight WASM module (https://github.com/MobiFlight/MobiFlight-WASM-Module,
MIT) runs inside MSFS and executes calculator code for external clients. This console uses its protocol (ideas only,
no code copied): it registers its own client, `FS24PilotFlying`, reads variables, writes an LVar, runs an event and
lists the aircraft's LVars.

## Running it

Requirement: the MobiFlight module (`mobiflight-event-module`) in the Community folder, activated.

1. Start MSFS 2024 and load a flight, aircraft at a parking spot (any state, engine off is fine).
2. In this folder:
   ```
   dotnet run
   ```
3. About 30 s. The only visible effect: the **beacon light blinks 4 times** and ends in its initial state.
4. Results in `results\`: `C1-<date>-report.txt`, the aircraft's LVar list `C1-<date>-lvars.txt`, the raw log
   `C1-<date>-log.csv`.

Run it once on the Asobo C172, then once on the Fenix A320 (its LVar list prepares questions B1 to B4).

Replay the analysis: `dotnet run -- --analyse results\C1-<date>-log.csv`. Without MSFS: `dotnet run -- --synthetic`.

## Steps

| Step | What it checks |
|---|---|
| 1. `MF.Ping` / `MF.Version.Get` on the default channel `MobiFlight.Command` / `.Response` | The module is loaded and answers (C1) |
| 2. `MF.Clients.Add.FS24PilotFlying` | Our own client gets its own channels (C1) |
| 3. `MF.SimVars.Add.(…)` × 4 | Variables are sent back as floats, one per 4 bytes of `FS24PilotFlying.LVars` |
| 4. 10 s without action | Update rate of `(E:SIMULATION TIME,seconds)` through the module vs. simulation frames (C4) |
| 5. `MF.SimVars.Set.1 (>L:FS24PF_TEST)` then 0 | Write an LVar and read it back: delay (C4) |
| 6. `MF.SimVars.Set.0 (>K:TOGGLE_BEACON_LIGHTS)` × 4 | Run an event; delay seen directly by SimConnect and through the module (C4) |
| 7. `MF.LVars.List` | All LVars of the loaded aircraft, saved to a file |

SimConnect constants checked in the MSFS 2024 SDK header (`SimConnect.h`) on 9 October 2026; module protocol read in
its source (version 1.0.1, the latest release, also the installed one).

Tested on 9 October 2026 outside the simulator only (build + report on synthetic data): the SimConnect part has not
run yet.
