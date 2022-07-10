
/*
/ //// / Whip's Subgrid Wheel Control Script / //// /

Instructions:
1.) Place this script on your ship

2.) Place some wheels on subgrids

3.) Place a cockpit

4.) Enjoy :)

All subgrid wheels to the left of the seat you are controlling will be treated as left wheels. Same thing goes for the right side.
*/


/*
//=============================================
// THERE IS NO REASON TO TOUCH ANYTHING BELOW
// DOING SO WILL VOID YOUR WARRANTY
//=============================================
*/
const string VERSION = "11.7.2";
const string DATE = "2022/07/09";

const string INI_SECTION_SWCS = "SWCS Config";
const string INI_KEY_IGNORE_TAG = "Wheel ignore name tag";
const string INI_KEY_BRAKING_CONST = "Braking constant";
const string INI_KEY_SCAN_CONNECTORS = "Detect blocks over connectors";
const string INI_KEY_TITLE_SCREEN = "Draw title screen";

string wheelIgnoreNameTag = "Ignore";
float brakingConstant = 0.1f; //Increase this if your brakes are not strong enough!
bool detectBlocksOverConnectors = false;

bool drawTitleScreen = true;
bool canReadHandbrake = false;
bool handbrakeOverride = false;
bool isSetup = false;
IMyShipController lastControlledSeat = null;
RuntimeTracker runtimeTracker;
Scheduler scheduler;
ScheduledAction setupAction;
StringBuilder echoBuilder = new StringBuilder();
StringBuilder finalEchoBuilder = new StringBuilder();

List<IMyMotorSuspension> subgridWheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> ignoredWheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();
List<IMyShipController> shipControllers = new List<IMyShipController>();
StringBuilder setupBuilder = new StringBuilder();
SwcsTitleScreen titleScreen;
readonly MyIni _ini = new MyIni();

void ParseIni()
{
    _ini.Clear();
    string customData = Me.CustomData;
    if (_ini.TryParse(customData))
    {
        // Read
        wheelIgnoreNameTag = _ini.Get(INI_SECTION_SWCS, INI_KEY_IGNORE_TAG).ToString(wheelIgnoreNameTag);
        brakingConstant = _ini.Get(INI_SECTION_SWCS, INI_KEY_BRAKING_CONST).ToSingle(brakingConstant);
        detectBlocksOverConnectors = _ini.Get(INI_SECTION_SWCS, INI_KEY_SCAN_CONNECTORS).ToBoolean(detectBlocksOverConnectors);
        drawTitleScreen = _ini.Get(INI_SECTION_SWCS, INI_KEY_TITLE_SCREEN).ToBoolean(drawTitleScreen);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }
    
    // Write
    _ini.Set(INI_SECTION_SWCS, INI_KEY_IGNORE_TAG, wheelIgnoreNameTag);
    _ini.Set(INI_SECTION_SWCS, INI_KEY_BRAKING_CONST, brakingConstant);
    _ini.Set(INI_SECTION_SWCS, INI_KEY_SCAN_CONNECTORS, detectBlocksOverConnectors);
    _ini.Set(INI_SECTION_SWCS, INI_KEY_TITLE_SCREEN, drawTitleScreen);

    string output = _ini.ToString();
    if (!string.Equals(output, customData))
        Me.CustomData = output;
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    titleScreen = new SwcsTitleScreen(VERSION, this);
    runtimeTracker = new RuntimeTracker(this);
    scheduler = new Scheduler(this);
    setupAction = new ScheduledAction(GetBlocks, 0.1);

    scheduler.AddScheduledAction(setupAction);
    scheduler.AddScheduledAction(ControlSubgridWheels, 10);
    scheduler.AddScheduledAction(DrawTitleScreen, 6);
    scheduler.AddScheduledAction(titleScreen.RestartDraw, 0.2);
    scheduler.AddScheduledAction(PrintEcho, 1);

    GetBlocks();
}

void DrawTitleScreen()
{
    if (drawTitleScreen)
    {
        titleScreen.Draw();
    }
}

/*
 * Hiding default echo implementation so that we can display precisely when we want.
 */
