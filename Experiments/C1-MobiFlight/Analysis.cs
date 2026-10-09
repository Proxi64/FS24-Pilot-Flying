using System.Text;

namespace PilotFlying.Experiments.MobiFlight;

internal static class Analysis
{
    public static string Report(Log log, string source, IReadOnlyList<string> lvars)
    {
        var e = log.Entries;
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);
        bool Has(Func<Entry, bool> p) => e.Any(p);
        Entry? First(Func<Entry, bool> p) => e.FirstOrDefault(p);

        L($"=== Experiment C1 — MobiFlight WASM module as a third-party client — {source} ===");
        L($"Aircraft: {First(x => x.Kind == "info" && x.Name == "aircraft")?.Text ?? "(unknown)"}");
        foreach (var x in e.Where(x => x.Kind == "info" && x.Name is "failure" or "exception" or "quit")) L($"  {x.Name}: {x.Text}");

        // ------------------------------------------------------------------ C1: protocol
        L();
        L("--- C1: does the module answer a third-party client? ---");
        L($"  Ping on the default channel (MF.Ping → MF.Pong): {(Has(x => x.Kind == "resp" && x.Name == "default" && x.Text == "MF.Pong") ? "yes" : "NO")}");
        L($"  Module version: {First(x => x.Kind == "resp" && x.Text.StartsWith("MF.Version."))?.Text["MF.Version.".Length..] ?? "(no answer)"}");
        var finished = First(x => x.Kind == "resp" && x.Text == $"MF.Clients.Add.{Probe.ClientName}.Finished");
        var added = First(x => x.Kind == "cmd" && x.Text == $"MF.Clients.Add.{Probe.ClientName}");
        L($"  Own client \"{Probe.ClientName}\" registered: "
          + (finished is null ? "NO" : $"yes, confirmed in {(finished.T - added!.T) * 1000:F0} ms"));
        L($"  Ping on our own channel: {(Has(x => x.Kind == "resp" && x.Name == "client" && x.Text == "MF.Pong") ? "yes" : "NO")}");
        L("  Variables registered (first value received):");
        foreach (var v in Probe.Vars)
        {
            var first = First(x => x.Kind == "mf" && x.Name == v);
            L($"    {v,-30} {(first is null ? "no value received" : $"{first.Value:G6}")}");
        }

        // ------------------------------------------------------------------ C4: rate and delay
        L();
        L("--- C4: how fast do the values arrive? ---");
        var start = First(x => x.Kind == "mark" && x.Name == "rate-start")?.T;
        var end = First(x => x.Kind == "mark" && x.Name == "rate-end")?.T;
        if (start is { } t0 && end is { } t1)
        {
            var mf = e.Where(x => x.Kind == "mf" && x.Name == Probe.Vars[0] && x.T >= t0 && x.T <= t1).ToList();
            var frames = e.Where(x => x.Kind == "direct" && x.Name == Probe.Direct[0].Name && x.T >= t0 && x.T <= t1).Count();
            L($"  Simulation frames seen directly by SimConnect: {frames / (t1 - t0):F1} per second");
            if (mf.Count >= 3)
            {
                var dt = mf.Zip(mf.Skip(1), (a, b) => b.T - a.T).OrderBy(x => x).ToList();
                var simDt = mf.Zip(mf.Skip(1), (a, b) => b.Value - a.Value).Where(x => x > 0).OrderBy(x => x).ToList();
                L($"  {Probe.Vars[0]} sent by the module: {mf.Count / (t1 - t0):F1} updates per second, interval median "
                  + $"{dt[dt.Count / 2] * 1000:F1} ms, p95 {dt[(int)(dt.Count * 0.95)] * 1000:F1} ms, max {dt[^1] * 1000:F1} ms"
                  + (simDt.Count > 0 ? $"; simulation time step median {simDt[simDt.Count / 2] * 1000:F1} ms" : ""));
            }
            else L($"  {Probe.Vars[0]}: {mf.Count} update(s) only (does the variable change at every frame?)");
        }
        else L("  (rate step not run)");

        // Write then read back our own LVar.
        foreach (var value in new[] { 1.0, 0.0 })
        {
            var cmd = First(x => x.Kind == "cmd" && x.Text == $"MF.SimVars.Set.{value} (>L:FS24PF_TEST)");
            if (cmd is null) continue;
            var back = First(x => x.Kind == "mf" && x.Name == "(L:FS24PF_TEST)" && x.T > cmd.T && x.Value == value);
            L($"  L:FS24PF_TEST set to {value}: " + (back is null ? "NOT read back" : $"read back after {(back.T - cmd.T) * 1000:F0} ms"));
        }

