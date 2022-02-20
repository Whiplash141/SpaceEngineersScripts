
/*
 * / //// / TCES | Turret Controller Enhancement Script (by Whiplash141) / //// /
 *
 * Description
 *
 * This is a simple script that enhances the functionality of the
 * Custom Turret Controller (CTC) block with the following features:
 * - Automatic CTC block configuration
 * - Turret rotor rest angles
 * - Support of more than 2 rotors
 */

public const string Version = "1.2.6",
                    Date = "2022/02/19",
                    IniSectionGeneral = "TCES - General",
                    IniKeyGroupName = "Group name tag",
                    IniKeyAzimuthName = "Azimuth rotor name tag",
                    IniKeyElevationName = "Elevation rotor name tag",
                    IniKeyAutoRestAngle = "Should auto return to rest angle",
                    IniKeyAutoRestDelay = "Auto return to rest angle delay (s)",
                    IniKeyDrawTitleScreen = "Draw title screen",
                    IniSectionRotor = "TCES - Rotor",
                    IniKeyRestAngle = "Rest angle (deg)";

RuntimeTracker _runtimeTracker;
long _runCount = 0;
public string GroupName { get; private set; } = "TCES";
public string AzimuthName { get; private set; } = "Azimuth";
public string ElevationName { get; private set; } = "Elevation";
public bool AutomaticRest { get; private set; } = true;
public float AutomaticRestDelay { get; private set; } = 2f;
public bool DrawTitleScreen = true;
TCESTitleScreen _titleScreen;

List<CustomTurretController> _turretControllers = new List<CustomTurretController>();

MyIni _ini = new MyIni();

class CustomTurretController
{
    const float RotorStopThresholdRad = 1f * (MathHelper.Pi / 180f);

    List<IMyFunctionalBlock> _tools = new List<IMyFunctionalBlock>();
    List<IMyFunctionalBlock> _mainTools = new List<IMyFunctionalBlock>();
    List<IMyFunctionalBlock> _otherTools = new List<IMyFunctionalBlock>();

    ITerminalProperty<long>
        _azimuthProperty = null,
        _elevationProperty = null;

    List<IMyMotorStator> _extraRotors = new List<IMyMotorStator>();
    IMyMotorStator _azimuthRotor;
    IMyMotorStator _elevationRotor;
    IMyTurretControlBlock _controller;
    IMyCameraBlock _camera;

    Program _p;
    IMyBlockGroup _group;
    readonly string _groupName;

    Dictionary<IMyCubeGrid, IMyFunctionalBlock> _gridToToolDict = new Dictionary<IMyCubeGrid, IMyFunctionalBlock>();
    Dictionary<IMyMotorStator, float?> _restAngles = new Dictionary<IMyMotorStator, float?>();
    long _updateCount = 0;

    bool _wasActive = false;
    bool _wasShooting = false;
    bool _shouldRest = false;
    MyIni _ini = new MyIni();
    float _idleTime = 0f;

    const float RestSpeed = 10f;

    enum ReturnCode
    {
        None = 0,
        NoExtraRotors = 1,
        MultipleTurretControllers = 1 << 1,
        MissingCamera = 1 << 2,
        MissingAzimuth = 1 << 3,
        MissingElevation = 1 << 4,
        MissingTools = 1 << 5,
        MissingController = 1 << 6,
        MissingRotors = MissingAzimuth | MissingElevation,
        MissingToolAndCamera = MissingTools | MissingCamera,
    }
    ReturnCode _setupReturnCode = ReturnCode.None;

    bool IsActive
    {
        get
        {
            return _controller != null && (_controller.HasTarget || _controller.IsUnderControl);
        }
    }

    public CustomTurretController(Program p, IMyBlockGroup group)
    {
        _p = p;
        _group = group;
        _groupName = group.Name;
        Setup();
        SetBlocks();
    }

    public void GoToRest()
    {
        if (_controller != null && !IsActive)
        {
            _shouldRest = true;
        }
    }

