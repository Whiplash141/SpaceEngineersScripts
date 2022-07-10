
/*
/ //// / Whip's Ship Speed Timers / //// /

INSTRUCTIONS:
    1. Add timers to a group named "Speed Timers"
    2. Place this script in a programmable block
    3. Configure the custom data of the timers
    4. Recompile to process any block or custom data changes.















===================================
DO NOT EDIT VARIABLES IN THE SCRIPT
       USE THE CUSTOM DATA
===================================














*/


public const string Version = "1.2.0",
                    Date = "2022/07/09";

public const string ScriptName = "Speed Timers";
const string IniSectionGeneral = ScriptName + " - General Config";
const string IniKeyGroupName = "Timer group name";
const string IniKeyTitleScreen = "Draw title screen";

string _timerGroupName = ScriptName;
bool _drawTitleScreen = true;
int _drawCount = 0;

MyIni _ini = new MyIni();
List<IMyShipController> _shipControllers = new List<IMyShipController>();
List<SpeedTimer> _speedTimers = new List<SpeedTimer>();
ReturnCode _returnCode;
Vector3D _lastPosition = Vector3D.Zero;
RuntimeTracker _runtimeTracker;
Vector3D _lastVelocity;
Vector3D _lastAngularVelocity;
MatrixD _lastWm;
SpeedTimerTitleScreen _titleScreen;

enum ReturnCode { Success = 0, NoShipControllers = 1, NoGroup = 1 << 1, NoTimers = 1 << 2, Errors = NoGroup | NoTimers }

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    _runtimeTracker = new RuntimeTracker(this);
    _titleScreen = new SpeedTimerTitleScreen(Version, this);
    _returnCode = GetBlocks();
}

void Main(string arg, UpdateType update)
{
    _runtimeTracker.AddRuntime();
    if ((update & UpdateType.Update10) == 0)
        return;

    PrintStatus();
    
    if (_drawTitleScreen)
    {
        _drawCount = ++_drawCount % 30;
        if (_drawCount == 0)
        {
            _titleScreen.RestartDraw();
        }
        _titleScreen.Draw();
    }

    if ((_returnCode & ReturnCode.Errors) != 0)
        return;

    Vector3D? velocity = null;
    Vector3D? angVelocity = null;
    if ((_returnCode & ReturnCode.NoShipControllers) == 0)
    {
        foreach (var sc in _shipControllers)
        {
            if (GridTerminalSystem.CanAccess(sc))
            {
                var velocities = sc.GetShipVelocities();
                velocity = velocities.LinearVelocity;
                angVelocity = velocities.AngularVelocity;
                break;
            }
        }
    }

    if (!velocity.HasValue)
    {
        velocity = (Me.GetPosition() - _lastPosition) * 6;
    }
    var accel = (velocity.Value - _lastVelocity) * 6;
    _lastVelocity = velocity.Value;

    if (!angVelocity.HasValue)
    {
        angVelocity =(Vector3D.Cross(Me.WorldMatrix.Backward, _lastWm.Backward)
                    + Vector3D.Cross(Me.WorldMatrix.Right, _lastWm.Right)
                    + Vector3D.Cross(Me.WorldMatrix.Up, _lastWm.Up)) * 6;
        _lastWm = Me.WorldMatrix;
    }
    var angAccel = (angVelocity.Value - _lastAngularVelocity) * 6;
    _lastAngularVelocity = angVelocity.Value;

    foreach (var st in _speedTimers)
    {
        st.Update(velocity.Value.LengthSquared(), accel.LengthSquared(), angVelocity.Value.LengthSquared(), angAccel.LengthSquared());
    }
    _runtimeTracker.AddInstructions();
}

