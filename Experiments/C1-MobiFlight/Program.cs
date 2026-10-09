// Experiment C1 — FS24 Pilot Flying feasibility study.
// Talks to the MobiFlight WASM module (MIT) as a third-party SimConnect client, to answer:
//   C1  does the module work under MSFS 2024 with our own registered client (read and write variables, run events)?
//   C4  how fast do the values sent "on change" by the module arrive, compared with SimConnect itself?
// It also saves the list of the loaded aircraft's LVars (needed for questions B1 to B4 on the Fenix).
// The only visible effect in the simulator: the beacon light is toggled 4 times (back to its initial state).
// Usage: dotnet run                          run in MSFS (aircraft loaded, any state)
//        dotnet run -- --analyse <file>      replay the analysis of a saved "...-log.csv" file
//        dotnet run -- --synthetic           test the report on fake data (no MSFS needed)

using System.Globalization;
using System.Text;
using PilotFlying.Experiments.MobiFlight;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var folder = Path.Combine(Directory.GetCurrentDirectory(), "results");
Directory.CreateDirectory(folder);
var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
Log? log;
List<string> lvars;
string basePath;

if (args.Length >= 2 && args[0] == "--analyse")
{
    log = Log.Load(args[1]);
    basePath = args[1].EndsWith("-log.csv") ? args[1][..^"-log.csv".Length] : args[1];
    lvars = File.Exists(basePath + "-lvars.txt") ? File.ReadAllLines(basePath + "-lvars.txt").ToList() : [];
    Console.WriteLine(Analysis.Report(log, Path.GetFileName(basePath), lvars));
    return 0;
}
if (args.Length >= 1 && args[0] == "--synthetic")
{
    (log, lvars) = Analysis.Synthetic();
    basePath = Path.Combine(folder, "C1-synthetic");
}
else
{
    Console.WriteLine("Experiment C1: MobiFlight WASM module. Aircraft loaded (at a parking spot); about 30 s; the beacon light will blink.");
    basePath = Path.Combine(folder, $"C1-{stamp}");
    log = Probe.Run(basePath + "-lvars.txt", out var error);
    if (log is null)
    {
        Console.WriteLine(error);
        return 1;
    }
    lvars = File.Exists(basePath + "-lvars.txt") ? File.ReadAllLines(basePath + "-lvars.txt").ToList() : [];
}

var report = Analysis.Report(log, Path.GetFileName(basePath), lvars);
Console.WriteLine(report);
log.Save(basePath + "-log.csv");
File.WriteAllText(basePath + "-report.txt", report, new UTF8Encoding(false));
if (!File.Exists(basePath + "-lvars.txt")) File.WriteAllLines(basePath + "-lvars.txt", lvars);
Console.WriteLine($"Files written: {basePath}-report.txt, -log.csv, -lvars.txt");
return 0;
