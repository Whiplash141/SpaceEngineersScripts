
#region In-game Script

/*
/ //// / Whip's Skid Steering / //// /

INSTRUCTIONS
    1. Place all wheels, gyros, and cockpits that you want
        to use for skid steering in a group named
        "Skid steering".
    2. Configure the custom data to your heart's content.
    
NOTES
    - Adaptive turn friction will automatically change the
        friction of wheels to make skid steering possible
        without the use of gyroscopes.
    - Vehicles will turn better with gyroscopes, but they are
        not required.
*/

#region Fields
bool _invertSteerWhenReversing = false;
string _groupName = "Skid Steering";
float _turnGyroPower = 100f;
float _driveGyroPower = 10f;
float _gyroRotationSpeedRpm = 1f;
float _driveFriction = 50f;
float _turnFriction = 50f;
float _driveFrictionRampTime = 2f;
float _turnFrictionRampTime = 0f;
float _adaptiveFrictionAngleDeg = 45f;

bool _hasGroup = false;
bool _adaptiveFriction = true;

float _driveToTurnFrictionRatio = 0;
float _adaptiveFrictionCos = 0;

const float UpdateInterval = 1.0f / 6.0f;

List<IMyShipController> _controllers = new List<IMyShipController>();
List<IMyGyro> _gyros = new List<IMyGyro>();
List<IMyMotorSuspension> _allWheels = new List<IMyMotorSuspension>();

Scheduler _scheduler;
ScheduledAction _scheduledSetup;
IMyShipController _lastController = null;

StringBuilder _echo = new StringBuilder(512);
MyIni _ini = new MyIni();
SkidSteeringScreenManager _screenManager;
RuntimeTracker _runtimeTracker;
bool _drawTitleScreen = true;

const string
    ScriptName = "WMI Skid Steering",
    Version = "1.4.0",
    Date = "2021/10/12",
    IniSection = "Skid Steering",
    IniKeyGroupName = "Group name tag",
    IniKeyDrawTitleScreen = "Draw title screen",
    IniKeyTurnWheelFriction = "Wheel friction - Turning (%)",
    IniKeyDriveWheelFriction = "Wheel friction - Driving (%)",
    IniKeyTurnTime = "Friction ramp-up time - Driving to Turning (s)",
    IniKeyDriveTime = "Friction ramp-up time - Turning to Driving (s)",
    IniKeyTurnGyroPower = "Gyro power - Turning (%)",
    IniKeyDrivePower = "Gyro power - Driving (%)",
    IniKeyGyroRotationSpeed = "Gyro rotation speed (RPM)",
    IniKeyInvertSteerWhenReversing = "Invert steer while reversing",
    IniKeyAdaptiveFriction = "Enable adaptive steering friction",
    IniKeyAdaptiveFrictionMaxAngle = "Adaptive steering friction max angle (deg)";

#endregion

