namespace PilotFlying.Experiments.GroundTracking;

/// <summary>Values computed at each frame while following a route, recorded for the analysis.</summary>
internal sealed record RouteTelemetry(string Phase, double S, double CrossTrack, double HeadingError, double SteerDeg,
    double TargetSpeed, string Name, double ToStop);

/// <summary>
/// Follows a route from a parking spot to a hold-short point.
/// Lateral law: pure pursuit (aim at the route point Ld metres ahead: steer = atan(2 · L · sin α / Ld)), which handles
/// turns, plus the integral of the offset for the left drift of the C172 (test A1), limited to ±20° (full rudder).
/// Speed: 5 kt, 3 kt when the route turns by more than 20° within the next 25 m, then a stop 4 m before the hold-short
/// point (aircraft reference point), never beyond it.
/// </summary>
internal sealed class Follower(Route route)
{
    public const double Cruise = 5, TurnSpeed = 3;   // kt
    public const double TurnAngle = 20, TurnLook = 25; // degrees within metres ahead
    public const double Wheelbase = 2.1;              // m: implied by speed, nose wheel angle and yaw rate (test A1)
    public const double MinLookahead = 5, LookaheadTime = 1.5; // m, s
    public const double Ki = 0.5, MaxSteer = 20, StartSteer = 10;
    public const double StopBefore = 4;               // m before the hold-short point
    public const double MaxSpeed = 10, MaxOffset = 4, MaxLost = 8; // emergency limits (kt, m, m)

    public Route Route => route;
    public string Phase { get; private set; } = "release";
    private double _s, _phaseStart = double.NaN, _lastT = double.NaN, _integral, _speedIntegral, _rollingSince = double.NaN;
    private double _distance;

    public double StopAt => route.Length - StopBefore;

    public (Command Command, RouteTelemetry Telemetry) Update(State st)
    {
        if (double.IsNaN(_phaseStart)) _phaseStart = st.T;
        var dt = double.IsNaN(_lastT) ? 0 : Math.Clamp(st.T - _lastT, 0, 0.1);
        _lastT = st.T;

        // Progress along the route: nearest point, searched just around the previous one.
        var (s, offset, distance) = route.Project(st.E, st.N, Math.Max(0, _s - 5), _s + 25);
        _s = s;
        _distance = distance;
        var headingError = ((st.Heading - route.BearingAt(s)) % 360 + 540) % 360 - 180;

        // Pure pursuit on the point Ld ahead.
        var v = st.GroundSpeed * 0.5144;
        var ld = Math.Max(MinLookahead, LookaheadTime * v);
        var (te, tn) = route.PointAt(s + ld);
        var alpha = ((Math.Atan2(te - st.E, tn - st.N) * 180 / Math.PI - st.Heading) % 360 + 540) % 360 - 180;
        if (Phase == "taxi") _integral = Math.Clamp(_integral + offset * dt, -10, 10);
        var steer = Math.Atan(2 * Wheelbase * Math.Sin(alpha * Math.PI / 180) / ld) * 180 / Math.PI - Ki * _integral;
        if (double.IsNaN(_rollingSince) && st.GroundSpeed >= 0.5) _rollingSince = st.T;
        var limit = double.IsNaN(_rollingSince) || st.T - _rollingSince < 3 ? StartSteer : MaxSteer;
        steer = Math.Clamp(steer, -limit, limit);
        var rudder = Phase == "release" || st.GroundSpeed < 0.5 ? 0 : -steer / MaxSteer;

        // Target speed: cruise, slower before turns, down to zero at the stop point.
        var toStop = StopAt - s;
        var target = Math.Min(Cruise, Math.Clamp((toStop - 0.5) * 0.6, 0, Cruise));
        if (route.TurnAhead(s, TurnLook) > TurnAngle) target = Math.Min(target, TurnSpeed);

        Command cmd;
        switch (Phase)
        {
            case "release":
                cmd = new Command(0, 0, 1, 0);
                if (st.T - _phaseStart >= 2) Next("taxi", st.T);
                break;
            case "taxi":
                var error = target - st.GroundSpeed;
                _speedIntegral = Math.Clamp(_speedIntegral + error * dt, -5, 15);
                var throttle = Math.Clamp(0.10 + 0.05 * error + 0.02 * _speedIntegral, 0, 0.45);
                var brake = st.GroundSpeed > target + 0.3 ? Math.Clamp(0.1 + 0.3 * (st.GroundSpeed - target - 0.3), 0, 0.8) : 0;
                cmd = new Command(rudder, brake > 0 || target < 0.5 ? 0 : throttle, brake, 0);
                // Route run 1 (09/10/2026): the aircraft stopped 0.8 m short of the stop point at a near-zero target speed
                // and the phase never changed; it now also ends when the target is below 0.5 kt and the aircraft stopped.
                if (toStop <= 0.5 || (target < 0.5 && st.GroundSpeed < 0.3)) Next("stop", st.T);
                break;
            case "stop":
                var stopped = st.GroundSpeed < 0.3;
                cmd = new Command(stopped ? 0 : rudder, 0, stopped ? 1 : 0.7, stopped ? 1 : 0);
                if (stopped && st.T - _phaseStart > 1) Next("done", st.T);
                break;
            default:
                cmd = Tracker.Safe;
                break;
        }
        return (cmd, new RouteTelemetry(Phase, s, offset, headingError, steer, target, route.NameAt(s), toStop));
    }

    private void Next(string phase, double t)
    {
        Phase = phase;
        _phaseStart = t;
    }

    /// <summary>Reason to stop at once, or null. The hold-short point is never passed.</summary>
    public string? Watchdog(State st, RouteTelemetry tm) =>
        st.GroundSpeed > MaxSpeed ? $"ground speed {st.GroundSpeed:F1} kt above {MaxSpeed} kt"
        : Phase != "release" && Math.Abs(tm.CrossTrack) > MaxOffset ? $"{tm.CrossTrack:+0.0;-0.0} m from the route"
        : _distance > MaxLost ? $"route lost ({_distance:F0} m from it)"
        : tm.S > route.Length - 1 ? "less than 1 m from the hold-short point"
        : !st.OnGround ? "aircraft no longer on the ground"
        : null;
}
