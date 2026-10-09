using System.Text;

namespace PilotFlying.Experiments.TaxiLayout;

internal static class Analysis
{

    public static readonly string[] PathTypes = ["NONE", "TAXI", "RUNWAY", "PARKING", "PATH", "CLOSED", "VEHICLE", "ROAD", "PAINTEDLINE"];
    public static readonly string[] PointTypes = ["NONE", "NORMAL", "HOLD_SHORT", "(3 ?)", "ILS_HOLD_SHORT", "HOLD_SHORT_NO_DRAW", "ILS_HOLD_SHORT_NO_DRAW"];

    /// <summary>Paths an aircraft may use.</summary>
    public static bool ForAircraft(TaxiPath t) => t.Type is 1 or 2 or 3 or 4;

    public static bool IsHoldShort(int type) => type is 2 or 4 or 5 or 6;

    public static string TypeName(string[] names, int type) => type >= 0 && type < names.Length ? names[type] : $"({type} ?)";

    /// <summary>"09", "13L"… (1–36, then N, NE…; L, R, C, W, A, B).</summary>
    public static string EndName(int number, int designator)
    {
        var n = number switch
        {
            >= 1 and <= 36 => number.ToString("00"),
            37 => "N", 38 => "NE", 39 => "E", 40 => "SE", 41 => "S", 42 => "SW", 43 => "W", 44 => "NW",
            0 => "",
            _ => number.ToString(),
        };
        var d = designator switch { 1 => "L", 2 => "R", 3 => "C", 4 => "W", 5 => "A", 6 => "B", _ => "" };
        return n + d;
    }

    // ------------------------------------------------------------------ geometry (local east / north frame, metres)

    // Metres per degree on the WGS84 ellipsoid at the reference latitude (meridian and parallel radii of curvature).
    // A sphere of 111,320 m per degree is 0.2 % off in latitude and 0.16 % in longitude at Pau: about 1.5 m of lateral
    // error 950 m from the reference point (tests D4 and E1, 09/10/2026, confirmed with the GSX ground map).
    public static (double Lat, double Lon) MetresPerDegree(double lat0)
    {
        const double a = 6378137, e2 = 0.00669437999014;
        var phi = lat0 * Math.PI / 180;
        var w = 1 - e2 * Math.Sin(phi) * Math.Sin(phi);
        return (a * (1 - e2) / Math.Pow(w, 1.5) * Math.PI / 180, a / Math.Sqrt(w) * Math.Cos(phi) * Math.PI / 180);
    }

    public static (double East, double North) Local(AirportLayout a, double lat, double lon)
    {
        var m = MetresPerDegree(a.Lat);
        return ((lon - a.Lon) * m.Lon, (lat - a.Lat) * m.Lat);
    }

    public static (double Lat, double Lon) Geo(AirportLayout a, double east, double north)
    {
        var m = MetresPerDegree(a.Lat);
        return (a.Lat + north / m.Lat, a.Lon + east / m.Lon);
    }

    /// <summary>Taxi point position; <paramref name="swapped"/>: BIAS_X = north and BIAS_Z = east (hypothesis being tested).</summary>
    public static (double East, double North) Position(TaxiPoint pt, bool swapped = false) => swapped ? (pt.Z, pt.X) : (pt.X, pt.Z);

    /// <summary>End points of a path (END is a parking spot for type 3), null if an index is unknown.</summary>
    public static ((double East, double North) A, (double East, double North) B)? Ends(AirportLayout a, TaxiPath t, bool swapped = false)
    {
        if (!a.Points.TryGetValue(t.Start, out var start)) return null;
        (double, double) b;
        if (t.Type == 3)
        {
            var parking = a.Parkings.FirstOrDefault(pk => pk.Index == t.End);
            if (parking is null) return null;
            b = swapped ? (parking.Z, parking.X) : (parking.X, parking.Z);
        }
        else if (a.Points.TryGetValue(t.End, out var end)) b = Position(end, swapped);
        else return null;
        return (Position(start, swapped), b);
    }

    /// <summary>Lateral distance (m) to the runway centreline and position along it (m from the centre).</summary>
    public static (double Lateral, double Along) RelativeToRunway(AirportLayout a, Runway rwy, (double East, double North) pt)
    {
        var (ce, cn) = Local(a, rwy.Lat, rwy.Lon);
        var hdg = rwy.Heading * Math.PI / 180;
        double ue = Math.Sin(hdg), un = Math.Cos(hdg);
        double de = pt.East - ce, dn = pt.North - cn;
        return (Math.Abs(de * un - dn * ue), de * ue + dn * un);
    }

    private static (Runway Runway, double Lateral)? NearestRunway(AirportLayout a, (double East, double North) pt)
    {
        (Runway, double)? best = null;
        foreach (var rwy in a.Runways)
        {
            var (lat, along) = RelativeToRunway(a, rwy, pt);
            if (Math.Abs(along) > rwy.Length / 2 + 300) continue;
            if (best is null || lat < best.Value.Item2) best = (rwy, lat);
        }
        return best;
    }