#region Entrypoints
Program()
{
    _runtimeTracker = new RuntimeTracker(this);
    
    _screenManager = new SkidSteeringScreenManager(Version, this);

    _scheduledSetup = new ScheduledAction(Setup, 0.1);

    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(SkidSteer, 6);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(_scheduledSetup);
    _scheduler.AddScheduledAction(DrawTitleScreen, 6);
    _scheduler.AddScheduledAction(_screenManager.RestartDraw, 1);

    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    Setup();
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void DrawTitleScreen()
{
    if (_drawTitleScreen)
    {
        _screenManager.Draw();
    }
}
#endregion

#region Skid Steering Logic
void SkidSteer()
{
    var controller = GetControlledShipController();
    if (controller == null)
    {
        return;
    }
    _lastController = controller;

    Vector3 moveVector = controller.MoveIndicator;
    Vector3D velocityWorld = controller.GetShipVelocities().LinearVelocity;
    float forwardSpeed = (float)Vector3D.Dot(controller.WorldMatrix.Forward, velocityWorld);
    bool isTurning = Math.Abs(moveVector.X) > 1e-3;
    float leftPropulsion = 0f;
    float rightPropulsion = 0f;
    float gyroTurnDirection = 0f;

    // +left -> forward left
    // -right -> forward right
    // +gyro -> turn right
    if (!isTurning) // No turning
    {
        leftPropulsion = -Math.Sign(moveVector.Z); 
        rightPropulsion = Math.Sign(moveVector.Z);
        gyroTurnDirection = 0f;
    }
    else
    {
        float speedScale = Math.Abs(moveVector.Z) > 1e-3 ? 0.1f : 1f;
        if (moveVector.Z < 0) // moving forward
        {
            if (moveVector.X > 0) // Turn right: left full forward, right fraction backwards
            {
                leftPropulsion = 1f;
                rightPropulsion = speedScale;
                gyroTurnDirection = 0.5f;
            }
            else // Turn left: right full forward, left fraction backwards
            {
                leftPropulsion = -speedScale;
                rightPropulsion = -1f;
                gyroTurnDirection = -0.5f;
            }
        }
        else if (moveVector.Z > 0) // moving backwards
        {
            if (!_invertSteerWhenReversing) // No steer inversion
            {
                if (moveVector.X > 0) // Turn left: right full backwards, left fraction forwards
                {
                    leftPropulsion = speedScale;
                    rightPropulsion = 1f;
                    gyroTurnDirection = 0.5f;
                }
                else // Turn right: left full backwards, right fraction forwards
                {
                    leftPropulsion = -1f;
                    rightPropulsion = -speedScale;
                    gyroTurnDirection = -0.5f;
                }                
            }
            else // Steer inversion
            {
                if (moveVector.X > 0) // Turn right: left full backwards, right fraction forwards
                {
                    leftPropulsion = -1f;
                    rightPropulsion = -speedScale;
                    gyroTurnDirection = -0.5f;
                }
                else // Turn left: right full backwards, left fraction forwards
                {
                    leftPropulsion = speedScale;
                    rightPropulsion = 1f;
                    gyroTurnDirection = 0.5f;
                }
            }
        }
        else // Spin in place
        {
            if (moveVector.X > 0) // Turn right: left full forward, right full backwards
            {
                leftPropulsion = 1f;
                rightPropulsion = 1f;
                gyroTurnDirection = 1f;
            }
            else // Turn left: right full forward, left full backwards
            {
                leftPropulsion = -1;
                rightPropulsion = -1f;
                gyroTurnDirection = -1f;
            }
        }
   }

    float rampTime = isTurning ? _turnFrictionRampTime : _driveFrictionRampTime;
    float rampSign = isTurning ? 1 : -1;
    if (rampTime < 1e-3)
    {
        rampTime = UpdateInterval;
    }
    _driveToTurnFrictionRatio += rampSign * UpdateInterval / rampTime;
    _driveToTurnFrictionRatio = MathHelper.Clamp(_driveToTurnFrictionRatio, 0, 1);

    float gyroPower = isTurning ? _turnGyroPower : _driveGyroPower;
    Vector3D avgWheelPos = GetAverageWheelPosition(_allWheels);

    SetWheelPropulsionAndFriction(
        controller,
        avgWheelPos,
        _allWheels,
        leftPropulsion,
        rightPropulsion,
        _turnFriction,
        _driveFriction,
        _adaptiveFriction,
        isTurning);

    ApplyGyroOverride(0, isTurning ? _gyroRotationSpeedRpm * gyroTurnDirection : 0, 0, gyroPower, _gyros, controller.WorldMatrix);
}
#endregion


#region Utility Functions
void SetWheelPropulsionAndFriction(
    IMyShipController reference,
    Vector3D referencePos,
    List<IMyMotorSuspension> wheels,
    float leftPropulsion,
    float rightPropulsion,
    float turnFriction,
    float driveFriction,
    bool dynamicFriction,
    bool isTurning)
{
    foreach (var w in wheels)
    {
        Vector3D diff = w.GetPosition() - referencePos;
        Vector3D left = reference.WorldMatrix.Left;
        Vector3D right = reference.WorldMatrix.Right;

        // Determine what propulsion we should use
        bool isLeft = Vector3D.Dot(diff, left) > 0;

        // Determine if the wheel is pointing outwards (+) or inwards (-) and compensate
        float sign = Math.Sign(Vector3D.Dot(w.WorldMatrix.Up, isLeft ? left : right));

        // Set propulsion
        float propulsion = isLeft ? leftPropulsion : rightPropulsion;
        w.PropulsionOverride = sign * propulsion * w.Power * 0.01f;

        // Set steering
        float scaledTurnFriction;
        if (dynamicFriction)
        {
            /*
             * For dynamic friction, the wheels closest to the center of the tank
             * will get the most friction. The further away you get from the 
             * center wheels, the less friction is applied. This drastically
             * reduces binding and allows tanks to skid steer *without gyros*!
             */
            float dot = (float)Math.Abs(Vector3D.Dot(Vector3D.Normalize(diff), w.WorldMatrix.Up));
            if (dot < _adaptiveFrictionCos)
            {
                dot = 0; // too far off axis, so don't give this wheel any friction.
            }
            scaledTurnFriction = dot * turnFriction;
        }
        else
        {
            scaledTurnFriction = turnFriction;
        }
        w.Friction = driveFriction + (scaledTurnFriction - driveFriction) * _driveToTurnFrictionRatio;
        w.Steering = false;
        w.InvertPropulsion = false;
    }
}

Vector3D GetAverageWheelPosition(List<IMyMotorSuspension> wheels)
{
    Vector3D sum = Vector3D.Zero;
    int count = 0;
    foreach (var w in wheels)
    {
        if (w.IsAttached)
        {
            sum += w.GetPosition();
            count++;
        }
    }

    if (count == 0)
    {
        return Vector3D.Zero;
    }

    return sum / count;
}

IMyShipController GetControlledShipController()
{
    if (_controllers.Count == 0)
        return null;

    if (_lastController != null && _lastController.IsUnderControl)
    {
        return _lastController;
    }

    IMyShipController mainController = null;
    IMyShipController controlled = null;

    foreach (IMyShipController thisController in _controllers)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
        {
            if (controlled == null)
            {
                controlled = thisController;
            }

            if (thisController.IsMainCockpit)
            {
                mainController = thisController; // Only one per grid so no null check needed
            }
        }
    }

    if (mainController != null)
    {
        return mainController;
    }

    if (controlled != null)
    {
        return controlled;
    }

    return _controllers[0];
}

