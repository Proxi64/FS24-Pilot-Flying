// Experiment A1 — FS24 Pilot Flying feasibility study.
// With the aircraft stopped at a parking spot, engine off, sends control axes through SimConnect key events and records
// every simulation frame, to answer:
//   A1  do elevator, ailerons, rudder, throttle and brakes follow axis events sent continuously (30 to 60 Hz)?
//   A2  what happens with the user's joystick plugged in, and when the program stops sending?
//   A3  which reading and sending rates do we reach, with how much jitter, and with which delay?
//   A4  at a standstill, does the rudder steer the nose wheel? does the tiller event (AXIS_STEERING_SET) work?
// A second script ("--rolling") repeats A4 while rolling slowly (4 kt), engine at idle, and checks that the aircraft turns.
// Usage: dotnet run                              run in MSFS, at a standstill (results in .\results)
//        dotnet run -- --rolling                 run in MSFS, rolling slowly on a straight taxiway
//        dotnet run -- --analyse <file>          replay the analysis of a saved "...-samples.csv" file
//        dotnet run -- --synthetic [--rolling]   run the analysis on fake data (no MSFS needed)

using System.Globalization;
using System.Text;
using PilotFlying.Experiments.AxisControl;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var folder = Path.Combine(Directory.GetCurrentDirectory(), "results");
var script = args.Contains("--rolling") ? Script.Rolling() : Script.Standstill;
Recording? rec;
string source;
string? basePath = null;

if (args.Length >= 2 && args[0] == "--analyse")
{
    rec = Recording.Load(args[1]);
    source = Path.GetFileName(args[1]);
}
else if (args.Length >= 1 && args[0] == "--synthetic")
{
    rec = Synthetic.Run(script);
    basePath = Path.Combine(folder, $"A1-synthetic-{script.Name}");
    source = "synthetic data";
}
else
{
    Console.WriteLine($"Experiment A1 ({script.Name}): {script.Setup}");
    rec = Live.Run(script, out var error);
    if (rec is null)
    {
        Console.WriteLine(error);
        return 1;
    }
    basePath = Path.Combine(folder, $"A1-{script.Name}-{DateTime.Now:yyyyMMdd-HHmmss}");
    source = Path.GetFileName(basePath);
}

var report = Analysis.Report(rec, source);
Console.WriteLine();
Console.WriteLine(report);
if (basePath is not null)
{
    Directory.CreateDirectory(folder);
    rec.Save(basePath);
    File.WriteAllText(basePath + "-report.txt", report, new UTF8Encoding(false));
    Console.WriteLine($"Files written: {basePath}-report.txt, -samples.csv, -sends.csv, -notes.txt");
}
return 0;