    public void Update()
    {
        _updateCount++;

        if (_updateCount % 30 == 0)
        {
            SetBlocks();
        }

        if (_controller == null)
        {
            return;
        }

        if (IsActive)
        {
            if (_shouldRest)
            {
                if (_azimuthRotor != null)
                {
                    _controller.AzimuthRotor = _azimuthRotor;
                    _azimuthRotor.TargetVelocityRad = 0;
                }
                if (_elevationRotor != null)
                {
                    _controller.ElevationRotor = _elevationRotor;
                    _elevationRotor.TargetVelocityRad = 0;
                }
                _shouldRest = false;
            }
            _idleTime = 0f;
        }
        else
        {
            _idleTime += (1f / 6f);
            if (_wasActive)
            {
                foreach (var r in _extraRotors)
                {
                    r.TargetVelocityRad = 0f;
                }
            }
            if (_p.AutomaticRest && _idleTime >= _p.AutomaticRestDelay)
            {
                _shouldRest = true;
            }
        }

        bool shouldShoot = false;
        if (_shouldRest)
        {
            bool done = HandleAzimuthAndElevationRestAngles();
            foreach (var r in _extraRotors)
            {
                done &= TryMoveRotorToRestAngle(r);
            }
            _shouldRest = !done;
        }
        else if (_extraRotors.Count > 0)
        {
            shouldShoot = HandleExtraRotors();
        }

        if (shouldShoot != _wasShooting)
        {
            foreach (var t in _otherTools)
            {
                var tool = t as IMyShipToolBase;
                if (tool != null)
                {
                    tool.Enabled = shouldShoot;
                }

                var gun = t as IMyUserControllableGun;
                if (gun != null)
                {
                    gun.Shoot = shouldShoot;
                }
            }
        }

        _wasShooting = shouldShoot;
        _wasActive = IsActive;
    }

    #region Setup
    void Setup()
    {
        _setupReturnCode = ReturnCode.None;

        _controller = null;
        _azimuthRotor = null;
        _elevationRotor = null;
        _camera = null;
        _extraRotors.Clear();
        _tools.Clear();
        _gridToToolDict.Clear();
        _restAngles.Clear();

        // TODO: Remove both of these once the setters work properly for unassigning
        _azimuthProperty = null;
        _elevationProperty = null;

        _group.GetBlocks(null, CollectBlocks);

        if (_controller == null)
        {
            _setupReturnCode |= ReturnCode.MissingController;
        }
        if (_azimuthRotor == null)
        {
            _setupReturnCode |= ReturnCode.MissingAzimuth;
        }
        if (_elevationRotor == null)
        {
            _setupReturnCode |= ReturnCode.MissingElevation;
        }
        if (_extraRotors.Count == 0)
        {
            _setupReturnCode |= ReturnCode.NoExtraRotors;
        }
        if (_camera == null)
        {
            _setupReturnCode |= ReturnCode.MissingCamera;
        }
        if (_tools.Count == 0)
        {
            _setupReturnCode |= ReturnCode.MissingTools;
        }
    }

    void ParseRotorIni(IMyMotorStator r)
    {
        _ini.Clear();
        float? restAngle = null;
        const string defaultValue = "none";

        if (_ini.TryParse(r.CustomData))
        {
            string angleStr = _ini.Get(IniSectionRotor, IniKeyRestAngle).ToString(defaultValue);
            float temp;
            if (float.TryParse(angleStr, out temp))
            {
                restAngle = temp;
            }
        }
        else if (!string.IsNullOrWhiteSpace(r.CustomData))
        {
            _ini.EndContent = r.CustomData;
        }

        _ini.Set(IniSectionRotor, IniKeyRestAngle, restAngle.HasValue ? restAngle.Value.ToString() : defaultValue);

        string output = _ini.ToString();
        if (output != r.CustomData)
        {
            r.CustomData = output;
        }

        _restAngles[r] = restAngle.HasValue ? MathHelper.ToRadians(restAngle.Value) : restAngle;
    }

