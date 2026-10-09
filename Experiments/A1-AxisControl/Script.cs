namespace PilotFlying.Experiments.AxisControl;

/// <summary>A control the experiment sends, with its key event and the conversion of a normalised value.</summary>
internal sealed record Channel(string Name, string Event, double Min, double Max, Func<double, int> ToRaw);

/// <summary>A simulation variable read at every simulation frame.</summary>
internal sealed record Measure(string Name, string Unit, bool Position = false);

/// <summary>
/// One phase of the experiment: what it checks, what Hugues must do, and the command sent at time t (seconds from
/// the start of the phase), given the latest SimVars. A null command means "send nothing on that channel".
/// </summary>
internal sealed record Phase(string Name, string Question, string Instruction, double Duration, int RateHz,
    Func<double, double[], double?[]> Command, bool NeutralAtEnd = true);

/// <summary>
/// A sequence of phases. Chained: the phases follow each other without waiting for Enter (the aircraft is rolling).
/// SafeState: what is sent when the script ends or is stopped. Watchdog: reason to stop at once, or null.
/// </summary>
internal sealed record ScriptDef(string Name, string Setup, Phase[] Phases, double?[] SafeState, bool Chained,
    Func<double[], string?> Precheck, Func<double[], string?> Watchdog);

internal static class Script
{
    // Key event parameters checked against the MSFS 2024 SDK documentation (Key Events pages), 9 October 2026.
    public static readonly Channel[] Channels =
    [
        new("Elevator", "AXIS_ELEVATOR_SET", -1, 1, v => (int)Math.Round(v * 16383)),
        new("Ailerons", "AXIS_AILERONS_SET", -1, 1, v => (int)Math.Round(v * 16383)),
        new("Rudder", "AXIS_RUDDER_SET", -1, 1, v => (int)Math.Round(v * 16383)),
        new("Throttle", "THROTTLE1_SET", 0, 1, v => (int)Math.Round(v * 16383)),
        // -16383 = 0 % braking, +16383 = 100 % (non-linear scale according to the documentation).
        new("LeftBrake", "AXIS_LEFT_BRAKE_SET", 0, 1, v => (int)Math.Round(-16383 + v * 32766)),
        new("RightBrake", "AXIS_RIGHT_BRAKE_SET", 0, 1, v => (int)Math.Round(-16383 + v * 32766)),
        // Nose wheel steering ("tiller"): 0 = straight ahead, -16384 far left, +16384 far right.
        new("Steering", "AXIS_STEERING_SET", -1, 1, v => (int)Math.Round(v * 16383)),
        new("ParkingBrake", "PARKING_BRAKE_SET", 0, 1, v => v > 0.5 ? 1 : 0),
    ];

    public const int Elevator = 0, Ailerons = 1, Rudder = 2, Throttle = 3, LeftBrake = 4, RightBrake = 5, Steering = 6,
        ParkingBrake = 7;

    // SimVars checked against the MSFS 2024 SDK documentation (SimVars pages), 9 October 2026.
    // New SimVars go at the end, so that older recordings can still be replayed.
    public static readonly Measure[] Measures =
    [
        new("ELEVATOR POSITION", "position", true),
        new("AILERON POSITION", "position", true),
        new("RUDDER POSITION", "position", true),
        new("YOKE Y POSITION", "position", true),
        new("YOKE X POSITION", "position", true),
        new("RUDDER PEDAL POSITION", "position", true),
        new("GENERAL ENG THROTTLE LEVER POSITION:1", "percent over 100"),
        new("BRAKE LEFT POSITION", "position", true),
        new("BRAKE RIGHT POSITION", "position", true),
        new("BRAKE PARKING POSITION", "bool"),
        new("GEAR CENTER STEER ANGLE", "degrees"),
        new("STEER INPUT CONTROL", "percent over 100"),
        new("SIM ON GROUND", "bool"),
        new("GROUND VELOCITY", "knots"),
        new("ENG COMBUSTION:1", "bool"),
        new("CAMERA STATE", "number"),
        // Added for the rolling test.
        new("GEAR STEER ANGLE:0", "degrees"),
        new("CONTACT POINT STEER ANGLE:0", "degrees"),
        new("NOSEWHEEL MAX STEERING ANGLE", "degrees"),
        new("NOSEWHEEL LOCK ON", "bool"),
        new("ROTATION VELOCITY BODY Y", "degrees per second"),
        new("PLANE HEADING DEGREES TRUE", "degrees"),
        new("PLANE LATITUDE", "degrees"),
        new("PLANE LONGITUDE", "degrees"),
    ];

