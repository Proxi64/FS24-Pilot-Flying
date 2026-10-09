namespace PilotFlying.Experiments.GroundTracking;

/// <summary>The straight centreline to follow, oriented in the direction of travel (metres east / north).</summary>
internal sealed record Line(double AE, double AN, double Bearing, double StartAlong, double StopAlong, string Name, int Path)
{
    private double UE => Math.Sin(Bearing * Math.PI / 180);
    private double UN => Math.Cos(Bearing * Math.PI / 180);

    /// <summary>Lateral offset, + = right of the line in the direction of travel.</summary>
    public double CrossTrack(double e, double n) => (e - AE) * UN - (n - AN) * UE;

    public double Along(double e, double n) => (e - AE) * UE + (n - AN) * UN;

    /// <summary>The line from the nearest layout segment, oriented with the aircraft heading.</summary>
    public static Line? From(Layout layout, double e, double n, double heading, out string refused)
    {
        refused = "";
        if (layout.Nearest(e, n, heading) is not { } x) { refused = "no taxiway nearby"; return null; }
        var s = x.Seg;
        if (Math.Abs(x.Offset) > 3) { refused = $"aircraft {x.Offset:F1} m from the centreline of {s.Name} (path {s.Path}): put the nose wheel on the line"; return null; }
        if (x.Angle > 15) { refused = $"aircraft {x.Angle:F0}° off the axis of {s.Name} (path {s.Path}): align it with the line"; return null; }
        var forward = Math.Abs(((heading - s.Bearing) % 360 + 540) % 360 - 180) <= 90;
        var line = forward
            ? new Line(s.AE, s.AN, s.Bearing, 0, 0, s.Name, s.Path)
            : new Line(s.BE, s.BN, (s.Bearing + 180) % 360, 0, 0, s.Name, s.Path);
        var start = line.Along(e, n);
        // The layout cuts a straight taxiway into several segments: chain the next ones while they stay on the line.
        var (straight, pieces) = StraightAhead(layout, s, forward, line);
        var stop = Math.Min(start + Tracker.MaxDistance, straight - Tracker.EndClearance);
        if (stop - start < 40) { refused = $"only {Math.Max(0, stop - start):F0} m of straight line ahead on {s.Name} (path {s.Path}, {pieces} segment(s), {straight:F0} m): need 40 m or more"; return null; }
        return line with { StartAlong = start, StopAlong = stop };
    }

    /// <summary>
    /// Length of straight line from the start of the line, following the connected aircraft segments whose far end
    /// stays within 0.5 m of the line (and which go on in the same direction).
    /// </summary>
    private static (double Length, int Pieces) StraightAhead(Layout layout, Segment first, bool forward, Line line)
    {
        var node = forward ? first.EndNode : first.StartNode;
        var length = first.Length;
        var pieces = 1;
        var used = new HashSet<int> { first.Path };
        while (true)
        {
            Segment? next = null;
            double nextLength = 0;
            foreach (var s in layout.Segments)
            {
                if (used.Contains(s.Path) || s.Type is 3) continue;
                double fe, fn;
                if (s.StartNode == node) (fe, fn) = (s.BE, s.BN);
                else if (s.EndNode == node) (fe, fn) = (s.AE, s.AN);
                else continue;
                var along = line.Along(fe, fn);
                if (along <= length + 1 || Math.Abs(line.CrossTrack(fe, fn)) > 0.5) continue;
                next = s;
                nextLength = along;
            }
            if (next is null) return (length, pieces);
            used.Add(next.Path);
            node = next.StartNode == node ? next.EndNode : next.StartNode;
            length = nextLength;
            pieces++;
        }
    }
}

/// <summary>One simulation frame, in the frame of the line.</summary>
internal sealed record State(double T, double E, double N, double Heading, double GroundSpeed, double YawRate, bool OnGround);

/// <summary>What the tracker sends: rudder (−1..1, which steers the nose wheel), throttle, brakes (0..1), parking brake.</summary>
internal sealed record Command(double Rudder, double Throttle, double Brake, double ParkingBrake);

/// <summary>Values computed at each frame, recorded for the analysis.</summary>
internal sealed record Telemetry(string Phase, double CrossTrack, double HeadingError, double Along, double SteerDeg);

