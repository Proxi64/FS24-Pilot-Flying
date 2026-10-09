namespace PilotFlying.Experiments.AxisControl;

/// <summary>
/// Fake run that follows a script, to test the analysis without MSFS. Model: 30 Hz frames with jitter, 40 ms dead time
/// + 60 ms lag on every control, elevator direction inverted, brakes received as raw 0–32K positions, joystick that
/// wins whenever it moves. On the ground: nose wheel turned by the rudder (10° at full deflection) and by the tiller
/// (30°), simple speed and yaw dynamics (wheelbase 1.6 m).
/// </summary>
internal static class Synthetic
{
    public static Recording Run(ScriptDef script)
    {
        var rng = new Random(42);
        var rec = new Recording();
        rec.Notes.Add($"SYNTHETIC DATA (model, not MSFS). Script: {script.Name}");
        var rolling = script.Chained;
        var state = new double[Script.Channels.Length];
        state[Script.ParkingBrake] = 1;
        double speed = 0, heading = 90, lat = 43.38, lon = -0.42;
        var m = Measures(state, speed, heading, lat, lon, rolling, 0);
        var t = 0.0;
        foreach (var phase in script.Phases)
        {
            var start = t;
            var pending = new List<(double At, int Channel, double Value)>();
            var cmd = Enumerable.Repeat(double.NaN, Script.Channels.Length).ToArray();
            var nextSend = 0.0;
            var period = phase.RateHz > 0 ? 1.0 / phase.RateHz : double.PositiveInfinity;
            while (t - start < phase.Duration)
            {
                var local = t - start;
                if (script.Watchdog(m) is { } danger)
                {
                    rec.Notes.Add($"Stopped during {phase.Name}: {danger}");
                    return rec;
                }
                while (local >= nextSend)
                {
                    var c = phase.Command(nextSend, m);
                    for (var i = 0; i < c.Length; i++)
                    {
                        cmd[i] = c[i] ?? double.NaN;
                        if (c[i] is not { } v) continue;
                        var at = start + nextSend + rng.NextDouble() * 0.002;
                        rec.Sends.Add(new Send(at, phase.Name, i, v));
                        pending.Add((at + 0.04, i, v));
                    }
                    nextSend += period;
                }
                foreach (var p in pending.Where(p => p.At <= t).ToList())
                {
                    state[p.Channel] += (p.Value - state[p.Channel]) * (1 - Math.Exp(-0.033 / 0.06));
                    if (Math.Abs(state[p.Channel] - p.Value) < 1e-3) pending.Remove(p);
                }
                // The joystick: moved by Hugues in these two phases, and then it overrides the program.
                if (phase.Name is "joystick-hands-on" or "joystick-take-back")
                {
                    var stick = 0.3 * Math.Sin(2 * Math.PI * local / 1.5);
                    if (Math.Abs(Math.Cos(2 * Math.PI * local / 1.5)) > 0.5) state[Script.Elevator] = stick;
                }

                var dt = 0.033 + (rng.NextDouble() - 0.5) * 0.01 + (rng.NextDouble() < 0.01 ? 0.03 : 0);
                var steer = Steer(state);
                if (rolling)
                {
                    var brake = Math.Max((state[Script.LeftBrake] + state[Script.RightBrake]) / 2, state[Script.ParkingBrake]);
                    var accel = 4 * Math.Max(0, state[Script.Throttle] - 0.08) - 6 * brake - (speed > 0 ? 0.3 : 0);
                    speed = speed <= 0.05 && accel <= 0.3 ? 0 : Math.Max(0, speed + accel * dt);
                    var v = speed * 0.5144;
                    var yaw = v * Math.Tan(steer * Math.PI / 180) / 1.6 * 180 / Math.PI;
                    heading = (heading + yaw * dt + 360) % 360;
                    lat += v * Math.Cos(heading * Math.PI / 180) * dt / 111320;
                    lon += v * Math.Sin(heading * Math.PI / 180) * dt / (111320 * Math.Cos(lat * Math.PI / 180));
                }
                m = Measures(state, speed, heading, lat, lon, rolling, steer);
                rec.Samples.Add(new Sample(t, phase.Name, (double[])cmd.Clone(), m));
                t += dt;
            }
            if (phase.NeutralAtEnd)
                for (var i = 0; i < Script.ParkingBrake; i++) state[i] = 0;
            if (!script.Chained) t += 2; // Hugues pressing Enter
        }
        return rec;
    }

    private static double Steer(double[] state) => Math.Clamp(10 * state[Script.Rudder] + 30 * state[Script.Steering], -30, 30);

    private static double[] Measures(double[] state, double speed, double heading, double lat, double lon, bool rolling, double steer)
    {
        var m = new double[Script.Measures.Length];
        m[Script.MElevator] = -state[Script.Elevator];
        m[Script.MYokeY] = state[Script.Elevator];
        m[Script.MAilerons] = state[Script.Ailerons];
        m[Script.MYokeX] = state[Script.Ailerons];
        m[Script.MRudder] = state[Script.Rudder];
        m[Script.MPedal] = state[Script.Rudder];
        m[Script.MThrottle] = Math.Clamp(state[Script.Throttle], 0, 1);
        m[Script.MBrakeLeft] = Math.Pow(Math.Clamp(state[Script.LeftBrake], 0, 1), 1.5) * 32768;
        m[Script.MBrakeRight] = Math.Pow(Math.Clamp(state[Script.RightBrake], 0, 1), 1.5) * 32768;
        m[Script.MParkingBrake] = state[Script.ParkingBrake] > 0.5 ? 1 : 0;
        m[Script.MSteerAngle] = m[Script.MGearSteer0] = m[Script.MContactSteer0] = steer;
        m[Script.MSteerInput] = state[Script.Steering];
        m[Script.MOnGround] = 1;
        m[Script.MGroundSpeed] = speed;
        m[Script.MCombustion] = rolling ? 1 : 0;
        m[Script.MCamera] = 2;
        m[Script.MMaxSteer] = 30;
        m[Script.MYawRate] = speed * 0.5144 * Math.Tan(steer * Math.PI / 180) / 1.6 * 180 / Math.PI;
        m[Script.MHeading] = heading;
        m[Script.MLat] = lat;
        m[Script.MLon] = lon;
        return m;
    }
}
