// Experiment D4 — FS24 Pilot Flying feasibility study.
// Does the taxiway centreline of the airport layout (Facilities API, read by experiment D2) match the painted line of
// the scenery? Hugues taxis by hand with the nose wheel on the yellow line; the console records the aircraft position
// at every frame and measures its lateral offset from the layout centreline, on straight pieces only.
// Usage: dotnet run -- [ICAO]                         record in MSFS, then analyse (layout: results\layout-ICAO.json)
//        dotnet run -- --analyse <track.csv> [ICAO]   replay the analysis of a saved "...-track.csv" file
//        dotnet run -- --synthetic [ICAO]             test the analysis on a fake track (no MSFS needed)

using System.Globalization;
using System.Text;
using PilotFlying.Experiments.CentrelineMatch;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var folder = Path.Combine(Directory.GetCurrentDirectory(), "results");
var mode = args.Length > 0 && args[0].StartsWith("--") ? args[0] : "";
var rest = mode == "" ? args : args.Skip(mode == "--analyse" ? 2 : 1).ToArray();
var icao = rest.Length > 0 ? rest[0].ToUpperInvariant() : "LFBP";
var layoutFile = Path.Combine(folder, $"layout-{icao}.json");
if (!File.Exists(layoutFile))
{
    Console.WriteLine($"{layoutFile} not found: run experiment D2 for {icao} first (dotnet run --project Experiments/D2-TaxiLayout -- {icao}).");
    return 1;
}
var layout = Layout.Load(layoutFile);
Console.WriteLine($"Layout {layout.Icao}: {layout.Segments.Count} aircraft segments.");

List<Fix>? fixes;
string basePath;
if (mode == "--analyse" && args.Length >= 2)
{
    fixes = Track.Load(args[1]);
    basePath = args[1].EndsWith("-track.csv") ? args[1][..^"-track.csv".Length] : args[1];
    Console.WriteLine(Analysis.Report(layout, fixes, Path.GetFileName(basePath)));
    return 0;
}
if (mode == "--synthetic")
{
    fixes = Analysis.Synthetic(layout);
    basePath = Path.Combine(folder, $"D4-synthetic-{icao}");
}
else
{
    fixes = Track.Record(layout, out var error);
    if (fixes is null || fixes.Count == 0)
    {
        Console.WriteLine(error.Length > 0 ? error : "No position received.");
        return 1;
    }
    if (error.Length > 0) Console.WriteLine(error);
    basePath = Path.Combine(folder, $"D4-{icao}-{DateTime.Now:yyyyMMdd-HHmmss}");
}

var report = Analysis.Report(layout, fixes, Path.GetFileName(basePath));
Console.WriteLine(report);
Track.Save(fixes, basePath + "-track.csv");
File.WriteAllText(basePath + "-report.txt", report, new UTF8Encoding(false));
File.WriteAllText(basePath + "-track.geojson", Analysis.GeoJson(layout, fixes), new UTF8Encoding(false));
Console.WriteLine($"Files written: {basePath}-report.txt, -track.csv, -track.geojson");
return 0;
