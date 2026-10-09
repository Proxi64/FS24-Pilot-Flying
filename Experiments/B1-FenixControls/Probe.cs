using System.Diagnostics;
using System.Text;

namespace PilotFlying.Experiments.FenixControls;

/// <summary>Runs the actions in MSFS with native SimConnect and logs every change of the watched variables.</summary>
internal static unsafe class Probe
{
    private const uint DefWatch = 1, DefWriteStart = 100;
    private const uint ReqWatch = 1, ReqAircraft = 2, ReqEnumerate = 3, ReqGetInputEvent = 4;
    private const uint FirstEvent = 10;

    public static Log? Run(out string error)
    {
        error = "";
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment B1", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
            {
                error = "MSFS 2024 cannot be reached: start the simulator and load the Fenix.";
                return null;
            }
        }
        catch (DllNotFoundException)
        {
            error = "SimConnect.dll not found next to the executable (see MsfsSdk in the .csproj).";
            return null;
        }
        Native.timeBeginPeriod(1);
        var session = new Session(h);
        try
        {
            return session.Execute(out error);
        }
        finally
        {
            Script.SafeState(session);
            Native.SimConnect_Close(h);
            Native.timeEndPeriod(1);
        }
    }

    private sealed class Stopped : Exception;

    private sealed class Session(IntPtr h) : ICockpit
    {
        private readonly Log _log = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly string[] _watched = [.. Script.LVars.Select(l => "L:" + l), .. Script.SimVars.Select(s => s.Name)];
        private readonly Dictionary<string, double> _latest = [];
        private readonly Dictionary<uint, string> _setupPackets = [];
        private readonly List<string> _exceptions = [];
        private readonly Dictionary<string, (ulong Hash, uint Type)> _inputEvents = [];
        private readonly Dictionary<ulong, string> _inputEventNames = [];
        private readonly Dictionary<string, double> _inputEventValues = [];
        private int _enumPages, _enumPagesTotal = -1;
        private string _gotInputEvent = "";
        private bool _quit, _running;

        private double Now => _clock.Elapsed.TotalSeconds;

        public Log? Execute(out string error)
        {
            error = "";
            Native.SimConnect_RequestSystemState(h, ReqAircraft, "AircraftLoaded");
            foreach (var l in Script.LVars)
            {
                Native.SimConnect_AddToDataDefinition(h, DefWatch, "L:" + l, "number", Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
                Remember($"LVar {l}");
            }
            foreach (var (name, unit) in Script.SimVars)
            {
                Native.SimConnect_AddToDataDefinition(h, DefWatch, name, unit, Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
                Remember($"SimVar \"{name}\" ({unit})");
            }
            for (var i = 0; i < Script.Writable.Length; i++)
                Native.SimConnect_AddToDataDefinition(h, DefWriteStart + (uint)i, "L:" + Script.Writable[i], "number", Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
            for (var i = 0; i < Script.KeyEvents.Length; i++)
            {
                Native.SimConnect_MapClientEventToSimEvent(h, FirstEvent + (uint)i, Script.KeyEvents[i]);
                Remember($"event {Script.KeyEvents[i]}");
            }
            Native.SimConnect_RequestDataOnSimObject(h, ReqWatch, DefWatch, Native.OBJECT_ID_USER, Native.PERIOD_SIM_FRAME,
                Native.DATA_REQUEST_FLAG_CHANGED, 0, 0, 0);
            Native.SimConnect_EnumerateInputEvents(h, ReqEnumerate);

            var until = Now + 5;
            while (Now < until && (_latest.Count == 0 || _enumPagesTotal < 0 || _enumPages < _enumPagesTotal || Now < until - 3.5) && !_quit)
            {
                Pump();
                Thread.Sleep(1);
            }
            if (_exceptions.Count > 0)
            {
                error = "SimConnect rejected part of the setup:\n  " + string.Join("\n  ", _exceptions);
                return null;
            }
            if (_latest.Count == 0)
            {
                error = "No aircraft data received: load the Fenix at a parking spot first.";
                return null;
            }
            _log.Add(Now, "info", "input-events", _inputEvents.Count);
            if (_inputEvents.TryGetValue(Script.SpeedKnobInputEvent, out var knob))
            {
                _gotInputEvent = Script.SpeedKnobInputEvent;
                Native.SimConnect_GetInputEvent(h, ReqGetInputEvent, knob.Hash);
                Native.SimConnect_SubscribeInputEvent(h, knob.Hash);
            }

            var volts = Get("ELECTRICAL MAIN BUS VOLTAGE");
            var check = $"On ground {Get("SIM ON GROUND")}, ground speed {Get("GROUND VELOCITY"):F1} kt, engines {Get("ENG COMBUSTION:1")}/{Get("ENG COMBUSTION:2")}, "
                        + $"main bus {volts:F1} V, parking brake {Get("BRAKE PARKING POSITION")}";
            Console.WriteLine(check);
            _log.Add(Now, "info", "check", text: check);
            if (Get("SIM ON GROUND") < 0.5 || Get("GROUND VELOCITY") > 1)
            {
                error = "The aircraft must be stopped on the ground (at a parking spot).";
                return null;
            }
            if (Get("ENG COMBUSTION:1") > 0.5 || Get("ENG COMBUSTION:2") > 0.5)
            {
                error = "The engines must be off.";
                return null;
            }
            if (!(volts > 20))
            {
                error = "The aircraft is not powered: batteries and EXT PWR on, then run again.";
                return null;
            }

            Console.WriteLine();
            Console.WriteLine($"{Script.Actions.Length} actions, about {Script.Actions.Length * (Script.Settle + 1.5):F0} s. Watch the cockpit. Esc = stop.");
            Console.Write("Enter = start, Esc = cancel: ");
            while (!_quit)
            {
                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).Key;
                    if (k == ConsoleKey.Escape) return _log;
                    if (k == ConsoleKey.Enter) break;
                }
                Pump();
                Thread.Sleep(5);
            }
            Console.WriteLine();

            _running = true;
            try
            {
                for (var i = 0; i < Script.Actions.Length; i++)
                {
                    var a = Script.Actions[i];
                    Console.WriteLine($"[{i + 1,2}/{Script.Actions.Length}] {a.Name,-22} ({a.Question}) look at: {a.Look}");
                    _log.Add(Now, "act", a.Name, text: a.Question);
                    a.Do(this);
                    Wait(Script.Settle);
                }
            }
            catch (Stopped)
            {
                Console.WriteLine("Stopped (Esc).");
                _log.Add(Now, "info", "stopped", text: "Esc pressed");
            }
            _log.Add(Now, "act", "end");
            foreach (var e in _exceptions) _log.Add(Now, "info", "exception", text: e);
            return _log;
        }

        // ------------------------------------------------------------------ ICockpit

        public double Get(string name) => _latest.TryGetValue(name, out var v) ? v : double.NaN;

        public void SetL(string lvar, double value)
        {
            var i = Array.IndexOf(Script.Writable, lvar);
            if (i < 0) throw new ArgumentException($"{lvar} is not in Script.Writable");
            var v = value;
            Native.SimConnect_SetDataOnSimObject(h, DefWriteStart + (uint)i, Native.OBJECT_ID_USER, 0, 0, 8, ref v);
            _log.Add(Now, "cmd", text: $"L:{lvar} = {value}");
        }

        public void Key(string evt, int raw)
        {
            var i = Array.IndexOf(Script.KeyEvents, evt);
            Native.SimConnect_TransmitClientEvent(h, Native.OBJECT_ID_USER, FirstEvent + (uint)i, unchecked((uint)raw),
                Native.GROUP_PRIORITY_HIGHEST, Native.EVENT_FLAG_GROUPID_IS_PRIORITY);
            _log.Add(Now, "cmd", text: $"{evt} {raw}");
        }

        public double? InputEventValue(string name) => _inputEventValues.TryGetValue(name, out var v) ? v : null;

        public bool SetInputEvent(string name, double value)
        {
            if (!_inputEvents.TryGetValue(name, out var ie)) return false;
            var v = value;
            Native.SimConnect_SetInputEvent(h, ie.Hash, 8, ref v);
            _log.Add(Now, "cmd", text: $"input event {name} = {value}");
            return true;
        }

        public void Wait(double seconds)
        {
            var until = Now + seconds;
            while (Now < until && !_quit)
            {
                if (_running && Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) throw new Stopped();
                Pump();
                Thread.Sleep(1);
            }
        }

        public void Note(string text)
        {
            Console.WriteLine("   " + text);
            _log.Add(Now, "info", "note", text: text);
        }

        // ------------------------------------------------------------------ SimConnect

        private void Remember(string what)
        {
            if (Native.SimConnect_GetLastSentPacketID(h, out var packet) >= 0) _setupPackets[packet] = what;
        }

        private void Pump()
        {
            while (Native.SimConnect_GetNextDispatch(h, out var p, out _) >= 0 && p != IntPtr.Zero)
            {
                var recv = (Native.Recv*)p;
                switch (recv->dwID)
                {
                    case Native.RECV_ID_QUIT:
                        _quit = true;
                        _log.Add(Now, "info", "quit", text: "MSFS closed");
                        return;
                    case Native.RECV_ID_EXCEPTION:
                        var ex = (Native.RecvException*)p;
                        var what = _setupPackets.TryGetValue(ex->dwSendID, out var w) ? w : $"packet {ex->dwSendID}";
                        _exceptions.Add($"SimConnect exception {ex->dwException} on {what} (index {ex->dwIndex}) at {Now:F2} s");
                        break;
                    case Native.RECV_ID_SYSTEM_STATE:
                        var st = (Native.RecvSystemState*)p;
                        if (st->dwRequestID == ReqAircraft)
                            _log.Add(Now, "info", "aircraft", text: Text((byte*)p + sizeof(Native.RecvSystemState), (int)recv->dwSize - sizeof(Native.RecvSystemState)));
                        break;
                    case Native.RECV_ID_SIMOBJECT_DATA:
                        var d = (Native.RecvData*)p;
                        if (d->dwRequestID != ReqWatch || recv->dwSize < sizeof(Native.RecvData) + 8 * _watched.Length) break;
                        var values = (double*)((byte*)p + sizeof(Native.RecvData));
                        for (var i = 0; i < _watched.Length; i++)
                        {
                            if (_latest.TryGetValue(_watched[i], out var old) && old == values[i]) continue;
                            _latest[_watched[i]] = values[i];
                            _log.Add(Now, "v", _watched[i], values[i]);
                        }
                        break;
                    case Native.RECV_ID_ENUMERATE_INPUT_EVENTS:
                        var list = (Native.RecvList*)p;
                        if (list->dwRequestID != ReqEnumerate) break;
                        var desc = (Native.InputEventDescriptor*)((byte*)p + sizeof(Native.RecvList));
                        for (var i = 0; i < list->dwArraySize; i++)
                        {
                            var name = Text(desc[i].Name, 64);
                            _inputEvents[name] = (desc[i].Hash, desc[i].eType);
                            _inputEventNames[desc[i].Hash] = name;
                        }
                        _enumPages++;
                        _enumPagesTotal = (int)list->dwOutOf;
                        break;
                    case Native.RECV_ID_GET_INPUT_EVENT:
                        var g = (Native.RecvGetInputEvent*)p;
                        if (g->dwRequestID == ReqGetInputEvent && g->eType == Native.INPUT_EVENT_TYPE_DOUBLE)
                            InputEventChanged(_gotInputEvent, *(double*)((byte*)p + sizeof(Native.RecvGetInputEvent)));
                        break;
                    case Native.RECV_ID_SUBSCRIBE_INPUT_EVENT:
                        var sub = (Native.RecvSubscribeInputEvent*)p;
                        if (sub->eType == Native.INPUT_EVENT_TYPE_DOUBLE && _inputEventNames.TryGetValue(sub->Hash, out var n))
                            InputEventChanged(n, *(double*)((byte*)p + sizeof(Native.RecvSubscribeInputEvent)));
                        break;
                }
            }
        }

        private void InputEventChanged(string name, double value)
        {
            _inputEventValues[name] = value;
            _log.Add(Now, "v", "B:" + name, value);
        }

        private static string Text(byte* p, int max)
        {
            var n = 0;
            while (n < max && p[n] != 0) n++;
            return Encoding.ASCII.GetString(p, n);
        }
    }
}
