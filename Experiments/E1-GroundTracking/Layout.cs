using System.Text.Json;

namespace PilotFlying.Experiments.GroundTracking;

/// <summary>A straight piece of the aircraft taxi network, in metres east / north of the airport reference point.</summary>
internal sealed record Segment(int Path, int Type, string Name, double AE, double AN, double BE, double BN, int StartNode = -1, int EndNode = -1)
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
    // Metres per degree on the WGS84 ellipsoid at the reference latitude (meridian and parallel radii of curvature).
    // A sphere of 111,320 m per degree is 0.2 % off in latitude and 0.16 % in longitude at Pau: about 1.5 m of lateral
    // error 950 m from the reference point (tests D4 and E1, 09/10/2026, confirmed with the GSX ground map).
    private static (double Lat, double Lon) MetresPerDegree(double lat0)
    {
        const double a = 6378137, e2 = 0.00669437999014;
        var phi = lat0 * Math.PI / 180;
        var w = 1 - e2 * Math.Sin(phi) * Math.Sin(phi);
        return (a * (1 - e2) / Math.Pow(w, 1.5) * Math.PI / 180, a / Math.Sqrt(w) * Math.Cos(phi) * Math.PI / 180);
    }

    public string Icao = "";
    public double Lat0, Lon0;
    public List<Segment> Segments { get; } = [];

    public (double East, double North) Local(double lat, double lon)
    {
        var m = MetresPerDegree(Lat0);
        return ((lon - Lon0) * m.Lon, (lat - Lat0) * m.Lat);
    }

    public (double Lat, double Lon) Geo(double east, double north)
    {
        var m = MetresPerDegree(Lat0);
        return (Lat0 + north / m.Lat, Lon0 + east / m.Lon);
    }

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
            // Nodes: taxi point index; a parking spot (END of a PARKING path) gets a negative node number.
            var startNode = p.GetProperty("Start").GetInt32();
            layout.Segments.Add(new Segment(p.GetProperty("Index").GetInt32(), type, name, a.E, a.N, b.E, b.N,
                startNode, type == 3 ? -1 - endIndex : endIndex));
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