new void Echo(string text)
{
    echoBuilder.AppendLine(text);
}

void PrintEcho()
{
    finalEchoBuilder.Clear();
    finalEchoBuilder.AppendLine($"Whip's Subgrid Wheel Control Script\n(Version {VERSION} - {DATE})\n");

    finalEchoBuilder.Append(echoBuilder);

    finalEchoBuilder.AppendLine($"\nNext refresh in {Math.Ceiling(Math.Max(setupAction.RunInterval - setupAction.TimeSinceLastRun, 0))} seconds\n");
    finalEchoBuilder.AppendLine($"Last setup results:\n{setupBuilder}{(isSetup ? "> Setup Successful!" : "> Setup Failed!")}\n");
    finalEchoBuilder.AppendLine(runtimeTracker.Write());

    base.Echo(finalEchoBuilder.ToString());
}

void ClearEcho()
{
    echoBuilder.Clear();
}

void Main(string arg, UpdateType updateSource)
{
    try
    {
        runtimeTracker.AddRuntime();

        if (!string.IsNullOrWhiteSpace(arg))
        {
            switch (arg.ToLowerInvariant())
            {
                case "brake_toggle":
                    handbrakeOverride = !handbrakeOverride;
                    break;
                case "brake_on":
                    handbrakeOverride = true;
                    break;
                case "brake_off":
                    handbrakeOverride = false;
                    break;
            }
        }
    
        scheduler.Update();
        runtimeTracker.AddInstructions();
    }
    catch (Exception e)
    {
        BlueScreenOfDeath.Show(Me.GetSurface(0), "SWCS", VERSION, e);
    }
}

void ControlSubgridWheels()
{
    if (!isSetup)
        return;

    ClearEcho();

    var referenceController = GetControlledShipController(shipControllers);
    if (referenceController == null)
    {
        Echo("> No driver detected");

        if (lastControlledSeat != null)
            referenceController = lastControlledSeat;
        else
            referenceController = shipControllers[0];
    }
    else
    {
        Echo("> Wheels are being controlled");
        lastControlledSeat = referenceController;
    }

    var avgWheelPosition = GetSubgridWheels(referenceController);
    canReadHandbrake = ignoredWheels.Count + wheels.Count - subgridWheels.Count > 0;
    SynchronizeHandBrakes(referenceController);

    if (subgridWheels.Count == 0)
    {
        Echo("> No subgrid wheels found\n> Pausing execution...");
        return;
    }

    var wasdInput = referenceController.MoveIndicator;
    bool brakes = wasdInput.Y > 0 || (canReadHandbrake ? referenceController.HandBrake : handbrakeOverride);
    var velocity = Vector3D.TransformNormal(referenceController.GetShipVelocities().LinearVelocity, MatrixD.Transpose(referenceController.WorldMatrix)) * brakingConstant;

    if (!canReadHandbrake)
    {
        Echo($"> No wheels on main grid.\n    To enable handbrake, use the\n    arguments:\n      brake_toggle\n      brake_on\n      brake_off");
    }
    else
    {
        Echo($"> Found wheels on main grid.\n    To enable handbrake, use the [P] key");
    }
    Echo($"> Brakes enabled: {brakes}");
    Echo($"> Reference controller: {referenceController.CustomName}");

    foreach (var wheel in subgridWheels)
    {
        var steerMult = Math.Sign(Math.Round(Vector3D.Dot(wheel.WorldMatrix.Forward, referenceController.WorldMatrix.Up), 2)) * Math.Sign(Vector3D.Dot(wheel.GetPosition() - avgWheelPosition, referenceController.WorldMatrix.Forward));
        var propulsionMult = -Math.Sign(Math.Round(Vector3D.Dot(wheel.WorldMatrix.Up, referenceController.WorldMatrix.Right), 2));
        var steerValue = steerMult * wasdInput.X;
        var power = wheel.Power * 0.01f;
        var propulsionValue = 0f;
        if (brakes && wheel.Brake)
        {
            propulsionValue = propulsionMult * (float)velocity.Z;  
        }
        else
        {
            //steer + is right if up == up
            //steer + is left if up == down
            propulsionValue = propulsionMult * power * -wasdInput.Z;
        }
        wheel.PropulsionOverride = propulsionValue;
        wheel.SteeringOverride = steerValue;
    }
}

