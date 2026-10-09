# Experiment D2 — complete taxi layout of an airport

Throw-away console of the feasibility study (see `Documentation/English/06-feasibility.md`, questions D1 to D3;
French: `Documentation/French/06-feasibility.md`).
It changes nothing in MSFS: it reads one airport layout through the SimConnect Facilities API and writes a report.

## Running it

1. Start MSFS 2024 and wait for the **main menu** (no need to be in flight).
2. In this folder:
   ```
   dotnet run -- LFBP
   ```
   (another airport: `dotnet run -- LFBZ`; SDK installed somewhere other than `C:\MSFS 2024 SDK`:
   `dotnet run -p:MsfsSdk="D:\MSFS 2024 SDK" -- LFBP`)
3. Results in `results\`: `report-LFBP.txt`, `layout-LFBP.json` (raw data), `layout-LFBP.geojson`.

Requirements: .NET 10 SDK, MSFS 2024 SDK (for the native `SimConnect.dll`, copied at build time).

## What to look at

- The whole **report**.
- The **taxiway names**: do they match the airport chart, and is their "centre" in the right place?
  This tells whether `NAME_INDEX` really points into the `TAXI_NAME` list.
- Optional: open `layout-LFBP.geojson` on https://geojson.io (drag and drop) to see the network over a map
  (orange = taxiways, dark grey = runways, green = parking access, red / purple = hold-short / ILS hold points).

## What the experiment checks

| Question | What the report shows |
|---|---|
| D1 | Number of points by type; list of hold-short points with their taxiway and distance to the runway centreline |
| D2 | List of names; for each `NAME_INDEX`, number of paths, length, number of pieces (a real taxiway name forms a continuous track) |
| D3 | Number of paths by type, painted centreline, width, weight limit |
| Axes | Distance of runway paths to the runway centreline under both possible BIAS_X / BIAS_Z conventions |
| Routing | Is the aircraft network a single piece? Are parking spots and hold-short points connected? |

The element numbers for START (2) and TAXI_NAME (17) are assumed; if they are wrong, the report lists them under
"Unexpected element types".

Tested on 9 October 2026 outside the simulator only (build + analysis on synthetic data): the SimConnect part has
not run yet.
