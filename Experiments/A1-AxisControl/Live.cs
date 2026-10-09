using System.Diagnostics;

namespace PilotFlying.Experiments.AxisControl;

/// <summary>Runs a script in MSFS: sends the key events at the phase rate and records every simulation frame.</summary>
internal static unsafe class Live
{
    private const uint Definition = 1;
    private const uint Request = 1;
    private const uint FirstEvent = 100;

    public static Recording? Run(ScriptDef script, out string error)
    {
        error = "";
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment A1", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
            {
                error = "MSFS 2024 cannot be reached: start the simulator and load a flight.";
                return null;
            }
        }
        catch (DllNotFoundException)
        {
            error = "SimConnect.dll not found next to the executable (see MsfsSdk in the .csproj).";
            return null;
        }

        Native.timeBeginPeriod(1);
        var run = new Session(h, script);
        try
        {
            return run.Execute(out error);
        }
        finally
        {
            run.Neutralise();
            Native.SimConnect_Close(h);
            Native.timeEndPeriod(1);
        }
    }

    private sealed class Session(IntPtr h, ScriptDef script)
    {
        private readonly Recording _rec = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Dictionary<uint, string> _setupPackets = [];
        private readonly List<string> _exceptions = [];
        private double[]? _latest;
        private string? _phase;
        private double[] _cmd = NaNs();
        private bool _quit;

        private double Now => _clock.Elapsed.TotalSeconds;

        private static double[] NaNs() => Enumerable.Repeat(double.NaN, Script.Channels.Length).ToArray();

        public Recording? Execute(out string error)
        {
            error = "";
            for (var i = 0; i < Script.Channels.Length; i++)
            {
                Native.SimConnect_MapClientEventToSimEvent(h, FirstEvent + (uint)i, Script.Channels[i].Event);
                Remember($"event {Script.Channels[i].Event}");
            }
            foreach (var m in Script.Measures)
            {
                Native.SimConnect_AddToDataDefinition(h, Definition, m.Name, m.Unit, Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
                Remember($"SimVar \"{m.Name}\" ({m.Unit})");
            }
            Native.SimConnect_RequestDataOnSimObject(h, Request, Definition, Native.OBJECT_ID_USER, Native.PERIOD_SIM_FRAME, 0, 0, 0, 0);

            // Setup check: SimConnect reports unknown events or SimVars as exceptions.
            var until = Now + 5;
            while (Now < until && (_latest is null || Now < until - 3.5) && !_quit) { Pump(); Thread.Sleep(1); }
            if (_exceptions.Count > 0)
            {
                error = "SimConnect rejected part of the setup:\n  " + string.Join("\n  ", _exceptions);
                return null;
            }
            if (_latest is null)
            {
                error = "No aircraft data received: load a flight first.";
                return null;
            }

            var m0 = _latest;
            _rec.Notes.Add($"Script: {script.Name}. Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _rec.Notes.Add($"On ground {m0[Script.MOnGround]}, ground speed {m0[Script.MGroundSpeed]:F2} kt, engine combustion {m0[Script.MCombustion]}, "
                           + $"parking brake {m0[Script.MParkingBrake]}, camera state {m0[Script.MCamera]}, "
                           + $"nose wheel max steering {m0[Script.MMaxSteer]:F1}°, nose wheel lock {m0[Script.MNoseLock]}");
            Console.WriteLine(_rec.Notes[^1]);
            if (script.Precheck(m0) is { } refused)
            {
                error = refused;
                return null;
            }

            var phases = script.Phases;
            Console.WriteLine();
            if (script.Chained)
                Console.WriteLine($"{phases.Length} phases chained without stopping ({phases.Sum(p => p.Duration):F0} s). Esc = emergency stop at any time.");
            else
            {
                Console.WriteLine($"{phases.Length} phases. Before each one: Enter = run it, S = skip it, Esc = stop the experiment.");
                Console.WriteLine("During a phase, Esc stops everything and puts the controls back to neutral.");
            }
            for (var i = 0; i < phases.Length && !_quit; i++)
            {
                var phase = phases[i];
                Console.WriteLine();
                Console.WriteLine($"[{i + 1}/{phases.Length}] {phase.Name} ({phase.Question}, {phase.Duration:F0} s)");
                if (!script.Chained || i == 0)
                {
                    Console.WriteLine($"    {phase.Instruction}");
                    Console.Write(script.Chained ? "    Enter = start, Esc = cancel: " : "    Enter = run, S = skip, Esc = stop: ");
                    var key = WaitKey(allowSkip: !script.Chained);
                    Console.WriteLine();
                    if (key == ConsoleKey.Escape) break;
                    if (key == ConsoleKey.S) continue;
                }
                if (!RunPhase(phase)) break;
            }

            _rec.Notes.Add($"End: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (_exceptions.Count > 0) _rec.Notes.AddRange(_exceptions.Select(e => "Exception during the run: " + e));
            return _rec;
        }

        /// <returns>false if the user, the watchdog or MSFS stopped the experiment.</returns>
        private bool RunPhase(Phase phase)
        {
            _phase = phase.Name;
            _cmd = NaNs();
            var start = Now;
            var period = phase.RateHz > 0 ? 1.0 / phase.RateHz : double.PositiveInfinity;
            var nextSend = 0.0;
            var nextDisplay = 0.0;
            var driven = new bool[Script.Channels.Length];
            while (true)
            {
                var t = Now - start;
                if (t >= phase.Duration) break;
                if (_quit) return false;
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    Stop("Esc pressed");
                    return false;
                }
                if (script.Watchdog(_latest!) is { } danger)
                {
                    Stop(danger);
                    return false;
                }
                if (t >= nextSend)
                {
                    var command = phase.Command(t, _latest!);
                    for (var c = 0; c < command.Length; c++)
                    {
                        if (command[c] is { } v)
                        {
                            Transmit(c, v);
                            _cmd[c] = v;
                            driven[c] = true;
                        }
                        else _cmd[c] = double.NaN;
                    }
                    nextSend += period;
                    if (nextSend < t) nextSend = t; // late: do not try to catch up with a burst
                }
                if (t >= nextDisplay)
                {
                    Console.Write($"\r    {t,5:F1} s / {phase.Duration:F0} s   ground speed {_latest![Script.MGroundSpeed],4:F1} kt   ");
                    nextDisplay += 1;
                }
                Pump();
                Thread.Sleep(1);
            }
            Console.WriteLine($"\r    done ({phase.Duration:F0} s)                                   ");
            _phase = null;
            if (phase.NeutralAtEnd)
                for (var c = 0; c < driven.Length; c++)
                    if (driven[c]) Transmit(c, 0, record: false);
            return true;
        }

        private void Stop(string reason)
        {
            Console.WriteLine($"\n    STOP: {reason}. Controls put back to the safe state.");
            _rec.Notes.Add($"Stopped during {_phase} at {Now:F2} s: {reason}");
            _phase = null;
            Neutralise();
        }

        private ConsoleKey WaitKey(bool allowSkip)
        {
            while (!_quit)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).Key;
                    if (k is ConsoleKey.Enter or ConsoleKey.Escape || (allowSkip && k == ConsoleKey.S)) return k;
                }
                Pump();
                Thread.Sleep(5);
            }
            return ConsoleKey.Escape;
        }

        private void Transmit(int channel, double value, bool record = true)
        {
            var raw = Script.Channels[channel].ToRaw(value);
            Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + (uint)channel, unchecked((uint)raw),
                Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
            if (record && _phase is not null) _rec.Sends.Add(new Send(Now, _phase, channel, value));
        }

        /// <summary>Sends the script's safe state (neutral controls; when rolling, also idle, brakes and parking brake).</summary>
        public void Neutralise()
        {
            for (var c = 0; c < Script.Channels.Length; c++)
                if (script.SafeState[c] is { } v) Transmit(c, v, record: false);
        }

        private void Remember(string what)
        {
            if (Native.SimConnect_GetLastSentPacketID(h, out var packet) >= 0) _setupPackets[packet] = what;
        }

        private void Pump()
        {
            while (Native.SimConnect_GetNextDispatch(h, out var p, out _) >= 0 && p != IntPtr.Zero)
            {
                switch (((Native.Recv*)p)->dwID)
                {
                    case Native.RECV_ID_QUIT:
                        Console.WriteLine("\nMSFS closed.");
                        _quit = true;
                        return;
                    case Native.RECV_ID_EXCEPTION:
                        var ex = (Native.RecvException*)p;
                        var what = _setupPackets.TryGetValue(ex->dwSendID, out var w) ? w : $"packet {ex->dwSendID}";
                        _exceptions.Add($"exception {ex->dwException} on {what} (index {ex->dwIndex}) at {Now:F2} s");
                        break;
                    case Native.RECV_ID_SIMOBJECT_DATA:
                        var d = (Native.RecvSimObjectData*)p;
                        if (d->dwRequestID != Request) break;
                        var size = sizeof(Native.RecvSimObjectData) + Script.Measures.Length * 8;
                        if (d->Header.dwSize < size) break;
                        var values = new double[Script.Measures.Length];
                        var data = (double*)((byte*)p + sizeof(Native.RecvSimObjectData));
                        for (var i = 0; i < values.Length; i++) values[i] = data[i];
                        _latest = values;
                        if (_phase is not null) _rec.Samples.Add(new Sample(Now, _phase, (double[])_cmd.Clone(), values));
                        break;
                }
            }
        }
    }
}