        // Beacon toggles: delay seen directly and through the module.
        var toggles = e.Where(x => x.Kind == "cmd" && x.Text.Contains("TOGGLE_BEACON_LIGHTS")).ToList();
        for (var i = 0; i < toggles.Count; i++)
        {
            var c = toggles[i];
            var next = i + 1 < toggles.Count ? toggles[i + 1].T : double.MaxValue;
            double? before = e.LastOrDefault(x => x.Kind == "direct" && x.Name == "LIGHT BEACON" && x.T <= c.T)?.Value;
            var direct = First(x => x.Kind == "direct" && x.Name == "LIGHT BEACON" && x.T > c.T && x.T < next && x.Value != before);
            var viaMf = First(x => x.Kind == "mf" && x.Name == "(A:LIGHT BEACON,Bool)" && x.T > c.T && x.T < next);
            L($"  Beacon toggle {i + 1}: {before} → "
              + (direct is null ? "NO CHANGE (direct SimVar)" : $"{direct.Value} seen directly after {(direct.T - c.T) * 1000:F0} ms")
              + (viaMf is null ? ", not seen through the module" : $", through the module after {(viaMf.T - c.T) * 1000:F0} ms"));
        }

        // ------------------------------------------------------------------ LVars
        L();
        L("--- LVars of the loaded aircraft (MF.LVars.List) ---");
        L($"  {lvars.Count} LVars" + (lvars.Count > 0 ? ". First ones: " + string.Join(", ", lvars.Take(15)) : ""));
        L("  The full list is saved next to this report (-lvars.txt).");

