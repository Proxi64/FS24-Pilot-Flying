namespace PilotFlying.Experiments.FenixControls;

/// <summary>What an action can do in the cockpit (native SimConnect only, no WASM module).</summary>
internal interface ICockpit
{
    /// <summary>Latest value of a watched variable ("L:NAME" or SimVar name), NaN if not received.</summary>
    double Get(string name);
    /// <summary>Writes an LVar ("NAME", without "L:").</summary>
    void SetL(string lvar, double value);
    /// <summary>Sends a key event with its raw parameter.</summary>
    void Key(string evt, int raw);
    /// <summary>Latest value of an input event, null if unknown or not received.</summary>
    double? InputEventValue(string name);
    /// <summary>Sets an input event by name; false if the aircraft does not have it.</summary>
    bool SetInputEvent(string name, double value);
    void Wait(double seconds);
    void Note(string text);
}

/// <summary>One step of the experiment: which question, what to look at in the cockpit, what it does.</summary>
internal sealed record Act(string Name, string Question, string Look, Action<ICockpit> Do);

internal static class Script
{
    // Fenix LVar names: HubHop presets for "FenixSim A320" (https://hubhop.mobiflight.com, used as a reference only,
    // no licence stated). Push buttons are counters (each press and each release adds 1), knobs are encoders (+1/−1
    // per detent), autobrake buttons are 1 while pressed.
    public static readonly string[] LVars =
    [
        "S_FCU_AP1", "S_FCU_AP2", "S_FCU_ATHR", "S_FCU_LOC", "S_FCU_APPR",
        "S_FCU_SPEED", "S_FCU_HEADING", "S_FCU_ALTITUDE", "S_FCU_VERTICAL_SPEED",
        "E_FCU_SPEED", "E_FCU_HEADING", "E_FCU_ALTITUDE", "E_FCU_VS",
        "N_FCU_SPEED", "N_FCU_HEADING", "N_FCU_ALTITUDE", "N_FCU_VS",
        "I_FCU_AP1", "I_FCU_AP2", "I_FCU_ATHR", "I_FCU_LOC", "I_FCU_APPR",
        "I_FCU_SPEED_MANAGED", "I_FCU_HEADING_MANAGED", "I_FCU_ALTITUDE_MANAGED",
        "B_FCU_SPEED_DASHED", "B_FCU_HEADING_DASHED", "B_FCU_VERTICALSPEED_DASHED", "B_FCU_POWER",
        "S_MIP_AUTOBRAKE_LO", "S_MIP_AUTOBRAKE_MED", "I_MIP_AUTOBRAKE_LO_L", "I_MIP_AUTOBRAKE_LO_U",
        "I_MIP_AUTOBRAKE_MED_L", "I_MIP_AUTOBRAKE_MED_U",
        "S_FC_FLAPS", "A320_FC_L_FLAPS", "S_MIP_PARKING_BRAKE", "S_MIP_GEAR",
        "N_FC_CAPT_TILLER", "A_FC_THROTTLE_LEFT_INPUT", "A_FC_THROTTLE_RIGHT_INPUT",
        "S_OH_EXT_LT_BEACON", "I_MIP_MASTER_WARNING_CAPT", "I_MIP_MASTER_CAUTION_CAPT",
        "S_ENG_MASTER_1", "S_ENG_MASTER_2",
    ];

    /// <summary>Standard SimVars watched as well (name, unit).</summary>
    public static readonly (string Name, string Unit)[] SimVars =
    [
        ("YOKE X POSITION", "position"), ("YOKE Y POSITION", "position"),
        ("ELEVATOR POSITION", "position"), ("AILERON POSITION", "position"), ("RUDDER POSITION", "position"),
        ("RUDDER PEDAL POSITION", "position"),
        ("GENERAL ENG THROTTLE LEVER POSITION:1", "percent over 100"), ("GENERAL ENG THROTTLE LEVER POSITION:2", "percent over 100"),
        ("STEER INPUT CONTROL", "percent over 100"), ("GEAR CENTER STEER ANGLE", "degrees"),
        ("FLAPS HANDLE INDEX", "number"), ("SPOILERS HANDLE POSITION", "percent over 100"),
        ("BRAKE PARKING POSITION", "bool"), ("AUTOPILOT MASTER", "bool"),
        ("AUTOPILOT AIRSPEED HOLD VAR", "knots"), ("AUTOPILOT HEADING LOCK DIR", "degrees"),
        ("AUTOPILOT ALTITUDE LOCK VAR", "feet"),
        ("LIGHT BEACON", "bool"), ("ELECTRICAL MAIN BUS VOLTAGE", "volts"),
        ("SIM ON GROUND", "bool"), ("GROUND VELOCITY", "knots"), ("ENG COMBUSTION:1", "bool"), ("ENG COMBUSTION:2", "bool"),
    ];

