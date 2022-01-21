
#region In-game Script
/*
/ //// / Whip's Stop Distance Calculator / //// /
*/

//-------------------------------------------------------------------------
//============ NO TOUCH BELOW HERE!!! =====================================
//-------------------------------------------------------------------------

#region Fields
const string DATE = "09/02/2019";
const string VERSION = "7.2.1";
const string TEXT_PANEL_NAME_TAG = "Stop Distance";

readonly Scheduler scheduler;
readonly StringBuilder _echoOutput = new StringBuilder();
readonly StringBuilder _setupOutput = new StringBuilder();
readonly StringBuilder _textOutput = new StringBuilder();
readonly List<IMyThrust> _thrusters = new List<IMyThrust>();
readonly List<IMyShipController> _shipControllers = new List<IMyShipController>();
readonly List<IMyTextPanel> _textPanels = new List<IMyTextPanel>();
readonly ScheduledAction _setupActionTenSeconds;
readonly Vector3D[] _baseDirection = new Vector3D[3]
{
Vector3D.Right,
Vector3D.Up,
Vector3D.Backward,
};

readonly Dictionary<Vector3D, double> _thrustDirectionDict = new Dictionary<Vector3D, double>();
readonly ThrustDirectionCalculator _thrustCalculator;

IMyShipController _reference = null;
ScheduledAction _setupAction = null;
double _shipMass = 0;
#endregion

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;

    scheduler = new Scheduler(this);
    scheduler.AddScheduledAction(CalculateStopParameters, 6);
    scheduler.AddScheduledAction(PrintEchos, 1);

    _setupAction = new ScheduledAction(Setup, 0.1, true);
    scheduler.AddQueuedAction(_setupAction);

    _thrustCalculator = new ThrustDirectionCalculator(Me.CubeGrid, _thrusters);
    Setup();
}

void Main(string arg, UpdateType updateSource)
{
    scheduler.Update();
}

void PrintEchos()
{
    Echo(_echoOutput.ToString());
    Echo(_textOutput.ToString());
    Echo(_setupOutput.ToString());
}

void Setup()
{
    _setupOutput.Clear();
    _setupOutput.AppendLine("\nLast setup results: ");

    _thrusters.Clear();
    _textPanels.Clear();
    _shipControllers.Clear();

    /*
     * I'm leveraging GTS's collection function to populate all my blocks in one loop
     * to reduce iterations.
     */
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectBlocks);

    _thrustCalculator.ThrusterList = _thrusters;

    bool setup = true;
    if (_shipControllers.Count == 0)
    {
        _setupOutput.AppendLine("> Error: No ship controllers found on grid");
        setup = false;
    }
    else
    {
        _reference = _shipControllers[0];
        _shipMass = _reference.CalculateShipMass().PhysicalMass;
    }

    if (_thrusters.Count == 0)
    {
        _setupOutput.AppendLine("> Error: No thrusters found on grid");
        setup = false;
    }

    if (_textPanels.Count == 0)
    {
        _setupOutput.AppendLine($"> Error: No text panels with name\ntag \"{TEXT_PANEL_NAME_TAG}\" found on grid");
        setup = false;
    }

    if (!setup)
    {
        _setupOutput.AppendLine("> Setup failed!");
    }
    else
    {
        _setupOutput.AppendLine("> Setup successful!");
    }

    scheduler.AddQueuedAction(_setupAction);
}

bool CollectBlocks(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    var thrust = block as IMyThrust;
    if (thrust != null)
    {
        _thrusters.Add(thrust);

        return false;
    }

    var textPanel = block as IMyTextPanel;
    if (textPanel != null && block.CustomName.Contains(TEXT_PANEL_NAME_TAG))
    {
        _textPanels.Add(textPanel);
        return false;
    }

    var controller = block as IMyShipController;
    if (controller != null)
    {
        _shipControllers.Add(controller);
        return false;
    }

    return false;
}

