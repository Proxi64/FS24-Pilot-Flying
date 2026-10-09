using System.Text;

namespace PilotFlying.Experiments.FenixControls;

internal static class Analysis
{
    /// <summary>For each action: what was sent, and every watched variable that changed before the next action.</summary>
    public static string Report(Log log, string source)
    {
        var e = log.Entries;
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);

        L($"=== Experiment B1 — Fenix A320 through native SimConnect — {source} ===");
        L($"Aircraft: {e.FirstOrDefault(x => x.Kind == "info" && x.Name == "aircraft")?.Text ?? "(unknown)"}");
        foreach (var x in e.Where(x => x.Kind == "info" && x.Name is "check" or "exception" or "stopped" or "quit" or "note"))
            L($"  {x.Name}: {x.Text}");
        L($"  Input events of the aircraft: {e.FirstOrDefault(x => x.Kind == "info" && x.Name == "input-events")?.Value ?? double.NaN:F0}");

        var acts = e.Where(x => x.Kind == "act").ToList();
        double ValueAt(string name, double t) => e.LastOrDefault(x => x.Kind == "v" && x.Name == name && x.T <= t)?.Value ?? double.NaN;

        // Baseline: every watched value at the end of the baseline.
        var baseEnd = acts.Count > 1 ? acts[1].T : e.LastOrDefault()?.T ?? 0;
        L();
        L("--- B4: values read at a standstill (end of the baseline) ---");
        var names = e.Where(x => x.Kind == "v").Select(x => x.Name).Distinct().ToList();
        foreach (var n in names) L($"  {n,-42} {ValueAt(n, baseEnd):G6}");
        var unchanged = names.Where(n => e.Count(x => x.Kind == "v" && x.Name == n) == 1).ToList();
        L($"  ({unchanged.Count} of {names.Count} variables never changed during the whole run)");

        // Reactions.
        L();
        L("--- Reactions to each action (B1, B2, B3): variables that changed before the next action ---");
        var summary = new List<string>();
        for (var i = 1; i < acts.Count - 1; i++)
        {
            var a = acts[i];
            var end = acts[i + 1].T;
            var sent = e.Where(x => x.Kind == "cmd" && x.T >= a.T && x.T < end).Select(x => x.Text).ToList();
            L($"  [{a.Text}] {a.Name}: sent {(sent.Count == 0 ? "nothing" : string.Join("; ", sent.Take(6)) + (sent.Count > 6 ? $"; … ({sent.Count})" : ""))}");
            var changed = 0;
            foreach (var n in names)
            {
                var before = ValueAt(n, a.T);
                var during = e.Where(x => x.Kind == "v" && x.Name == n && x.T > a.T && x.T < end).ToList();
                if (during.Count == 0) continue;
                // Numerical noise (e.g. GROUND VELOCITY around 1e-7 kt at a standstill) is not a reaction.
                if (during.Max(x => Math.Abs(x.Value - before)) < 1e-3) continue;
                changed++;
                var first = during[0];
                L($"      {n,-42} {before:G6} → {during[^1].Value:G6}   (range {during.Min(x => x.Value):G6} to {during.Max(x => x.Value):G6}, first change after {(first.T - a.T) * 1000:F0} ms, {during.Count} change(s))");
            }
            if (changed == 0) L("      (no watched variable changed)");
            summary.Add($"  {a.Name,-22} {changed,3} variable(s) changed");
        }
        L();
        L("--- Summary ---");
        foreach (var s in summary) L(s);
        L();
        L("Compare with what Hugues saw in the cockpit (lights, FCU windows, levers).");
        return r.ToString();
    }

    /// <summary>Fake run (some actions react, some do not), to test the report without MSFS.</summary>
    public static Log Synthetic()
    {
        var log = new Log();
        log.Add(0, "info", "aircraft", text: "SYNTHETIC DATA (not MSFS)");
        log.Add(0, "info", "check", text: "On ground 1, ground speed 0.0 kt, engines 0/0, main bus 28.0 V, parking brake 1");
        log.Add(0, "info", "input-events", 220);
        var initial = new Dictionary<string, double> { ["L:N_FCU_SPEED"] = 100, ["L:B_FCU_SPEED_DASHED"] = 1, ["ELECTRICAL MAIN BUS VOLTAGE"] = 28 };
        foreach (var n in Script.LVars.Select(l => "L:" + l).Concat(Script.SimVars.Select(s => s.Name)))
            log.Add(0.1, "v", n, initial.GetValueOrDefault(n));
        var t = 1.0;
        double speedCounter = 0, spd = 100;
        foreach (var a in Script.Actions)
        {
            log.Add(t, "act", a.Name, text: a.Question);
            switch (a.Name)
            {
                case "ap1-click":
                    log.Add(t, "cmd", text: "L:S_FCU_AP1 = 1"); log.Add(t + 0.15, "cmd", text: "L:S_FCU_AP1 = 2");
                    log.Add(t + 0.03, "v", "L:S_FCU_AP1", 1); log.Add(t + 0.18, "v", "L:S_FCU_AP1", 2);
                    break;
                case "spd-knob-plus-5":
                    for (var k = 0; k < 5; k++)
                    {
                        log.Add(t + k * 0.1, "cmd", text: $"L:E_FCU_SPEED = {k + 1}");
                        log.Add(t + k * 0.1 + 0.03, "v", "L:E_FCU_SPEED", k + 1);
                        log.Add(t + k * 0.1 + 0.05, "v", "L:N_FCU_SPEED", ++spd);
                    }
                    break;
                case "spd-knob-pull":
                    log.Add(t, "cmd", text: "L:S_FCU_SPEED = 1");
                    log.Add(t + 0.03, "v", "L:S_FCU_SPEED", ++speedCounter);
                    log.Add(t + 0.08, "v", "L:B_FCU_SPEED_DASHED", 0);
                    break;
                case "axis-elevator":
                    log.Add(t, "cmd", text: "AXIS_ELEVATOR_SET 8192");
                    log.Add(t + 0.03, "v", "YOKE Y POSITION", -0.5);
                    log.Add(t + 2, "cmd", text: "AXIS_ELEVATOR_SET -8192");
                    log.Add(t + 2.03, "v", "YOKE Y POSITION", 0.5);
                    log.Add(t + 4, "cmd", text: "AXIS_ELEVATOR_SET 0");
                    log.Add(t + 4.03, "v", "YOKE Y POSITION", 0);
                    break;
                case "tiller-lvar":
                    log.Add(t, "cmd", text: "L:N_FC_CAPT_TILLER = 30");
                    log.Add(t + 0.03, "v", "L:N_FC_CAPT_TILLER", 30);
                    log.Add(t + 0.06, "v", "STEER INPUT CONTROL", 0.4);
                    break;
            }
            t += 4.5;
        }
        log.Add(t, "act", "end");
        var sorted = log.Entries.OrderBy(x => x.T).ToList();
        log.Entries.Clear();
        log.Entries.AddRange(sorted);
        return log;
    }
}