void SynchronizeHandBrakes(IMyShipController controller)
{
    bool handbrake = canReadHandbrake ? controller.HandBrake : handbrakeOverride;
    foreach (var block in shipControllers)
    {
        if (block.HandBrake != handbrake)
            block.HandBrake = handbrake;
    }
}

IMyShipController GetControlledShipController(List<IMyShipController> controllers)
{
    IMyShipController firstControlled = null;
    foreach (IMyShipController thisController in controllers)
    {
        // Main controller takes priority
        if (thisController.IsMainCockpit)
        {
            return thisController;
        }
        
        if (firstControlled == null && thisController.ControlWheels && thisController.IsUnderControl && thisController.CanControlShip)
        {
            firstControlled = thisController;
        }
    }

    return firstControlled;
}

Vector3D GetSubgridWheels(IMyTerminalBlock reference)
{
    var summedWheelPosition = Vector3D.Zero;
    subgridWheels.Clear();
    
    foreach (IMyMotorSuspension block in ignoredWheels)
    {
        summedWheelPosition += block.GetPosition();
    }
    
    foreach (IMyMotorSuspension block in wheels)
    {
        summedWheelPosition += block.GetPosition();

        if (reference.CubeGrid != block.CubeGrid)
        {
            subgridWheels.Add(block);
        }
        else
        {
            block.PropulsionOverride = 0f;
            block.SteeringOverride = 0f;
        }
    }

    return summedWheelPosition / (wheels.Count + ignoredWheels.Count);
}

bool BlockCollect(IMyTerminalBlock block)
{
    if (!detectBlocksOverConnectors && !Me.IsSameConstructAs(block))
    {
        return false;
    }

    if (block is IMyMotorSuspension)
    {
        var wheel = (IMyMotorSuspension)block;
        if (StringExtensions.Contains(block.CustomName, wheelIgnoreNameTag))
        {
            ignoredWheels.Add(wheel);
        }
        else
        {
            wheels.Add(wheel);
        }
    }
    else if (block is IMyShipController)
    {
        shipControllers.Add((IMyShipController)block);
    }

    return false;
}

void GetBlocks()
{
    wheels.Clear();
    ignoredWheels.Clear();
    shipControllers.Clear();

    setupBuilder.Clear();
    
    ParseIni();

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, BlockCollect);

    if (shipControllers.Count == 0)
    {
        setupBuilder.AppendLine($">> Error: No ship controllers");
        isSetup = false;
        return;
    }

    if (wheels.Count == 0)
    {
        setupBuilder.AppendLine(">> Error: No wheels");
        isSetup = false;
        return;
    }

    isSetup = true;
}


