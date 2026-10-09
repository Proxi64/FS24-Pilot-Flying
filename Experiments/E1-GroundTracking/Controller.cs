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
        var stop = Math.Min(start + Tracker.MaxDistance, s.Length - Tracker.EndClearance);
        if (stop - start < 40) { refused = $"only {Math.Max(0, stop - start):F0} m of straight line ahead on {s.Name} (path {s.Path}, {s.Length:F0} m): need 40 m or more"; return null; }
        return line with { StartAlong = start, StopAlong = stop };
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
    public const double MaxSpeed = 10;        // kt: emergency stop above
    public const double MaxCrossTrack = 4;    // m: emergency stop above
    public const double MaxHeadingError = 25; // degrees: emergency stop above

    public Line Line => line;
    public string Phase { get; private set; } = "release";
    private double _phaseStart = double.NaN, _lastT = double.NaN, _integral, _speedIntegral;

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
        var v = Math.Max(s.GroundSpeed * 0.5144, 1);
        if (Phase is "track") _integral = Math.Clamp(_integral + xte * dt, -10, 10);
        var steer = -(headingError + Math.Atan(K * xte / v) * 180 / Math.PI + Ki * _integral) - Kd * s.YawRate;
        steer = Math.Clamp(steer, -MaxSteer, MaxSteer);
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