    bool CollectBlocks(IMyTerminalBlock b)
    {
        if (!b.IsSameConstructAs(_p.Me))
        {
            return false;
        }
        var rotor = b as IMyMotorStator;
        if (rotor != null)
        {
            ParseRotorIni(rotor);
            if (StringExtensions.Contains(b.CustomName, _p.AzimuthName))
            {
                if (_azimuthRotor != null)
                {
                    _extraRotors.Add(rotor);
                }
                else
                {
                    _azimuthRotor = rotor;
                }
            }
            else if (StringExtensions.Contains(b.CustomName, _p.ElevationName))
            {
                if (_elevationRotor != null)
                {
                    _extraRotors.Add(rotor);
                }
                else
                {
                    _elevationRotor = rotor;
                }
            }
            else
            {
                _extraRotors.Add(rotor);
            }
        }

        var cam = b as IMyCameraBlock;
        if (cam != null)
        {
            if (_camera != null)
            {
                _tools.Add(cam);
            }
            else
            {
                _camera = cam;
            }
        }

        var tcb = b as IMyTurretControlBlock;
        if (tcb != null)
        {
            if (_controller != null)
            {
                _setupReturnCode |= ReturnCode.MultipleTurretControllers;
            }
            else
            {
                _controller = tcb;
            }
        }

        var func = b as IMyFunctionalBlock;
        if (func != null)
        {
            if (!(func is IMyLargeTurretBase)
                && (func is IMyUserControllableGun
                    || func is IMyLightingBlock
                    || func is IMyShipToolBase
                    || func is IMyShipConnector))
            {
                _tools.Add(func);
                _gridToToolDict[func.CubeGrid] = func;
            }
        }

        return false;
    }

    void SetBlocks()
    {
        if (_controller == null)
        {
            return;
        }
        _azimuthProperty = TerminalPropertiesHelper.GetProperty<long>(_controller, "RotorAzimuth");
        _elevationProperty = TerminalPropertiesHelper.GetProperty<long>(_controller, "RotorElevation");
        if (_camera != null)
        {
            _controller.Camera = _camera;
        }
        if (!_shouldRest)
        {
            if (_azimuthRotor != null)
            {
                _controller.AzimuthRotor = _azimuthRotor;
            }
            if (_elevationRotor != null)
            {
                _controller.ElevationRotor = _elevationRotor;
            }
        }
        _controller.ClearTools();
        _otherTools.Clear();
        _mainTools.Clear();

        if (_extraRotors.Count > 0) // Special behavior
        {
            foreach (var tool in _tools)
            {
                if ((_azimuthRotor != null && _azimuthRotor.IsAttached && tool.CubeGrid == _azimuthRotor.TopGrid)
                    || (_elevationRotor != null && _elevationRotor.IsAttached && tool.CubeGrid == _elevationRotor.TopGrid))
                {
                    _mainTools.Add(tool);
                    _controller.AddTool(tool);
                }
                else
                {
                    _otherTools.Add(tool);
                }
            }
        }
        else // Default behavior
        {
            _controller.AddTools(_tools);
        }

    }
    #endregion

    #region Utilities
    void Echo(string msg)
    {
        _p.Echo(msg);
    }

    public void WriteStatus()
    {
        Echo(_groupName);
        bool missingRotors = false;
        bool missingTools = false;
        if ((_setupReturnCode & ReturnCode.MissingController) != 0)
        {
            Echo("> ERROR: No custom turret controller.");
        }
        if ((_setupReturnCode & ReturnCode.MissingRotors) == ReturnCode.MissingRotors)
        {
            Echo("> ERROR: No azimuth or elevation rotor.");
            missingRotors = true;
        }

        if ((_setupReturnCode & ReturnCode.MissingToolAndCamera) == ReturnCode.MissingToolAndCamera)
        {
            Echo("> ERROR: No weapons, tools, or cameras.");
            missingTools = true;
        }

        if ((_setupReturnCode & ReturnCode.MultipleTurretControllers) != 0)
        {
            Echo("> WARN: Multiple custom turret controllers. Only one is supported.");
        }

        if (!missingRotors)
        {
            if ((_setupReturnCode & ReturnCode.MissingAzimuth) != 0)
            {
                Echo("> INFO: No azimuth rotor.");
            }
            if ((_setupReturnCode & ReturnCode.MissingElevation) != 0)
            {
                Echo("> INFO: No elevation rotor.");
            }
        }

        if ((_setupReturnCode & ReturnCode.NoExtraRotors) != 0)
        {
            Echo("> INFO: No extra rotors.");
        }
        else
        {
            Echo($"> INFO: {_extraRotors.Count} extra rotors.");
        }

        if (!missingTools)
        {
            if ((_setupReturnCode & ReturnCode.MissingCamera) != 0)
            {
                Echo("> INFO: No camera.");
            }
            if ((_setupReturnCode & ReturnCode.MissingTools) != 0)
            {
                Echo("> INFO: No tools.");
            }
            else
            {
                Echo($"> INFO: {_tools.Count} weapons/tools.");
            }
        }
        Echo("");
    }
    #endregion

