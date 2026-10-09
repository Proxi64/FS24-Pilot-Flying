using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PilotFlying.Experiments.GroundTracking;

/// <summary>One recorded frame of a route run.</summary>
internal sealed record RouteFrame(State State, RouteTelemetry Telemetry, Command Command, double SteerAngle);

/// <summary>Taxi along a route (live in MSFS, or simulated), its record and its analysis.</summary>
internal static unsafe class RouteMode
{
    private static readonly (string Name, string Unit)[] Vars =
    [
        ("PLANE LATITUDE", "degrees"), ("PLANE LONGITUDE", "degrees"), ("PLANE HEADING DEGREES TRUE", "degrees"),
        ("GROUND VELOCITY", "knots"), ("ROTATION VELOCITY BODY Y", "degrees per second"), ("SIM ON GROUND", "bool"),
        ("ENG COMBUSTION:1", "bool"), ("BRAKE PARKING POSITION", "bool"), ("GEAR CENTER STEER ANGLE", "degrees"),
    ];
    private static readonly string[] Events = ["AXIS_RUDDER_SET", "THROTTLE1_SET", "AXIS_LEFT_BRAKE_SET", "AXIS_RIGHT_BRAKE_SET", "PARKING_BRAKE_SET"];
    private const uint FirstEvent = 10;
    public const double MaxParkingDistance = 5;  // m between the aircraft and the parking spot (consistency check)
    public const double MaxStartAngle = 90;      // degrees: beyond, the aircraft would need a pushback

    /// <summary>Nearest parking spot, the route from it, and the start checks.</summary>
    public static Route? Prepare(Layout layout, double e, double n, double heading, string[] clearance, List<string> notes, out string refused)
    {
        refused = "";
        var (parking, pk) = layout.Parkings.MinBy(p => (p.Value.E - e) * (p.Value.E - e) + (p.Value.N - n) * (p.Value.N - n));
        var d = Math.Sqrt((pk.E - e) * (pk.E - e) + (pk.N - n) * (pk.N - n));
        notes.Add($"Nearest parking spot: index {parking}, {d:F1} m from the aircraft, heading {pk.Heading:F0}°");
        if (d > MaxParkingDistance) { refused = $"the aircraft is {d:F0} m from the nearest parking spot (index {parking}): start from a parking spot"; return null; }
        var route = Route.Find(layout, parking, clearance, out refused);
        if (route is null) return null;
        notes.Add($"Route ({route.Length:F0} m): {route.Summary}");
        var angle = Math.Abs(((route.BearingAt(Follower.MinLookahead) - heading) % 360 + 540) % 360 - 180);
        notes.Add($"Route direction {Follower.MinLookahead:F0} m ahead: {angle:F0}° from the aircraft heading");
        if (angle > MaxStartAngle) { refused = $"the route starts {angle:F0}° away from the aircraft heading: a pushback would be needed"; return null; }
        return route;
    }

    public static List<RouteFrame>? Run(Layout layout, string[] clearance, List<string> notes, out Route? route, out string error)
    {
        error = "";
        route = null;
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment E1 route", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
            { error = "MSFS 2024 cannot be reached: start the simulator and load the flight."; return null; }
        }
        catch (DllNotFoundException) { error = "SimConnect.dll not found next to the executable (see MsfsSdk in the .csproj)."; return null; }