#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    public double CurrentTimeSinceLastRun { get; private set; } = 0;
    public long CurrentTicksSinceLastRun { get; private set; } = 0;

    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<QueuedAction> _queuedActions = new Queue<QueuedAction>();
    readonly Program _program;

    public const long TicksPerSecond = 60;
    public const double TickDurationSeconds = 1.0 / TicksPerSecond;
    const long ClockTicksPerGameTick = 166666L;

    /// <summary>
    /// Constructs a scheduler object with timing based on the runtime of the input program.
    /// </summary>
    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

    /// <summary>
    /// Updates all ScheduledAcions in the schedule and the queue.
    /// </summary>
    public void Update()
    {
        _inUpdate = true;
        long deltaTicks = Math.Max(0, _program.Runtime.TimeSinceLastRun.Ticks / ClockTicksPerGameTick);

        if (_firstRun)
        {
            if (_ignoreFirstRun)
            {
                deltaTicks = 0;
            }
            _firstRun = false;
        }

        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTicksSinceLastRun = action.TicksSinceLastRun + deltaTicks;
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTicks * TickDurationSeconds;
            action.Update(deltaTicks);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        if (_actionsToDispose.Count > 0)
        {
            _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));
        }

        if (_currentlyQueuedAction == null)
        {
            // If queue is not empty, populate current queued action
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        // If queued action is populated
        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTicks);
            if (_currentlyQueuedAction.JustRan)
            {
                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
        _inUpdate = false;

        if (_actionsToAdd.Count > 0)
        {
            _scheduledActions.AddRange(_actionsToAdd);
            _actionsToAdd.Clear();
        }
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    /// <summary>
    /// Adds an Action to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(Action action, double updateInterval)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        QueuedAction scheduledAction = new QueuedAction(action, updateInterval);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(QueuedAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class QueuedAction : ScheduledAction
{
    public QueuedAction(Action action, double runInterval)
        : base(action, 1.0 / runInterval, removeAfterRun: true, timeOffset: 0)
    { }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get { return TicksSinceLastRun * Scheduler.TickDurationSeconds; } }
    public long TicksSinceLastRun { get; private set; } = 0;
    public double RunInterval
    {
        get
        {
            return RunIntervalTicks * Scheduler.TickDurationSeconds;
        }
        set
        {
            RunIntervalTicks = (long)Math.Round(value * Scheduler.TicksPerSecond);
        }
    }
    public long RunIntervalTicks
    {
        get
        {
            return _runIntervalTicks;
        }
        set
        {
            if (value == _runIntervalTicks)
                return;

            _runIntervalTicks = value < 0 ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : 1.0 / _runIntervalTicks;
        }
    }

    public double RunFrequency
    {
        get
        {
            return _runFrequency;
        }
        set
        {
            if (value == _runFrequency)
                return;

            if (value == 0)
                RunIntervalTicks = long.MaxValue;
            else
                RunIntervalTicks = (long)Math.Round(Scheduler.TicksPerSecond / value);
        }
    }

    long _runIntervalTicks;
    double _runFrequency;
    readonly Action _action;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TicksSinceLastRun = (long)Math.Round(timeOffset * Scheduler.TicksPerSecond);
    }

    public void Update(long deltaTicks)
    {
        TicksSinceLastRun += deltaTicks;

        if (TicksSinceLastRun >= RunIntervalTicks)
        {
            _action.Invoke();
            TicksSinceLastRun = 0;

            JustRan = true;
        }
        else
        {
            JustRan = false;
        }
    }
}
#endregion

/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime {get; private set;}
    public double MaxInstructions {get; private set;}
    public double AverageRuntime {get; private set;}
    public double AverageInstructions {get; private set;}
    public double LastRuntime {get; private set;}
    public double LastInstructions {get; private set;}
    
    readonly Queue<double> _runtimes = new Queue<double>();
    readonly Queue<double> _instructions = new Queue<double>();
    readonly StringBuilder _sb = new StringBuilder();
    readonly int _instructionLimit;
    readonly Program _program;
    const double MS_PER_TICK = 16.6666;

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
        _sb.Clear();
        _sb.AppendLine("General Runtime Info");
        _sb.AppendLine($"  Avg instructions: {AverageInstructions:n2}");
        _sb.AppendLine($"  Last instructions: {LastInstructions:n0}");
        _sb.AppendLine($"  Max instructions: {MaxInstructions:n0}");
        _sb.AppendLine($"  Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
        _sb.AppendLine($"  Avg runtime: {AverageRuntime:n4} ms");
        _sb.AppendLine($"  Last runtime: {LastRuntime:n4} ms");
        _sb.AppendLine($"  Max runtime: {MaxRuntime:n4} ms");
        return _sb.ToString();
    }
}

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