/*
Whip's ApplyGyroOverride - Last modified: 2020/08/27

Takes pitch, yaw, and roll speeds relative to the gyro's backwards
ass rotation axes. 
*/
void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, float power, List<IMyGyro> gyroList, MatrixD worldMatrix)
{
    var rotationVec = new Vector3D(pitchSpeed, yawSpeed, rollSpeed);
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, worldMatrix);

    foreach (var g in gyroList)
    {
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(g.WorldMatrix));

        g.Pitch = (float)transformedRotationVec.X;
        g.Yaw = (float)transformedRotationVec.Y;
        g.Roll = (float)transformedRotationVec.Z;
        g.GyroOverride = true;
        g.GyroPower = power * 0.01f;
    }
}
#endregion

#region Ini Processing
void ProcessIni()
{
    _ini.Clear();

    if (_ini.TryParse(Me.CustomData))
    {
        _groupName = _ini.Get(IniSection, IniKeyGroupName).ToString(_groupName);
        _drawTitleScreen = _ini.Get(IniSection, IniKeyDrawTitleScreen).ToBoolean(_drawTitleScreen);
        _driveFriction = _ini.Get(IniSection, IniKeyDriveWheelFriction).ToSingle(_driveFriction);
        _turnFriction = _ini.Get(IniSection, IniKeyTurnWheelFriction).ToSingle(_turnFriction);
        _driveFrictionRampTime = _ini.Get(IniSection, IniKeyDriveTime).ToSingle(_driveFrictionRampTime);
        _turnFrictionRampTime = _ini.Get(IniSection, IniKeyTurnTime).ToSingle(_turnFrictionRampTime);
        _driveGyroPower = _ini.Get(IniSection, IniKeyDrivePower).ToSingle(_driveGyroPower);
        _turnGyroPower = _ini.Get(IniSection, IniKeyTurnGyroPower).ToSingle(_turnGyroPower);
        _gyroRotationSpeedRpm = _ini.Get(IniSection, IniKeyGyroRotationSpeed).ToSingle(_gyroRotationSpeedRpm);
        _invertSteerWhenReversing = _ini.Get(IniSection, IniKeyInvertSteerWhenReversing).ToBoolean(_invertSteerWhenReversing);
        _adaptiveFriction = _ini.Get(IniSection, IniKeyAdaptiveFriction).ToBoolean(_adaptiveFriction);

        float lastAngle = _adaptiveFrictionAngleDeg;
        float newAngle = _ini.Get(IniSection, IniKeyAdaptiveFrictionMaxAngle).ToSingle(_adaptiveFrictionAngleDeg);
        _adaptiveFrictionAngleDeg = MathHelper.Clamp(newAngle, 0, 90);
        if (Math.Abs(_adaptiveFrictionAngleDeg - lastAngle) < 1e-3)
        {
            _adaptiveFrictionCos = (float)Math.Cos(MathHelper.ToRadians(_adaptiveFrictionAngleDeg));
        }
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }
    _ini.Set(IniSection, IniKeyGroupName, _groupName);
    _ini.Set(IniSection, IniKeyDrawTitleScreen, _drawTitleScreen);
    _ini.Set(IniSection, IniKeyDriveWheelFriction, MathHelper.Clamp(_driveFriction, 0, 100));
    _ini.Set(IniSection, IniKeyTurnWheelFriction, MathHelper.Clamp(_turnFriction, 0, 100));
    _ini.Set(IniSection, IniKeyDriveTime, Math.Max(0, _driveFrictionRampTime));
    _ini.Set(IniSection, IniKeyTurnTime, Math.Max(0, _turnFrictionRampTime));
    _ini.Set(IniSection, IniKeyDrivePower, MathHelper.Clamp(_driveGyroPower, 0, 100));
    _ini.Set(IniSection, IniKeyTurnGyroPower, MathHelper.Clamp(_turnGyroPower, 0, 100));
    _ini.Set(IniSection, IniKeyGyroRotationSpeed, _gyroRotationSpeedRpm);
    _ini.Set(IniSection, IniKeyInvertSteerWhenReversing, _invertSteerWhenReversing);
    _ini.Set(IniSection, IniKeyAdaptiveFriction, _adaptiveFriction);
    _ini.Set(IniSection, IniKeyAdaptiveFrictionMaxAngle, _adaptiveFrictionAngleDeg);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}
