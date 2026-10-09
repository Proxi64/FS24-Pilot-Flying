using System.Text.Json;

namespace PilotFlying.Experiments.CentrelineMatch;

/// <summary>A straight piece of the aircraft taxi network, in metres east / north of the airport reference point.</summary>
internal sealed record Segment(int Path, int Type, string Name, double AE, double AN, double BE, double BN)
{
    public double Length => Math.Sqrt((BE - AE) * (BE - AE) + (BN - AN) * (BN - AN));
    /// <summary>Direction A → B, degrees true.</summary>
    public double Bearing => (Math.Atan2(BE - AE, BN - AN) * 180 / Math.PI + 360) % 360;
}

/// <summary>
/// Airport layout saved by experiment D2 (results\layout-ICAO.json). BIAS_X = east, BIAS_Z = north, metres from the
/// reference point (confirmed by D2).
/// </summary>
internal sealed class Layout
{
    private const double MetresPerDegree = 111_320;

    public string Icao = "";
    public double Lat0, Lon0;
    public List<Segment> Segments { get; } = [];

    public (double East, double North) Local(double lat, double lon) =>
        ((lon - Lon0) * MetresPerDegree * Math.Cos(Lat0 * Math.PI / 180), (lat - Lat0) * MetresPerDegree);

    public (double Lat, double Lon) Geo(double east, double north) =>
        (Lat0 + north / MetresPerDegree, Lon0 + east / (MetresPerDegree * Math.Cos(Lat0 * Math.PI / 180)));

    public static Layout Load(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var root = doc.RootElement;
        var layout = new Layout
        {
            Icao = root.GetProperty("Icao").GetString() ?? "",
            Lat0 = root.GetProperty("Reference").GetProperty("Lat").GetDouble(),
            Lon0 = root.GetProperty("Reference").GetProperty("Lon").GetDouble(),
        };
        var points = root.GetProperty("Points").EnumerateArray()
            .ToDictionary(p => p.GetProperty("Index").GetInt32(), p => (E: p.GetProperty("X").GetDouble(), N: p.GetProperty("Z").GetDouble()));
        var parkings = root.GetProperty("Parkings").EnumerateArray()
            .ToDictionary(p => p.GetProperty("Index").GetInt32(), p => (E: p.GetProperty("X").GetDouble(), N: p.GetProperty("Z").GetDouble()));
        var names = root.GetProperty("TaxiNames").EnumerateArray()
            .ToDictionary(n => n.GetProperty("Index").GetInt32(), n => n.GetProperty("Name").GetString() ?? "");
        foreach (var p in root.GetProperty("Paths").EnumerateArray())
        {
            var type = p.GetProperty("Type").GetInt32();
            if (type is < 1 or > 4) continue; // aircraft paths only: TAXI, RUNWAY, PARKING, PATH
            if (!points.TryGetValue(p.GetProperty("Start").GetInt32(), out var a)) continue;
            var endIndex = p.GetProperty("End").GetInt32();
            if (!(type == 3 ? parkings : points).TryGetValue(endIndex, out var b)) continue;
            var nameIndex = p.GetProperty("NameIndex").GetInt32();
            var name = type == 2 ? "(runway)" : type == 3 ? "(parking)" : names.TryGetValue(nameIndex, out var n) && n.Length > 0 ? n : "(unnamed)";
            layout.Segments.Add(new Segment(p.GetProperty("Index").GetInt32(), type, name, a.E, a.N, b.E, b.N));
        }
        return layout;
    }

    /// <summary>
    /// Nearest segment to a point: lateral offset (signed, + = to the right when travelling along the given heading),
    /// position along the segment (m from A), and the angle between the heading and the segment line (0–90°).
    /// </summary>
    public (Segment Seg, double Offset, double Along, double Angle)? Nearest(double e, double n, double headingTrue)
    {
        (Segment, double, double, double)? best = null;
        var bestDist = double.MaxValue;
        foreach (var s in Segments)
        {
            var len = s.Length;
            if (len < 0.5) continue;
            double ue = (s.BE - s.AE) / len, un = (s.BN - s.AN) / len;
            var along = (e - s.AE) * ue + (n - s.AN) * un;
            var clamped = Math.Clamp(along, 0, len);
            double pe = s.AE + ue * clamped, pn = s.AN + un * clamped;
            var dist = Math.Sqrt((e - pe) * (e - pe) + (n - pn) * (n - pn));
            if (dist >= bestDist) continue;
            bestDist = dist;
            // Signed offset relative to the segment direction (+ = right of A → B), then to the travel direction.
            var cross = (e - s.AE) * un - (n - s.AN) * ue;
            var diff = ((headingTrue - s.Bearing) % 360 + 540) % 360 - 180;
            var sameWay = Math.Abs(diff) <= 90;
            var angle = sameWay ? Math.Abs(diff) : 180 - Math.Abs(diff);
            best = (s, sameWay ? cross : -cross, along, angle);
        }
        return best;
    }
}
