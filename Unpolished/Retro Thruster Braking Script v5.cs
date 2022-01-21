/*
 * / //// / Whip's Retro Thruster Braking Script v5 - 01.02.2019 / //// /
*/
const double speedDisableThreshold = 5; // meters/second
const double brakingAngleTolerance = 10; // degrees

const double rotationConstant = 0.05;


//---------------------------------------------
//No touch below here!
//---------------------------------------------

const double proportionalConstant = 10;
const double integralConstant = 0;
const double derivativeConstant = 4;

const double updatesPerSecond = 10;
const double rad2deg = 180 / Math.PI;
bool shouldBrake = false;
IMyShipController reference = null;

List<IMyGyro> gyros = new List<IMyGyro>();
List<IMyThrust> mainThrust = new List<IMyThrust>();
List<IMyThrust> allThrust = new List<IMyThrust>();
List<IMyShipController> referenceList = new List<IMyShipController>();

Scheduler scheduler;

PID yawPid = new PID(proportionalConstant, integralConstant, derivativeConstant, 0.1, 1.0 / updatesPerSecond);
PID pitchPid = new PID(proportionalConstant, integralConstant, derivativeConstant, 0.1, 1.0 / updatesPerSecond);

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    scheduler = new Scheduler(this);
    
    scheduler.AddScheduledAction(MainProcess, updatesPerSecond);
    scheduler.AddScheduledAction(GetBlocks, 0.1);
    
    GetBlocks();  
}
    
    

void Main(string arg, UpdateType updateSource)
{
    // Argument Handling
    switch (arg.ToLower())
    {
        case "on":
            shouldBrake = true;
            break;

        case "off":
            shouldBrake = false;
            break;

        case "toggle":
            if (shouldBrake)
                shouldBrake = false;
            else
                shouldBrake = true;
            break;

        default:
            break;
    }

    scheduler.Update();
}

void MainProcess()
{
    Echo("WMI Retro Braking\nSystem Online... " + RunningSymbol());
    
    Echo($"Braking Status: {shouldBrake.ToString()}");
    
    if (referenceList.Count == 0) 
    { 
        Echo($"No ship controller was found");
        return; 
    }
    
    if (allThrust.Count == 0)
    {
        Echo("No thrusters found");
        return;
    }
    
    if (gyros.Count == 0)
    {
        Echo("No gyros found");
        return;
    }
    
    reference = GetControlledShipController(referenceList);
    GetThrusters(reference);
    
    if (shouldBrake)
    {
        StartBraking(reference);
    }
    else
    {
        StopBraking();
    }
}

void GetBlocks()
{
    GridTerminalSystem.GetBlocksOfType(referenceList, x => Me.IsSameConstructAs(x));
    GridTerminalSystem.GetBlocksOfType(allThrust, x => Me.IsSameConstructAs(x));
    GridTerminalSystem.GetBlocksOfType(gyros, x => Me.IsSameConstructAs(x)); //gets all gyros on same grid as reference block
}

IMyShipController GetControlledShipController(List<IMyShipController> controllers)
{
    foreach (IMyShipController thisController in controllers)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return controllers[0];
}

//Whip's Running Symbol Method v9
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] {"−", "\\", "|", "/"};

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        runningSymbolVariant = runningSymbolVariant++ % runningSymbols.Length;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}

void GetThrusters(IMyShipController reference)
{   
    mainThrust.Clear();

    foreach (IMyThrust thrust in allThrust)                                                                                                                                                                          ///w.h-i*p
    {
        if (thrust.WorldMatrix.Backward == reference.WorldMatrix.Forward)
        {
            mainThrust.Add(thrust);
        }
    }
}