#endregion

#region Detailed Info Printing
void PrintDetailedInfo()
{
    Echo($"{ScriptName}\n(v{Version} - {Date})\n");
    Echo($"Next refresh in {Math.Max(0, _scheduledSetup.RunInterval - _scheduledSetup.TimeSinceLastRun):n0} second(s)\n");
    if (!_hasGroup)
    {
        Echo($"No group named '{_groupName}'\n    found!\n");
    }
    else
    {
        Echo($"Controller: {(_lastController == null ? "(none)" : _lastController.CustomName)}");
        Echo($"Wheel count: {_allWheels.Count}");
        Echo($"Gyro count: {_gyros.Count}\n");
    }
    Echo(_runtimeTracker.Write());
    WriteEcho();
}

new void Echo(string text)
{
    _echo.Append(text).Append("\n");
}

void WriteEcho()
{
    string output = _echo.ToString();
    base.Echo(output);
    _echo.Clear();
}

#endregion

#region Setup
void Setup()
{
    _controllers.Clear();
    _gyros.Clear();
    _allWheels.Clear();

    ProcessIni();

    var g = GridTerminalSystem.GetBlockGroupWithName(_groupName);
    if (g != null)
    {
        g.GetBlocks(null, CollectBlocks);
        _hasGroup = true;
    }
    else
    {
        _hasGroup = false;
    }
}

bool CollectBlocks(IMyTerminalBlock t)
{
    if (!GridTerminalSystem.CanAccess(t, MyTerminalAccessScope.Construct))
        return false;

    AddToListIfType(t, _controllers);
    AddToListIfType(t, _allWheels);
    AddToListIfType(t, _gyros);
    return false;
}

bool AddToListIfType<T>(IMyTerminalBlock block, List<T> list) where T : class, IMyTerminalBlock
{
    T typedBlock;
    return AddToListIfType(block, list, out typedBlock);
}

