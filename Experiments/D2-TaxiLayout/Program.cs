// Experiment D2 — FS24 Pilot Flying feasibility study.
// Reads from MSFS 2024 (the main menu is enough) the complete taxi layout of one airport, with every field that
// automatic taxiing will need, then checks:
//   D1  taxi point types (hold-short points);
//   D2  whether a path's NAME_INDEX is its index in the TAXI_NAME list (each name should form a continuous track);
//   D3  taxi path types;
//   -   the BIAS_X / BIAS_Z convention (east / north?), by comparing runway paths with the runway centrelines.
// Usage: dotnet run -- [ICAO] [output folder]      (defaults: LFBP, .\results)

using System.Diagnostics;
using System.Globalization;
using System.Text;
using PilotFlying.Experiments.TaxiLayout;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var icao = args.Length > 0 ? args[0].Trim().ToUpperInvariant() : "LFBP";
var folder = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "results");

var layout = LayoutReader.Read(icao, out var error);
if (layout is null)
{
    Console.WriteLine(error);
    return 1;
}

Directory.CreateDirectory(folder);
var report = Analysis.Report(layout);
Console.WriteLine(report);
File.WriteAllText(Path.Combine(folder, $"report-{icao}.txt"), report, new UTF8Encoding(false));
File.WriteAllText(Path.Combine(folder, $"layout-{icao}.json"), Outputs.Json(layout), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(folder, $"layout-{icao}.geojson"), Outputs.GeoJson(layout), new UTF8Encoding(false));
Console.WriteLine($"Files written to {folder}: report-{icao}.txt, layout-{icao}.json, layout-{icao}.geojson");
return 0;

// ---------------------------------------------------------------------------------------------- data read

internal sealed record Runway(double Lat, double Lon, double Alt, float Heading, float Length, float Width,
    int Number1, int Designator1, int Number2, int Designator2, string Ils1, string Ils2)
{
    public string Name => $"{Analysis.EndName(Number1, Designator1)}/{Analysis.EndName(Number2, Designator2)}";
}

internal sealed record RunwayStart(double Lat, double Lon, double Alt, float Heading, int Number, int Designator, int Type);
internal sealed record Parking(int Index, int Type, int Name, uint Number, float Heading, float Radius, float X, float Z);
internal sealed record TaxiPoint(int Index, int Type, int Orientation, float X, float Z);
internal sealed record TaxiPath(int Index, int Type, float Width, uint Weight, int RunwayNumber, int RunwayDesignator,
    int CenterLine, int Start, int End, uint NameIndex);

internal sealed class AirportLayout
{
    public string Icao = "";
    public string Name = "";
    public double Lat, Lon, Alt;
    public List<Runway> Runways { get; } = [];
    public List<RunwayStart> Starts { get; } = [];
    public List<Parking> Parkings { get; } = [];
    public Dictionary<int, TaxiPoint> Points { get; } = [];
    public List<TaxiPath> Paths { get; } = [];
    public SortedDictionary<int, string> TaxiNames { get; } = [];
    public Dictionary<uint, int> UnexpectedTypes { get; } = [];
    public List<string> Errors { get; } = [];
}

// ---------------------------------------------------------------------------------------------- SimConnect reading

internal static unsafe class LayoutReader
{
    private const uint Definition = 1;
    private const uint Request = 100;

    private static readonly string[] Fields =
    [
        "OPEN AIRPORT",
        "LATITUDE", "LONGITUDE", "ALTITUDE", "ICAO", "NAME64",
        "OPEN RUNWAY",
        "LATITUDE", "LONGITUDE", "ALTITUDE", "HEADING", "LENGTH", "WIDTH",
        "PRIMARY_NUMBER", "PRIMARY_DESIGNATOR", "SECONDARY_NUMBER", "SECONDARY_DESIGNATOR",
        "PRIMARY_ILS_ICAO", "SECONDARY_ILS_ICAO",
        "CLOSE RUNWAY",
        "OPEN START",
        "LATITUDE", "LONGITUDE", "ALTITUDE", "HEADING", "NUMBER", "DESIGNATOR", "TYPE",
        "CLOSE START",
        "OPEN TAXI_PARKING",
        "TYPE", "NAME", "NUMBER", "HEADING", "RADIUS", "BIAS_X", "BIAS_Z",
        "CLOSE TAXI_PARKING",
        "OPEN TAXI_POINT",
        "TYPE", "ORIENTATION", "BIAS_X", "BIAS_Z",
        "CLOSE TAXI_POINT",
        "OPEN TAXI_PATH",
        "TYPE", "WIDTH", "WEIGHT", "RUNWAY_NUMBER", "RUNWAY_DESIGNATOR", "CENTER_LINE", "START", "END", "NAME_INDEX",
        "CLOSE TAXI_PATH",
        "OPEN TAXI_NAME",
        "NAME",
        "CLOSE TAXI_NAME",
        "CLOSE AIRPORT",
    ];