    #region Rotor Control
    /*
     * In order to properly set the rest angle, we need to unassign the azinuth
     * and elevation rotors temporarily from the CTC. However, we can't do this
     * at the same time, or the CTC will be marked as invalid and will lose
     * its AI and ability to be manually controlled until the rest angle has been
     * reached. This is undesireable, so what we will do instead is get thhe
     * azimuth rotor to its rest angle, then try and get the elevation rotor to
     * its rest angle
     */
    bool HandleAzimuthAndElevationRestAngles()
    {
        bool done = true;
        done = TryMoveRotorToRestAngle(_azimuthRotor);
        if (done)
        {
            if (_azimuthRotor != null)
            {
                _controller.AzimuthRotor = _azimuthRotor;
            }
            done = TryMoveRotorToRestAngle(_elevationRotor);
            if (done)
            {
                if (_elevationRotor != null)
                {
                    _controller.ElevationRotor = _elevationRotor;
                }
            }
            else
            {
                _elevationProperty.SetValue(_controller, 0);
            }
        }
        else
        {
            _azimuthProperty.SetValue(_controller, 0);
            if (_elevationRotor != null)
            {
                _controller.ElevationRotor = _elevationRotor;
            }
        }
        return done;
    }

    bool TryMoveRotorToRestAngle(IMyMotorStator r)
    {
        float? restAngle;
        if (r == null
            || !_restAngles.TryGetValue(r, out restAngle)
            || !restAngle.HasValue)
        {
            return true;
        }
        return MoveRotorToEquilibrium(r, restAngle.Value);
    }

    bool HandleExtraRotors()
    {
        var directionSource = _controller.GetDirectionSource();
        if (directionSource == null)
        {
            return false;
        }

        bool isShooting = false;
        foreach (var t in _mainTools)
        {
            var tool = t as IMyShipToolBase;
            if (tool != null && tool.IsActivated)
            {
                isShooting = true;
                break;
            }

            var gun = t as IMyUserControllableGun;
            if (gun != null && gun.IsShooting)
            {
                isShooting = true;
                break;
            }
        }

        foreach (var r in _extraRotors)
        {
            if (!r.IsAttached)
            {
                continue;
            }

            IMyFunctionalBlock reference;
            if (!_gridToToolDict.TryGetValue(r.TopGrid, out reference))
            {
                // TODO: Warning
                continue;
            }

            AimRotorAtPosition(r, _controller.GetShootDirection(), reference.WorldMatrix.Forward);
        }

        return isShooting;
    }