    private static double Median(List<double> v)
    {
        if (v.Count == 0) return double.NaN;
        v.Sort();
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    // ------------------------------------------------------------------ report

    public static string Report(AirportLayout a)
    {
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);

        L($"=== Experiment D2 — taxi layout of {a.Icao} ({a.Name}) — {DateTime.Now:yyyy-MM-dd HH:mm} ===");
        L($"Reference: {a.Lat:F6}, {a.Lon:F6}, {a.Alt:F1} m");
        L($"Runways: {a.Runways.Count} · starts (START): {a.Starts.Count} · parking spots: {a.Parkings.Count} · "
          + $"taxi points: {a.Points.Count} · taxi paths: {a.Paths.Count} · taxiway names: {a.TaxiNames.Count}");
        if (a.UnexpectedTypes.Count > 0)
            L("Unexpected element types (SIMCONNECT_FACILITY_DATA_TYPE number: count): "
              + string.Join(", ", a.UnexpectedTypes.Select(kv => $"{kv.Key}: {kv.Value}")));
        if (a.Errors.Count > 0)
        {
            L("SimConnect errors:");
            foreach (var e in a.Errors.Take(30)) L("  " + e);
        }

        L();
        L("--- Runways ---");
        foreach (var rwy in a.Runways)
            L($"  {rwy.Name,-8} heading {rwy.Heading:F1}  {rwy.Length:F0} x {rwy.Width:F0} m  "
              + $"ILS: {(rwy.Ils1.Length > 0 ? rwy.Ils1 : "-")} / {(rwy.Ils2.Length > 0 ? rwy.Ils2 : "-")}");
        L("--- Runway starts (START) ---");
        foreach (var s in a.Starts)
            L($"  {EndName(s.Number, s.Designator),-5} type {s.Type}  heading {s.Heading:F1}  {s.Lat:F6}, {s.Lon:F6}");

        // D3 and D1: types
        L();
        L("--- D3: taxi path types (TAXI_PATH.TYPE) ---");
        foreach (var g in a.Paths.GroupBy(t => t.Type).OrderBy(g => g.Key))
        {
            var withLine = g.Count(t => t.CenterLine == 1);
            L($"  {g.Key} {TypeName(PathTypes, g.Key),-12} {g.Count(),5}  (painted centreline: {withLine}, median width "
              + $"{Median(g.Select(t => (double)t.Width).ToList()):F1} m)");
        }
        var weights = a.Paths.Where(t => t.Weight > 0).Select(t => t.Weight).Distinct().OrderBy(w => w).ToList();
        L($"  WEIGHT (lb): {(weights.Count == 0 ? "always 0" : string.Join(", ", weights.Take(10)))}");

        L();
        L("--- D1: taxi point types (TAXI_POINT.TYPE) ---");
        foreach (var g in a.Points.Values.GroupBy(pt => pt.Type).OrderBy(g => g.Key))
            L($"  {g.Key} {TypeName(PointTypes, g.Key),-24} {g.Count(),5}  (orientation REVERSE: {g.Count(pt => pt.Orientation == 1)})");

        // Axis convention
        L();
        L("--- BIAS_X / BIAS_Z convention: distance of runway paths (type 2) to the nearest runway centreline ---");
        foreach (var swapped in new[] { false, true })
        {
            var distances = new List<double>();
            foreach (var t in a.Paths.Where(t => t.Type == 2))
                if (Ends(a, t, swapped) is { } e)
                    foreach (var pt in new[] { e.A, e.B })
                        if (NearestRunway(a, pt) is { } nr) distances.Add(nr.Lateral);
            var label = swapped ? "BIAS_X = north, BIAS_Z = east" : "BIAS_X = east,  BIAS_Z = north";
            L(distances.Count == 0
                ? $"  {label}: no runway path end point to compare"
                : $"  {label}: median {Median(distances):F1} m over {distances.Count} end points");
        }
        L("  (the right convention gives a few metres; the wrong one, hundreds)");

        // D2: names
        L();
        L("--- D2: taxiway names (TAXI_NAME) and path NAME_INDEX ---");
        L("  List: " + (a.TaxiNames.Count == 0 ? "(empty)" : string.Join(", ", a.TaxiNames.Select(kv => $"[{kv.Key}] \"{kv.Value}\""))));
        var taxiways = a.Paths.Where(t => t.Type is 1 or 4).ToList();
        var outside = taxiways.Count(t => !a.TaxiNames.ContainsKey((int)t.NameIndex));
        L($"  TAXI/PATH paths: {taxiways.Count}, of which NAME_INDEX outside the list: {outside}");
        foreach (var g in a.Paths.GroupBy(t => t.Type).OrderBy(g => g.Key))
        {
            var idx = g.Select(t => t.NameIndex).Distinct().OrderBy(i => i).ToList();
            L($"    type {g.Key} {TypeName(PathTypes, g.Key),-12} distinct NAME_INDEX: {string.Join(", ", idx.Take(25))}{(idx.Count > 25 ? "…" : "")}");
        }
        L("  By NAME_INDEX (a taxiway name should form a continuous track: 1 piece, or few):");
        foreach (var g in taxiways.GroupBy(t => t.NameIndex).OrderBy(g => g.Key))
        {
            var name = a.TaxiNames.TryGetValue((int)g.Key, out var n) ? $"\"{n}\"" : "(outside list)";
            var pieces = Pieces(g.Select(t => (t.Start, t.End)));
            var length = g.Sum(t => Ends(a, t) is { } e ? Math.Sqrt(Math.Pow(e.A.East - e.B.East, 2) + Math.Pow(e.A.North - e.B.North, 2)) : 0);
            var pts = g.SelectMany(t => Ends(a, t) is { } e ? new[] { e.A, e.B } : []).ToList();
            var centre = pts.Count > 0 ? Geo(a, pts.Average(q => q.East), pts.Average(q => q.North)) : (0, 0);
            L($"    [{g.Key,3}] {name,-14} {g.Count(),4} paths, {length,6:F0} m, {pieces} piece(s), centre {centre.Item1:F5}, {centre.Item2:F5}");
        }

        // Hold-short points
        L();
        L("--- Hold-short points ---");
        var holds = a.Points.Values.Where(pt => IsHoldShort(pt.Type)).OrderBy(pt => pt.Index).ToList();
        if (holds.Count == 0) L("  (none)");
        foreach (var pt in holds)
        {
            var names = a.Paths.Where(t => (t.Start == pt.Index || (t.Type != 3 && t.End == pt.Index)) && t.Type is 1 or 4)
                .Select(t => a.TaxiNames.TryGetValue((int)t.NameIndex, out var n) ? n : "?").Distinct();
            var pos = Position(pt);
            var nr = NearestRunway(a, pos);
            var g = Geo(a, pos.East, pos.North);
            L($"  point {pt.Index,4} {TypeName(PointTypes, pt.Type),-22} {(pt.Orientation == 1 ? "REVERSE" : "FORWARD")}  "
              + $"taxiway(s) {string.Join("/", names),-8} "
              + (nr is { } x ? $"{x.Lateral:F0} m from the {x.Runway.Name} centreline" : "far from runways")
              + $"  ({g.Lat:F6}, {g.Lon:F6})");
        }

        // Network connectivity
        L();
        L("--- Aircraft network (types 1 to 4) ---");
        var edges = a.Paths.Where(ForAircraft).Select(t => (t.Start, t.Type == 3 ? -1 - t.End : t.End)).ToList();
        var components = Components(edges);
        var sizes = components.GroupBy(kv => kv.Value).Select(g => g.Count()).OrderByDescending(n => n).ToList();
        L($"  {sizes.Count} piece(s); sizes: {string.Join(", ", sizes.Take(10))}{(sizes.Count > 10 ? "…" : "")}");
        if (components.Count > 0)
        {
            var main = components.GroupBy(kv => kv.Value).OrderByDescending(g => g.Count()).First().Key;
            var parkingsOut = a.Parkings.Where(pk => pk.Type != 13 && (!components.TryGetValue(-1 - pk.Index, out var c) || c != main)).ToList();
            L($"  Parking spots (excluding \"vehicle\") not connected to the main network: {parkingsOut.Count} / {a.Parkings.Count(pk => pk.Type != 13)}");
            var holdsOut = holds.Count(pt => !components.TryGetValue(pt.Index, out var c) || c != main);
            L($"  Hold-short points outside the main network: {holdsOut} / {holds.Count}");
        }

        L();
        L("To check: do the names and their position (centre) match the airport chart?");
        L("The .geojson file opens on https://geojson.io to see the network over a map.");
        return r.ToString();
    }

    /// <summary>Number of connected pieces formed by edges (node indices).</summary>
    private static int Pieces(IEnumerable<(int, int)> edges) =>
        Components(edges).Values.Distinct().Count();

    private static Dictionary<int, int> Components(IEnumerable<(int A, int B)> edges)
    {
        var parent = new Dictionary<int, int>();
        int Root(int x)
        {
            if (!parent.TryGetValue(x, out var px)) { parent[x] = x; return x; }
            if (px == x) return x;
            var root = Root(px);
            parent[x] = root;
            return root;
        }
        foreach (var (a, b) in edges)
        {
            var ra = Root(a);
            var rb = Root(b);
            if (ra != rb) parent[ra] = rb;
        }
        return parent.Keys.ToList().ToDictionary(k => k, Root);
    }
}
