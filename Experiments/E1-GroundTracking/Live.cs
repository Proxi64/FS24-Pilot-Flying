using System.Diagnostics;

namespace PilotFlying.Experiments.GroundTracking;

/// <summary>One recorded frame: the state, the telemetry and the command sent.</summary>
internal sealed record Frame(State State, Telemetry Telemetry, Command Command, double SteerAngle, double Lat, double Lon);

/// <summary>Runs the tracker in MSFS: reads the aircraft at every frame, sends the commands at 30 Hz.</summary>
internal static unsafe class Live
{
    // SimVars and key events checked against the MSFS 2024 SDK documentation and used by experiment A1.
    private static readonly (string Name, string Unit)[] Vars =
    [
        ("PLANE LATITUDE", "degrees"), ("PLANE LONGITUDE", "degrees"), ("PLANE HEADING DEGREES TRUE", "degrees"),
        ("GROUND VELOCITY", "knots"), ("ROTATION VELOCITY BODY Y", "degrees per second"), ("SIM ON GROUND", "bool"),
        ("ENG COMBUSTION:1", "bool"), ("BRAKE PARKING POSITION", "bool"), ("GEAR CENTER STEER ANGLE", "degrees"),
    ];
    private static readonly string[] Events = ["AXIS_RUDDER_SET", "THROTTLE1_SET", "AXIS_LEFT_BRAKE_SET", "AXIS_RIGHT_BRAKE_SET", "PARKING_BRAKE_SET"];
    private const uint FirstEvent = 10;

    public static List<Frame>? Run(Layout layout, List<string> notes, out string error)
    {
        error = "";
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment E1", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
            {
                error = "MSFS 2024 cannot be reached: start the simulator and load the flight.";
                return null;
            }
        }
        catch (DllNotFoundException)
        {
            error = "SimConnect.dll not found next to the executable (see MsfsSdk in the .csproj).";
            return null;
        }

        Native.timeBeginPeriod(1);
        var frames = new List<Frame>();
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
            bool Pump()
            {
                var got = false;
                while (Native.SimConnect_GetNextDispatch(h, out var p, out _) >= 0 && p != IntPtr.Zero)
                {
                    var recv = (Native.Recv*)p;
                    if (recv->dwID == Native.RECV_ID_QUIT) quit = "MSFS closed";
                    else if (recv->dwID == Native.RECV_ID_EXCEPTION)
                    {
                        var ex = (Native.RecvException*)p;
                        notes.Add($"SimConnect exception {ex->dwException} (packet {ex->dwSendID}) at {clock.Elapsed.TotalSeconds:F2} s");
                    }
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
            void Send(Command c)
            {
                void Tx(int i, int raw) => Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + (uint)i,
                    unchecked((uint)raw), Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Tx(0, (int)Math.Round(Math.Clamp(c.Rudder, -1, 1) * 16383));
                Tx(1, (int)Math.Round(Math.Clamp(c.Throttle, 0, 1) * 16383));
                var brake = (int)Math.Round(-16383 + Math.Clamp(c.Brake, 0, 1) * 32766); // −16383 = 0 %, +16383 = 100 %
                Tx(2, brake);
                Tx(3, brake);
                Tx(4, c.ParkingBrake > 0.5 ? 1 : 0);
            }

            var until = clock.Elapsed.TotalSeconds + 5;
            while (latest is null && clock.Elapsed.TotalSeconds < until) { Pump(); Thread.Sleep(5); }
            if (latest is null) { error = "No aircraft data received: load the flight first."; return null; }
            if (notes.Count > 0) { error = "SimConnect rejected part of the setup: " + string.Join("; ", notes); return null; }

            var m = latest;
            var (e0, n0) = layout.Local(m[0], m[1]);
            var check = $"On ground {m[5]}, ground speed {m[3]:F1} kt, engine {m[6]}, parking brake {m[7]}, heading {m[2]:F1}°";
            Console.WriteLine(check);
            notes.Add(check);
            if (m[5] < 0.5 || m[3] > 1) { error = "The aircraft must be stopped on the ground."; return null; }
            if (m[6] < 0.5) { error = "The engine must be running (idle)."; return null; }
            if (m[7] < 0.5) { error = "Set the parking brake before starting."; return null; }
            var line = Line.From(layout, e0, n0, m[2], out var refused);
            if (line is null) { error = "Cannot start: " + refused + "."; return null; }

            var info = $"Line: {line.Name} (path {line.Path}), bearing {line.Bearing:F1}°, {line.StopAlong - line.StartAlong:F0} m of automatic taxi at "
                       + $"{Tracker.TargetSpeed} kt, then stop. Initial offset {line.CrossTrack(e0, n0):+0.00;-0.00} m.";
            Console.WriteLine(info);
            notes.Add(info);
            // Consistency check: the aircraft is placed with the nose wheel on the painted line, and the layout matches
            // the paint within about 0.5 m (test D4). A larger measured offset means a measurement problem (as the
            // spherical conversion of runs 1 and 2), not an aircraft off the line: do not start silently.
            var initial = line.CrossTrack(e0, n0);
            if (Math.Abs(initial) > Tracker.MaxInitialOffset)
            {
                Console.WriteLine($"WARNING: initial offset {initial:+0.00;-0.00} m. If the nose wheel is on the yellow line, the measurement is wrong:");
                Console.WriteLine("Esc and report it. If the aircraft really is off the line, type C to continue.");
                notes.Add($"Initial offset {initial:+0.00;-0.00} m above {Tracker.MaxInitialOffset} m: confirmation asked");
                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var k = Console.ReadKey(true).Key;
                        if (k == ConsoleKey.Escape) { notes.Add("Cancelled after the initial offset warning"); return frames; }
                        if (k == ConsoleKey.C) { notes.Add("Continued after the initial offset warning"); break; }
                    }
                    Pump();
                    Thread.Sleep(5);
                }
            }
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

            var tracker = new Tracker(line);
            var nextSend = 0.0;
            var nextDisplay = 0.0;
            Command last = Tracker.Safe;
            while (tracker.Phase != "done")
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
                var (cmd, tm) = tracker.Update(state);
                if (tracker.Watchdog(state, tm) is { } danger)
                {
                    notes.Add($"Emergency stop at {t:F1} s: {danger}");
                    Console.WriteLine($"\n   STOP: {danger}.");
                    frames.Add(new Frame(state, tm, Tracker.Safe, d[8], d[0], d[1]));
                    break;
                }
                if (t >= nextSend)
                {
                    Send(cmd);
                    last = cmd;
                    nextSend = Math.Max(nextSend + 1.0 / 30, t);
                }
                frames.Add(new Frame(state, tm, last, d[8], d[0], d[1]));
                if (t >= nextDisplay)
                {
                    nextDisplay = t + 0.5;
                    Console.Write($"\r   {tm.Phase,-7} {d[3],4:F1} kt   offset {tm.CrossTrack,6:+0.00;-0.00} m   heading {tm.HeadingError,5:+0.0;-0.0}°   "
                                  + $"steer {tm.SteerDeg,5:+0.0;-0.0}°   {tm.Along - line.StartAlong,4:F0} m   ");
                }
            }
            Console.WriteLine();
            return frames;
        }
        finally
        {
            // Whatever happened: idle, full brakes, parking brake, rudder centred.
            for (var i = 0; i < 3; i++)
            {
                Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + 0, 0, Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + 1, 0, Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + 2, 16383, Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + 3, 16383, Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + 4, 1, Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
                Thread.Sleep(30);
            }
            Native.SimConnect_Close(h);
            Native.timeEndPeriod(1);
        }
    }
}