    bool MoveRotorToEquilibrium(IMyMotorStator rotor, float restAngle)
    {
        if (rotor == null)
        {
            return true;
        }

        if (!rotor.Enabled)
            rotor.Enabled = true;

        float currentAngle = rotor.Angle;
        float lowerLimitRad = rotor.LowerLimitRad;
        float upperLimitRad = rotor.UpperLimitRad;

        if (lowerLimitRad >= -MathHelper.TwoPi && upperLimitRad <= MathHelper.TwoPi)
        {
            if (restAngle > upperLimitRad)
                restAngle -= MathHelper.TwoPi;
            else if (restAngle < lowerLimitRad)
                restAngle += MathHelper.TwoPi;
        }
        else
        {
            if (restAngle > currentAngle + MathHelper.Pi)
                restAngle -= MathHelper.TwoPi;
            else if (restAngle < currentAngle - MathHelper.Pi)
                restAngle += MathHelper.TwoPi;
        }


        float angularDeviation = (restAngle - currentAngle);
        float targetVelocity = (float)Math.Round(angularDeviation * RestSpeed, 2);

        bool belowTolerance = Math.Abs(angularDeviation) < RotorStopThresholdRad;
        if (belowTolerance)
        {
            rotor.TargetVelocityRPM = 0;
        }
        else
        {
            rotor.TargetVelocityRPM = targetVelocity;
        }

        return belowTolerance;
    }

    static double VectorAngleBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }

    static Vector3D VectorRejection(Vector3D a, Vector3D b) //reject a on b
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    static double GetAllowedRotationAngle(double desiredDelta, IMyMotorStator rotor)
    {
        double desiredAngle = rotor.Angle + desiredDelta;
        if ((desiredAngle < rotor.LowerLimitRad && desiredAngle + MathHelper.TwoPi < rotor.UpperLimitRad)
            || (desiredAngle > rotor.UpperLimitRad && desiredAngle - MathHelper.TwoPi > rotor.LowerLimitRad))
        {
            return -Math.Sign(desiredDelta) * (MathHelper.TwoPi - Math.Abs(desiredDelta));
        }
        return desiredDelta;
    }

    void AimRotorAtPosition(IMyMotorStator rotor, Vector3D desiredDirection, Vector3D currentDirection, float rotationScale = 1f)
    {
        Vector3D desiredDirectionFlat = VectorRejection(desiredDirection, rotor.WorldMatrix.Up);
        Vector3D currentDirectionFlat = VectorRejection(currentDirection, rotor.WorldMatrix.Up);
        double angle = VectorAngleBetween(desiredDirectionFlat, currentDirectionFlat);
        Vector3D axis = Vector3D.Cross(desiredDirection, currentDirection);
        angle *= Math.Sign(Vector3D.Dot(axis, rotor.WorldMatrix.Up));
        angle = GetAllowedRotationAngle(angle, rotor);
        rotor.TargetVelocityRad = rotationScale * (float)angle / (10f / 60f);
    }
    #endregion
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    Setup();
    _titleScreen = new TCESTitleScreen(Version, this);

    _runtimeTracker = new RuntimeTracker(this);
}

void Setup()
{
    _turretControllers.Clear();

    ProcessIni();
    GridTerminalSystem.GetBlockGroups(null, CollectGroups);
}

void ProcessIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        GroupName = _ini.Get(IniSectionGeneral, IniKeyGroupName).ToString(GroupName);
        AzimuthName = _ini.Get(IniSectionGeneral, IniKeyAzimuthName).ToString(AzimuthName);
        ElevationName = _ini.Get(IniSectionGeneral, IniKeyElevationName).ToString(ElevationName);
        AutomaticRest = _ini.Get(IniSectionGeneral, IniKeyAutoRestAngle).ToBoolean(AutomaticRest);
        AutomaticRestDelay = _ini.Get(IniSectionGeneral, IniKeyAutoRestDelay).ToSingle(AutomaticRestDelay);
        DrawTitleScreen = _ini.Get(IniSectionGeneral, IniKeyDrawTitleScreen).ToBoolean(DrawTitleScreen);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSectionGeneral, IniKeyGroupName, GroupName);
    _ini.Set(IniSectionGeneral, IniKeyAzimuthName, AzimuthName);
    _ini.Set(IniSectionGeneral, IniKeyElevationName, ElevationName);
    _ini.Set(IniSectionGeneral, IniKeyAutoRestAngle, AutomaticRest);
    _ini.Set(IniSectionGeneral, IniKeyAutoRestDelay, AutomaticRestDelay);
    _ini.Set(IniSectionGeneral, IniKeyDrawTitleScreen, DrawTitleScreen);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