        // ------------------------------------------------------------------ native SimConnect
        L();
        L("--- Native SimConnect (MSFS 2024), without the module ---");
        var mfWrite = First(x => x.Kind == "cmd" && x.Text == "MF.SimVars.Set.1 (>L:FS24PF_TEST)");
        if (mfWrite is not null)
        {
            var seen = First(x => x.Kind == "native" && x.Name == "L:FS24PF_TEST" && x.T > mfWrite.T && x.Value == 1);
            L("  L:FS24PF_TEST written through the module: " + (seen is null ? "NOT seen by native SimConnect" : $"seen natively after {(seen.T - mfWrite.T) * 1000:F0} ms"));
        }
        foreach (var c in e.Where(x => x.Kind == "cmd" && x.Text.StartsWith("SetDataOnSimObject ")))
        {
            var value = c.Text.EndsWith("=1") ? 1.0 : 0.0;
            var native = First(x => x.Kind == "native" && x.Name == "L:FS24PF_NATIVE" && x.T > c.T && x.Value == value);
            var viaMf = First(x => x.Kind == "mf" && x.Name == "(L:FS24PF_NATIVE)" && x.T > c.T && x.Value == value);
            L($"  {c.Text}: " + (native is null ? "NOT read back natively" : $"read back natively after {(native.T - c.T) * 1000:F0} ms")
              + (viaMf is null ? ", not seen through the module" : $", through the module after {(viaMf.T - c.T) * 1000:F0} ms"));
        }
        var count = First(x => x.Kind == "info" && x.Name == "input-events");
        L($"  Input events of the aircraft (SimConnect_EnumerateInputEvents): {(count is null ? "step not run" : $"{count.Value:F0}")}"
          + " (full list in -inputevents.txt)");
        var candidates = First(x => x.Kind == "info" && x.Name == "beacon-candidates");
        if (candidates is not null) L($"  Beacon input events: {(candidates.Text.Length == 0 ? "none" : candidates.Text)}");
        foreach (var skip in e.Where(x => x.Kind == "info" && x.Name == "input-event-skip")) L("  " + skip.Text);
        foreach (var c in e.Where(x => x.Kind == "cmd" && x.Text.StartsWith("SetInputEvent ")))
        {
            double? before = e.LastOrDefault(x => x.Kind == "direct" && x.Name == "LIGHT BEACON" && x.T <= c.T)?.Value;
            var direct = First(x => x.Kind == "direct" && x.Name == "LIGHT BEACON" && x.T > c.T && x.T < c.T + 1.4 && x.Value != before);
            var name = c.Text["SetInputEvent ".Length..c.Text.IndexOf('=')];
            var echo = First(x => x.Kind == "ie" && x.Name == name && x.T > c.T && x.T < c.T + 1.4);
            L($"  {c.Text}: LIGHT BEACON {before} → "
              + (direct is null ? "NO CHANGE" : $"{direct.Value} after {(direct.T - c.T) * 1000:F0} ms")
              + (echo is null ? ", no subscription echo" : $", subscription echo {echo.Value} after {(echo.T - c.T) * 1000:F0} ms"));
        }
        return r.ToString();
    }

    /// <summary>Fake run (module answering, 41 Hz frames), to test the report without MSFS.</summary>
    public static (Log Log, List<string> LVars) Synthetic()
    {
        var log = new Log();
        var rng = new Random(1);
        log.Add(0, "info", "aircraft", text: "SYNTHETIC DATA (not MSFS)");
        log.Add(0.5, "cmd", "default", text: "MF.Ping");
        log.Add(0.53, "resp", "default", text: "MF.Pong");
        log.Add(0.54, "cmd", "default", text: "MF.Version.Get");
        log.Add(0.57, "resp", "default", text: "MF.Version.1.0.1");
        log.Add(0.6, "cmd", "default", text: $"MF.Clients.Add.{Probe.ClientName}");
        log.Add(0.65, "resp", "default", text: $"MF.Clients.Add.{Probe.ClientName}.Finished");
        log.Add(0.7, "cmd", "client", text: "MF.Ping");
        log.Add(0.73, "resp", "client", text: "MF.Pong");
        var beacon = 0.0;
        var lvar = 0.0;
        var commands = new SortedList<double, string>
        {
            [14] = "MF.SimVars.Set.1 (>L:FS24PF_TEST)", [15.5] = "MF.SimVars.Set.0 (>L:FS24PF_TEST)",
            [17] = "MF.SimVars.Set.0 (>K:TOGGLE_BEACON_LIGHTS)", [18.5] = "MF.SimVars.Set.0 (>K:TOGGLE_BEACON_LIGHTS)",
            [20] = "MF.SimVars.Set.0 (>K:TOGGLE_BEACON_LIGHTS)", [21.5] = "MF.SimVars.Set.0 (>K:TOGGLE_BEACON_LIGHTS)",
        };
        var pending = new List<(double At, Action Do)>();
        for (var t = 1.0; t < 23; t += 0.0244 + rng.NextDouble() * 0.001)
        {
            if (Math.Abs(t - 3) < 0.013) log.Add(t, "mark", "rate-start");
            if (Math.Abs(t - 13) < 0.013) log.Add(t, "mark", "rate-end");
            while (commands.Count > 0 && commands.Keys[0] <= t)
            {
                var c = commands.Values[0];
                commands.RemoveAt(0);
                log.Add(t, "cmd", "client", text: c);
                if (c.Contains("FS24PF_TEST")) { var v = c.Contains(".1 ") ? 1 : 0; pending.Add((t + 0.03, () => lvar = v)); }
                else pending.Add((t + 0.025, () => beacon = 1 - beacon));
            }
            foreach (var p in pending.Where(p => p.At <= t).ToList()) { p.Do(); pending.Remove(p); }
            log.Add(t, "direct", "LIGHT BEACON", beacon);
            log.Add(t, "direct", "GENERAL ENG RPM:1", 0);
            log.Add(t + 0.002, "mf", Probe.Vars[0], Math.Round(t, 3));
            if (log.Entries.LastOrDefault(x => x.Kind == "mf" && x.Name == Probe.Vars[1])?.Value != beacon)
                log.Add(t + 0.002, "mf", Probe.Vars[1], beacon);
            if (log.Entries.LastOrDefault(x => x.Kind == "mf" && x.Name == Probe.Vars[2])?.Value != lvar)
                log.Add(t + 0.002, "mf", Probe.Vars[2], lvar);
        }
        // Native part: the module's LVar seen natively, a native LVar write, two input event toggles.
        log.Add(14.03, "native", "L:FS24PF_TEST", 1);
        log.Add(15.53, "native", "L:FS24PF_TEST", 0);
        foreach (var (t, v) in new[] { (24.0, 1.0), (25.5, 0.0) })
        {
            log.Add(t, "cmd", "native", text: $"SetDataOnSimObject L:FS24PF_NATIVE={v}");
            log.Add(t + 0.026, "native", "L:FS24PF_NATIVE", v);
            log.Add(t + 0.05, "mf", "(L:FS24PF_NATIVE)", v);
        }
        log.Add(27, "info", "input-events", 212);
        log.Add(27, "info", "beacon-candidates", 1, "LIGHTING_BEACON_1");
        log.Add(28, "ie", "LIGHTING_BEACON_1", 0);
        foreach (var (t, v) in new[] { (28.5, 1.0), (30.0, 0.0) })
        {
            log.Add(t, "cmd", "native", text: $"SetInputEvent LIGHTING_BEACON_1={v}");
            log.Add(t + 0.025, "direct", "LIGHT BEACON", v);
            log.Add(t + 0.03, "ie", "LIGHTING_BEACON_1", v);
        }
        log.Entries.Sort((a, b) => a.T.CompareTo(b.T));
        return (log, ["XMLVAR_Example_1", "A32NX_Example_2", "S_FCU_EXAMPLE"]);
    }
}
