namespace PilotFlying.Experiments.GroundTracking;

/// <summary>A point of the route polyline: position (m), distance from the start (m), taxiway name of the piece ending here.</summary>
internal sealed record RoutePoint(double E, double N, double S, string Name);

/// <summary>
/// A taxi route from a parking spot to a hold-short point, following an ATC-like clearance ("C NG NW N5"): computed in
/// the layout graph, then handled as a polyline (distance along it, nearest point, direction, turns ahead).
/// </summary>
internal sealed class Route
{
    public required List<RoutePoint> Points { get; init; }
    public required int Parking { get; init; }
    public required int HoldNode { get; init; }
    public required string Summary { get; init; }
    public double Length => Points[^1].S;

    /// <summary>
    /// Shortest route from the parking spot following the clearance names in order. Unnamed connecting pieces are allowed
    /// at three times their length; runways, vehicle roads and other names are not. The route ends at the first hold-short
    /// point that touches the last name of the clearance (the taxiway leading onto the runway), which is never entered.
    /// </summary>
    public static Route? Find(Layout layout, int parking, string[] clearance, out string refused)
    {
        refused = "";
        var last = clearance[^1];
        var adjacency = new Dictionary<int, List<(int To, Segment Seg)>>();
        void Add(int from, int to, Segment s)
        {
            if (!adjacency.TryGetValue(from, out var list)) adjacency[from] = list = [];
            list.Add((to, s));
        }
        foreach (var s in layout.Segments.Where(s => s.Type is 1 or 4))
        {
            Add(s.StartNode, s.EndNode, s);
            Add(s.EndNode, s.StartNode, s);
        }

        // Dijkstra on (node, position in the clearance).
        var dist = new Dictionary<(int, int), double>();
        var previous = new Dictionary<(int, int), ((int, int) From, Segment Seg)?>();
        var queue = new PriorityQueue<(int Node, int K), double>();
        var exits = layout.Segments.Where(s => s.Type == 3 && s.EndNode == -1 - parking).ToList();
        if (exits.Count == 0) { refused = $"parking {parking} has no PARKING path to the taxi network"; return null; }
        foreach (var exit in exits)
        {
            dist[(exit.StartNode, 0)] = 0;
            previous[(exit.StartNode, 0)] = null;
            queue.Enqueue((exit.StartNode, 0), 0);
        }
        (int, int)? goal = null;
        while (queue.TryDequeue(out var state, out var d))
        {
            if (d > dist[state]) continue;
            var (node, k) = state;
            if (layout.IsHoldShort(node) && k >= clearance.Length - 2
                && adjacency.GetValueOrDefault(node, []).Any(x => x.Seg.Name == last)) { goal = state; break; }
            foreach (var (to, seg) in adjacency.GetValueOrDefault(node, []))
            {
                if (seg.Name == last) continue; // the taxiway towards the runway is never entered
                int k2;
                var cost = seg.Length;
                if (seg.Name == clearance[k]) k2 = k;
                else if (k + 1 < clearance.Length && seg.Name == clearance[k + 1]) k2 = k + 1;
                else if (seg.Name == "(unnamed)") { k2 = k; cost *= 3; }
                else continue;
                var next = (to, k2);
                if (dist.TryGetValue(next, out var old) && old <= d + cost) continue;
                dist[next] = d + cost;
                previous[next] = (state, seg);
                queue.Enqueue(next, d + cost);
            }
        }
        if (goal is null) { refused = $"no route from parking {parking} via {string.Join(" ", clearance)} to a hold-short point"; return null; }

        // Back from the goal: the list of nodes, then the polyline from the parking spot.
        var nodes = new List<(int Node, string Name)>();
        var cur = goal.Value;
        while (previous[cur] is { } p)
        {
            nodes.Add((cur.Item1, p.Seg.Name));
            cur = p.From;
        }
        nodes.Add((cur.Item1, "(parking)"));
        nodes.Reverse();
        var pk = layout.Parkings[parking];
        var points = new List<RoutePoint> { new(pk.E, pk.N, 0, "(parking)") };
        foreach (var (node, name) in nodes)
        {
            var pt = layout.Points[node];
            var prev = points[^1];
            var step = Math.Sqrt((pt.E - prev.E) * (pt.E - prev.E) + (pt.N - prev.N) * (pt.N - prev.N));
            if (step < 0.05) continue;
            points.Add(new RoutePoint(pt.E, pt.N, prev.S + step, name));
        }
        var parts = new List<(string Name, double Length)>();
        for (var i = 1; i < points.Count; i++)
        {
            var len = points[i].S - points[i - 1].S;
            if (parts.Count > 0 && parts[^1].Name == points[i].Name) parts[^1] = (parts[^1].Name, parts[^1].Length + len);
            else parts.Add((points[i].Name, len));
        }
        return new Route
        {
            Points = points, Parking = parking, HoldNode = goal.Value.Item1,
            Summary = string.Join(" → ", parts.Select(p => $"{p.Name} {p.Length:F0} m")) + $" → hold-short point {goal.Value.Item1}",
        };
    }

    // ------------------------------------------------------------------ geometry along the polyline

    public (double E, double N) PointAt(double s)
    {
        s = Math.Clamp(s, 0, Length);
        var i = Segment(s);
        var (a, b) = (Points[i], Points[i + 1]);
        var f = (s - a.S) / (b.S - a.S);
        return (a.E + f * (b.E - a.E), a.N + f * (b.N - a.N));
    }

    /// <summary>Direction of the route at distance s, degrees true.</summary>
    public double BearingAt(double s)
    {
        var i = Segment(Math.Clamp(s, 0, Length));
        return (Math.Atan2(Points[i + 1].E - Points[i].E, Points[i + 1].N - Points[i].N) * 180 / Math.PI + 360) % 360;
    }

    public string NameAt(double s) => Points[Segment(Math.Clamp(s, 0, Length)) + 1].Name;

    /// <summary>Largest change of direction between s and s + ahead (degrees).</summary>
    public double TurnAhead(double s, double ahead)
    {
        var b0 = BearingAt(s);
        var max = 0.0;
        for (var x = s; x <= Math.Min(Length, s + ahead); x += 1)
            max = Math.Max(max, Math.Abs(((BearingAt(x) - b0) % 360 + 540) % 360 - 180));
        return max;
    }

    /// <summary>Nearest point of the route between sFrom and sTo: distance along, signed offset (+ = right), distance.</summary>
    public (double S, double Offset, double Distance) Project(double e, double n, double sFrom, double sTo)
    {
        (double, double, double) best = (sFrom, 0, double.MaxValue);
        for (var i = 0; i < Points.Count - 1; i++)
        {
            var (a, b) = (Points[i], Points[i + 1]);
            if (b.S < sFrom || a.S > sTo) continue;
            var len = b.S - a.S;
            double ue = (b.E - a.E) / len, un = (b.N - a.N) / len;
            var along = Math.Clamp((e - a.E) * ue + (n - a.N) * un, 0, len);
            double pe = a.E + ue * along, pn = a.N + un * along;
            var dist = Math.Sqrt((e - pe) * (e - pe) + (n - pn) * (n - pn));
            if (dist < best.Item3) best = (a.S + along, (e - a.E) * un - (n - a.N) * ue, dist);
        }
        return best;
    }

    private int Segment(double s)
    {
        for (var i = 0; i < Points.Count - 2; i++)
            if (s <= Points[i + 1].S) return i;
        return Points.Count - 2;
    }
}