bool CollectGroups(IMyBlockGroup g)
{
    if (!g.Name.Contains(GroupName))
    {
        return false;
    }
    _turretControllers.Add(new CustomTurretController(this, g));
    return false;
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();

    switch (arg)
    {
        case "setup":
            Setup();
            break;
        case "rest":
            foreach (var c in _turretControllers)
            {
                c.GoToRest();
            }
            break;
        default:
            break;
    }

    if ((updateSource & UpdateType.Update10) != 0)
    {
        OnUpdate10();

        ++_runCount;
        if (_runCount % 6 == 0)
        {
            OnUpdate60();
        }
    }

    _runtimeTracker.AddInstructions();
}

void OnUpdate10()
{
    foreach (var c in _turretControllers)
    {
        c.Update();
    }

    if (DrawTitleScreen)
    {
        _titleScreen.Draw();
    }
}

StringBuilder _echo = new StringBuilder(1024);
public new void Echo(string msg)
{
    _echo.Append(msg).Append("\n");
}

public void WriteEcho()
{
    base.Echo(_echo.ToString());
    _echo.Clear();
}

void OnUpdate60()
{
    Echo($"TCES | Turret Controller\nEnhancement Script\n(Version {Version} - {Date})\n");
    Echo("Run the argument \"setup\" to refetch blocks or process custom data changes.\n");
    Echo($"Custom Turret Groups: {_turretControllers.Count}\n");

    foreach (var controller in _turretControllers)
    {
        controller.WriteStatus();
    }

    Echo(_runtimeTracker.Write());

    WriteEcho();

    _titleScreen.RestartDraw();
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

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}


