using System.Text.Json;

namespace PilotFlying.Experiments.TaxiLayout;

internal static class Outputs
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Raw data as received (to replay the analysis without MSFS).</summary>
    public static string Json(AirportLayout a) => JsonSerializer.Serialize(new
    {
        a.Icao,
        a.Name,
        Reference = new { a.Lat, a.Lon, a.Alt },
        ReadAt = DateTimeOffset.Now,
        a.Runways,
        a.Starts,
        a.Parkings,
        Points = a.Points.Values.OrderBy(pt => pt.Index),
        a.Paths,
        TaxiNames = a.TaxiNames.Select(kv => new { Index = kv.Key, Name = kv.Value }),
        a.Errors,
    }, Options);

    /// <summary>Network over a map (geojson.io): taxi paths, hold-short points, parking spots.</summary>
    public static string GeoJson(AirportLayout a)
    {
        var features = new List<object>();
        double[] C((double East, double North) pt)
        {
            var g = Analysis.Geo(a, pt.East, pt.North);
            return [Math.Round(g.Lon, 7), Math.Round(g.Lat, 7)];
        }

        foreach (var t in a.Paths)
        {
            if (Analysis.Ends(a, t) is not { } e) continue;
            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "LineString", coordinates = new[] { C(e.A), C(e.B) } },
                properties = new Dictionary<string, object>
                {
                    ["path"] = t.Index,
                    ["type"] = Analysis.TypeName(Analysis.PathTypes, t.Type),
                    ["name"] = a.TaxiNames.TryGetValue((int)t.NameIndex, out var n) ? n : $"({t.NameIndex})",
                    ["width"] = t.Width,
                    ["centerLine"] = t.CenterLine,
                    ["runway"] = Analysis.EndName(t.RunwayNumber, t.RunwayDesignator),
                    ["stroke"] = t.Type switch { 2 => "#444444", 3 => "#2a9d8f", 6 or 7 => "#bbbbbb", 5 => "#e63946", _ => "#e9a23b" },
                    ["stroke-width"] = t.Type == 2 ? 4 : 2,
                },
            });
        }

        foreach (var pt in a.Points.Values.Where(pt => Analysis.IsHoldShort(pt.Type)))
            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = C(Analysis.Position(pt)) },
                properties = new Dictionary<string, object>
                {
                    ["point"] = pt.Index,
                    ["type"] = Analysis.TypeName(Analysis.PointTypes, pt.Type),
                    ["orientation"] = pt.Orientation == 1 ? "REVERSE" : "FORWARD",
                    ["marker-color"] = pt.Type is 4 or 6 ? "#7b2cbf" : "#d62828",
                    ["marker-symbol"] = "roadblock",
                },
            });

        foreach (var pk in a.Parkings)
            features.Add(new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = C((pk.X, pk.Z)) },
                properties = new Dictionary<string, object>
                {
                    ["parking"] = pk.Index,
                    ["parkingType"] = pk.Type,
                    ["number"] = pk.Number,
                    ["heading"] = pk.Heading,
                    ["radius"] = pk.Radius,
                    ["marker-color"] = "#1d3557",
                    ["marker-size"] = "small",
                },
            });

        return JsonSerializer.Serialize(new { type = "FeatureCollection", features }, Options);
    }
}
