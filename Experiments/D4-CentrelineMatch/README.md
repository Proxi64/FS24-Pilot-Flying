# Experiment D4 — layout centrelines vs. painted lines

Throw-away console of the feasibility study (see `Documentation/English/06-feasibility.md`, question D4;
French: `Documentation/French/06-feasibility.md`).

Automatic taxiing will follow the taxiway centrelines of the airport layout read through the Facilities API
(experiment D2). Do they match the yellow lines painted in the scenery? The pilot taxis by hand with the nose wheel on
the painted line; the console records the aircraft position at every frame and measures its lateral offset from the
layout centreline, on straight pieces only.

## Running it

Requirement: the layout file `results\layout-LFBP.json` written by experiment D2 (already there for LFBP and LFPG).

1. MSFS 2024: Asobo Cessna 172 at a parking spot at Pau (LFBP), engine running.
2. From the repository root:
   ```
   dotnet run --project Experiments/D4-CentrelineMatch -- LFBP
   ```
3. Taxi by hand for 5 to 10 minutes along as many taxiways as possible, **nose wheel on the yellow centreline**,
   at a steady 5 to 10 kt. An outside view from above helps to stay exactly on the line. The console shows the
   nearest taxiway and the current offset live.
4. Esc stops the recording and writes the results in `results\`: `D4-LFBP-<date>-report.txt`, the track
   `-track.csv`, and `-track.geojson` (track over the network, to open on https://geojson.io).

Replay: `dotnet run --project Experiments/D4-CentrelineMatch -- --analyse results\D4-LFBP-<date>-track.csv LFBP`.
Without MSFS: `dotnet run --project Experiments/D4-CentrelineMatch -- --synthetic LFBP`.

## What is measured

- Fixes used: on the ground, at least 2 kt, heading within 8° of a straight layout segment, more than 3 m from its
  ends, less than 15 m from it.
- Offset: lateral distance of the aircraft reference point (`PLANE LATITUDE` / `PLANE LONGITUDE`) from the layout
  centreline, + = right of it in the direction of travel. On a straight line, every point of the aircraft axis has
  the same lateral offset, so the position of the reference point along the aircraft does not matter.
- Report: overall, by taxiway name and by segment. A constant offset everywhere is the pilot's habit; a segment far
  from the others means the layout and the paint differ there.

Run in MSFS 2024 at LFBP on 9 October 2026: see the D4 row of `06-feasibility.md` for the results.