void CalculateStopParameters()
{
    _textOutput.Clear();
    _echoOutput.Clear();
    _echoOutput.AppendLine($"Whip's Stop Distance Calculator\n(Version {VERSION} - {DATE})");
    _echoOutput.AppendLine($"\nNext block refresh in {Math.Max(0, _setupAction.RunInterval - _setupAction.TimeSinceLastRun):n0} second(s).");

    if (_reference == null || _thrusters.Count == 0 || _textPanels.Count == 0)
        return;

    Vector3D worldVelocity = _reference.GetShipVelocities().LinearVelocity;
    Vector3D localVelocity = Vector3D.TransformNormal(worldVelocity, MatrixD.Transpose(_reference.WorldMatrix));
    Vector3D thrustSumVector = _thrustCalculator.GetThrustInDirection(worldVelocity);
    thrustSumVector = Vector3D.Rotate(thrustSumVector, MatrixD.Transpose(_reference.WorldMatrix));

    /*
     * This vector sum needs to be along orthagonal axes (subgrids will botch this). Will need to check
     * against controller reference directions. Maybe a TransformNormal?
     */
    Vector3D displacementVector = Vector3D.Zero;
    double maxTimeToStop = 0;
    for (int i = 0; i < 3; ++i)
    {
        double thrustSum = thrustSumVector.GetDim(i);
        Vector3D direction = _baseDirection[i] * Math.Sign(thrustSum);
        thrustSum = Math.Abs(thrustSum);

        double acceleration = thrustSum / _shipMass;
        double relevantSpeed = Math.Abs(localVelocity.GetDim(i));
        double timeToStop = acceleration == 0 ? 0 : relevantSpeed / acceleration;
        double distToStop = relevantSpeed * timeToStop - 0.5 * acceleration * timeToStop * timeToStop;

        if (timeToStop > maxTimeToStop)
        {
            maxTimeToStop = timeToStop;
        }

        displacementVector += direction * distToStop;
    }

    _textOutput.AppendLine($" Stop dist: {PrefixMetricUnits(displacementVector.Length(), "m", 2)}");
    _textOutput.Append($" Stop time: {maxTimeToStop:n1} s");

    WriteText(_textOutput);
}

void WriteText(StringBuilder output)
{
    foreach (IMyTextPanel textPanel in _textPanels)
    {
        textPanel.WriteText(output);

        if (textPanel.ContentType != ContentType.TEXT_AND_IMAGE)
            textPanel.ContentType = ContentType.TEXT_AND_IMAGE;

        if (!textPanel.Font.Equals("Monospace"))
            textPanel.Font = "Monospace";
    }
}

string[] prefixes = new string[]
{
"Y",
"Z",
"E",
"P",
"T",
"G",
"M",
"k",
};

double[] exponents = new double[]
{
1e24,
1e21,
1e18,
1e15,
1e12,
1e9,
1e6,
1e3,
};

string PrefixMetricUnits(double num, string unit, int digits)
{
    string prefix = "";

    for (int i = 0; i < exponents.Length; ++i)
    {
        double thisExponent = exponents[i];

        if (num >= thisExponent)
        {
            prefix = prefixes[i];
            num /= thisExponent;
            break;
        }
    }

    return (prefix == "" ? num.ToString("n0") : num.ToString($"n{digits}")) + $" {prefix}{unit}";
}

#region Thruster Direction Container
public class ThrustDirectionCalculator
{
    #region Fields
    public List<IMyThrust> ThrusterList
    {
        private get
        {
            return _thrusterList;
        }

        set
        {
            _thrusterList = value;
            UpdateThrustDirectionSums();
        }
    }
    List<IMyThrust> _thrusterList = null;


    public IMyCubeGrid ReferenceGrid
    {
        get
        {
            return _referenceGrid;
        }

        set
        {
            if (value != _referenceGrid)
            {
                _referenceGrid = value;
                UpdateThrustDirectionSums();
            }
        }
    }
    IMyCubeGrid _referenceGrid = null;