    public const int MElevator = 0, MAilerons = 1, MRudder = 2, MYokeY = 3, MYokeX = 4, MPedal = 5, MThrottle = 6,
        MBrakeLeft = 7, MBrakeRight = 8, MParkingBrake = 9, MSteerAngle = 10, MSteerInput = 11, MOnGround = 12,
        MGroundSpeed = 13, MCombustion = 14, MCamera = 15, MGearSteer0 = 16, MContactSteer0 = 17, MMaxSteer = 18,
        MNoseLock = 19, MYawRate = 20, MHeading = 21, MLat = 22, MLon = 23;

    /// <summary>For each channel, the SimVars that should follow it (first = main one).</summary>
    public static readonly int[][] Followers =
    [
        [MElevator, MYokeY], [MAilerons, MYokeX], [MRudder, MPedal, MSteerAngle, MGearSteer0, MContactSteer0, MYawRate],
        [MThrottle], [MBrakeLeft], [MBrakeRight], [MSteerInput, MSteerAngle, MGearSteer0, MContactSteer0, MYawRate],
        [MParkingBrake],
    ];

    public static double?[] None() => new double?[Channels.Length];

    public static double?[] One(int channel, double value)
    {
        var c = None();
        c[channel] = value;
        return c;
    }

    // ================================================================== at a standstill, engine off

    public static readonly ScriptDef Standstill = new(
        "standstill",
        "Asobo Cessna 172 at a parking spot, engine off. The program moves the controls.",
        [
            new("read-only", "A3", "Do not touch any control.", 20, 0, (_, _) => None()),
            Steps("elevator-steps", Elevator, [(0, 1), (0.5, 1.5), (-0.5, 1.5), (1, 1.5), (-1, 1.5), (0, 1)]),
            Steps("ailerons-steps", Ailerons, [(0, 1), (0.5, 1.5), (-0.5, 1.5), (1, 1.5), (-1, 1.5), (0, 1)]),
            Steps("rudder-steps", Rudder, [(0, 1), (0.5, 1.5), (-0.5, 1.5), (1, 1.5), (-1, 1.5), (0, 1)]),
            Steps("throttle-steps", Throttle, [(0, 1), (0.5, 1.5), (1, 1.5), (0, 1.5)]),
            Steps("left-brake-steps", LeftBrake, [(0, 1), (0.25, 1), (0.5, 1), (0.75, 1), (1, 1), (0, 1)]),
            Steps("right-brake-steps", RightBrake, [(0, 1), (0.25, 1), (0.5, 1), (0.75, 1), (1, 1), (0, 1)]),
            Sine("elevator-sine-30hz", "A3", Elevator, 30),
            Sine("elevator-sine-60hz", "A3", Elevator, 60),
            Sine("rudder-steering", "A4", Rudder, 30, period: 4, amplitude: 1),
            Sine("tiller-steering", "A4", Steering, 30, period: 4, amplitude: 1),
            new("joystick-hands-off", "A2", "Do not touch the joystick.", 6, 30, (_, _) => One(Elevator, 0.5)),
            new("joystick-hands-on", "A2", "Move the joystick fore and aft during the whole phase.", 10, 30, (_, _) => One(Elevator, 0.5)),
            new("joystick-stop-sending", "A2", "Do not touch the joystick.", 8, 30,
                (t, _) => t < 2 ? One(Elevator, 0.5) : None(), NeutralAtEnd: false),
            new("joystick-take-back", "A2", "Move the joystick slightly fore and aft.", 6, 0, (_, _) => None()),
        ],
        // Everything back to neutral, parking brake untouched.
        [0, 0, 0, 0, 0, 0, 0, null],
        Chained: false,
        Precheck: m =>
            m[MOnGround] < 0.5 || m[MGroundSpeed] > 1 ? "The aircraft must be stopped on the ground (at a parking spot)."
            : m[MCombustion] > 0.5 ? "The engine must be off."
            : null,
        Watchdog: _ => null);

    private static Phase Steps(string name, int channel, (double Value, double Seconds)[] steps) =>
        new(name, "A1", "Do not touch any control.", steps.Sum(s => s.Seconds), 30, (t, _) =>
        {
            foreach (var (value, seconds) in steps)
            {
                if (t < seconds) return One(channel, value);
                t -= seconds;
            }
            return One(channel, steps[^1].Value);
        });

    private static Phase Sine(string name, string question, int channel, int rate, double period = 2, double amplitude = 0.6) =>
        new(name, question, "Do not touch any control.", 2 * period + 1, rate,
            (t, _) => One(channel, t < 2 * period ? amplitude * Math.Sin(2 * Math.PI * t / period) : 0));

