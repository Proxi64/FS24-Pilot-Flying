using System.Diagnostics;
using System.Text;

namespace PilotFlying.Experiments.MobiFlight;

/// <summary>
/// Talks to the MobiFlight WASM module as a third-party client, following its documented protocol
/// (https://github.com/MobiFlight/MobiFlight-WASM-Module, MIT): commands are strings of 1024 bytes written to
/// "&lt;client&gt;.Command", answers come back on "&lt;client&gt;.Response", and each registered variable is a float
/// at offset 4 × index in "&lt;client&gt;.LVars", sent only when its value changes.
/// </summary>
internal static unsafe class Probe
{
    public const string ClientName = "FS24PilotFlying";
    private const int MessageSize = 1024;

    /// <summary>Variables registered with the module (calculator code), read back at every change.</summary>
    public static readonly string[] Vars =
    [
        "(E:SIMULATION TIME,seconds)",   // changes at every frame: update rate of the module (C4)
        "(A:LIGHT BEACON,Bool)",         // compared with the same SimVar read directly
        "(L:FS24PF_TEST)",               // our own LVar: write then read back
        "(A:GENERAL ENG RPM:1,rpm)",
    ];

    // Client data areas (our numbering), definitions and requests.
    private const uint AreaDefaultCommand = 0, AreaDefaultResponse = 1, AreaCommand = 2, AreaResponse = 3, AreaVars = 4;
    private const uint DefString = 1, DefVarsStart = 100, DefDirect = 2;
    private const uint ReqDefaultResponse = 1, ReqResponse = 2, ReqVarsStart = 100, ReqDirect = 50, ReqAircraft = 60;

    /// <summary>SimVars read directly by SimConnect at every frame, to compare with the module.</summary>
    public static readonly (string Name, string Unit)[] Direct = [("LIGHT BEACON", "bool"), ("GENERAL ENG RPM:1", "rpm")];

    public static Log? Run(string lvarsFile, out string error)
    {
        error = "";
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment C1", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
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
        try
        {
            return new Session(h).Execute(lvarsFile);
        }
        finally
        {
            Native.SimConnect_Close(h);
            Native.timeEndPeriod(1);
        }
    }

    private sealed class Session(IntPtr h)
    {
        private readonly Log _log = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<string> _lvars = [];
        private bool _listing, _quit;

        private double Now => _clock.Elapsed.TotalSeconds;

