# 07 — Glossary

*Français : [../French/07-glossary.md](../French/07-glossary.md)*

| Term | Definition |
|---|---|
| **A\* / Dijkstra** | Classic shortest-path search algorithms on a graph; test E1 uses Dijkstra to compute the taxi route that follows a clearance. |
| **AI Pilot** | MSFS 2020 feature that flew the aircraft for the user; missing from MSFS 2024. |
| **Facilities API** | Part of SimConnect that provides facility data: airports, runways, taxiways, parking spots, frequencies, procedures. |
| **Autoland** | Automatic ILS landing, including flare and rollout, handled by the aircraft itself (e.g. A320). |
| **Autothrust (A/THR)** | Automatic thrust management to hold a speed. |
| **CommBus** | MSFS messaging mechanism between WASM modules, HTML/JS panels and external applications. |
| **Calculator code (RPN)** | Small programs in reverse Polish notation that the simulator can execute (`(L:VAR) 1 + (>L:VAR)`). |
| **Clearance (taxi)** | ATC instruction giving the taxiways to follow and where to stop ("taxi via C, NG, NW, hold short runway 31"). |
| **Cross-track error (offset)** | Lateral distance between the aircraft and the line it should follow; the main measure of the taxi tests. |
| **Wheelbase** | Distance between the nose wheel and the main gear; determines the offset to apply in turns. |
| **FCU / MCP** | Autopilot control panel (Airbus / Boeing). |
| **FMA** | Flight mode annunciator, the strip of active autopilot modes at the top of the primary flight display. |
| **FMS / MCDU** | Flight management system and its keyboard-display unit. |
| **GSX** | Commercial ground-services add-on (FSDreamTeam); reads the same airport layouts and places the aircraft on its stand. |
| **H-event** | Aircraft-specific event ("HTML event"), triggered by name. |
| **HubHop** | MobiFlight community database listing, aircraft by aircraft, the variables and events to use. |
| **ILS** | Instrument landing system (lateral and vertical guidance). |
| **Input event (B: variable)** | MSFS 2024 cockpit control (switch, knob…) that SimConnect can list, read, set and watch. |
| **LVar** | Local variable defined by an aircraft or a module (`L:NAME`); MSFS 2024 SimConnect reads and writes it natively (test C1). |
| **WASM module** | C/C++ program compiled to WebAssembly and loaded **inside** the simulator, with access to internal variables. |
| **Hold-short point** | Point where the aircraft must stop before entering a runway. |
| **Aircraft profile** | FS24 Pilot Flying file that describes an aircraft: characteristics and mapping between abstract actions and actual controls. |
| **Pure pursuit / Stanley** | Path-following algorithms from mobile robotics: aim at a point of the path a few metres ahead / correct the heading error and the lateral offset. Both used in test E1. |
| **Pushback** | Moving the aircraft backwards out of its stand, with a tug. |
| **SimConnect** | Official interface between MSFS and external programs. |
| **SimVar** | Simulator variable readable (and sometimes writable) through SimConnect (`PLANE ALTITUDE`, `GROUND VELOCITY`…). |
| **Tiller** | Nose-wheel steering handle, used on the ground on airliners. |
| **Vr / Vref** | Rotation speed at takeoff / reference approach speed. |
| **WGS84** | Reference ellipsoid of GPS and of the simulator; its scales are used to convert latitude / longitude to metres. |