class TCESTitleScreen
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.5f;
    const float BaseTextHeightPx = 37f;

    const float CTCSpriteScale = 0.4f;
    const float TurretSpriteScale = 0.8f;

    const string Font = "Debug";
    const string TitleFormat = "TCES - v{0}";
    readonly string _titleText;

    Program _program;

    int _idx = 0;
    readonly Vector2 _ctcPos = new Vector2(-200, 120);
    readonly Vector2 _turretPos = new Vector2(150, 40);

    struct AnimationParams
    {
        public readonly float ElevationAngle;
        public readonly bool DrawMuzzleFlash;

        public AnimationParams(float elevationDeg, bool muzzleFlash = false)
        {
            ElevationAngle = MathHelper.ToRadians(elevationDeg);
            DrawMuzzleFlash = muzzleFlash;
        }
    }

    readonly AnimationParams[] _animSequence = new AnimationParams[] {
        new AnimationParams(0),
        new AnimationParams(10),
        new AnimationParams(20),
        new AnimationParams(30),
        new AnimationParams(30),
        new AnimationParams(30, true),
        new AnimationParams(30, true),
        new AnimationParams(30),
        new AnimationParams(30),
        new AnimationParams(20),
        new AnimationParams(10),
        new AnimationParams(0),
        new AnimationParams(0),
        new AnimationParams(0),
        new AnimationParams(0),
        new AnimationParams(0),
    };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public TCESTitleScreen(string version, Program program)
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

            DrawCTC(frame, screenCenter + _ctcPos * minScale, CTCSpriteScale * minScale);
            DrawTurret(frame, screenCenter + _turretPos * minScale, TurretSpriteScale * minScale, anim.ElevationAngle, anim.DrawMuzzleFlash);

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

    void DrawCTC(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-50f, 50f) * scale + centerPos, new Vector2(100f, 100f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(40f, 40f) * scale + centerPos, new Vector2(80f, 80f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(50f, 95f) * scale + centerPos, new Vector2(100f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(40f, 85f) * scale + centerPos, new Vector2(80f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(30f, 35f) * scale + centerPos, new Vector2(20f, 30f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(30f, 10f) * scale + centerPos, new Vector2(20f, 20f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(5f, 10f) * scale + centerPos, new Vector2(30f, 20f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-25f, -45f) * scale + centerPos, new Vector2(50f, 70f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(14f, -45f) * scale + centerPos, new Vector2(70f, 30f) * scale, _white, null, TextAlignment.CENTER, 1.5708f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-64f, -45f) * scale + centerPos, new Vector2(30f, 50f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-95f, -50f) * scale + centerPos, new Vector2(10f, 100f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(5f, -95f) * scale + centerPos, new Vector2(190f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(35f, -15f) * scale + centerPos, new Vector2(10f, 60f) * scale, _white, null, TextAlignment.CENTER, 0.7854f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(55f, -66f) * scale + centerPos, new Vector2(10f, 65f) * scale, _white, null, TextAlignment.CENTER, 0f));
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-25f, -5f) * scale + centerPos, new Vector2(30f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f));
    }

    void DrawTurret(MySpriteDrawFrame frame, Vector2 centerPos, float scale, float rotation, bool drawMuzzleFlash)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);

        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -90f - sin * 0f, sin * -90f + cos * 0f) * scale + centerPos, new Vector2(90f, 80f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // gun body
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(cos * -175f - sin * -25f, sin * -175f + cos * -25f) * scale + centerPos, new Vector2(30f, 80f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, -1.5708f + rotation)); // gun front slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -150f - sin * 12f, sin * -150f + cos * 12f) * scale + centerPos, new Vector2(50f, 45f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // gun bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -195f - sin * 2f, sin * -195f + cos * 2f) * scale + centerPos, new Vector2(40f, 25f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // gun mid
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(cos * -185f - sin * 24f, sin * -185f + cos * 24f) * scale + centerPos, new Vector2(20f, 20f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 3.1416f + rotation)); // gun bottom slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -345f - sin * 2f, sin * -345f + cos * 2f) * scale + centerPos, new Vector2(200f, 10f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // barrel
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -230f - sin * 2f, sin * -230f + cos * 2f) * scale + centerPos, new Vector2(20f, 16f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // barrel base
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * 2f, sin * -88f + cos * 2f) * scale + centerPos, new Vector2(60f, 60f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // conveyor outline
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * 2f, sin * -88f + cos * 2f) * scale + centerPos, new Vector2(50f, 50f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // conveyor center
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * -3f, sin * -88f + cos * -3f) * scale + centerPos, new Vector2(50f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // conveyor crossCopy
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * 7f, sin * -88f + cos * 7f) * scale + centerPos, new Vector2(50f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // conveyor cross
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(cos * -134f - sin * 50f, sin * -134f + cos * 50f) * scale + centerPos, new Vector2(10f, 10f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, -1.5708f + rotation)); // gun bottom slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -90f - sin * 50f, sin * -90f + cos * 50f) * scale + centerPos, new Vector2(80f, 10f) * scale, new Color(255, 255, 255, 255), null, TextAlignment.CENTER, 0f + rotation)); // gun bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -174f - sin * 10f, sin * -174f + cos * 10f) * scale + centerPos, new Vector2(60f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, -0.3491f + rotation)); // stripe diag
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -141f - sin * 0f, sin * -141f + cos * 0f) * scale + centerPos, new Vector2(12f, 5f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // stripe horizontal
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -134f - sin * 0f, sin * -134f + cos * 0f) * scale + centerPos, new Vector2(5f, 82f) * scale, new Color(0, 0, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // stripe vertical

        if (drawMuzzleFlash)
        {
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * -480f - sin * 7f, sin * -480f + cos * 7f) * scale + centerPos, new Vector2(60f, 20f) * scale, new Color(255, 128, 0, 255), null, TextAlignment.CENTER, -0.1745f + rotation)); // muzzle flash bottom
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * -482f - sin * -3f, sin * -482f + cos * -3f) * scale + centerPos, new Vector2(60f, 20f) * scale, new Color(255, 128, 0, 255), null, TextAlignment.CENTER, 0.1745f + rotation)); // muzzle flash top
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * -501f - sin * 2f, sin * -501f + cos * 2f) * scale + centerPos, new Vector2(100f, 20f) * scale, new Color(255, 128, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // muzzle flash main
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos * -470f - sin * 2f, sin * -470f + cos * 2f) * scale + centerPos, new Vector2(40f, 20f) * scale, new Color(255, 255, 0, 255), null, TextAlignment.CENTER, 0f + rotation)); // muzzle flash center
        }

        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(25f, -4f) * scale + centerPos, new Vector2(150f, 105f) * scale, _black, null, TextAlignment.CENTER, 0f)); // turret top right shadow
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(25f, 0f) * scale + centerPos, new Vector2(150f, 100f) * scale, _white, null, TextAlignment.CENTER, 0f)); // turret top right
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(-155f, -3f) * scale + centerPos, new Vector2(105f, 210f) * scale, _black, null, TextAlignment.CENTER, -1.5708f)); // turret top slope shadow
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(-150f, 0f) * scale + centerPos, new Vector2(100f, 200f) * scale, _white, null, TextAlignment.CENTER, -1.5708f)); // turret top slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-125f, 100f) * scale + centerPos, new Vector2(350f, 100f) * scale, _white, null, TextAlignment.CENTER, 0f)); // turret bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(74f, 100f) * scale + centerPos, new Vector2(100f, 50f) * scale, _white, null, TextAlignment.CENTER, 1.5708f)); // turret bottom right
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(-325f, 100f) * scale + centerPos, new Vector2(100f, 50f) * scale, _white, null, TextAlignment.CENTER, -1.5708f)); // turret bottom left
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-281f, 102f) * scale + centerPos, new Vector2(5f, 120f) * scale, _black, null, TextAlignment.CENTER, 0.4712f)); // turret detail line front
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(26f, 102f) * scale + centerPos, new Vector2(5f, 120f) * scale, _black, null, TextAlignment.CENTER, 0.4712f)); // turret detail line back
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(53f, 0f) * scale + centerPos, new Vector2(5f, 100f) * scale, _black, null, TextAlignment.CENTER, 0f)); // turret detail line back top
    }
    #endregion
}