        public Log Execute(string lvarsFile)
        {
            // Direct SimVars at every frame, and the loaded aircraft.
            foreach (var (name, unit) in Direct)
                Native.SimConnect_AddToDataDefinition(h, DefDirect, name, unit, Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
            Native.SimConnect_RequestDataOnSimObject(h, ReqDirect, DefDirect, Native.OBJECT_ID_USER, Native.PERIOD_SIM_FRAME, 0, 0, 0, 0);
            Native.SimConnect_RequestSystemState(h, ReqAircraft, "AircraftLoaded");

            // Default MobiFlight channel, created by the module at start-up.
            Native.SimConnect_MapClientDataNameToID(h, "MobiFlight.Command", AreaDefaultCommand);
            Native.SimConnect_MapClientDataNameToID(h, "MobiFlight.Response", AreaDefaultResponse);
            Native.SimConnect_AddToClientDataDefinition(h, DefString, 0, MessageSize, 0, Native.UNUSED);
            Native.SimConnect_RequestClientData(h, AreaDefaultResponse, ReqDefaultResponse, DefString,
                Native.CLIENT_DATA_PERIOD_ON_SET, Native.CLIENT_DATA_REQUEST_FLAG_CHANGED, 0, 0, 0);
            Wait(0.5);

            Step("1. Ping on the default channel");
            Send(AreaDefaultCommand, "MF.Ping");
            if (!WaitResponse(r => r == "MF.Pong", 5))
            {
                Fail("no MF.Pong within 5 s: the MobiFlight module does not answer (not installed, not loaded, or no flight loaded)");
                return _log;
            }
            Send(AreaDefaultCommand, "MF.Version.Get");
            WaitResponse(r => r.StartsWith("MF.Version."), 3);

            Step("2. Register our own client");
            Send(AreaDefaultCommand, $"MF.Clients.Add.{ClientName}");
            if (!WaitResponse(r => r == $"MF.Clients.Add.{ClientName}.Finished", 5))
            {
                Fail("the module did not confirm the registration of our client within 5 s");
                return _log;
            }
            Native.SimConnect_MapClientDataNameToID(h, $"{ClientName}.Command", AreaCommand);
            Native.SimConnect_MapClientDataNameToID(h, $"{ClientName}.Response", AreaResponse);
            Native.SimConnect_MapClientDataNameToID(h, $"{ClientName}.LVars", AreaVars);
            Native.SimConnect_RequestClientData(h, AreaResponse, ReqResponse, DefString,
                Native.CLIENT_DATA_PERIOD_ON_SET, Native.CLIENT_DATA_REQUEST_FLAG_CHANGED, 0, 0, 0);
            for (var i = 0; i < Vars.Length; i++)
            {
                Native.SimConnect_AddToClientDataDefinition(h, DefVarsStart + (uint)i, (uint)(4 * i), 4, 0, Native.UNUSED);
                Native.SimConnect_RequestClientData(h, AreaVars, ReqVarsStart + (uint)i, DefVarsStart + (uint)i,
                    Native.CLIENT_DATA_PERIOD_ON_SET, Native.CLIENT_DATA_REQUEST_FLAG_CHANGED, 0, 0, 0);
            }
            Send(AreaCommand, "MF.Ping");
            WaitResponse(r => r == "MF.Pong", 3);
            Send(AreaCommand, "MF.SimVars.Clear");
            Wait(0.5);

            Step("3. Register variables");
            foreach (var v in Vars) Send(AreaCommand, "MF.SimVars.Add." + v);
            Wait(1);

            Step("4. Update rate (10 s, do not touch anything)");
            _log.Add(Now, "mark", "rate-start");
            Wait(10);
            _log.Add(Now, "mark", "rate-end");

            Step("5. Write our own LVar");
            Send(AreaCommand, "MF.SimVars.Set.1 (>L:FS24PF_TEST)");
            Wait(1.5);
            Send(AreaCommand, "MF.SimVars.Set.0 (>L:FS24PF_TEST)");
            Wait(1.5);

            Step("6. Toggle the beacon light 4 times (watch it blink)");
            for (var i = 0; i < 4; i++)
            {
                Send(AreaCommand, "MF.SimVars.Set.0 (>K:TOGGLE_BEACON_LIGHTS)");
                Wait(1.5);
            }

            Step("7. List the aircraft's LVars");
            _listing = true;
            Send(AreaCommand, "MF.LVars.List");
            WaitResponse(r => r == "MF.LVars.List.End", 15);
            _listing = false;
            File.WriteAllLines(lvarsFile, _lvars, new UTF8Encoding(false));

            Send(AreaCommand, "MF.SimVars.Clear");
            Wait(0.5);
            _log.Add(Now, "mark", "end");
            Console.WriteLine();
            return _log;
        }

        private void Step(string title)
        {
            Console.WriteLine(title);
            _log.Add(Now, "mark", title);
        }

        private void Fail(string reason)
        {
            Console.WriteLine("   FAILED: " + reason);
            _log.Add(Now, "info", "failure", text: reason);
        }

        private void Send(uint area, string command)
        {
            var bytes = new byte[MessageSize];
            Encoding.ASCII.GetBytes(command, 0, Math.Min(command.Length, MessageSize - 1), bytes, 0);
            Native.SimConnect_SetClientData(h, area, DefString, 0, 0, MessageSize, bytes);
            _log.Add(Now, "cmd", area == AreaDefaultCommand ? "default" : "client", text: command);
        }

        private void Wait(double seconds)
        {
            var until = Now + seconds;
            while (Now < until && !_quit) { Pump(); Thread.Sleep(1); }
        }

        private bool WaitResponse(Func<string, bool> match, double seconds)
        {
            var from = _log.Entries.Count;
            var until = Now + seconds;
            while (Now < until && !_quit)
            {
                Pump();
                for (var i = from; i < _log.Entries.Count; i++)
                    if (_log.Entries[i].Kind == "resp" && match(_log.Entries[i].Text)) return true;
                from = _log.Entries.Count;
                Thread.Sleep(1);
            }
            return false;
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
                        _log.Add(Now, "info", "exception", ex->dwException, $"SimConnect exception {ex->dwException}, packet {ex->dwSendID}, index {ex->dwIndex}");
                        break;
                    case Native.RECV_ID_SYSTEM_STATE:
                        var st = (Native.RecvSystemState*)p;
                        if (st->dwRequestID == ReqAircraft)
                            _log.Add(Now, "info", "aircraft", text: Text((byte*)p + sizeof(Native.RecvSystemState), (int)recv->dwSize - sizeof(Native.RecvSystemState)));
                        break;
                    case Native.RECV_ID_SIMOBJECT_DATA:
                        var d = (Native.RecvData*)p;
                        if (d->dwRequestID != ReqDirect) break;
                        var values = (double*)((byte*)p + sizeof(Native.RecvData));
                        for (var i = 0; i < Direct.Length; i++) _log.Add(Now, "direct", Direct[i].Name, values[i]);
                        break;
                    case Native.RECV_ID_CLIENT_DATA:
                        var c = (Native.RecvData*)p;
                        var data = (byte*)p + sizeof(Native.RecvData);
                        if (c->dwRequestID is ReqDefaultResponse or ReqResponse)
                        {
                            var text = Text(data, Math.Min(MessageSize, (int)recv->dwSize - sizeof(Native.RecvData)));
                            _log.Add(Now, "resp", c->dwRequestID == ReqResponse ? "client" : "default", text: text);
                            if (_listing && !text.StartsWith("MF.")) _lvars.Add(text);
                        }
                        else if (c->dwRequestID >= ReqVarsStart && c->dwRequestID < ReqVarsStart + Vars.Length)
                            _log.Add(Now, "mf", Vars[c->dwRequestID - ReqVarsStart], *(float*)data);
                        break;
                }
            }
        }

        private static string Text(byte* p, int max)
        {
            var n = 0;
            while (n < max && p[n] != 0) n++;
            return Encoding.ASCII.GetString(p, n);
        }
    }
}