bool AddToListIfType<T>(IMyTerminalBlock block, List<T> list, out T typedBlock) where T : class, IMyTerminalBlock
{
    typedBlock = block as T;
    if (typedBlock != null)
    {
        list.Add(typedBlock);
        return true;
    }
    return false;
}
#endregion

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    public double CurrentTimeSinceLastRun = 0;

    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
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
        _inUpdate = true;
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);

        if (_firstRun)
        {
            if (_ignoreFirstRun)
            {
                deltaTime = 0;
            }
            _firstRun = false;
        }

        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTime;
            action.Update(deltaTime);
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
            _currentlyQueuedAction.Update(deltaTime);
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
    public double RunInterval
    {
        get
        {
            return _runInterval;
        }
        set
        {
            if (value == _runInterval)
                return;

            _runInterval = value < Epsilon ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : 1.0 / _runInterval;
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
                RunInterval = double.MaxValue;
            else
                RunInterval = 1.0 / value;
        }
    }

    double _runInterval = -1e9;
    double _runFrequency = -1e9;
    readonly Action _action;

    const double Epsilon = 1e-12;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency = 0, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TimeSinceLastRun = timeOffset;
    }

    public void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun + Epsilon >= RunInterval)
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

class SkidSteeringScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _pressedColor = new Color(100, 100, 100);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;

    const float WasdSpriteScale = 1f;
    const float TankSpriteScale = 1f;

    const string Font = "Debug";
    const string TitleFormat = "Whip's Skid Steering - v{0}";
    readonly string _titleText;

    Program _program;

    int _idx = 0;

    readonly Vector2 _wasdPos = new Vector2(-120, 45);
    readonly Vector2 _tankPos = new Vector2(100, 20);

    enum WasdKey { None, W, A, S, D }

    struct AnimationParams
    {
        public readonly WasdKey PressedKey;
        public readonly float TankRotation;

        public AnimationParams(WasdKey pressedKey, float tankRotation)
        {
            PressedKey = pressedKey;
            TankRotation = tankRotation;
        }
    }

    AnimationParams[] _animSequence = new AnimationParams[] {
new AnimationParams(WasdKey.A, 0f),
new AnimationParams(WasdKey.A, -15f),
new AnimationParams(WasdKey.A, -30f),
new AnimationParams(WasdKey.A, -45f),
new AnimationParams(WasdKey.None, -45f),
new AnimationParams(WasdKey.None, -45f),
new AnimationParams(WasdKey.None, -45f),
new AnimationParams(WasdKey.D, -45f),
new AnimationParams(WasdKey.D, -30f),
new AnimationParams(WasdKey.D, -15f),
new AnimationParams(WasdKey.D, 0f),
new AnimationParams(WasdKey.D, 15f),
new AnimationParams(WasdKey.D, 30f),
new AnimationParams(WasdKey.D, 45f),
new AnimationParams(WasdKey.None, 45f),
new AnimationParams(WasdKey.None, 45f),
new AnimationParams(WasdKey.None, 45f),
new AnimationParams(WasdKey.A, 45f),
new AnimationParams(WasdKey.A, 30f),
new AnimationParams(WasdKey.A, 15f),
};

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public SkidSteeringScreenManager(string version, Program program)
    {
        _titleText = string.Format(TitleFormat, version);
        _program = program;
        _surface = _program.Me.GetSurface(0);
    }

    public void Draw()
    {
        if (_surface == null)
            return;

        AnimationParams anim = _animSequence[_idx];
        _idx = ++_idx % _animSequence.Length;
        float angle = MathHelper.ToRadians(anim.TankRotation);

        SetupDrawSurface(_surface);

        Vector2 screenCenter = _surface.TextureSize * 0.5f;
        Vector2 scale = _surface.SurfaceSize / 512f;
        float minScale = Math.Min(scale.X, scale.Y);

        using (var frame = _surface.DrawFrame())
        {
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }

            DrawWasdIcon(frame, screenCenter + _wasdPos * minScale, anim.PressedKey, minScale);
            DrawTankIcon(frame, screenCenter + _tankPos * minScale, minScale, angle);

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

    void DrawTankIcon(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(100f, 160f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // tank body
        frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", new Vector2(cos * 0f - sin * -74f, sin * 0f + cos * -74f) * scale + centerPos, new Vector2(80f, 8f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // tank frontCopy
        frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", new Vector2(cos * 0f - sin * -85f, sin * 0f + cos * -85f) * scale + centerPos, new Vector2(100f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // tank front
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(80f, 140f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // tank body inside
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 0f - sin * -80f, sin * 0f + cos * -80f) * scale + centerPos, new Vector2(20f, 100f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // barrel shadow
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 0f - sin * -80f, sin * 0f + cos * -80f) * scale + centerPos, new Vector2(10f, 100f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // barrel
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(70f, 70f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // turret
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // turretCopy
    }

    #endregion
}

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

#endregion
