using System.Text;
using System.Text.Json;

namespace PilotFlying.Experiments.CentrelineMatch;

internal static class Analysis
{
    // A fix is used only when the aircraft rolls along a straight piece of centreline:
    public const double MinSpeed = 2;        // kt
    public const double MaxAngle = 8;        // degrees between the heading and the segment
    public const double EndMargin = 3;       // m: away from the segment ends (junctions; curves are excluded by the angle)
    public const double MaxOffset = 15;      // m: farther means the aircraft is not on this segment

    public sealed record Used(Fix Fix, Segment Seg, double Offset);

    public static List<Used> Select(Layout layout, List<Fix> fixes)
    {
        var used = new List<Used>();
        foreach (var f in fixes)
        {
            if (!f.OnGround || f.GroundSpeed < MinSpeed) continue;
            var (e, n) = layout.Local(f.Lat, f.Lon);
            if (layout.Nearest(e, n, f.Heading) is not { } x) continue;
            if (x.Angle > MaxAngle || x.Along < EndMargin || x.Along > x.Seg.Length - EndMargin || Math.Abs(x.Offset) > MaxOffset) continue;
            used.Add(new Used(f, x.Seg, x.Offset));
        }
        return used;
    }

    private static double Pct(IEnumerable<double> values, double pct)
    {
        var v = values.OrderBy(x => x).ToList();
        return v.Count == 0 ? double.NaN : v[(int)Math.Round(pct / 100 * (v.Count - 1))];
    }

    public static string Report(Layout layout, List<Fix> fixes, string source)
    {
        var r = new StringBuilder();
        void L(string s = "") => r.AppendLine(s);
        var used = Select(layout, fixes);
        var duration = fixes.Count > 1 ? fixes[^1].T - fixes[0].T : 0;

        L($"=== Experiment D4 — hand-flown taxi track vs. layout centrelines at {layout.Icao} — {source} ===");
        L($"Fixes: {fixes.Count} over {duration:F0} s; used (on ground, ≥ {MinSpeed} kt, within {MaxAngle}° of a straight segment, "
          + $"more than {EndMargin} m from its ends, offset ≤ {MaxOffset} m): {used.Count}");
        if (used.Count == 0)
        {
            L("No usable fix: taxi longer along straight taxiways.");
            return r.ToString();
        }
        L("Offset = lateral distance of the aircraft reference point (PLANE LATITUDE / LONGITUDE) from the centreline of");
        L("the layout, + = right of the centreline in the direction of travel.");

        L();
        L("--- Overall ---");
        var all = used.Select(u => u.Offset).ToList();
        L($"  median {Pct(all, 50):+0.00;-0.00} m, |offset| median {Pct(all.Select(Math.Abs), 50):F2} m, p90 {Pct(all.Select(Math.Abs), 90):F2} m, "
          + $"max {all.Max(Math.Abs):F2} m");

        L();
        L("--- By taxiway name ---");
        L("  name          fixes  distance   median offset   |offset| p90");
        foreach (var g in used.GroupBy(u => u.Seg.Name).OrderByDescending(g => g.Count()))
        {
            var o = g.Select(u => u.Offset).ToList();
            L($"  {g.Key,-12} {g.Count(),6}  {Distance(g.Select(u => u.Fix).ToList()),6:F0} m   {Pct(o, 50),+8:+0.00;-0.00} m   {Pct(o.Select(Math.Abs), 90),8:F2} m");
        }

        L();
        L("--- By segment (straight pieces with at least 20 fixes) ---");
        L("  path  name          length  fixes   median offset   spread (p10–p90)");
        foreach (var g in used.GroupBy(u => u.Seg).Where(g => g.Count() >= 20).OrderBy(g => g.Key.Name).ThenBy(g => g.Key.Path))
        {
            var o = g.Select(u => u.Offset).ToList();
            L($"  {g.Key.Path,4}  {g.Key.Name,-12} {g.Key.Length,5:F0} m  {g.Count(),5}   {Pct(o, 50),+8:+0.00;-0.00} m   {Pct(o, 10):+0.00;-0.00} to {Pct(o, 90):+0.00;-0.00} m");
        }
        L();
        L("Reading: a constant offset on every segment is the pilot's habit (where the line is kept); a segment far from the");
        L("others means the layout centreline and the painted line differ there. The -track.geojson file shows the track");
        L("over the network on https://geojson.io.");
        return r.ToString();
    }