static class TerminalPropertiesHelper
{
    static Dictionary<Type, Dictionary<string, ITerminalAction>> _terminalActionDict = new Dictionary<Type, Dictionary<string, ITerminalAction>>();
    static Dictionary<Type, Dictionary<string, ITerminalProperty>> _terminalPropertyDict = new Dictionary<Type, Dictionary<string, ITerminalProperty>>();

    public static ITerminalAction GetAction(IMyTerminalBlock block, string actionName)
    {
        Type type = block.GetType();
        Dictionary<string, ITerminalAction> dict;
        ITerminalAction act;

        if (!_terminalActionDict.TryGetValue(type, out dict))
        {
            dict = new Dictionary<string, ITerminalAction>();
        }

        if (dict.TryGetValue(actionName, out act))
        {
            return act;
        }

        act = block.GetActionWithName(actionName);
        dict[actionName] = act;
        _terminalActionDict[type] = dict;
        return act;
    }

    public static void ApplyAction(IMyTerminalBlock block, string actionName)
    {
        ITerminalAction act = GetAction(block, actionName);
        if (act != null)
            act.Apply(block);
    }

    public static ITerminalProperty<T> GetProperty<T>(IMyTerminalBlock block, string propertyName)
    {
        Type type = block.GetType();
        Dictionary<string, ITerminalProperty> dict;
        ITerminalProperty prop;

        if (!_terminalPropertyDict.TryGetValue(type, out dict))
        {
            dict = new Dictionary<string, ITerminalProperty>();
        }

        if (dict.TryGetValue(propertyName, out prop))
        {
            return prop.Cast<T>();
        }

        prop = block.GetProperty(propertyName);
        dict[propertyName] = prop;
        _terminalPropertyDict[type] = dict;
        if (prop == null)
            return null;
        return prop.Cast<T>();
    }

    public static void SetValue<T>(IMyTerminalBlock block, string propertyName, T value)
    {
        ITerminalProperty<T> prop = GetProperty<T>(block, propertyName);

        if (prop != null)
            prop.SetValue(block, value);
    }

    public static T GetValue<T>(IMyTerminalBlock block, string propertyName)
    {
        ITerminalProperty<T> prop = GetProperty<T>(block, propertyName);

        if (prop != null)
            return prop.GetValue(block);
        return default(T);
    }
}
