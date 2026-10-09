// Experiment B1 — FS24 Pilot Flying feasibility study.
// Fenix A320 at a parking spot, powered (batteries + external power), engines and hydraulics off. Through native
// SimConnect only (LVars, key events, input events, no WASM module), it presses FCU buttons, turns FCU knobs, presses
// autobrake buttons, moves the flaps lever and the beacon switch, sends the standard axes and the tiller, and logs
// every watched variable that reacts:
//   B1  does the Fenix accept the standard axes (sidestick, rudder, thrust levers)?
//   B2  which control for the tiller?
//   B3  do the FCU controls, flaps lever and autobrake work when written from outside?
//   B4  can we read the state (annunciators, FCU windows, levers)?
// Every action is undone (second press, knob turned back, lever back), axes end centred and thrust levers at idle.
// Usage: dotnet run                          run in MSFS
//        dotnet run -- --analyse <file>      replay the analysis of a saved "...-log.csv" file
//        dotnet run -- --synthetic           test the report on fake data (no MSFS needed)

using System.Globalization;
using System.Text;
using PilotFlying.Experiments.FenixControls;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var folder = Path.Combine(Directory.GetCurrentDirectory(), "results");
Log? log;
string basePath;

if (args.Length >= 2 && args[0] == "--analyse")
{
    log = Log.Load(args[1]);
    basePath = args[1].EndsWith("-log.csv") ? args[1][..^"-log.csv".Length] : args[1];
    Console.WriteLine(Analysis.Report(log, Path.GetFileName(basePath)));
    return 0;
}
if (args.Length >= 1 && args[0] == "--synthetic")
{
    log = Analysis.Synthetic();
    basePath = Path.Combine(folder, "B1-synthetic");
}
else
{
    Console.WriteLine("Experiment B1: Fenix A320 at a parking spot, batteries and EXT PWR on, engines and hydraulics off.");
    log = Probe.Run(out var error);
    if (log is null)
    {
        Console.WriteLine(error);
        return 1;
    }
    basePath = Path.Combine(folder, $"B1-{DateTime.Now:yyyyMMdd-HHmmss}");
}

Directory.CreateDirectory(folder);
var report = Analysis.Report(log, Path.GetFileName(basePath));
Console.WriteLine();
Console.WriteLine(report);
log.Save(basePath + "-log.csv");
File.WriteAllText(basePath + "-report.txt", report, new UTF8Encoding(false));
Console.WriteLine($"Files written: {basePath}-report.txt, -log.csv");
return 0;