StringBuilder _echo = new StringBuilder(1024);
void PrintStatus()
{
    _echo.Append($"Whip's {ScriptName} Script\n(Version {Version} - {Date})\n\n");
    _echo.Append("Recompile to process block and custom data changes.\n\n");
    _echo.Append($"Last run: {DateTime.Now}\n");
    _echo.Append($"Speed timers: {_speedTimers.Count}\n");
    _echo.Append($"Precise velocity: {_shipControllers.Count != 0}\n");

    if ((_returnCode & ReturnCode.Errors) != 0)
    {
        _echo.Append("\nERRORS:\n");
        if ((_returnCode & ReturnCode.NoGroup) != 0)
        {
            _echo.Append($"- No block group named '{_timerGroupName}'\n");
        }
        else if ((_returnCode & ReturnCode.NoTimers) != 0)
        {
            _echo.Append($"- No timers in '{_timerGroupName}' group\n");
        }
    }
    _echo.Append("\n");
    _echo.Append(_runtimeTracker.Write());

    Echo(_echo.ToString());
    _echo.Clear();
}

ReturnCode GetBlocks()
{
    _shipControllers.Clear();
    _speedTimers.Clear();

    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _timerGroupName = _ini.Get(IniSectionGeneral, IniKeyGroupName).ToString(_timerGroupName);
        _drawTitleScreen = _ini.Get(IniSectionGeneral, IniKeyTitleScreen).ToBoolean(_drawTitleScreen);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSectionGeneral, IniKeyGroupName, _timerGroupName);
    _ini.Set(IniSectionGeneral, IniKeyTitleScreen, _drawTitleScreen);

    string content = _ini.ToString();
    if (content != Me.CustomData)
    {
        Me.CustomData = content;
    }

    _returnCode = ReturnCode.Success;
    GridTerminalSystem.GetBlocksOfType(_shipControllers, b => b.IsSameConstructAs(Me));
    if (_shipControllers.Count == 0)
    {
        _returnCode |= ReturnCode.NoShipControllers;
    }
    var group = GridTerminalSystem.GetBlockGroupWithName(_timerGroupName);
    if (group == null)
    {
        _returnCode |= ReturnCode.NoGroup;
    }
    else
    {
        group.GetBlocksOfType<IMyTimerBlock>(null, b => {
            _speedTimers.Add(new SpeedTimer(b, _ini));
            return false;
        });

        if (_speedTimers.Count == 0)
        {
            _returnCode |= ReturnCode.NoTimers;
        }
    }

    return _returnCode;
}

public class SpeedTimer
{
    const string IniSection = ScriptName + " - Timer Config";
    const string IniKeyThresholdType = "Threshold type";
    const string IniCommentThresholdType = " Accepted values are SPEED, ACCEL, ANGULAR_SPEED, or ANGULAR_ACCEL";
    const string IniKeyThresholdDirection = "Trigger when";
    const string IniCommentThresholdDirection = " Accepted values are BELOW or ABOVE";
    const string IniKeyValueThreshold = "Value threshold";
    const string IniCommentUnits = 
        " Accepted \"Threshold type\" values:\n"
        + "     SPEED, ACCEL, ANGULAR_SPEED, or ANGULAR_ACCEL\n"
        + " Accepted \"Trigger when\" values:\n"
        + "     BELOW or ABOVE\n"
        + " Units for speed are: m/s\n"
        + " Units for accel are: m/s²\n"
        + " Units for angular speed are: rad/s\n"
        + " Units for angular accel are: rad/s²";

    IMyTimerBlock _timer;
    MyIni _ini;
    ThresholdDirection _direction = ThresholdDirection.Above;
    ThresholdType _type = ThresholdType.Speed;
    double _thresholdValue = 50;
    bool _lastThresholdMet = false;

    enum ThresholdDirection { Below, Above }
    enum ThresholdType { Speed, Acceleration, AngularSpeed, AngularAcceleration }

    public SpeedTimer(IMyTimerBlock timer, MyIni ini)
    {
        _timer = timer;
        _ini = ini;
        ProcessIni();
    }