    public static AirportLayout? Read(string icao, out string error)
    {
        error = "";
        IntPtr h;
        try
        {
            if (Native.SimConnect_Open(out h, "FS24 Pilot Flying - experiment D2", IntPtr.Zero, 0, IntPtr.Zero, 0) < 0)
            {
                error = "MSFS 2024 cannot be reached: start the simulator and wait for the main menu.";
                return null;
            }
        }
        catch (DllNotFoundException)
        {
            error = "SimConnect.dll not found next to the executable (see MsfsSdk in the .csproj).";
            return null;
        }

        var layout = new AirportLayout { Icao = icao };
        try
        {
            // Packet id of each field: a SimConnect exception (unknown field…) reports this id.
            var fieldByPacket = new Dictionary<uint, string>();
            foreach (var field in Fields)
            {
                if (Native.SimConnect_AddToFacilityDefinition(h, Definition, field) < 0)
                {
                    error = $"MSFS rejected the field \"{field}\".";
                    return null;
                }
                if (Native.SimConnect_GetLastSentPacketID(h, out var packet) >= 0) fieldByPacket[packet] = field;
            }
            if (Native.SimConnect_RequestFacilityData(h, Definition, Request, icao, "") < 0)
            {
                error = $"Layout request for {icao} rejected.";
                return null;
            }

            var clock = Stopwatch.StartNew();
            var done = false;
            while (!done)
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(30))
                {
                    error = $"No complete answer from MSFS for {icao} within 30 s (unknown ICAO code?).";
                    return null;
                }
                if (Native.SimConnect_GetNextDispatch(h, out var p, out _) < 0 || p == IntPtr.Zero)
                {
                    Thread.Sleep(1);
                    continue;
                }
                switch (((Native.Recv*)p)->dwID)
                {
                    case Native.RECV_ID_QUIT:
                        error = "MSFS closed during the reading.";
                        return null;
                    case Native.RECV_ID_EXCEPTION:
                        var ex = (Native.RecvException*)p;
                        var field = fieldByPacket.TryGetValue(ex->dwSendID, out var f) ? $" (field \"{f}\")" : "";
                        layout.Errors.Add($"SimConnect exception {ex->dwException}{field}, index {ex->dwIndex}");
                        break;
                    case Native.RECV_ID_FACILITY_DATA:
                        var header = (Native.RecvFacilityData*)p;
                        if (header->UserRequestId == Request)
                            ReadElement(layout, header->Type, (int)header->ItemIndex, (byte*)p + sizeof(Native.RecvFacilityData));
                        break;
                    case Native.RECV_ID_FACILITY_DATA_END:
                        if (((Native.RecvFacilityDataEnd*)p)->RequestId == Request) done = true;
                        break;
                }
            }
            if (layout.Lat == 0 && layout.Lon == 0)
            {
                error = $"No airport {icao} in MSFS." + (layout.Errors.Count > 0 ? " " + string.Join("; ", layout.Errors) : "");
                return null;
            }
            return layout;
        }
        finally
        {
            Native.SimConnect_Close(h);
        }
    }

    private static void ReadElement(AirportLayout layout, uint type, int index, byte* data)
    {
        var r = new FieldReader(data);
        switch (type)
        {
            case Native.DATA_AIRPORT:
                layout.Lat = r.Double();
                layout.Lon = r.Double();
                layout.Alt = r.Double();
                var icaoRead = r.Text(8);
                if (icaoRead.Length > 0) layout.Icao = icaoRead;
                layout.Name = r.Text(64);
                break;
            case Native.DATA_RUNWAY:
                layout.Runways.Add(new Runway(r.Double(), r.Double(), r.Double(), r.Float(), r.Float(), r.Float(),
                    r.Int(), r.Int(), r.Int(), r.Int(), r.Text(8), r.Text(8)));
                break;
            case Native.DATA_START:
                layout.Starts.Add(new RunwayStart(r.Double(), r.Double(), r.Double(), r.Float(), r.Int(), r.Int(), r.Int()));
                break;
            case Native.DATA_TAXI_PARKING:
                layout.Parkings.Add(new Parking(index, r.Int(), r.Int(), r.UInt(), r.Float(), r.Float(), r.Float(), r.Float()));
                break;
            case Native.DATA_TAXI_POINT:
                layout.Points[index] = new TaxiPoint(index, r.Int(), r.Int(), r.Float(), r.Float());
                break;
            case Native.DATA_TAXI_PATH:
                layout.Paths.Add(new TaxiPath(index, r.Int(), r.Float(), r.UInt(), r.Int(), r.Int(), r.Int(), r.Int(), r.Int(), r.UInt()));
                break;
            case Native.DATA_TAXI_NAME:
                layout.TaxiNames[index] = r.Text(32);
                break;
            default:
                layout.UnexpectedTypes[type] = layout.UnexpectedTypes.GetValueOrDefault(type) + 1;
                break;
        }
    }
}
