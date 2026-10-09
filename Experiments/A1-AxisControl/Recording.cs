using System.Globalization;
using System.Text;

namespace PilotFlying.Experiments.AxisControl;

/// <summary>One set of SimVars received at a simulation frame. Cmd = last value sent in the current phase (NaN: none).</summary>
internal sealed record Sample(double T, string Phase, double[] Cmd, double[] Meas);

/// <summary>One key event sent.</summary>
internal sealed record Send(double T, string Phase, int Channel, double Value);

/// <summary>Everything recorded during a run; saved as CSV so that the analysis can be replayed without MSFS.</summary>
internal sealed class Recording
{
    public List<Sample> Samples { get; } = [];
    public List<Send> Sends { get; } = [];
    public List<string> Notes { get; } = [];

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("R", Inv);
    private static double P(string s) => s.Length == 0 ? double.NaN : double.Parse(s, Inv);

    public void Save(string basePath)
    {
        var s = new StringBuilder();
        s.AppendLine(string.Join(",", ["t", "phase", .. Script.Channels.Select(c => "cmd:" + c.Name), .. Script.Measures.Select(m => m.Name)]));
        foreach (var x in Samples)
            s.AppendLine(string.Join(",", [F(x.T), x.Phase, .. x.Cmd.Select(F), .. x.Meas.Select(F)]));
        File.WriteAllText(basePath + "-samples.csv", s.ToString(), new UTF8Encoding(false));

        s.Clear().AppendLine("t,phase,channel,value");
        foreach (var x in Sends) s.AppendLine($"{F(x.T)},{x.Phase},{Script.Channels[x.Channel].Name},{F(x.Value)}");
        File.WriteAllText(basePath + "-sends.csv", s.ToString(), new UTF8Encoding(false));

        File.WriteAllLines(basePath + "-notes.txt", Notes, new UTF8Encoding(false));
    }

    /// <param name="samplesFile">A "...-samples.csv" file; the "-sends.csv" and "-notes.txt" files are read next to it.</param>
    public static Recording Load(string samplesFile)
    {
        var r = new Recording();
        var basePath = samplesFile.EndsWith("-samples.csv") ? samplesFile[..^"-samples.csv".Length] : samplesFile;
        var header = File.ReadLines(basePath + "-samples.csv").First().Split(',');
        var nc = header.Count(c => c.StartsWith("cmd:"));
        foreach (var line in File.ReadLines(basePath + "-samples.csv").Skip(1))
        {
            var f = line.Split(',');
            // Older recordings have fewer SimVars: the missing ones are NaN.
            var meas = f[(2 + nc)..].Select(P).Concat(Enumerable.Repeat(double.NaN, Script.Measures.Length)).Take(Script.Measures.Length);
            var cmd = f[2..(2 + nc)].Select(P).Concat(Enumerable.Repeat(double.NaN, Script.Channels.Length)).Take(Script.Channels.Length);
            r.Samples.Add(new Sample(P(f[0]), f[1], cmd.ToArray(), meas.ToArray()));
        }
        var names = Script.Channels.Select(c => c.Name).ToList();
        foreach (var line in File.ReadLines(basePath + "-sends.csv").Skip(1))
        {
            var f = line.Split(',');
            r.Sends.Add(new Send(P(f[0]), f[1], names.IndexOf(f[2]), P(f[3])));
        }
        if (File.Exists(basePath + "-notes.txt")) r.Notes.AddRange(File.ReadAllLines(basePath + "-notes.txt"));
        return r;
    }
}
