using System.Globalization;
using System.Text;

namespace PilotFlying.Experiments.MobiFlight;

/// <summary>
/// One timestamped event of the run. Kind: "direct" (SimVar read by SimConnect at every frame), "mf" (variable sent by
/// the MobiFlight module), "resp" (module response), "cmd" (command sent to the module), "mark" (step boundary),
/// "info" (aircraft, errors).
/// </summary>
internal sealed record Entry(double T, string Kind, string Name, double Value, string Text);

internal sealed class Log
{
    public List<Entry> Entries { get; } = [];

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public void Add(double t, string kind, string name = "", double value = double.NaN, string text = "") =>
        Entries.Add(new Entry(t, kind, name, value, text));

    private static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    public void Save(string file)
    {
        var s = new StringBuilder("t,kind,name,value,text\n");
        foreach (var e in Entries)
            s.Append($"{e.T.ToString("R", Inv)},{e.Kind},{Q(e.Name)},{(double.IsNaN(e.Value) ? "" : e.Value.ToString("R", Inv))},{Q(e.Text)}\n");
        File.WriteAllText(file, s.ToString(), new UTF8Encoding(false));
    }

    public static Log Load(string file)
    {
        var log = new Log();
        foreach (var line in File.ReadLines(file).Skip(1))
        {
            var f = SplitCsv(line);
            log.Add(double.Parse(f[0], Inv), f[1], f[2], f[3].Length == 0 ? double.NaN : double.Parse(f[3], Inv), f[4]);
        }
        return log;
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields;
    }
}
