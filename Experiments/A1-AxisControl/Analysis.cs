using System.Text;

namespace PilotFlying.Experiments.AxisControl;

internal static class Analysis
{
    public static string Report(Recording rec, string source)
    {
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);
        var scales = Scales(rec);
        double M(Sample s, int m) => s.Meas[m] / scales[m];

        L($"=== Experiment A1 — control axes from SimConnect — analysis of {source} ===");
        foreach (var n in rec.Notes) L(n);
        L($"Samples: {rec.Samples.Count} · key events sent: {rec.Sends.Count} · phases run: "
          + string.Join(", ", rec.Samples.Select(s => s.Phase).Distinct()));
        var scaled = Enumerable.Range(0, scales.Length).Where(m => scales[m] != 1).ToList();
        L(scaled.Count == 0
            ? "\"position\" SimVars received between -1 and 1 (no rescaling)."
            : "Rescaled (received as raw positions): " + string.Join(", ", scaled.Select(m => $"{Script.Measures[m].Name} / {scales[m]}")));

        // ------------------------------------------------------------------ A3: rates
        L();
        L("--- A3: reading rate (SimVars requested at every simulation frame) ---");
        foreach (var name in new[] { "read-only" }.Concat(rec.Samples.Select(s => s.Phase).Distinct().Where(p => p != "read-only")))
        {
            var t = rec.Samples.Where(s => s.Phase == name).Select(s => s.T).ToList();
            if (t.Count < 3) continue;
            var dt = Diffs(t);
            L($"  {name,-22} {t.Count,5} samples, {(t.Count - 1) / (t[^1] - t[0]),5:F1} Hz, interval median {Pct(dt, 50) * 1000:F1} ms, "
              + $"p95 {Pct(dt, 95) * 1000:F1} ms, max {dt.Max() * 1000:F1} ms, gaps > 50 ms: {dt.Count(d => d > 0.05)}");
        }

        L();
        L("--- A3: sending rate (key events per channel) ---");
        foreach (var g in rec.Sends.GroupBy(s => (s.Phase, s.Channel)))
        {
            var t = g.Select(s => s.T).ToList();
            if (t.Count < 3) continue;
            var dt = Diffs(t);
            L($"  {g.Key.Phase,-22} {Script.Channels[g.Key.Channel].Name,-10} {t.Count,4} events, {(t.Count - 1) / (t[^1] - t[0]),5:F1} Hz, "
              + $"interval median {Pct(dt, 50) * 1000:F1} ms, p95 {Pct(dt, 95) * 1000:F1} ms, max {dt.Max() * 1000:F1} ms");
        }

        // ------------------------------------------------------------------ A1: steps
        L();
        L("--- A1: step responses (steady value = median of the last 40 % of each step; time = to 90 % of the change) ---");
        var gains = new Dictionary<int, double>();
        foreach (var phase in rec.Samples.Select(s => s.Phase).Distinct().Where(p => p.EndsWith("-steps")))
        {
            var sends = rec.Sends.Where(s => s.Phase == phase).ToList();
            var samples = rec.Samples.Where(s => s.Phase == phase).ToList();
            if (sends.Count == 0 || samples.Count == 0) continue;
            var channel = sends[0].Channel;
            var followers = Script.Followers[channel];
            L($"  {phase} ({Script.Channels[channel].Event}):");
            L("    command  " + string.Join("  ", followers.Select(m => $"{Script.Measures[m].Name,-34}")));
            var segments = Segments(sends.Where(s => s.Channel == channel).ToList(), samples[^1].T);
            var previous = new double[Script.Measures.Length];
            for (var m = 0; m < previous.Length; m++) previous[m] = M(samples[0], m);
            var fit = new List<(double Cmd, double Meas)>();
            foreach (var (value, start, end) in segments)
            {
                var inSeg = samples.Where(s => s.T >= start && s.T < end).ToList();
                if (inSeg.Count < 3) continue;
                var cells = new List<string>();
                foreach (var m in followers)
                {
                    var tail = inSeg.Where(s => s.T >= start + 0.6 * (end - start)).Select(s => M(s, m)).ToList();
                    var steady = Pct(tail, 50);
                    var change = steady - previous[m];
                    var reached = Math.Abs(change) < 0.02 ? null
                        : inSeg.FirstOrDefault(s => Math.Abs(M(s, m) - previous[m]) >= 0.9 * Math.Abs(change));
                    var time = Math.Abs(change) < 0.02 ? "no change" : reached is null ? "not reached" : $"{(reached.T - start) * 1000:F0} ms";
                    cells.Add($"{steady,7:F3} ({time})".PadRight(34));
                    previous[m] = steady;
                    if (m == followers[0]) fit.Add((value, steady));
                }
                L($"    {value,7:F2}  " + string.Join("  ", cells));
            }
            if (fit.Count >= 2)
            {
                var (slope, intercept, r2) = LinearFit(fit);
                gains[channel] = slope;
                L($"    {Script.Measures[followers[0]].Name} ≈ {slope:F3} × command {(intercept >= 0 ? "+" : "-")} {Math.Abs(intercept):F3}  "
                  + $"(R² {r2:F3}; {(slope < 0 ? "direction inverted" : "same direction")})");
            }
        }