    // ================================================================== rolling slowly, engine at idle

    public const double TargetSpeed = 4;   // kt
    public const double MaxSpeed = 8;      // kt: emergency stop above
    public const double MaxTurn = 60;      // degrees of heading change: emergency stop above

    /// <summary>Phases chained without stopping: about 75 m travelled in 45 s.</summary>
    public static ScriptDef Rolling()
    {
        var speed = new SpeedHold();
        double? startHeading = null;
        double?[] Hold(double t, double[] m, int steeringChannel = -1, double steering = 0)
        {
            var c = None();
            (c[Throttle], var brake) = speed.Update(t, m[MGroundSpeed]);
            c[LeftBrake] = c[RightBrake] = brake;
            c[ParkingBrake] = 0;
            if (steeringChannel >= 0) c[steeringChannel] = steering;
            return c;
        }
        const string hands = "Hands and feet off the controls. Esc = emergency stop (throttle idle, brakes, parking brake).";
        return new ScriptDef(
            "rolling",
            "Asobo Cessna 172 on a straight, clear taxiway (at least 150 m ahead), engine running at idle, parking brake set.",
            [
                new("roll-release", "A4", hands, 3, 30, (_, _) =>
                {
                    var c = None();
                    c[Throttle] = 0;
                    c[LeftBrake] = c[RightBrake] = 1;
                    c[ParkingBrake] = 0;
                    return c;
                }, NeutralAtEnd: false),
                new("roll-accelerate", "A4", hands, 15, 30, (t, m) => Hold(t, m), NeutralAtEnd: false),
                new("roll-rudder", "A4", hands, 8, 30,
                    (t, m) => Hold(t, m, Rudder, 0.5 * Math.Sin(2 * Math.PI * t / 4)), NeutralAtEnd: false),
                new("roll-straight-1", "A4", hands, 3, 30, (t, m) => Hold(t, m, Rudder, 0), NeutralAtEnd: false),
                new("roll-tiller", "A4", hands, 8, 30,
                    (t, m) => Hold(t, m, Steering, 0.5 * Math.Sin(2 * Math.PI * t / 4)), NeutralAtEnd: false),
                new("roll-straight-2", "A4", hands, 3, 30, (t, m) => Hold(t, m, Steering, 0), NeutralAtEnd: false),
                new("roll-stop", "A4", hands, 8, 30, (_, m) =>
                {
                    var c = None();
                    c[Throttle] = 0;
                    var stopped = m[MGroundSpeed] < 0.3;
                    c[LeftBrake] = c[RightBrake] = stopped ? 1 : 0.5;
                    c[ParkingBrake] = stopped ? 1 : 0;
                    return c;
                }, NeutralAtEnd: false),
            ],
            // Throttle idle, controls centred, full brakes and parking brake.
            [0, 0, 0, 0, 1, 1, 0, 1],
            Chained: true,
            Precheck: m =>
                m[MOnGround] < 0.5 || m[MGroundSpeed] > 1 ? "The aircraft must be stopped on the ground."
                : m[MCombustion] < 0.5 ? "The engine must be running (idle)."
                : m[MParkingBrake] < 0.5 ? "Set the parking brake before starting."
                : null,
            Watchdog: m =>
            {
                startHeading ??= m[MHeading];
                var turn = Math.Abs(((m[MHeading] - startHeading.Value) % 360 + 540) % 360 - 180);
                return m[MGroundSpeed] > MaxSpeed ? $"ground speed {m[MGroundSpeed]:F1} kt above {MaxSpeed} kt"
                    : turn > MaxTurn ? $"heading changed by {turn:F0}°"
                    : m[MOnGround] < 0.5 ? "aircraft no longer on the ground"
                    : null;
            });
    }

    /// <summary>Holds the ground speed at TargetSpeed with the throttle (PI, capped), brakes only if too fast.</summary>
    private sealed class SpeedHold
    {
        private double _integral, _lastT = double.NaN;

        public (double Throttle, double Brake) Update(double t, double groundSpeed)
        {
            var dt = double.IsNaN(_lastT) || t < _lastT ? 0 : Math.Min(t - _lastT, 0.1);
            _lastT = t;
            var error = TargetSpeed - groundSpeed;
            _integral = Math.Clamp(_integral + error * dt, -5, 15);
            var throttle = Math.Clamp(0.10 + 0.05 * error + 0.02 * _integral, 0, 0.45);
            var brake = groundSpeed > TargetSpeed + 1.5 ? Math.Clamp(0.3 * (groundSpeed - TargetSpeed - 1.5), 0, 0.6) : 0;
            return (brake > 0 ? 0 : throttle, brake);
        }
    }
}