void StartBraking(IMyShipController reference)
{
    var velocityVec = reference.GetShipVelocities().LinearVelocity; //gets current travel vector
    var speedSquared = velocityVec.LengthSquared();
    
    if (speedSquared < 1)
    {
        shouldBrake = false;
        return;
    }
    
    var forwardVec = reference.WorldMatrix.Forward; //gets backwards vector
    var leftVec = reference.WorldMatrix.Left; //gets Right vector
    var upVec = reference.WorldMatrix.Up; //gets up vector

    double yawAngle = 0, pitchAngle = 0;
    GetRotationAngles(-velocityVec, reference.WorldMatrix, out yawAngle, out pitchAngle);

    //double yawSpeed = proportionalConstant * yawAngle + Math.Abs(Math.Sign(yawAngle)) * derivativeConstant * (yawAngle - lastYawAngle) / timeCurrentCycle;
    //double pitchSpeed = proportionalConstant * pitchAngle + Math.Abs(Math.Sign(pitchAngle)) * derivativeConstant * (pitchAngle - lastPitchAngle) / timeCurrentCycle;
    
    //double yawSpeed = MathHelper.Clamp(proportionalConstant * yawAngle * timeCurrentCycle, -Math.Abs(yawAngle) * 2, Math.Abs(yawAngle) * 2);
    //double pitchSpeed = MathHelper.Clamp(proportionalConstant * pitchAngle * timeCurrentCycle, -Math.Abs(pitchAngle) * 2, Math.Abs(pitchAngle) * 2);

    //double yawSpeed = yawPid.Control(yawAngle);
    //double pitchSpeed = pitchPid.Control(pitchAngle);
    
    double yawSpeed = yawAngle * updatesPerSecond * rotationConstant;
    double pitchSpeed = pitchAngle * updatesPerSecond * rotationConstant;
    
    // Scales the rotation speed to be constant regardless od number of gyros
    //yawSpeed /= gyros.Count;
    //pitchSpeed /= gyros.Count;

    ApplyGyroOverride(pitchSpeed, yawSpeed, 0, gyros, reference);

    double brakingAngle = VectorMath.AngleBetween(forwardVec, -velocityVec);

    if (brakingAngle * rad2deg <= brakingAngleTolerance)
    {
        if (!reference.DampenersOverride)
            reference.DampenersOverride = true;
    }
    else
    {
        if (reference.DampenersOverride)
            reference.DampenersOverride = false;
    }
}

void StopBraking()
{
    var gyros = new List<IMyGyro>();
    GridTerminalSystem.GetBlocksOfType(gyros); //messy fix later
    
    foreach (IMyGyro thisGyro in gyros)
    {
        thisGyro.SetValue("Override", false);
    }
}

void ApplyThrustOverride(List<IMyThrust> thrusterList, float thrustOverride = 0)
{
    foreach (IMyThrust thisThrust in thrusterList)
    {
        thisThrust.SetValueFloat("Override", thrustOverride);
    }
}

//Whip's ApplyGyroOverride Method v10 - 8/19/17
void ApplyGyroOverride(double pitch_speed, double yaw_speed, double roll_speed, List<IMyGyro> gyro_list, IMyTerminalBlock reference) 
{ 
    var rotationVec = new Vector3D(-pitch_speed, yaw_speed, roll_speed); //because keen does some weird stuff with signs 
    var shipMatrix = reference.WorldMatrix;
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, shipMatrix); 

    foreach (var thisGyro in gyro_list) 
    { 
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(thisGyro.WorldMatrix)); 
 
        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y; 
        thisGyro.Roll = (float)transformedRotationVec.Z; 
        thisGyro.GyroOverride = true; 
    } 
}

/*
/// Whip's Get Rotation Angles Method v14 - 9/25/18 ///
Dependencies: VectorMath
* Fix to solve for zero cases when a vertical target vector is input
* Fixed straight up case
* Fixed sign on straight up case
* Converted math to local space
*/
void GetRotationAngles(Vector3D targetVector, MatrixD worldMatrix, out double yaw, out double pitch)
{
    var localTargetVector = Vector3D.TransformNormal(targetVector, MatrixD.Transpose(worldMatrix));
    var flattenedTargetVector = new Vector3D(0, localTargetVector.Y, localTargetVector.Z);
    
    pitch = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
    
    if (Math.Abs(pitch) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
        pitch = Math.PI;
    
    if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
        yaw = MathHelper.PiOver2 * Math.Sign(localTargetVector.X);
    else
        yaw = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.X); //right is positive
}