    void ProcessIni()
    {
        _ini.Clear();
        if (_ini.TryParse(_timer.CustomData))
        {
            string typeStr = _ini.Get(IniSection, IniKeyThresholdType).ToString();
            _type = StringToThresholdType(typeStr, _type);

            string dirnStr = _ini.Get(IniSection, IniKeyThresholdDirection).ToString();
            _direction = StringToThresholdDirection(dirnStr, _direction);

            _thresholdValue = _ini.Get(IniSection, IniKeyValueThreshold).ToDouble(_thresholdValue);
        }
        else if (!string.IsNullOrWhiteSpace(_timer.CustomData))
        {
            _ini.EndContent = _timer.CustomData;
        }

        _ini.Set(IniSection, IniKeyThresholdType, ThresholdTypeToString(_type));
        _ini.Set(IniSection, IniKeyThresholdDirection, ThresholdDirectionToString(_direction));
        _ini.Set(IniSection, IniKeyValueThreshold, _thresholdValue);

        // _ini.SetComment(IniSection, IniKeyThresholdType, IniCommentThresholdType);
        // _ini.SetComment(IniSection, IniKeyThresholdDirection, IniCommentThresholdDirection);
        
        _ini.EndComment = IniCommentUnits;

        string content = _ini.ToString();
        if (content != _timer.CustomData)
        {
            _timer.CustomData = content;
        }
    }

    public void Update(double speedSq, double accelSq, double angularSpeedSq, double angularAccelSq)
    {
        double val = 0;
        switch (_type)
        {
            case ThresholdType.Speed:
                val = speedSq;
                break;
            case ThresholdType.Acceleration:
                val = accelSq;
                break;
            case ThresholdType.AngularSpeed:
                val = angularSpeedSq;
                break;
            case ThresholdType.AngularAcceleration:
                val = angularAccelSq;
                break;
        }

        bool thresholdMet = false;
        var threshSq = _thresholdValue * _thresholdValue;
        if (_direction == ThresholdDirection.Above)
        {
            thresholdMet = val > threshSq;
        }
        else
        {
            thresholdMet = val < threshSq;
        }

        // Only trigger once per crossing
        if (thresholdMet && thresholdMet != _lastThresholdMet)
        {
            _timer.Trigger();
        }
        _lastThresholdMet = thresholdMet;
    }

    ThresholdDirection StringToThresholdDirection(string val, ThresholdDirection defaultVal)
    {
        switch (val.ToUpperInvariant())
        {
            default:
                return defaultVal;
            case "ABOVE":
                return ThresholdDirection.Above;
            case "BELOW":
                return ThresholdDirection.Below;
        }
    }

    string ThresholdDirectionToString(ThresholdDirection val)
    {
        switch (val)
        {
            default:
            case ThresholdDirection.Above:
                return "ABOVE";
            case ThresholdDirection.Below:
                return "BELOW";
        }
    }

    ThresholdType StringToThresholdType(string val, ThresholdType defaultVal)
    {
        switch (val.ToUpperInvariant())
        {
            default:
                return defaultVal;
            case "SPEED":
                return ThresholdType.Speed;
            case "ACCEL":
                return ThresholdType.Acceleration;
            case "ANGULAR_SPEED":
                return ThresholdType.AngularSpeed;
            case "ANGULAR_ACCEL":
                return ThresholdType.AngularAcceleration;
        }
    }

    string ThresholdTypeToString(ThresholdType val)
    {
        switch (val)
        {
            default:
            case ThresholdType.Speed:
                return "SPEED";
            case ThresholdType.Acceleration:
                return "ACCEL";
            case ThresholdType.AngularSpeed:
                return "ANGULAR_SPEED";
            case ThresholdType.AngularAcceleration:
                return "ANGULAR_ACCEL";
        }
    }
}

