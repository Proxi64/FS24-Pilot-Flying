using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PilotFlying.Experiments.CentrelineMatch;

/// <summary>Aircraft position at one simulation frame (PLANE LATITUDE / LONGITUDE: the aircraft reference point).</summary>
internal sealed record Fix(double T, double Lat, double Lon, double Heading, double GroundSpeed, bool OnGround);

internal static class Track
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Save(List<Fix> fixes, string file)
    {
        var s = new StringBuilder("t,lat,lon,heading,groundSpeed,onGround\n");
        foreach (var f in fixes)
            s.Append($"{f.T.ToString("R", Inv)},{f.Lat.ToString("R", Inv)},{f.Lon.ToString("R", Inv)},{f.Heading.ToString("R", Inv)},"
                     + $"{f.GroundSpeed.ToString("R", Inv)},{(f.OnGround ? 1 : 0)}\n");
        File.WriteAllText(file, s.ToString(), new UTF8Encoding(false));
    }

    public static List<Fix> Load(string file) =>
        File.ReadLines(file).Skip(1).Select(l => l.Split(',')).Select(f => new Fix(double.Parse(f[0], Inv), double.Parse(f[1], Inv),
            double.Parse(f[2], Inv), double.Parse(f[3], Inv), double.Parse(f[4], Inv), f[5] == "1")).ToList();

    /// <summary>Records the track until Esc, showing the nearest taxiway and the offset live.</summary>
    public static unsafe List<Fix>? Record(Layout layout, out string error)
    {
        error = "";
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment D4", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
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

        var fixes = new List<Fix>();
        try
        {
            // SimVars checked against the MSFS 2024 SDK documentation (used by experiment A1).
            foreach (var (name, unit) in new[] { ("PLANE LATITUDE", "degrees"), ("PLANE LONGITUDE", "degrees"),
                         ("PLANE HEADING DEGREES TRUE", "degrees"), ("GROUND VELOCITY", "knots"), ("SIM ON GROUND", "bool") })
                Native.SimConnect_AddToDataDefinition(h, 1, name, unit, Native.DATATYPE_FLOAT64, 0, Native.UNUSED);
            Native.SimConnect_RequestDataOnSimObject(h, 1, 1, Native.OBJECT_ID_USER, Native.PERIOD_SIM_FRAME, 0, 0, 0, 0);

            var clock = Stopwatch.StartNew();
            var nextDisplay = 0.0;
            Console.WriteLine("Recording. Taxi with the nose wheel on the yellow centreline. Esc = stop and analyse.");
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) break;
                while (Native.SimConnect_GetNextDispatch(h, out var p, out _) >= 0 && p != IntPtr.Zero)
                {
                    var recv = (Native.Recv*)p;
                    if (recv->dwID == Native.RECV_ID_QUIT) { error = "MSFS closed."; return fixes; }
                    if (recv->dwID == Native.RECV_ID_EXCEPTION)
                    {
                        var ex = (Native.RecvException*)p;
                        error = $"SimConnect exception {ex->dwException} (packet {ex->dwSendID}).";
                        return null;
                    }
                    if (recv->dwID != Native.RECV_ID_SIMOBJECT_DATA) continue;
                    var v = (double*)((byte*)p + sizeof(Native.RecvData));
                    fixes.Add(new Fix(clock.Elapsed.TotalSeconds, v[0], v[1], v[2], v[3], v[4] > 0.5));
                }
                if (fixes.Count > 0 && clock.Elapsed.TotalSeconds >= nextDisplay)
                {
                    nextDisplay += 0.5;
                    var f = fixes[^1];
                    var (e, n) = layout.Local(f.Lat, f.Lon);
                    var near = layout.Nearest(e, n, f.Heading);
                    Console.Write(near is { } x
                        ? $"\r  {f.GroundSpeed,5:F1} kt   {x.Seg.Name,-10} offset {x.Offset,6:+0.00;-0.00} m   angle {x.Angle,5:F1}°   {fixes.Count} fixes   "
                        : $"\r  {f.GroundSpeed,5:F1} kt   no taxiway nearby   {fixes.Count} fixes   ");
                }
                Thread.Sleep(5);
            }
            Console.WriteLine();
            return fixes;
        }
        finally
        {
            Native.SimConnect_Close(h);
        }
    }
}