public static class VectorMath
{
    /// <summary>
    ///  Normalizes a vector only if it is non-zero and non-unit
    /// </summary>
    public static Vector3D SafeNormalize(Vector3D a)
    {
        if (Vector3D.IsZero(a))
            return Vector3D.Zero;

        if (Vector3D.IsUnit(ref a))
            return a;

        return Vector3D.Normalize(a);
    }

    /// <summary>
    /// Reflects vector a over vector b with an optional rejection factor
    /// </summary>
    public static Vector3D Reflection(Vector3D a, Vector3D b, double rejectionFactor = 1) //reflect a over b
    {
        Vector3D project_a = Projection(a, b);
        Vector3D reject_a = a - project_a;
        return project_a - reject_a * rejectionFactor;
    }

    /// <summary>
    /// Rejects vector a on vector b
    /// </summary>
    public static Vector3D Rejection(Vector3D a, Vector3D b) //reject a on b
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    /// <summary>
    /// Projects vector a onto vector b
    /// </summary>
    public static Vector3D Projection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a.Dot(b) / b.LengthSquared() * b;
    }

    /// <summary>
    /// Scalar projection of a onto b
    /// </summary>
    public static double ScalarProjection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;

        if (Vector3D.IsUnit(ref b))
            return a.Dot(b);

        return a.Dot(b) / b.Length();
    }

    /// <summary>
    /// Computes angle between 2 vectors
    /// </summary>
    public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }

    /// <summary>
    /// Computes cosine of the angle between 2 vectors
    /// </summary>
    public static double CosBetween(Vector3D a, Vector3D b, bool useSmallestAngle = false) //returns radians
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
    }

    /// <summary>
    /// Returns if the normalized dot product between two vectors is greater than the tolerance.
    /// This is helpful for determining if two vectors are "more parallel" than the tolerance.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
    {
        double dot = Vector3D.Dot(a, b);
        double num = a.LengthSquared() * b.LengthSquared() * tolerance * tolerance;
        return dot * dot > num;
    }
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
    const double runtimeToRealtime = 1.0 / 0.96;
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

//Whip's PID controller class v6 - 11/22/17
public class PID
{
    double _kP = 0;
    double _kI = 0;
    double _kD = 0;
    double _integralDecayRatio = 0;
    double _lowerBound = 0;
    double _upperBound = 0;
    double _timeStep = 0;
    double _inverseTimeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;
    bool _integralDecay = false;
    public double Value { get; private set; }

    public PID(double kP, double kI, double kD, double lowerBound, double upperBound, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        _integralDecay = false;
    }

    public PID(double kP, double kI, double kD, double integralDecayRatio, double timeStep)
    {
        _kP = kP;
        _kI = kI;
        _kD = kD;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        _integralDecayRatio = integralDecayRatio;
        _integralDecay = true;
    }

    public double Control(double error)
    {
        //Compute derivative term
        var errorDerivative = (error - _lastError) * _inverseTimeStep;

        if (_firstRun)
        {
            errorDerivative = 0;
            _firstRun = false;
        }

        //Compute integral term
        if (!_integralDecay)
        {
            _errorSum += error * _timeStep;

            //Clamp integral term
            if (_errorSum > _upperBound)
                _errorSum = _upperBound;
            else if (_errorSum < _lowerBound)
                _errorSum = _lowerBound;
        }
        else
        {
            _errorSum = _errorSum * (1.0 - _integralDecayRatio) + error * _timeStep;
        }

        //Store this error as last error
        _lastError = error;

        //Construct output
        this.Value = _kP * error + _kI * _errorSum + _kD * errorDerivative;
        return this.Value;
    }
    
    public double Control(double error, double timeStep)
    {
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
        return Control(error);
    }

    public void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}