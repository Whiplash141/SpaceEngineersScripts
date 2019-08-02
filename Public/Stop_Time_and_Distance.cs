#region In-game Script
/*
/ //// / Whip's Stop Time and Distance Script / //// /
*/

//-------------------------------------------------------------------------
//============ NO TOUCH BELOW HERE!!! =====================================
//-------------------------------------------------------------------------

#region Fields
const string DATE = "08/01/2019";
const string VERSION = "7.2.2";
const string TEXT_PANEL_NAME_TAG = "Stop Distance";

readonly Scheduler scheduler;
readonly StringBuilder _echoOutput = new StringBuilder();
readonly StringBuilder _setupOutput = new StringBuilder();
readonly StringBuilder _textOutput = new StringBuilder();
readonly List<IMyThrust> _thrusters = new List<IMyThrust>();
readonly List<IMyShipController> _shipControllers = new List<IMyShipController>();
readonly List<IMyTextPanel> _textPanels = new List<IMyTextPanel>();
readonly ScheduledAction _setupAction;
readonly Vector3D[] _baseDirection = new Vector3D[3]
{
    Vector3D.Right,
    Vector3D.Up,
    Vector3D.Backward,
};

IMyShipController _reference = null;
double _shipMass = 0;
#endregion

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;

    scheduler = new Scheduler(this);
    scheduler.AddScheduledAction(CalculateStopParameters, 10);
    scheduler.AddScheduledAction(PrintEchos, 1);
    _setupAction = new ScheduledAction(GrabBlocks, 0.1);
    scheduler.AddScheduledAction(_setupAction);
    
    GrabBlocks();
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

void GrabBlocks()
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
    _echoOutput.AppendLine($"Whip's Stop Time and Distance\nCalculator\n(Version {VERSION} - {DATE})");
    _echoOutput.AppendLine($"\nNext block refresh in {Math.Max(0, _setupAction.RunInterval - _setupAction.TimeSinceLastRun):n0} second(s).");

    if (_reference == null || _thrusters.Count == 0 || _textPanels.Count == 0)
        return;

    Vector3D worldVelocity = _reference.GetShipVelocities().LinearVelocity;
    Vector3D localVelocity = Vector3D.TransformNormal(worldVelocity, MatrixD.Transpose(_reference.WorldMatrix));
    Vector3D worldWeight = _reference.CalculateShipMass().PhysicalMass * _reference.GetNaturalGravity();
    Vector3D localWeight = Vector3D.TransformNormal(worldWeight, MatrixD.Transpose(_reference.WorldMatrix));
    Vector3D thrustSumVector = Vector3D.Zero;
    foreach (IMyThrust thrust in _thrusters)
    {
        double thisThrust = thrust.IsWorking ? thrust.MaxEffectiveThrust : 0;

        if (Vector3D.Dot(worldVelocity, thrust.WorldMatrix.Forward) > 0)
        {
            Vector3D thrustDirection = thrust.WorldMatrix.Forward * thisThrust;
            Vector3D localThrustDirection = Vector3D.TransformNormal(thrustDirection, MatrixD.Transpose(_reference.WorldMatrix)); //TODO: Optimize this

            /*
             * We need to verify that we only add the thrust components if they are the same direction as the
             * local velocity. To do this, we check the sign of the element-wise multiplication of the thrust
             * direction and the velocity direction.
             * NOTE: This gives us a low estimate, dont want this.
             */
            thrustSumVector += localThrustDirection;
        }
    }
    
    /*
     * We need to account for reduced thrust potential due to gravity
     */
    thrustSumVector -= localWeight;

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

    foreach (IMyTextPanel textPanel in _textPanels)
    {
        textPanel.WritePublicText(_textOutput);

        if (!textPanel.ShowText)
            textPanel.ShowPublicTextOnScreen();

        if (!textPanel.Font.Equals("Monospace"))
            textPanel.Font = "Monospace";
    }
}

string PrefixMetricUnits(double num, string unit, int digits)
{
    string prefix = "";

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

    for (int i = 0; i < exponents.Length; ++i)
    {
        double thisExponent = exponents[i];

        if (num > thisExponent)
        {
            prefix = prefixes[i];
            num /= thisExponent;
            break;
        }
    }

    return (prefix == "" ? num.ToString("n0") : num.ToString($"n{digits}")) + $" {prefix}{unit}";
}

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