        Native.timeBeginPeriod(1);
        var frames = new List<RouteFrame>();
        try
        {
            foreach (var (name, unit) in Vars)
                Native.SimConnect_AddToDataDefinition(h, 1, name, unit, Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
            for (var i = 0; i < Events.Length; i++)
                Native.SimConnect_MapClientEventToSimEvent(h, FirstEvent + (uint)i, Events[i]);
            Native.SimConnect_RequestDataOnSimObject(h, 1, 1, Native.OBJECT_ID_USER, Native.PERIOD_SIM_FRAME, 0, 0, 0, 0);

            var clock = Stopwatch.StartNew();
            double[]? latest = null;
            string? quit = null;
            var exceptions = new List<string>();
            bool Pump()
            {
                var got = false;
                while (Native.SimConnect_GetNextDispatch(h, out var p, out _) >= 0 && p != IntPtr.Zero)
                {
                    var recv = (Native.Recv*)p;
                    if (recv->dwID == Native.RECV_ID_QUIT) quit = "MSFS closed";
                    else if (recv->dwID == Native.RECV_ID_EXCEPTION)
                        exceptions.Add($"SimConnect exception {((Native.RecvException*)p)->dwException} at {clock.Elapsed.TotalSeconds:F2} s");
                    else if (recv->dwID == Native.RECV_ID_SIMOBJECT_DATA)
                    {
                        var v = (double*)((byte*)p + sizeof(Native.RecvSimObjectData));
                        latest = new double[Vars.Length];
                        for (var i = 0; i < Vars.Length; i++) latest[i] = v[i];
                        got = true;
                    }
                }
                return got;
            }
            void Tx(int i, int raw) => Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + (uint)i,
                unchecked((uint)raw), Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
            void Send(Command c)
            {
                Tx(0, (int)Math.Round(Math.Clamp(c.Rudder, -1, 1) * 16383));
                Tx(1, (int)Math.Round(Math.Clamp(c.Throttle, 0, 1) * 16383));
                var brake = (int)Math.Round(-16383 + Math.Clamp(c.Brake, 0, 1) * 32766);
                Tx(2, brake);
                Tx(3, brake);
                Tx(4, c.ParkingBrake > 0.5 ? 1 : 0);
            }

            var until = clock.Elapsed.TotalSeconds + 5;
            while (latest is null && clock.Elapsed.TotalSeconds < until) { Pump(); Thread.Sleep(5); }
            if (latest is null) { error = "No aircraft data received: load the flight first."; return null; }
            if (exceptions.Count > 0) { error = "SimConnect rejected part of the setup: " + string.Join("; ", exceptions); return null; }

            var m = latest;
            var check = $"On ground {m[5]}, ground speed {m[3]:F1} kt, engine {m[6]}, parking brake {m[7]}, heading {m[2]:F1}°";
            Console.WriteLine(check);
            notes.Add(check);
            if (m[5] < 0.5 || m[3] > 1) { error = "The aircraft must be stopped on the ground."; return null; }
            if (m[6] < 0.5) { error = "The engine must be running (idle)."; return null; }
            if (m[7] < 0.5) { error = "Set the parking brake before starting."; return null; }
            var (e0, n0) = layout.Local(m[0], m[1]);
            route = Prepare(layout, e0, n0, m[2], clearance, notes, out var refused);
            foreach (var note in notes.Skip(1)) Console.WriteLine(note);
            if (route is null) { error = "Cannot start: " + refused + "."; return null; }

            Console.WriteLine($"Taxi along {route.Length:F0} m at {Follower.Cruise} kt ({Follower.TurnSpeed} kt in turns), stop {Follower.StopBefore} m before the hold-short point.");
            Console.WriteLine("Hands and feet off the controls. Esc = emergency stop (idle, full brakes, parking brake).");
            Console.Write("Enter = start, Esc = cancel: ");
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).Key;
                    if (k == ConsoleKey.Escape) { Console.WriteLine(); return frames; }
                    if (k == ConsoleKey.Enter) break;
                }
                Pump();
                Thread.Sleep(5);
            }
            Console.WriteLine();

            var follower = new Follower(route);
            var nextSend = 0.0;
            var nextDisplay = 0.0;
            var last = Tracker.Safe;
            while (follower.Phase != "done")
            {
                if (quit is not null) { notes.Add(quit); break; }
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    notes.Add($"Emergency stop: Esc pressed at {clock.Elapsed.TotalSeconds:F1} s");
                    Console.WriteLine("\n   STOP: Esc pressed.");
                    break;
                }
                if (!Pump()) { Thread.Sleep(1); continue; }
                var t = clock.Elapsed.TotalSeconds;
                var d = latest!;
                var (e, n) = layout.Local(d[0], d[1]);
                var state = new State(t, e, n, d[2], d[3], d[4], d[5] > 0.5);
                var (cmd, tm) = follower.Update(state);
                if (follower.Watchdog(state, tm) is { } danger)
                {
                    notes.Add($"Emergency stop at {t:F1} s: {danger}");
                    Console.WriteLine($"\n   STOP: {danger}.");
                    frames.Add(new RouteFrame(state, tm, Tracker.Safe, d[8]));
                    break;
                }
                if (t >= nextSend)
                {
                    Send(cmd);
                    last = cmd;
                    nextSend = Math.Max(nextSend + 1.0 / 30, t);
                }
                frames.Add(new RouteFrame(state, tm, last, d[8]));
                if (t >= nextDisplay)
                {
                    nextDisplay = t + 0.5;
                    Console.Write($"\r   {tm.Phase,-7} {tm.Name,-10} {d[3],4:F1} kt (target {tm.TargetSpeed:F1})   offset {tm.CrossTrack,6:+0.00;-0.00} m   "
                                  + $"steer {tm.SteerDeg,5:+0.0;-0.0}°   {tm.ToStop,5:F0} m to the stop   ");
                }
            }
            Console.WriteLine();
            notes.AddRange(exceptions);
            return frames;
        }
        finally
        {
            for (var i = 0; i < 3; i++)
            {
                foreach (var (ev, raw) in new[] { (0, 0), (1, 0), (2, 16383), (3, 16383), (4, 1) })
                    Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + (uint)ev, unchecked((uint)raw),
                        Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Thread.Sleep(30);
            }
            Native.SimConnect_Close(h);
            Native.timeEndPeriod(1);
        }
    }

    /// <summary>
    /// Simulated run (no MSFS) from the parking spot, with the kinematic C172 model of Analysis.Simulate (nose wheel
    /// −20° × rudder in 0.1 s, yaw 0.2 s later, wheelbase 2.12 m, left drift −0.35°/s, idle giving 5.7 kt).
    /// </summary>
    public static (List<RouteFrame>, List<string>, Route?) Simulate(Layout layout, int parking, string[] clearance)
    {
        var notes = new List<string> { "SIMULATED RUN (kinematic model, not MSFS)" };
        var pk = layout.Parkings[parking];
        var heading = pk.Heading;
        var route = Prepare(layout, pk.E, pk.N, heading, clearance, notes, out var refused);
        if (route is null && Route.Find(layout, parking, clearance, out _) is { } r)
        {
            // The stand heading would need a pushback: simulate the law with the aircraft already facing the route.
            heading = r.BearingAt(Follower.MinLookahead);
            notes.Add($"Simulation started facing the route (heading {heading:F0}°) instead of the stand heading {pk.Heading:F0}°: {refused}");
            route = Prepare(layout, pk.E, pk.N, heading, clearance, notes, out refused);
        }
        if (route is null) { notes.Add("Refused: " + refused); return ([], notes, null); }
        var follower = new Follower(route);
        double e = pk.E, n = pk.N, speed = 0, steerAngle = 0, yawRate = 0;
        var frames = new List<RouteFrame>();
        const double dt = 0.025;
        for (var t = 0.0; t < 900 && follower.Phase != "done"; t += dt)
        {
            var state = new State(t, e, n, heading, speed, yawRate, true);
            var (cmd, tm) = follower.Update(state);
            if (follower.Watchdog(state, tm) is { } danger) { notes.Add($"Emergency stop at {t:F1} s: {danger}"); break; }
            frames.Add(new RouteFrame(state, tm, cmd, steerAngle));
            steerAngle += (-20 * cmd.Rudder - steerAngle) * Math.Min(1, dt / 0.1);
            var brake = Math.Max(cmd.Brake, cmd.ParkingBrake);
            var accel = 0.9 * (5.7 - speed) / 5.7 + 3 * Math.Max(0, cmd.Throttle - 0.1) - 6 * brake;
            speed = Math.Max(0, speed + accel * dt);
            var v = speed * 0.5144;
            var yawTarget = v * Math.Tan(steerAngle * Math.PI / 180) / 2.12 * 180 / Math.PI + (speed > 0.5 ? -0.35 : 0);
            yawRate += (yawTarget - yawRate) * Math.Min(1, dt / 0.2);
            heading = (heading + yawRate * dt + 360) % 360;
            e += v * Math.Sin(heading * Math.PI / 180) * dt;
            n += v * Math.Cos(heading * Math.PI / 180) * dt;
        }
        return (frames, notes, route);
    }

    // ------------------------------------------------------------------ record and analysis

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Save(List<RouteFrame> frames, List<string> notes, string basePath)
    {
        string F(double v) => v.ToString("R", Inv);
        var s = new StringBuilder("t,phase,name,e,n,heading,groundSpeed,yawRate,s,crossTrack,headingError,steerDeg,targetSpeed,toStop,rudder,throttle,brake,steerAngle\n");
        foreach (var f in frames)
        {
            var (st, tm, c) = (f.State, f.Telemetry, f.Command);
            s.Append(string.Join(",", F(st.T), tm.Phase, tm.Name, F(st.E), F(st.N), F(st.Heading), F(st.GroundSpeed), F(st.YawRate), F(tm.S),
                F(tm.CrossTrack), F(tm.HeadingError), F(tm.SteerDeg), F(tm.TargetSpeed), F(tm.ToStop), F(c.Rudder), F(c.Throttle), F(c.Brake),
                F(f.SteerAngle))).Append('\n');
        }
        File.WriteAllText(basePath + "-frames.csv", s.ToString(), new UTF8Encoding(false));
        File.WriteAllLines(basePath + "-notes.txt", notes, new UTF8Encoding(false));
    }

    public static (List<RouteFrame>, List<string>) Load(string framesFile)
    {
        double P(string x) => double.Parse(x, Inv);
        var frames = File.ReadLines(framesFile).Skip(1).Select(l => l.Split(',')).Select(f => new RouteFrame(
            new State(P(f[0]), P(f[3]), P(f[4]), P(f[5]), P(f[6]), P(f[7]), true),
            new RouteTelemetry(f[1], P(f[8]), P(f[9]), P(f[10]), P(f[11]), P(f[12]), f[2], P(f[13])),
            new Command(P(f[14]), P(f[15]), P(f[16]), 0), P(f[17]))).ToList();
        var notesFile = framesFile.Replace("-frames.csv", "-notes.txt");
        return (frames, File.Exists(notesFile) ? File.ReadAllLines(notesFile).ToList() : []);
    }

    private static double Pct(IEnumerable<double> values, double pct)
    {
        var v = values.OrderBy(x => x).ToList();
        return v.Count == 0 ? double.NaN : v[(int)Math.Round(pct / 100 * (v.Count - 1))];
    }

    public static string Report(List<RouteFrame> frames, List<string> notes, Route? route, string source)
    {
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);
        L($"=== Experiment E1 — automatic taxi along a route (C172) — {source} ===");
        foreach (var n in notes) L("  " + n);
        L($"Law: pure pursuit (Ld = max({Follower.MinLookahead} m, {Follower.LookaheadTime} s × speed), L = {Follower.Wheelbase} m) "
          + $"+ {Follower.Ki} · ∫offset; {Follower.Cruise} kt, {Follower.TurnSpeed} kt when the route turns more than {Follower.TurnAngle}° "
          + $"within {Follower.TurnLook} m; stop {Follower.StopBefore} m before the hold-short point.");
        var taxi = frames.Where(f => f.Telemetry.Phase == "taxi").ToList();
        if (taxi.Count < 10) { L("Taxi phase not reached."); return r.ToString(); }
        var t0 = taxi[0].State.T;
        L();
        L($"--- Route following ({taxi[^1].Telemetry.S - taxi[0].Telemetry.S:F0} m in {taxi[^1].State.T - t0:F0} s) ---");
        // Straight parts and turns, from the route geometry around each frame.
        bool InTurn(RouteFrame f) => route is null ? Math.Abs(f.Telemetry.SteerDeg) > 5 : route.TurnAhead(Math.Max(0, f.Telemetry.S - 10), 20) > 10;
        foreach (var (label, part) in new[] { ("straight parts", taxi.Where(f => !InTurn(f)).ToList()), ("turns", taxi.Where(InTurn).ToList()) })
        {
            if (part.Count == 0) continue;
            var x = part.Select(f => f.Telemetry.CrossTrack).ToList();
            L($"  {label,-15} {part.Count,6} frames: offset median {Pct(x, 50):+0.00;-0.00;0.00} m, |offset| p95 {Pct(x.Select(Math.Abs), 95):F2} m, "
              + $"max {x.Max(Math.Abs):F2} m; speed {part.Average(f => f.State.GroundSpeed):F1} kt; steer up to {part.Max(f => Math.Abs(f.Telemetry.SteerDeg)):F0}°");
        }
        L("  By taxiway:");
        foreach (var g in taxi.GroupBy(f => f.Telemetry.Name))
        {
            var x = g.Select(f => f.Telemetry.CrossTrack).ToList();
            L($"    {g.Key,-12} {g.Count(),6} frames: |offset| p95 {Pct(x.Select(Math.Abs), 95):F2} m, max {x.Max(Math.Abs):F2} m");
        }
        var end = frames[^1];
        L();
        L($"--- Stop --- phase at the end: {end.Telemetry.Phase}; aircraft reference point {end.Telemetry.ToStop + Follower.StopBefore:F1} m "
          + $"before the hold-short point (target {Follower.StopBefore} m), offset {end.Telemetry.CrossTrack:+0.00;-0.00;0.00} m, "
          + $"speed {end.State.GroundSpeed:F1} kt");
        return r.ToString();
    }

    /// <summary>The route (thick blue) and the track (red), for geojson.io.</summary>
    public static string GeoJson(Layout layout, Route route, List<RouteFrame> frames)
    {
        double[] C(double e, double n) { var g = layout.Geo(e, n); return [Math.Round(g.Lon, 8), Math.Round(g.Lat, 8)]; }
        var features = new List<object>
        {
            new { type = "Feature", geometry = new { type = "LineString", coordinates = route.Points.Select(p => C(p.E, p.N)).ToArray() },
                  properties = new Dictionary<string, object> { ["name"] = "route", ["stroke"] = "#1d6fd8", ["stroke-width"] = 5 } },
            new { type = "Feature", geometry = new { type = "LineString", coordinates = frames.Where((_, i) => i % 5 == 0).Select(f => C(f.State.E, f.State.N)).ToArray() },
                  properties = new Dictionary<string, object> { ["name"] = "aircraft track", ["stroke"] = "#e63946", ["stroke-width"] = 2 } },
        };
        return JsonSerializer.Serialize(new { type = "FeatureCollection", features });
    }
}