/// <summary>
/// Follows the line at TargetSpeed. Lateral law (Stanley-like, on a point ahead of the aircraft):
/// steer = −(heading error + atan(K · cross-track / speed) + Ki · ∫cross-track) − Kd · yaw rate, in degrees of nose wheel.
/// Test A1 (C172 rolling): the rudder steers the nose wheel linearly, 20° for a full command, positive = left; the
/// aircraft yaws about 0.22 s later; with the controls centred it drifts left (about −0.35°/s).
/// </summary>
internal sealed class Tracker(Line line)
{
    public const double TargetSpeed = 5;      // kt
    public const double MaxDistance = 150;    // m of automatic taxi
    public const double EndClearance = 30;    // m left before the end of the segment
    public const double Lookahead = 2;        // m ahead of the reference point (towards the nose wheel)
    public const double K = 0.6;              // 1/s
    public const double Kd = 0.3;             // degrees of steer per degree/s of yaw rate
    public const double Ki = 0.5;             // degrees of steer per metre·second of cross-track
    public const double MaxSteer = 20;        // degrees = full rudder (test A1)
    public const double MaxOffsetTerm = 10;   // degrees: limit of the cross-track term (gentle capture of an offset)
    public const double MinSpeed = 2;         // m/s used in the cross-track term (it explodes at very low speed)
    public const double StartSteer = 10;      // degrees: steering limit during the first 3 s of rolling
    public const double MaxSpeed = 10;        // kt: emergency stop above
    public const double MaxCrossTrack = 4;    // m: emergency stop above
    public const double MaxHeadingError = 25; // degrees: emergency stop above

    public Line Line => line;
    public string Phase { get; private set; } = "release";
    private double _phaseStart = double.NaN, _lastT = double.NaN, _integral, _speedIntegral, _rollingSince = double.NaN;

    public (Command Command, Telemetry Telemetry) Update(State s)
    {
        if (double.IsNaN(_phaseStart)) _phaseStart = s.T;
        var dt = double.IsNaN(_lastT) ? 0 : Math.Clamp(s.T - _lastT, 0, 0.1);
        _lastT = s.T;

        var h = s.Heading * Math.PI / 180;
        double pe = s.E + Lookahead * Math.Sin(h), pn = s.N + Lookahead * Math.Cos(h);
        var xte = line.CrossTrack(pe, pn);
        var along = line.Along(s.E, s.N);
        var headingError = ((s.Heading - line.Bearing) % 360 + 540) % 360 - 180;

        // Lateral law (also during the stop, so that the aircraft stays straight).
        // Test E1 run 1 (09/10/2026): with a 1 m/s floor and no limit, a 1.5 m offset at 0.7 kt gave full lock and
        // 16° of heading error. The cross-track term is now limited, and so is the steering just after the start.
        var v = Math.Max(s.GroundSpeed * 0.5144, MinSpeed);
        if (Phase is "track") _integral = Math.Clamp(_integral + xte * dt, -10, 10);
        if (double.IsNaN(_rollingSince) && s.GroundSpeed >= 0.5) _rollingSince = s.T;
        var offsetTerm = Math.Clamp(Math.Atan(K * xte / v) * 180 / Math.PI, -MaxOffsetTerm, MaxOffsetTerm);
        var steer = -(headingError + offsetTerm + Ki * _integral) - Kd * s.YawRate;
        var limit = double.IsNaN(_rollingSince) || s.T - _rollingSince < 3 ? StartSteer : MaxSteer;
        steer = Math.Clamp(steer, -limit, limit);
        var rudder = Phase == "release" || s.GroundSpeed < 0.5 ? 0 : -steer / MaxSteer;

        Command cmd;
        switch (Phase)
        {
            case "release":
                cmd = new Command(0, 0, 1, 0);
                if (s.T - _phaseStart >= 2) Next("track", s.T);
                break;
            case "track":
                var error = TargetSpeed - s.GroundSpeed;
                _speedIntegral = Math.Clamp(_speedIntegral + error * dt, -5, 15);
                var throttle = Math.Clamp(0.10 + 0.05 * error + 0.02 * _speedIntegral, 0, 0.45);
                var brake = s.GroundSpeed > TargetSpeed + 0.5 ? Math.Clamp(0.25 * (s.GroundSpeed - TargetSpeed - 0.5), 0, 0.5) : 0;
                cmd = new Command(rudder, brake > 0 ? 0 : throttle, brake, 0);
                if (along >= line.StopAlong) Next("stop", s.T);
                break;
            case "stop":
                var stopped = s.GroundSpeed < 0.3;
                cmd = new Command(stopped ? 0 : rudder, 0, stopped ? 1 : 0.5, stopped ? 1 : 0);
                if (stopped && s.T - _phaseStart > 1) Next("done", s.T);
                break;
            default:
                cmd = Safe;
                break;
        }
        return (cmd, new Telemetry(Phase, xte, headingError, along, steer));
    }

    private void Next(string phase, double t)
    {
        Phase = phase;
        _phaseStart = t;
    }

    /// <summary>Reason to stop at once, or null.</summary>
    public string? Watchdog(State s, Telemetry tm) =>
        s.GroundSpeed > MaxSpeed ? $"ground speed {s.GroundSpeed:F1} kt above {MaxSpeed} kt"
        : Phase != "release" && Math.Abs(tm.CrossTrack) > MaxCrossTrack ? $"{tm.CrossTrack:+0.0;-0.0} m from the centreline"
        : Math.Abs(tm.HeadingError) > MaxHeadingError ? $"heading {tm.HeadingError:+0;-0}° off the line"
        : !s.OnGround ? "aircraft no longer on the ground"
        : null;

    /// <summary>Idle, full brakes, parking brake, rudder centred.</summary>
    public static readonly Command Safe = new(0, 0, 1, 1);
}
