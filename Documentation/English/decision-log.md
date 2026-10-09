# Decision log

*Français : [../French/decision-log.md](../French/decision-log.md)*

## Decisions made

| Date | Decision | Reason |
|---|---|---|
| 09/10/2026 | Project chosen: a **virtual pilot** for MSFS 2024 (the application flies the aircraft) | Strongest community request, with no existing solution (see [01](01-vision.md)) |
| 09/10/2026 | **Feasibility study before any application code** | First establish what is possible |
| 09/10/2026 | Code shared with the previous project (airport layouts, SimConnect): **extract a shared library** rather than depend on the old project | Both projects need it |
| 09/10/2026 | Taxiing: rely on the **Facilities API airport layouts** | Already read for ~85,000 airports, payware sceneries included |
| 09/10/2026 | Feasibility tests = **throw-away consoles** in `Experiments/<question>/` | Keep exploration separate from future code |
| 09/10/2026 | Documentation **in French and English**, same structure and same file names, always updated together | Open the project to other community developers |
| 09/10/2026 | **MIT licence** for the project | Simple and attractive for contributors; moving later to a more protective licence (GPL) remains possible, the reverse would be very hard. Consequence: no reuse of GPL or LGPL code, only ideas |
| 09/10/2026 | Project name: **FS24 Pilot Flying** (formerly "Virtual Pilot") | "Pilot Flying" = the pilot at the controls, which is what the software does; "FS24" places the simulator. Name to be revisited if the project follows a later MSFS version |
| 09/10/2026 | **Whole project in English**: code, identifiers, text, folder and file names. Only exception: the documentation also exists in full in French, in `Documentation/French/`, with the same file names as `Documentation/English/` | Project open to an international community |
| 09/10/2026 | Hosted on **GitHub**: https://github.com/Proxi64/FS24-Pilot-Flying | The usual platform of the MSFS community |
| 09/10/2026 | Wassette ruled out | Unrelated to MSFS WASM modules |
| 09/10/2026 | During the feasibility study, **commit on `main` at each useful step**, without asking; the maintainer pushes | Phase of information gathering and documentation |

Dates are written day/month/year.

## Proposals awaiting validation

- Generic engine + open aircraft profiles; first profile **Fenix A320**, second **Asobo A320neo**.
- Access to third-party aircraft variables: **native SimConnect** (LVars and input events, enough for the Fenix in tests C1 and B1); the **MobiFlight WASM module** only for what native SimConnect cannot do (H-events, calculator code); HubHop as a reference for variable names (no licence stated); FSUIPC only as an option. *(Updated on 9 October 2026 after tests C1 and B1; previously: MobiFlight WASM module + HubHop.)*
- Stack: .NET 10, native SimConnect through P/Invoke, WinUI 3 user interface; our own WASM module only if measurements require it.

## Questions to decide with contributors

- **Licence for aircraft profiles and data** (for example CC BY 4.0), separate from the code licence.
- Contributor licence agreement (CLA), or simply a commitment to publish under MIT?
- Organisation (issues, code review).
- Free, or a model that can fund the work?