        // ------------------------------------------------------------------ A3: delay on sine waves
        L();
        L("--- A3: delay between command and response (sine waves) ---");
        foreach (var phase in rec.Samples.Select(s => s.Phase).Distinct()
                     .Where(p => p.Contains("sine") || p.EndsWith("-steering") || p is "roll-rudder" or "roll-tiller"))
        {
            var sends = rec.Sends.Where(s => s.Phase == phase).ToList();
            var samples = rec.Samples.Where(s => s.Phase == phase).ToList();
            if (sends.Count < 10 || samples.Count < 10) continue;
            // The channel moved by the phase (rolling phases also send throttle and brakes).
            var channel = sends.GroupBy(s => s.Channel).OrderByDescending(gr => gr.Max(s => s.Value) - gr.Min(s => s.Value)).First().Key;
            foreach (var m in Script.Followers[channel])
            {
                if (samples.All(s => double.IsNaN(s.Meas[m]))) continue; // SimVar absent from an older recording
                var meas = samples.Select(s => (s.T, V: M(s, m))).ToList();
                var (delay, corr, gain) = BestDelay(sends.Where(s => s.Channel == channel).ToList(), meas);
                var range = $"range {meas.Min(x => x.V):F3} to {meas.Max(x => x.V):F3}";
                L(double.IsNaN(corr)
                    ? $"  {phase,-20} {Script.Measures[m].Name,-34} does not move ({range})"
                    : $"  {phase,-20} {Script.Measures[m].Name,-34} delay {delay * 1000,4:F0} ms, correlation {corr:F3}, gain {gain:F3}, {range}");
            }
        }

        if (rec.Samples.Any(s => s.Phase.StartsWith("roll-"))) Rolling(rec, L);
        if (!rec.Samples.Any(s => s.Phase is "rudder-steering" or "tiller-steering" || s.Phase.StartsWith("joystick-")))
        {
            L();
            L("Exceptions or anomalies are listed in the notes at the top.");
            return r.ToString();
        }

        // ------------------------------------------------------------------ A4
        L();
        L("--- A4: nose wheel steering at a standstill ---");
        foreach (var phase in new[] { "rudder-steering", "tiller-steering" })
        {
            var samples = rec.Samples.Where(s => s.Phase == phase).ToList();
            if (samples.Count == 0) { L($"  {phase}: not run"); continue; }
            var angle = samples.Select(s => s.Meas[Script.MSteerAngle]).ToList();
            var input = samples.Select(s => s.Meas[Script.MSteerInput]).ToList();
            L($"  {phase,-16} GEAR CENTER STEER ANGLE {angle.Min():F1}° to {angle.Max():F1}° · STEER INPUT CONTROL {input.Min():F2} to {input.Max():F2} · "
              + $"max ground speed {samples.Max(s => s.Meas[Script.MGroundSpeed]):F2} kt");
        }
        L("  (A steer angle that follows the rudder means the pedals steer the nose wheel; one that follows the tiller phase");
        L("   means AXIS_STEERING_SET works on this aircraft.)");