    /// <summary>LVars the actions write (a data definition each).</summary>
    public static readonly string[] Writable =
    [
        "S_FCU_AP1", "S_FCU_ATHR", "S_FCU_LOC", "S_FCU_APPR", "S_FCU_SPEED",
        "E_FCU_SPEED", "E_FCU_HEADING", "E_FCU_ALTITUDE",
        "S_MIP_AUTOBRAKE_LO", "S_MIP_AUTOBRAKE_MED", "S_FC_FLAPS", "S_OH_EXT_LT_BEACON", "N_FC_CAPT_TILLER",
        "A_FC_THROTTLE_LEFT_INPUT", "A_FC_THROTTLE_RIGHT_INPUT",
    ];

    public static readonly string[] KeyEvents =
        ["AXIS_ELEVATOR_SET", "AXIS_AILERONS_SET", "AXIS_RUDDER_SET", "THROTTLE_SET", "AXIS_STEERING_SET"];

    public const string SpeedKnobInputEvent = "FNX320_INPUT_KNOB_PUSHPULL_E_FCU_SPEED_KNOB";

    /// <summary>
    /// Sent when the run ends or is stopped: axes and tiller centred, Fenix thrust levers back to their initial position.
    /// THROTTLE_SET 0 is NOT idle on the Fenix (test B1, 09/10/2026: it left the levers at about 50 %), so the
    /// levers are put back through their LVars.
    /// </summary>
    public static void SafeState(ICockpit c)
    {
        foreach (var e in new[] { "AXIS_ELEVATOR_SET", "AXIS_AILERONS_SET", "AXIS_RUDDER_SET", "AXIS_STEERING_SET" })
            c.Key(e, 0);
        if (!double.IsNaN(_thrustLeft0)) c.SetL("A_FC_THROTTLE_LEFT_INPUT", _thrustLeft0);
        if (!double.IsNaN(_thrustRight0)) c.SetL("A_FC_THROTTLE_RIGHT_INPUT", _thrustRight0);
    }

    // ------------------------------------------------------------------ primitives

    /// <summary>A push button: press (+1), release (+1).</summary>
    private static void Click(ICockpit c, string lvar)
    {
        var v = c.Get("L:" + lvar);
        if (double.IsNaN(v)) v = 0;
        c.SetL(lvar, v + 1);
        c.Wait(0.15);
        c.SetL(lvar, v + 2);
    }

    /// <summary>A knob turned by n detents (one every 0.1 s).</summary>
    private static void Turn(ICockpit c, string lvar, int detents)
    {
        for (var i = 0; i < Math.Abs(detents); i++)
        {
            var v = c.Get("L:" + lvar);
            c.SetL(lvar, (double.IsNaN(v) ? 0 : v) + Math.Sign(detents));
            c.Wait(0.1);
        }
    }

    private static void PressRelease(ICockpit c, string lvar)
    {
        c.SetL(lvar, 1);
        c.Wait(0.15);
        c.SetL(lvar, 0);
    }

    /// <summary>An axis event held at +v, then −v, then 0 (2 s each).</summary>
    private static void Sweep(ICockpit c, string evt, double v)
    {
        c.Key(evt, (int)Math.Round(v * 16383));
        c.Wait(2);
        c.Key(evt, (int)Math.Round(-v * 16383));
        c.Wait(2);
        c.Key(evt, 0);
    }

    // ------------------------------------------------------------------ the actions

    private static double _flaps0, _beacon0, _tiller0, _thrustLeft0 = double.NaN, _thrustRight0 = double.NaN;