class SwcsTitleScreen
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
    readonly string _titleText;
    Program _program;
    int _idx = 0;
    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    // Specific
    const string TitleFormat = "SWCS - v{0}";
    const float WasdScale = 1.5f;
    const float ArrowScale = 1f;
    const float WheelScale = 0.5f;
    const float RotorScale = 1f;
    readonly Vector2 _wasdPos = new Vector2(-100, 100);
    readonly Vector2 _arrowPos = new Vector2(100, -50);
    readonly Vector2 _wheelPos = new Vector2(100, -30);
    readonly Vector2 _rotorPos = new Vector2(100, 90);
    static readonly Color _pressedColor = new Color(100, 100, 100);
    enum WasdKey { None, W, A, S, D }
    enum Arrow { None, CW, CCW }

    struct Anim
    {
        public WasdKey Key;
        public float WheelAngle;
        public Arrow Arrow;

        public Anim(float wheelAng, WasdKey key = WasdKey.None, Arrow arrow = Arrow.None)
        {
            WheelAngle = MathHelper.ToRadians(wheelAng);
            Key = key;
            Arrow = arrow;
        }
    }

    readonly Anim[] _animSequence = new Anim[] {
        new Anim(0),
        new Anim(0),
        new Anim(30, WasdKey.D, Arrow.CW),
        new Anim(60, WasdKey.D, Arrow.CW),
        new Anim(90, WasdKey.D, Arrow.CW),
        new Anim(120, WasdKey.D, Arrow.CW),
        new Anim(150, WasdKey.D, Arrow.CW),
        new Anim(180),
        new Anim(180),
        new Anim(150, WasdKey.A, Arrow.CCW),
        new Anim(120, WasdKey.A, Arrow.CCW),
        new Anim(90, WasdKey.A, Arrow.CCW),
        new Anim(60, WasdKey.A, Arrow.CCW),
        new Anim(30, WasdKey.A, Arrow.CCW),
    };
    
    public SwcsTitleScreen(string version, Program program)
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
        Vector2 scaleVec = _surface.SurfaceSize / 512f;
        float scale = Math.Min(scaleVec.X, scaleVec.Y);

        using (var frame = _surface.DrawFrame())
        {
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }

            if (anim.Arrow == Arrow.CW)
            {
                DrawArrowCW(frame, _arrowPos * scale + center, ArrowScale * scale);
            }
            else if (anim.Arrow == Arrow.CCW)
            {
                DrawArrowCCW(frame, _arrowPos * scale + center, ArrowScale * scale);
            }
            DrawWasdIcon(frame, _wasdPos * scale + center, anim.Key, WasdScale * scale);
            DrawRotor(frame, _rotorPos * scale + center, RotorScale * scale);
            DrawWheel(frame, _wheelPos * scale + center, WheelScale * scale, anim.WheelAngle);

            DrawTitleBar(_surface, frame, scale);
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

    void DrawWasdIcon(MySpriteDrawFrame frame, Vector2 centerPos, WasdKey pressedKey = WasdKey.None, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(55f, 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, pressedKey == WasdKey.D ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // d key
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, pressedKey == WasdKey.S ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // s key
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-55f, 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, pressedKey == WasdKey.A ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // a key
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, -55f) * scale + centerPos, new Vector2(50f, 50f) * scale, pressedKey == WasdKey.W ? _pressedColor : _white, null, TextAlignment.CENTER, 0f)); // w key
        frame.Add(new MySprite(SpriteType.TEXT, "D", new Vector2(47f, -15f) * scale + centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f * scale)); // d
        frame.Add(new MySprite(SpriteType.TEXT, "S", new Vector2(-9f, -15f) * scale + centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f * scale)); // s
        frame.Add(new MySprite(SpriteType.TEXT, "A", new Vector2(-65f, -15f) * scale + centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f * scale)); // a
        frame.Add(new MySprite(SpriteType.TEXT, "W", new Vector2(-13f, -68f) * scale + centerPos, null, _black, "DEBUG", TextAlignment.LEFT, 1f * scale)); // w
    }

    void DrawRotor(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -15f) * scale + centerPos, new Vector2(80f, 10f) * scale, _white, null, Center, 0f)); // stator top
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 20f) * scale + centerPos, new Vector2(100f, 60f) * scale, _white, null, Center, 0f)); // stator
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -35f) * scale + centerPos, new Vector2(40f, 20f) * scale, _white, null, Center, 0f)); // rotor shaft
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -50f) * scale + centerPos, new Vector2(100f, 10f) * scale, _white, null, Center, 0f)); // rotor top
    }

    void DrawArrowCW(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(200f, 200f) * scale, _white, null, Center, 0f + rotation)); // body
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, -2.3562f + rotation)); // body mask2
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(150f, 150f) * scale, _black, null, Center, 0f + rotation)); // body mask center
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, 2.3562f + rotation)); // body mask1
        frame.Add(new MySprite(Texture, "Triangle", new Vector2(cos * 75f - sin * -48f, sin * 75f + cos * -48f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, Center, 2.3562f + rotation)); // head
    }

    void DrawArrowCCW(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(200f, 200f) * scale, _white, null, Center, 0f + rotation)); // body
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, -2.3562f + rotation)); // body mask2
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(150f, 150f) * scale, _black, null, Center, 0f + rotation)); // body mask center
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, 2.3562f + rotation)); // body mask1
        frame.Add(new MySprite(Texture, "Triangle", new Vector2(cos * -75f - sin * -48f, sin * -75f + cos * -48f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, Center, -2.3562f + rotation)); // head
    }

    public void DrawWheel(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(275f, 275f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // tire shadow
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // tire
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(150f, 150f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // cutout
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -23f - sin * 32f, sin * -23f + cos * 32f) * scale + centerPos, new Vector2(25f, 100f) * scale, _white, null, TextAlignment.CENTER, -2.5133f + rotation)); // spoke5
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 23f - sin * 32f, sin * 23f + cos * 32f) * scale + centerPos, new Vector2(25f, 100f) * scale, _white, null, TextAlignment.CENTER, 2.5133f + rotation)); // spoke4
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -38f - sin * -12f, sin * -38f + cos * -12f) * scale + centerPos, new Vector2(25f, 100f) * scale, _white, null, TextAlignment.CENTER, -1.2566f + rotation)); // spoke3
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 38f - sin * -12f, sin * 38f + cos * -12f) * scale + centerPos, new Vector2(25f, 100f) * scale, _white, null, TextAlignment.CENTER, 1.2566f + rotation)); // spoke2
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 0f - sin * -40f, sin * 0f + cos * -40f) * scale + centerPos, new Vector2(25f, 100f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // spoke1
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // center
    }

    #endregion
}

