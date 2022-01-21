
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
const string VERSION = "11.5.0";
const string DATE = "2021/07/31";

const string INI_SECTION_SWCS = "SWCS Config";
const string INI_KEY_IGNORE_TAG = "Wheel ignore name tag";
const string INI_KEY_BRAKING_CONST = "Braking constant";
const string INI_KEY_SCAN_CONNECTORS = "Detect blocks over connectors";

string wheelIgnoreNameTag = "Ignore";
float brakingConstant = 0.1f; //Increase this if your brakes are not strong enough!
bool detectBlocksOverConnectors = false;

bool canReadHandbrake = false;
bool handbrakeOverride = false;
bool isSetup = false;
IMyShipController lastControlledSeat = null;
string lastException = "";
DateTime lastExceptionTime;
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
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }
    
    // Write
    _ini.Set(INI_SECTION_SWCS, INI_KEY_IGNORE_TAG, wheelIgnoreNameTag);
    _ini.Set(INI_SECTION_SWCS, INI_KEY_BRAKING_CONST, brakingConstant);
    _ini.Set(INI_SECTION_SWCS, INI_KEY_SCAN_CONNECTORS, detectBlocksOverConnectors);
    
    string output = _ini.ToString();
    if (!string.Equals(output, customData))
        Me.CustomData = output;
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    runtimeTracker = new RuntimeTracker(this);
    scheduler = new Scheduler(this);
    setupAction = new ScheduledAction(GetBlocks, 0.1);

    scheduler.AddScheduledAction(setupAction);
    scheduler.AddScheduledAction(ControlSubgridWheels, 10);
    scheduler.AddScheduledAction(PrintEcho, 1);

    GetBlocks();
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

    if (!string.IsNullOrEmpty(lastException))
    {
        finalEchoBuilder.AppendLine($"\nLast exception:\n{lastException}\n{lastExceptionTime}");
    }

    base.Echo(finalEchoBuilder.ToString());
}

void ClearEcho()
{
    echoBuilder.Clear();
}

void Main(string arg, UpdateType updateSource)
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

    try
    {
        scheduler.Update();
    }
    catch (Exception e)
    {
        isSetup = false;
        lastException = e.StackTrace;
        lastExceptionTime = DateTime.Now;
    }

    runtimeTracker.AddInstructions();
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
    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    readonly Program _program;

    const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666;

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
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);

        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;
        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        // Remove all actions that we should dispose
        _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

        if (_currentlyQueuedAction == null)
        {
            // If queue is not empty, populate current queued action
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        // If queued action is populated
        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTime);
            if (_currentlyQueuedAction.JustRan)
            {
                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        _scheduledActions.Add(scheduledAction);
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
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(ScheduledAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get; private set; } = 0;
    public readonly double RunInterval;

    readonly double _runFrequency;
    readonly Action _action;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        _runFrequency = runFrequency;
        RunInterval = 1.0 / _runFrequency;
        DisposeAfterRun = removeAfterRun;
        TimeSinceLastRun = timeOffset;
    }

    public void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun >= RunInterval)
        {
            _action.Invoke();
            TimeSinceLastRun = 0;

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