        // ------------------------------------------------------------------ A2
        L();
        L("--- A2: joystick plugged in (elevator held at +0.5 by the program) ---");
        var elevatorGain = gains.TryGetValue(Script.Elevator, out var ge) ? ge : 1;
        var expected = elevatorGain * 0.5;
        L($"  Expected ELEVATOR POSITION while the program holds +0.5: {expected:F3} (gain from elevator-steps)");
        foreach (var phase in new[] { "joystick-hands-off", "joystick-hands-on" })
        {
            var v = rec.Samples.Where(s => s.Phase == phase).ToList();
            if (v.Count == 0) { L($"  {phase}: not run"); continue; }
            var settled = v.Where(s => s.T >= v[0].T + 0.3).Select(s => M(s, Script.MElevator)).ToList();
            L($"  {phase,-22} at the program's value (±0.05) {100.0 * settled.Count(x => Math.Abs(x - expected) <= 0.05) / settled.Count,5:F1} % of the time, "
              + $"range {settled.Min():F3} to {settled.Max():F3}");
        }
        var stop = rec.Samples.Where(s => s.Phase == "joystick-stop-sending").ToList();
        var lastSend = rec.Sends.Where(s => s.Phase == "joystick-stop-sending").Select(s => s.T).DefaultIfEmpty(double.NaN).Max();
        if (stop.Count > 0 && !double.IsNaN(lastSend))
        {
            var before = stop.Where(s => s.T < lastSend && s.T > lastSend - 0.5).Select(s => M(s, Script.MElevator)).ToList();
            var after = stop.Where(s => s.T > lastSend + 0.3).Select(s => M(s, Script.MElevator)).ToList();
            var held = before.Count > 0 ? Pct(before, 50) : expected;
            if (after.Count > 0)
                L($"  joystick-stop-sending  last value sent at {lastSend - stop[0].T:F1} s; before {held:F3}, after: median {Pct(after, 50):F3}, "
                  + $"range {after.Min():F3} to {after.Max():F3}, unchanged (±0.05) {100.0 * after.Count(x => Math.Abs(x - held) <= 0.05) / after.Count:F1} % of the time");
        }
        else L("  joystick-stop-sending: not run");
        var back = rec.Samples.Where(s => s.Phase == "joystick-take-back").Select(s => M(s, Script.MElevator)).ToList();
        L(back.Count == 0
            ? "  joystick-take-back: not run"
            : $"  joystick-take-back     start {back[0]:F3}, range {back.Min():F3} to {back.Max():F3}, end {back[^1]:F3} "
              + "(a range that widens means the joystick took control back as soon as it moved)");

