using System.Globalization;
using System.Text;

namespace PilotFlying.Experiments.GroundTracking;

internal static class Analysis
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string Header = "t,phase,e,n,heading,groundSpeed,yawRate,onGround,crossTrack,headingError,along,steerDeg,rudder,throttle,brake,parkingBrake,steerAngle,lat,lon";

    public static void Save(List<Frame> frames, List<string> notes, string basePath)
    {
        string F(double v) => v.ToString("R", Inv);
        var s = new StringBuilder(Header + "\n");
        foreach (var f in frames)
        {
            var (st, tm, c) = (f.State, f.Telemetry, f.Command);
            s.Append(string.Join(",", F(st.T), tm.Phase, F(st.E), F(st.N), F(st.Heading), F(st.GroundSpeed), F(st.YawRate), st.OnGround ? "1" : "0",
                F(tm.CrossTrack), F(tm.HeadingError), F(tm.Along), F(tm.SteerDeg), F(c.Rudder), F(c.Throttle), F(c.Brake), F(c.ParkingBrake),
                F(f.SteerAngle), F(f.Lat), F(f.Lon))).Append('\n');
        }
        File.WriteAllText(basePath + "-frames.csv", s.ToString(), new UTF8Encoding(false));
        File.WriteAllLines(basePath + "-notes.txt", notes, new UTF8Encoding(false));
    }

    public static (List<Frame> Frames, List<string> Notes) Load(string framesFile)
    {
        double P(string x) => double.Parse(x, Inv);
        var frames = File.ReadLines(framesFile).Skip(1).Select(l => l.Split(',')).Select(f => new Frame(
            new State(P(f[0]), P(f[2]), P(f[3]), P(f[4]), P(f[5]), P(f[6]), f[7] == "1"),
            new Telemetry(f[1], P(f[8]), P(f[9]), P(f[10]), P(f[11])),
            new Command(P(f[12]), P(f[13]), P(f[14]), P(f[15])), P(f[16]), P(f[17]), P(f[18]))).ToList();
        var notesFile = framesFile.Replace("-frames.csv", "-notes.txt");
        return (frames, File.Exists(notesFile) ? File.ReadAllLines(notesFile).ToList() : []);
    }

    private static double Pct(IEnumerable<double> values, double pct)
    {
        var v = values.OrderBy(x => x).ToList();
        return v.Count == 0 ? double.NaN : v[(int)Math.Round(pct / 100 * (v.Count - 1))];
    }

    private static double Rms(IEnumerable<double> values)
    {
        var v = values.ToList();
        return v.Count == 0 ? double.NaN : Math.Sqrt(v.Average(x => x * x));
    }

    public static string Report(List<Frame> frames, List<string> notes, string source)
    {
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);
        L($"=== Experiment E1 — first automatic taxi (C172, straight centreline) — {source} ===");
        foreach (var n in notes) L("  " + n);
        L($"Law: steer = −(heading error + atan({Tracker.K} · offset / speed) + {Tracker.Ki} · ∫offset) − {Tracker.Kd} · yaw rate, "
          + $"offset taken {Tracker.Lookahead} m ahead of the reference point; speed held at {Tracker.TargetSpeed} kt.");
        if (frames.Count == 0) { L("No frame recorded."); return r.ToString(); }

        var track = frames.Where(f => f.Telemetry.Phase == "track").ToList();
        L();
        L("--- E1: line tracking (from 5 s after the start of the tracking phase) ---");
        if (track.Count == 0) { L("  tracking phase not reached"); return r.ToString(); }
        var t0 = track[0].State.T;
        var settled = track.Where(f => f.State.T >= t0 + 5).ToList();
        var dist = track[^1].Telemetry.Along - track[0].Telemetry.Along;
        L($"  Duration {track[^1].State.T - t0:F1} s, distance {dist:F0} m, {track.Count} frames ({track.Count / Math.Max(0.1, track[^1].State.T - t0):F0} per second)");
        L($"  First 5 s: offset from {track[0].Telemetry.CrossTrack:+0.00;-0.00} m to {(settled.Count > 0 ? settled[0].Telemetry.CrossTrack : double.NaN):+0.00;-0.00} m");
        if (settled.Count > 10)
        {
            var x = settled.Select(f => f.Telemetry.CrossTrack).ToList();
            L($"  Offset (2 m ahead): mean {x.Average():+0.00;-0.00} m, RMS {Rms(x):F2} m, |offset| p95 {Pct(x.Select(Math.Abs), 95):F2} m, max {x.Max(Math.Abs):F2} m");
            var hdg = settled.Select(f => f.Telemetry.HeadingError).ToList();
            L($"  Heading error: mean {hdg.Average():+0.0;-0.0;0.0}°, RMS {Rms(hdg):F1}°, max {hdg.Max(Math.Abs):F1}°");
            var steer = settled.Select(f => f.Telemetry.SteerDeg).ToList();
            L($"  Steer command: mean {steer.Average():+0.0;-0.0;0.0}° (holds the drift), RMS {Rms(steer):F1}°, saturated {100.0 * steer.Count(s => Math.Abs(s) >= Tracker.MaxSteer - 0.01) / steer.Count:F0} % of the time");
            var crossings = x.Zip(x.Skip(1), (a, b) => Math.Sign(a - x.Average()) != Math.Sign(b - x.Average())).Count(c => c);
            L($"  Oscillation: offset crosses its mean {crossings} times in {settled[^1].State.T - settled[0].State.T:F0} s");
            var gs = settled.Select(f => f.State.GroundSpeed).ToList();
            L($"  Ground speed {gs.Average():F2} ± {Math.Sqrt(gs.Average(g => (g - gs.Average()) * (g - gs.Average()))):F2} kt, brakes used {100.0 * settled.Count(f => f.Command.Brake > 0) / settled.Count:F0} % of the time");
        }
        var stop = frames.Where(f => f.Telemetry.Phase is "stop" or "done").ToList();
        if (stop.Count > 0)
        {
            var stopped = stop.FirstOrDefault(f => f.State.GroundSpeed < 0.3);
            L($"  Stop: from {stop[0].State.GroundSpeed:F1} kt, " + (stopped is null ? "not stopped" :
                $"stopped in {stopped.State.T - stop[0].State.T:F1} s and {stopped.Telemetry.Along - stop[0].Telemetry.Along:F1} m, offset {stopped.Telemetry.CrossTrack:+0.00;-0.00} m"));
        }
        L();
        L("Reading: a stable law keeps the offset within a few decimetres without growing oscillations; a mean steer");
        L("command different from zero is the correction of the left drift seen in test A1.");
        return r.ToString();
    }

    /// <summary>
    /// Simulated run (no MSFS): kinematic model with the C172 values of test A1 (nose wheel angle = −20° × rudder,
    /// reached in about 0.1 s; yaw following 0.2 s later; wheelbase 2.12 m; left drift −0.35°/s), idle already giving 5.7 kt,
    /// start 0.8 m right of the line and 2° off its axis.
    /// </summary>
    public static (List<Frame>, List<string>) Simulate()
    {
        var line = new Line(0, 0, 90, 0, 150, "NE (synthetic)", 0);
        var tracker = new Tracker(line);
        double e = 0, n = -0.8, heading = 92, speed = 0, steerAngle = 0, yawRate = 0;
        var frames = new List<Frame>();
        var cmd = Tracker.Safe;
        for (var t = 0.0; t < 120 && tracker.Phase != "done"; t += 0.025)
        {
            var state = new State(t, e, n, heading, speed, yawRate, true);
            (cmd, var tm) = tracker.Update(state);
            frames.Add(new Frame(state, tm, cmd, steerAngle, 0, 0));
            const double dt = 0.025;
            steerAngle += (-20 * cmd.Rudder - steerAngle) * Math.Min(1, dt / 0.1);
            var brake = Math.Max(cmd.Brake, cmd.ParkingBrake);
            var accel = 0.9 * (5.7 - speed) / 5.7 + 3 * Math.Max(0, cmd.Throttle - 0.1) - 6 * brake;
            speed = Math.Max(0, speed + accel * dt);
            var v = speed * 0.5144;
            // The aircraft yaws about 0.22 s after the nose wheel turns (test A1): first-order lag of 0.2 s.
            var yawTarget = v * Math.Tan(steerAngle * Math.PI / 180) / 2.12 * 180 / Math.PI + (speed > 0.5 ? -0.35 : 0);
            yawRate += (yawTarget - yawRate) * Math.Min(1, dt / 0.2);
            heading += yawRate * dt;
            e += v * Math.Sin(heading * Math.PI / 180) * dt;
            n += v * Math.Cos(heading * Math.PI / 180) * dt;
        }
        return (frames, ["SIMULATED RUN (kinematic model, not MSFS)"]);
    }
}