    public static readonly Act[] Actions =
    [
        new("baseline", "B4", "nothing: 5 s of reading", c =>
        {
            c.Wait(5);
            _flaps0 = c.Get("L:S_FC_FLAPS");
            _beacon0 = c.Get("L:S_OH_EXT_LT_BEACON");
            _tiller0 = c.Get("L:N_FC_CAPT_TILLER");
            _thrustLeft0 = c.Get("L:A_FC_THROTTLE_LEFT_INPUT");
            _thrustRight0 = c.Get("L:A_FC_THROTTLE_RIGHT_INPUT");
        }),
        new("ap1-click", "B3", "AP1 button on the FCU", c => Click(c, "S_FCU_AP1")),
        new("ap1-click-again", "B3", "AP1 button (back)", c => Click(c, "S_FCU_AP1")),
        new("athr-click", "B3", "A/THR button", c => Click(c, "S_FCU_ATHR")),
        new("athr-click-again", "B3", "A/THR button (back)", c => Click(c, "S_FCU_ATHR")),
        new("loc-click", "B3", "LOC button", c => Click(c, "S_FCU_LOC")),
        new("loc-click-again", "B3", "LOC button (back)", c => Click(c, "S_FCU_LOC")),
        new("appr-click", "B3", "APPR button", c => Click(c, "S_FCU_APPR")),
        new("appr-click-again", "B3", "APPR button (back)", c => Click(c, "S_FCU_APPR")),
        new("spd-knob-plus-5", "B3", "SPD window of the FCU", c => Turn(c, "E_FCU_SPEED", 5)),
        new("spd-knob-minus-5", "B3", "SPD window (back)", c => Turn(c, "E_FCU_SPEED", -5)),
        new("hdg-knob-plus-10", "B3", "HDG window", c => Turn(c, "E_FCU_HEADING", 10)),
        new("hdg-knob-minus-10", "B3", "HDG window (back)", c => Turn(c, "E_FCU_HEADING", -10)),
        new("alt-knob-plus-5", "B3", "ALT window", c => Turn(c, "E_FCU_ALTITUDE", 5)),
        new("alt-knob-minus-5", "B3", "ALT window (back)", c => Turn(c, "E_FCU_ALTITUDE", -5)),
        new("spd-knob-pull", "B3", "SPD knob pulled: selected speed (no more dashes)", c =>
        {
            var v = c.Get("L:S_FCU_SPEED");
            c.SetL("S_FCU_SPEED", (double.IsNaN(v) ? 0 : v) + 1);
        }),
        new("spd-knob-push", "B3", "SPD knob pushed: managed speed (dashes)", c =>
        {
            var v = c.Get("L:S_FCU_SPEED");
            c.SetL("S_FCU_SPEED", (double.IsNaN(v) ? 0 : v) - 1);
        }),
        new("spd-knob-input-event", "B3", "SPD window, through the input event", c =>
        {
            var v = c.InputEventValue(SpeedKnobInputEvent);
            if (v is null) { c.Note($"{SpeedKnobInputEvent}: no value received, not set"); return; }
            c.SetInputEvent(SpeedKnobInputEvent, v.Value + 1);
            c.Wait(1.5);
            c.SetInputEvent(SpeedKnobInputEvent, v.Value);
        }),
        new("autobrake-lo", "B3", "autobrake LO button", c => PressRelease(c, "S_MIP_AUTOBRAKE_LO")),
        new("autobrake-lo-again", "B3", "autobrake LO button (back)", c => PressRelease(c, "S_MIP_AUTOBRAKE_LO")),
        new("autobrake-med", "B3", "autobrake MED button", c => PressRelease(c, "S_MIP_AUTOBRAKE_MED")),
        new("autobrake-med-again", "B3", "autobrake MED button (back)", c => PressRelease(c, "S_MIP_AUTOBRAKE_MED")),
        new("flaps-lever-1", "B3", "flaps lever to 1", c => c.SetL("S_FC_FLAPS", 1)),
        new("flaps-lever-back", "B3", "flaps lever back", c => c.SetL("S_FC_FLAPS", double.IsNaN(_flaps0) ? 0 : _flaps0)),
        new("beacon-switch", "B3", "BEACON switch on the overhead", c => c.SetL("S_OH_EXT_LT_BEACON", _beacon0 > 0.5 ? 0 : 1)),
        new("beacon-switch-back", "B3", "BEACON switch (back)", c => c.SetL("S_OH_EXT_LT_BEACON", double.IsNaN(_beacon0) ? 0 : _beacon0)),
        new("axis-elevator", "B1", "sidestick fore and aft", c => Sweep(c, "AXIS_ELEVATOR_SET", 0.5)),
        new("axis-ailerons", "B1", "sidestick left and right", c => Sweep(c, "AXIS_AILERONS_SET", 0.5)),
        new("axis-rudder", "B1", "rudder pedals", c => Sweep(c, "AXIS_RUDDER_SET", 0.5)),
        new("axis-throttle", "B1", "thrust levers (engines off)", c =>
        {
            c.Key("THROTTLE_SET", (int)Math.Round(0.3 * 16383));
            c.Wait(2);
            c.Key("THROTTLE_SET", 0);
            c.Wait(2);
            // THROTTLE_SET 0 is not idle on the Fenix: put the levers back through their LVars.
            if (!double.IsNaN(_thrustLeft0)) c.SetL("A_FC_THROTTLE_LEFT_INPUT", _thrustLeft0);
            if (!double.IsNaN(_thrustRight0)) c.SetL("A_FC_THROTTLE_RIGHT_INPUT", _thrustRight0);
        }),
        new("axis-steering", "B2", "tiller, through AXIS_STEERING_SET", c => Sweep(c, "AXIS_STEERING_SET", 0.5)),
        new("tiller-lvar", "B2", "tiller, through its LVar N_FC_CAPT_TILLER", c =>
        {
            c.SetL("N_FC_CAPT_TILLER", 30);
            c.Wait(2);
            c.SetL("N_FC_CAPT_TILLER", -30);
            c.Wait(2);
            c.SetL("N_FC_CAPT_TILLER", double.IsNaN(_tiller0) ? 0 : _tiller0);
        }),
    ];

    /// <summary>Seconds to observe after each action before the next one.</summary>
    public const double Settle = 2.5;
}