        L();
        L("Exceptions or anomalies are listed in the notes at the top.");
        return r.ToString();
    }

    // ------------------------------------------------------------------ A4 while rolling

    private static void Rolling(Recording rec, Action<string> L)
    {
        List<Sample> Of(string phase) => rec.Samples.Where(s => s.Phase == phase).ToList();
        double Speed(Sample s) => s.Meas[Script.MGroundSpeed];

        L("");
        L($"--- A4: rolling slowly (engine at idle, ground speed held at {Script.TargetSpeed} kt with the throttle) ---");
        var all = rec.Samples.Where(s => s.Phase.StartsWith("roll-")).ToList();
        L($"  Distance travelled {Distance(all):F0} m, max ground speed {all.Max(Speed):F1} kt, "
          + $"parking brake at the start {all[0].Meas[Script.MParkingBrake]}, at the end {all[^1].Meas[Script.MParkingBrake]}");

        var release = Of("roll-release");
        if (release.Count > 0)
            L($"  roll-release     parking brake {release[0].Meas[Script.MParkingBrake]} → {release[^1].Meas[Script.MParkingBrake]} "
              + "(PARKING_BRAKE_SET 0 sent, toe brakes held)");

        var acc = Of("roll-accelerate");
        if (acc.Count > 0)
        {
            var reached = acc.FirstOrDefault(s => Speed(s) >= Script.TargetSpeed - 1);
            var tail = acc.Where(s => s.T >= acc[^1].T - 5).ToList();
            var mean = tail.Average(Speed);
            var sd = Math.Sqrt(tail.Average(s => (Speed(s) - mean) * (Speed(s) - mean)));
            L($"  roll-accelerate  {Script.TargetSpeed - 1} kt reached "
              + (reached is null ? "never" : $"after {reached.T - acc[0].T:F1} s")
              + $"; last 5 s: {mean:F2} ± {sd:F2} kt, throttle {Pct(tail.Select(s => s.Meas[Script.MThrottle]), 50):F2}");
        }

        foreach (var phase in new[] { "roll-rudder", "roll-straight-1", "roll-tiller", "roll-straight-2" })
        {
            var v = Of(phase);
            if (v.Count == 0) { L($"  {phase}: not run"); continue; }
            string Range(int m) => $"{v.Min(s => s.Meas[m]):F1} to {v.Max(s => s.Meas[m]):F1}";
            var turn = v.Select(s => Turn(v[0].Meas[Script.MHeading], s.Meas[Script.MHeading])).ToList();
            L($"  {phase,-16} speed {v.Min(Speed):F1}–{v.Max(Speed):F1} kt · GEAR CENTER STEER ANGLE {Range(Script.MSteerAngle)}° · "
              + $"GEAR STEER ANGLE:0 {Range(Script.MGearSteer0)}° · CONTACT POINT STEER ANGLE:0 {Range(Script.MContactSteer0)}°");
            L($"  {"",-16} STEER INPUT CONTROL {v.Min(s => s.Meas[Script.MSteerInput]):F2} to {v.Max(s => s.Meas[Script.MSteerInput]):F2} · "
              + $"yaw rate {Range(Script.MYawRate)}°/s · heading change {turn.Min():F1}° to {turn.Max():F1}°");
            // Kinematic check: yaw rate = v · tan(steer angle) / wheelbase.
            var wheelbase = v.Where(s => Math.Abs(s.Meas[Script.MSteerAngle]) > 2 && Math.Abs(s.Meas[Script.MYawRate]) > 0.5 && Speed(s) > 1)
                .Select(s => Speed(s) * 0.5144 * Math.Tan(s.Meas[Script.MSteerAngle] * Math.PI / 180) / (s.Meas[Script.MYawRate] * Math.PI / 180))
                .ToList();
            if (wheelbase.Count >= 10)
                L($"  {"",-16} wheelbase implied by speed, steer angle and yaw rate: {Pct(wheelbase, 50):F2} m (median of {wheelbase.Count})");
        }

        var stop = Of("roll-stop");
        if (stop.Count > 0)
        {
            var stopped = stop.FirstOrDefault(s => Speed(s) < 0.3);
            var duration = (stopped ?? stop[^1]).T - stop[0].T;
            L($"  roll-stop        from {Speed(stop[0]):F1} kt, brakes 50 %: "
              + (stopped is null ? $"not stopped after {duration:F1} s" : $"stopped in {duration:F1} s")
              + $", {Distance(stop.TakeWhile(s => s != stopped).ToList()):F1} m, "
              + $"deceleration {(duration > 0 ? (Speed(stop[0]) - Speed(stopped ?? stop[^1])) / duration : 0):F2} kt/s");
        }
        foreach (var note in rec.Notes.Where(n => n.StartsWith("Stopped"))) L("  " + note);
    }

    /// <summary>Metres per degree of latitude and longitude on the WGS84 ellipsoid at a latitude.</summary>
    private static (double Lat, double Lon) MetresPerDegree(double lat)
    {
        const double a = 6378137, e2 = 0.00669437999014;
        var phi = lat * Math.PI / 180;
        var w = 1 - e2 * Math.Sin(phi) * Math.Sin(phi);
        return (a * (1 - e2) / Math.Pow(w, 1.5) * Math.PI / 180, a / Math.Sqrt(w) * Math.Cos(phi) * Math.PI / 180);
    }

    /// <summary>Signed heading change from a to b, in degrees (-180 to 180).</summary>
    private static double Turn(double a, double b) => ((b - a) % 360 + 540) % 360 - 180;

    /// <summary>Path length (m) from PLANE LATITUDE / LONGITUDE.</summary>
    private static double Distance(List<Sample> v)
    {
        var d = 0.0;
        for (var i = 1; i < v.Count; i++)
        {
            double lat0 = v[i - 1].Meas[Script.MLat], lon0 = v[i - 1].Meas[Script.MLon];
            var m = MetresPerDegree(lat0);
            double dn = (v[i].Meas[Script.MLat] - lat0) * m.Lat;
            double de = (v[i].Meas[Script.MLon] - lon0) * m.Lon;
            if (!double.IsNaN(dn) && !double.IsNaN(de)) d += Math.Sqrt(dn * dn + de * de);
        }
        return d;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Divisor per SimVar: 1, or 16384 / 32768 if "position" values arrive as raw positions.</summary>
    private static double[] Scales(Recording rec)
    {
        var scales = Enumerable.Repeat(1.0, Script.Measures.Length).ToArray();
        for (var m = 0; m < scales.Length; m++)
        {
            if (!Script.Measures[m].Position || rec.Samples.Count == 0) continue;
            var max = rec.Samples.Max(s => Math.Abs(s.Meas[m]));
            if (max > 2) scales[m] = max > 17000 ? 32768 : 16384;
        }
        return scales;
    }

    private static List<double> Diffs(List<double> t) => t.Zip(t.Skip(1), (a, b) => b - a).ToList();

    private static double Pct(IEnumerable<double> values, double pct)
    {
        var v = values.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
        if (v.Count == 0) return double.NaN;
        var i = (int)Math.Round(pct / 100 * (v.Count - 1));
        return v[Math.Clamp(i, 0, v.Count - 1)];
    }

    /// <summary>Constant-command segments: (value, first send, start of the next segment or end).</summary>
    private static List<(double Value, double Start, double End)> Segments(List<Send> sends, double end)
    {
        var result = new List<(double, double, double)>();
        for (var i = 0; i < sends.Count; i++)
        {
            if (i > 0 && sends[i].Value == sends[i - 1].Value) continue;
            var j = i;
            while (j + 1 < sends.Count && sends[j + 1].Value == sends[i].Value) j++;
            result.Add((sends[i].Value, sends[i].T, j + 1 < sends.Count ? sends[j + 1].T : end));
        }
        return result;
    }

    private static (double Slope, double Intercept, double R2) LinearFit(List<(double X, double Y)> p)
    {
        double mx = p.Average(q => q.X), my = p.Average(q => q.Y);
        double sxx = p.Sum(q => (q.X - mx) * (q.X - mx)), sxy = p.Sum(q => (q.X - mx) * (q.Y - my)), syy = p.Sum(q => (q.Y - my) * (q.Y - my));
        if (sxx == 0) return (double.NaN, double.NaN, double.NaN);
        var slope = sxy / sxx;
        return (slope, my - slope * mx, syy == 0 ? 1 : sxy * sxy / (sxx * syy));
    }

    /// <summary>Delay (0–500 ms) that best aligns the response with the command (zero-order hold of the sends).</summary>
    private static (double Delay, double Corr, double Gain) BestDelay(List<Send> sends, List<(double T, double V)> meas)
    {
        double Cmd(double t)
        {
            var i = sends.FindLastIndex(s => s.T <= t);
            return i < 0 ? 0 : sends[i].Value;
        }
        var window = meas.Where(x => x.T >= sends[0].T + 0.5 && x.T <= sends[^1].T).ToList();
        (double, double, double) best = (double.NaN, double.NaN, double.NaN);
        for (var d = 0.0; d <= 0.5; d += 0.005)
        {
            var pairs = window.Select(x => (X: Cmd(x.T - d), Y: x.V)).ToList();
            if (pairs.Count < 10) break;
            var (slope, _, r2) = LinearFit(pairs);
            if (double.IsNaN(r2) || pairs.Select(q => q.Y).Distinct().Count() < 3) continue;
            var corr = Math.Sign(slope) * Math.Sqrt(r2);
            if (double.IsNaN(best.Item2) || Math.Abs(corr) > Math.Abs(best.Item2)) best = (d, corr, slope);
        }
        return best;
    }
}