/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime { get; private set; }
    public double MaxInstructions { get; private set; }
    public double AverageRuntime { get; private set; }
    public double AverageInstructions { get; private set; }
    public double LastRuntime { get; private set; }
    public double LastInstructions { get; private set; }

    readonly Queue<double> _runtimes = new Queue<double>();
    readonly Queue<double> _instructions = new Queue<double>();
    readonly int _instructionLimit;
    readonly Program _program;
    const double MS_PER_TICK = 16.6666;

    const string Format = "General Runtime Info\n"
            + "- Avg runtime: {0:n4} ms\n"
            + "- Last runtime: {1:n4} ms\n"
            + "- Max runtime: {2:n4} ms\n"
            + "- Avg instructions: {3:n2}\n"
            + "- Last instructions: {4:n0}\n"
            + "- Max instructions: {5:n0}\n"
            + "- Avg complexity: {6:0.000}%";

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.005)
    {
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void AddRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        LastRuntime = runtime;
        AverageRuntime += (Sensitivity * runtime);
        int roundedTicksSinceLastRuntime = (int)Math.Round(_program.Runtime.TimeSinceLastRun.TotalMilliseconds / MS_PER_TICK);
        if (roundedTicksSinceLastRuntime == 1)
        {
            AverageRuntime *= (1 - Sensitivity);
        }
        else if (roundedTicksSinceLastRuntime > 1)
        {
            AverageRuntime *= Math.Pow((1 - Sensitivity), roundedTicksSinceLastRuntime);
        }

        _runtimes.Enqueue(runtime);
        if (_runtimes.Count == Capacity)
        {
            _runtimes.Dequeue();
        }

        MaxRuntime = _runtimes.Max();
    }

    public void AddInstructions()
    {
        double instructions = _program.Runtime.CurrentInstructionCount;
        LastInstructions = instructions;
        AverageInstructions = Sensitivity * (instructions - AverageInstructions) + AverageInstructions;

        _instructions.Enqueue(instructions);
        if (_instructions.Count == Capacity)
        {
            _instructions.Dequeue();
        }

        MaxInstructions = _instructions.Max();
    }

    public string Write()
    {
        return string.Format(
            Format,
            AverageRuntime,
            LastRuntime,
            MaxRuntime,
            AverageInstructions,
            LastInstructions,
            MaxInstructions,
            AverageInstructions / _instructionLimit);
    }
}

class SpeedTimerTitleScreen
{
    // General
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;

    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;
    const string Font = "Debug";
    const string TitleFormat = "Speed Timers - v{0}";
    readonly string _titleText;
    Program _program;
    int _idx = 0;

    // Specific
    const float TimerScale = 1.5f;
    const float SpeedometerScale = 1f;
    readonly Vector2 _timerPos = new Vector2(0, 120);
    readonly Vector2 _speedometerPos = new Vector2(0, -30);
    static readonly Color _timerBlinkColor = new Color(0, 100, 255, 255);
    static readonly Color _timerIdleColor = new Color(0, 255, 0);

    struct Anim
    {
        public float AngleRad;
        public bool Trigger;

        public Anim(float angleDeg, bool trigger = false)
        {
            AngleRad = MathHelper.ToRadians(angleDeg);
            Trigger = trigger;
        }
    }

    readonly Anim[] _animSequence = new Anim[] {
        new Anim(-90),
        new Anim(-90),
        new Anim(-90),
        new Anim(-90),
        new Anim(-60),
        new Anim(-30),
        new Anim(0, true),
        new Anim(30),
        new Anim(60),
        new Anim(90),
        new Anim(90),
        new Anim(90),
        new Anim(90),
        new Anim(60),
        new Anim(30),
        new Anim(0),
        new Anim(-30),
        new Anim(-60),
    };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public SpeedTimerTitleScreen(string version, Program program)
    {
        _titleText = string.Format(TitleFormat, version);
        _program = program;
        _surface = _program.Me.GetSurface(0);
    }