    Vector3D[] _directions = new Vector3D[6]
    {
Vector3D.Right,
Vector3D.Left,
Vector3D.Up,
Vector3D.Down,
Vector3D.Backward,
Vector3D.Forward,
    };

    /// <summary>
    /// Index map:
    /// 0: right    +X
    /// 1: left     -X
    /// 2: up       +Y
    /// 3: down     -Y
    /// 4: back     +Z
    /// 5: forward  -Z
    /// </summary>
    public double[] _thrustSums = new double[6];
    #endregion

    public ThrustDirectionCalculator(IMyCubeGrid referenceGrid, List<IMyThrust> thrusters)
    {
        ThrusterList = thrusters;
        ReferenceGrid = referenceGrid;
    }

    void UpdateThrustDirectionSums()
    {
        if (_referenceGrid == null)
            return;

        for (int i = 0; i < _thrustSums.Length; ++i)
        {
            _thrustSums[i] = 0;
        }

        MatrixD transposedWm = MatrixD.Transpose(ReferenceGrid.WorldMatrix);
        foreach (var thrust in ThrusterList)
        {
            Vector3D dirn = Vector3D.Rotate(thrust.WorldMatrix.Forward * thrust.MaxEffectiveThrust, transposedWm);
            if (dirn.X >= 0)
                _thrustSums[0] += dirn.X;
            else
                _thrustSums[1] -= dirn.X;

            if (dirn.Y >= 0)
                _thrustSums[2] += dirn.Y;
            else
                _thrustSums[3] -= dirn.Y;

            if (dirn.Z >= 0)
                _thrustSums[4] += dirn.Z;
            else
                _thrustSums[5] -= dirn.Z;
        }
    }

    public Vector3D GetThrustInDirection(Vector3D worldDirection)
    {
        MatrixD transposedWm = MatrixD.Transpose(ReferenceGrid.WorldMatrix);
        Vector3D localDirection = Vector3D.Rotate(worldDirection, transposedWm);
        Vector3D thrustSum = Vector3D.Zero;

        for (int i = 0; i < 6; ++i)
        {
            var thrustDirn = _directions[i] * _thrustSums[i];
            double dot = 0;
            Vector3D.Dot(ref thrustDirn, ref localDirection, out dot);
            if (dot > 0)
                thrustSum += thrustDirn;
        }

        return Vector3D.Rotate(thrustSum, _referenceGrid.WorldMatrix);
    }

}
#endregion

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    const double runtimeToRealtime = 1.0;
    private readonly Program _program;
    private ScheduledAction _currentlyQueuedAction = null;

    /// <summary>
    /// Constructs a scheduler object with timing based on the runtime of the input program.
    /// </summary>
    /// <param name="program"></param>
    public Scheduler(Program program)
    {
        _program = program;
    }

    /// <summary>
    /// Updates all ScheduledAcions in the schedule and the queue.
    /// </summary>
    public void Update()
    {
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * runtimeToRealtime);

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
                // If we should recycle, add it to the end of the queue
                if (!_currentlyQueuedAction.DisposeAfterRun)
                    _queuedActions.Enqueue(_currentlyQueuedAction);

                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="updateFrequency"></param>
    /// <param name="disposeAfterRun"></param>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun);
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    /// <param name="scheduledAction"></param>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary>
    /// Adds an Action to the queue. Queue is FIFO.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="updateInterval"></param>
    /// <param name="disposeAfterRun"></param>
    public void AddQueuedAction(Action action, double updateInterval, bool disposeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, disposeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    /// <param name="scheduledAction"></param>
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

    private readonly double _runFrequency;
    private readonly Action _action;
    protected bool _justRun = false;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false)
    {
        _action = action;
        _runFrequency = runFrequency;
        RunInterval = 1.0 / _runFrequency;
        DisposeAfterRun = removeAfterRun;
    }

    public virtual void Update(double deltaTime)
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
#endregion
