// Experiment E1 — FS24 Pilot Flying feasibility study: the first automatic taxi.
// The C172 is stopped on a straight taxiway centreline (engine at idle, parking brake set). The program releases the
// brakes, taxis at 5 kt for up to 150 m along the layout centreline (read by experiment D2), correcting the lateral
// offset and the heading error at every frame through the rudder (which steers the nose wheel), then stops and sets
// the parking brake.
//   E1  is ground path following stable at the rate measured in A3?
// Emergency stop (idle, full brakes, parking brake): Esc, above 10 kt, more than 4 m off the line, more than 25° off
// its axis, or off the ground.
// Usage: dotnet run -- [ICAO]                          run in MSFS (layout: results\layout-ICAO.json)
//        dotnet run -- --analyse <frames.csv>          replay the analysis of a saved "...-frames.csv" file
//        dotnet run -- --synthetic                     run the law on a simple C172 model (no MSFS needed)
//        dotnet run -- --line ICAO path along heading  which straight line would be followed from that point
// Route mode (taxi from the parking spot to a hold-short point, following a clearance):
//        dotnet run -- ICAO --route C,NG,NW,N5                  run in MSFS, aircraft at a parking spot
//        dotnet run -- --route-synthetic ICAO parking [metres ahead] C,NG,NW,N5   simulate it from that parking spot (no MSFS)
//        dotnet run -- --route-analyse ICAO <frames.csv> C,NG,NW,N5   replay the analysis of a saved route run

using System.Globalization;
using System.Text;
using PilotFlying.Experiments.GroundTracking;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var folder = Path.Combine(Directory.GetCurrentDirectory(), "results");
List<Frame>? frames;
List<string> notes;
string basePath;

if (args.Length >= 2 && args[0] == "--analyse")
{
    (frames, notes) = Analysis.Load(args[1]);
    Console.WriteLine(Analysis.Report(frames, notes, Path.GetFileName(args[1])));
    return 0;
}
if (args.Length >= 5 && args[0] == "--line")
{
    // Which straight line would be followed from this position: --line ICAO path along heading (no MSFS needed).
    var l = Layout.Load(Path.Combine(folder, $"layout-{args[1].ToUpperInvariant()}.json"));
    var seg = l.Segments.First(s => s.Path == int.Parse(args[2]));
    var d = double.Parse(args[3]);
    double ue = (seg.BE - seg.AE) / seg.Length, un = (seg.BN - seg.AN) / seg.Length;
    var found = Line.From(l, seg.AE + ue * d, seg.AN + un * d, double.Parse(args[4]), out var why);
    Console.WriteLine(found is null ? "Refused: " + why : $"{found.Name} (path {found.Path}), bearing {found.Bearing:F1}°, {found.StopAlong - found.StartAlong:F0} m of automatic taxi");
    return 0;
}
if (args.Length >= 4 && args[0] == "--route-synthetic" || args.Length >= 3 && args[1] == "--route" || args.Length >= 4 && args[0] == "--route-analyse")
{
    var icao = (args[0].StartsWith("--") ? args[1] : args[0]).ToUpperInvariant();
    var layout = Layout.Load(Path.Combine(folder, $"layout-{icao}.json"));
    var clearance = args[^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    List<RouteFrame>? routeFrames;
    Route? route;
    if (args[0] == "--route-analyse")
    {
        (routeFrames, notes) = RouteMode.Load(args[2]);
        var pk = layout.Parkings.MinBy(p => Math.Pow(p.Value.E - routeFrames[0].State.E, 2) + Math.Pow(p.Value.N - routeFrames[0].State.N, 2)).Key;
        route = Route.Find(layout, pk, clearance, routeFrames[0].State.Heading, out _);
        Console.WriteLine(RouteMode.Report(routeFrames, notes, route, Path.GetFileName(args[2])));
        return 0;
    }
    if (args[0] == "--route-synthetic")
    {
        // Optional argument before the clearance: start that many metres ahead of the parking point (LFBP 8A: 14.6 m).
        var startAhead = args.Length >= 5 && double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 0;
        (routeFrames, notes, route) = RouteMode.Simulate(layout, int.Parse(args[2]), clearance, startAhead);
        basePath = Path.Combine(folder, $"E1-route-synthetic-{icao}");
    }
    else
    {
        Console.WriteLine($"Experiment E1, route mode at {icao}: Asobo C172 at a parking spot, engine at idle, parking brake set. Clearance: {string.Join(" ", clearance)}.");
        notes = [];
        routeFrames = RouteMode.Run(layout, clearance, notes, out route, out var routeError);
        if (routeFrames is null) { Console.WriteLine(routeError); return 1; }
        basePath = Path.Combine(folder, $"E1-route-{icao}-{DateTime.Now:yyyyMMdd-HHmmss}");
    }
    Directory.CreateDirectory(folder);
    var routeReport = RouteMode.Report(routeFrames, notes, route, Path.GetFileName(basePath));
    Console.WriteLine(routeReport);
    RouteMode.Save(routeFrames, notes, basePath);
    File.WriteAllText(basePath + "-report.txt", routeReport, new UTF8Encoding(false));
    if (route is not null) File.WriteAllText(basePath + "-track.geojson", RouteMode.GeoJson(layout, route, routeFrames), new UTF8Encoding(false));
    Console.WriteLine($"Files written: {basePath}-report.txt, -frames.csv, -notes.txt, -track.geojson");
    return 0;
}
if (args.Length >= 1 && args[0] == "--synthetic")
{
    (frames, notes) = Analysis.Simulate();
    basePath = Path.Combine(folder, "E1-synthetic");
}
else
{
    var icao = args.Length > 0 ? args[0].ToUpperInvariant() : "LFBP";
    var layoutFile = Path.Combine(folder, $"layout-{icao}.json");
    if (!File.Exists(layoutFile))
    {
        Console.WriteLine($"{layoutFile} not found: run experiment D2 for {icao} first.");
        return 1;
    }
    var layout = Layout.Load(layoutFile);
    Console.WriteLine($"Experiment E1 at {icao}: Asobo C172 stopped on a straight taxiway centreline, engine at idle, parking brake set.");
    notes = [];
    frames = Live.Run(layout, notes, out var error);
    if (frames is null)
    {
        Console.WriteLine(error);
        return 1;
    }
    basePath = Path.Combine(folder, $"E1-{icao}-{DateTime.Now:yyyyMMdd-HHmmss}");
}

Directory.CreateDirectory(folder);
var report = Analysis.Report(frames, notes, Path.GetFileName(basePath));
Console.WriteLine(report);
Analysis.Save(frames, notes, basePath);
File.WriteAllText(basePath + "-report.txt", report, new UTF8Encoding(false));
Console.WriteLine($"Files written: {basePath}-report.txt, -frames.csv, -notes.txt");
return 0;