#region BSOD
static class BlueScreenOfDeath 
{
    const int MAX_BSOD_WIDTH = 35;
    const string BSOD_TEMPLATE =
    "{0} - v{1}\n\n"+ 
    "A fatal exception has occured at\n"+
    "{2}. The current\n"+
    "program will be terminated.\n"+
    "\n"+ 
    "EXCEPTION:\n"+
    "{3}\n"+
    "\n"+
    "* Please REPORT this crash message to\n"+ 
    "  the Bug Reports discussion of this script\n"+ 
    "\n"+
    "* Press RECOMPILE to restart the program";

    static StringBuilder bsodBuilder = new StringBuilder(256);
    
    public static void Show(IMyTextSurface surface, string scriptName, string version, Exception e)
    {
        if (surface == null) 
        { 
            return;
        }
        surface.ContentType = ContentType.TEXT_AND_IMAGE;
        surface.Alignment = TextAlignment.LEFT;
        float scaleFactor = 512f / (float)Math.Min(surface.TextureSize.X, surface.TextureSize.Y);
        surface.FontSize = scaleFactor * surface.TextureSize.X / (26f * MAX_BSOD_WIDTH);
        surface.FontColor = Color.White;
        surface.BackgroundColor = Color.Blue;
        surface.Font = "Monospace";
        string exceptionStr = e.ToString();
        string[] exceptionLines = exceptionStr.Split('\n');
        bsodBuilder.Clear();
        foreach (string line in exceptionLines)
        {
            if (line.Length <= MAX_BSOD_WIDTH)
            {
                bsodBuilder.Append(line).Append("\n");
            }
            else
            {
                string[] words = line.Split(' ');
                int lineLength = 0;
                foreach (string word in words)
                {
                    lineLength += word.Length;
                    if (lineLength >= MAX_BSOD_WIDTH)
                    {
                        lineLength = 0;
                        bsodBuilder.Append("\n");
                    }
                    bsodBuilder.Append(word).Append(" ");
                }
                bsodBuilder.Append("\n");
            }
        }

        surface.WriteText(string.Format(BSOD_TEMPLATE, 
                                        scriptName.ToUpperInvariant(),
                                        version,
                                        DateTime.Now, 
                                        bsodBuilder));
    }
}
#endregion