    private static double Distance(List<Fix> f)
    {
        var d = 0.0;
        for (var i = 1; i < f.Count; i++)
        {
            var m = MetresPerDegree(f[i].Lat);
            double dn = (f[i].Lat - f[i - 1].Lat) * m.Lat, de = (f[i].Lon - f[i - 1].Lon) * m.Lon;
            var step = Math.Sqrt(dn * dn + de * de);
            if (step < 5) d += step; // skip the jumps between separate straight pieces
        }
        return d;
    }

    /// <summary>Metres per degree of latitude and longitude on the WGS84 ellipsoid at a latitude.</summary>
    private static (double Lat, double Lon) MetresPerDegree(double lat)
    {
        const double a = 6378137, e2 = 0.00669437999014;
        var phi = lat * Math.PI / 180;
        var w = 1 - e2 * Math.Sin(phi) * Math.Sin(phi);
        return (a * (1 - e2) / Math.Pow(w, 1.5) * Math.PI / 180, a / Math.Sqrt(w) * Math.Cos(phi) * Math.PI / 180);
    }

    /// <summary>The track (thick red line) and the aircraft network (thin blue lines), for geojson.io.</summary>
    public static string GeoJson(Layout layout, List<Fix> fixes)
    {
        var features = new List<object>();
        foreach (var s in layout.Segments)
        {
            var a = layout.Geo(s.AE, s.AN);
            var b = layout.Geo(s.BE, s.BN);
            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "LineString", coordinates = new[] { new[] { a.Lon, a.Lat }, new[] { b.Lon, b.Lat } } },
                properties = new Dictionary<string, object> { ["path"] = s.Path, ["name"] = s.Name, ["stroke"] = "#1d6fd8", ["stroke-width"] = 1 },
            });
        }
        // The track as a thick red line (one point every 5 frames), easier to see than markers.
        features.Add(new
        {
            type = "Feature",
            geometry = new { type = "LineString", coordinates = fixes.Where((_, i) => i % 5 == 0).Select(f => new[] { Math.Round(f.Lon, 8), Math.Round(f.Lat, 8) }).ToArray() },
            properties = new Dictionary<string, object> { ["name"] = "aircraft track", ["stroke"] = "#e63946", ["stroke-width"] = 3 },
        });
        return JsonSerializer.Serialize(new { type = "FeatureCollection", features }, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>Fake track: the layout's first long named straight segments, driven 0.4 m right of the line with noise.</summary>
    public static List<Fix> Synthetic(Layout layout)
    {
        var rng = new Random(3);
        var fixes = new List<Fix>();
        var t = 0.0;
        foreach (var s in layout.Segments.Where(s => s.Type is 1 or 4 && s.Length > 40).Take(6))
        {
            var len = s.Length;
            double ue = (s.BE - s.AE) / len, un = (s.BN - s.AN) / len;
            for (var d = 0.0; d < len; d += 2.6 * 0.025) // 5 kt at 40 frames per second
            {
                var offset = 0.4 + (rng.NextDouble() - 0.5) * 0.3;
                double e = s.AE + ue * d + un * offset, n = s.AN + un * d - ue * offset;
                var (lat, lon) = layout.Geo(e, n);
                fixes.Add(new Fix(t, lat, lon, s.Bearing + (rng.NextDouble() - 0.5), 5, true));
                t += 0.025;
            }
        }
        return fixes;
    }
}