    public void Draw()
    {
        if (_surface == null)
            return;

        Anim anim = _animSequence[_idx];
        _idx = ++_idx % _animSequence.Length;

        SetupDrawSurface(_surface);

        Vector2 center = _surface.TextureSize * 0.5f;
        Vector2 scale = _surface.SurfaceSize / 512f;
        float minScale = Math.Min(scale.X, scale.Y);

        using (var frame = _surface.DrawFrame())
        {
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }
            DrawSpeedometer(frame, center + minScale * _speedometerPos, minScale, anim.AngleRad);
            DrawTimer(frame, center + minScale * _timerPos, anim.Trigger ? _timerBlinkColor : _timerIdleColor, minScale * TimerScale);
            DrawTitleBar(_surface, frame, minScale);
        }
    }

    public void RestartDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    #region Draw Helper Functions
    void DrawTitleBar(IMyTextSurface _surface, MySpriteDrawFrame frame, float scale)
    {
        float titleBarHeight = scale * TitleBarHeightPx;
        Vector2 topLeft = 0.5f * (_surface.TextureSize - _surface.SurfaceSize);
        Vector2 titleBarSize = new Vector2(_surface.TextureSize.X, titleBarHeight);
        Vector2 titleBarPos = topLeft + new Vector2(_surface.TextureSize.X * 0.5f, titleBarHeight * 0.5f);
        Vector2 titleBarTextPos = topLeft + new Vector2(_surface.TextureSize.X * 0.5f, 0.5f * (titleBarHeight - scale * BaseTextHeightPx));

        // Title bar
        frame.Add(new MySprite(
            Texture,
            "SquareSimple",
            titleBarPos,
            titleBarSize,
            _topBarColor,
            null,
            Center));

        // Title bar text
        frame.Add(new MySprite(
            SpriteType.TEXT,
            _titleText,
            titleBarTextPos,
            null,
            _white,
            Font,
            Center,
            TextSize * scale));
    }

    void SetupDrawSurface(IMyTextSurface _surface)
    {
        _surface.ScriptBackgroundColor = _black;
        _surface.ContentType = ContentType.SCRIPT;
        _surface.Script = "";
    }

    public void DrawTimer(MySpriteDrawFrame frame, Vector2 centerPos, Color blinkColor, float scale = 1f)
    {
        const string sq = "SquareSimple";
        frame.Add(new MySprite(Texture, sq, new Vector2(0f, 0f) * scale + centerPos, new Vector2(100f, 100f) * scale, _white, null, Center, 0f)); // block
        frame.Add(new MySprite(Texture, sq, new Vector2(20f, 0f) * scale + centerPos, new Vector2(10f, 100f) * scale, _black, null, Center, 0f)); // right stripe
        frame.Add(new MySprite(Texture, sq, new Vector2(0f, 20f) * scale + centerPos, new Vector2(50f, 10f) * scale, _black, null, Center, 0f)); // bottom stripe
        frame.Add(new MySprite(Texture, sq, new Vector2(0f, -20f) * scale + centerPos, new Vector2(50f, 10f) * scale, _black, null, Center, 0f)); // top stripe
        frame.Add(new MySprite(Texture, sq, new Vector2(-20f, 0f) * scale + centerPos, new Vector2(10f, 100f) * scale, _black, null, Center, 0f)); // left stripe
        frame.Add(new MySprite(Texture, sq, new Vector2(0f, 0f) * scale + centerPos, new Vector2(30f, 30f) * scale, blinkColor, null, Center, 0f)); // blinky bit
    }

    public void DrawSpeedometer(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _white, null, Center, 0f)); // arc
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(220f, 220f) * scale, _black, null, Center, 0f)); // center
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(30f, 30f) * scale, _white, null, Center, 0f + rotation)); // needle center
        frame.Add(new MySprite(Texture, "Triangle", new Vector2(cos * 0f - sin * -60f, sin * 0f + cos * -60f) * scale + centerPos, new Vector2(15f, 100f) * scale, _white, null, Center, 0f + rotation)); // needle
    }

    #endregion
}
