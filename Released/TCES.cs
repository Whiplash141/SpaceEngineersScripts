
#region In-game Script
/*
/ //// / TCES | Turret Controller Enhancement Script (by Whiplash141) / //// /

Description

This is a simple script that enhances the functionality of the
Custom Turret Controller (CTC) block with the following features:
- Automatic CTC block configuration
- Turret rotor rest angles
- Support of more than 2 rotors































=================================================
DO NOT MODIFY VARIABLES IN THE SCRIPT!

USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


*/

public const string Version = "1.8.3",
                    Date = "2023/03/11",
                    IniSectionGeneral = "TCES - General",
                    IniKeyGroupNameTag = "Group name tag",
                    IniKeyAzimuthName = "Azimuth rotor name tag",
                    IniKeyElevationName = "Elevation rotor name tag",
                    IniKeyAutoRestAngle = "Should auto return to rest angle",
                    IniKeyAutoRestDelay = "Auto return to rest angle delay (s)",
                    IniKeyDrawTitleScreen = "Draw title screen",
                    IniSectionRotor = "TCES - Rotor",
                    IniKeyRestAngle = "Rest angle (deg)",
                    IniKeyEnableStabilization = "Enable stabilization";

RuntimeTracker _runtimeTracker;
long _runCount = 0;

IConfigValue[] _config;
ConfigString _groupNameTag = new ConfigString(IniSectionGeneral, IniKeyGroupNameTag, "TCES");
public ConfigString AzimuthName { get; } = new ConfigString(IniSectionGeneral, IniKeyAzimuthName, "Azimuth");
public ConfigString ElevationName { get; } = new ConfigString(IniSectionGeneral, IniKeyElevationName, "Elevation");
public ConfigBool AutomaticRest { get; } = new ConfigBool(IniSectionGeneral, IniKeyAutoRestAngle, true);
public ConfigFloat AutomaticRestDelay { get; } = new ConfigFloat(IniSectionGeneral, IniKeyAutoRestDelay, 2f);
ConfigBool _drawTitleScreen = new ConfigBool(IniSectionGeneral, IniKeyDrawTitleScreen, true);

TCESTitleScreen _titleScreen;

List<CustomTurretController> _turretControllers = new List<CustomTurretController>();

MyIni _ini = new MyIni();

public class StabilizedRotor
{
    public float Velocity { get; private set; }

    MatrixD _lastOrientation = MatrixD.Identity;
    IMyMotorStator _rotor = null;
    public IMyMotorStator Rotor
    {
        get
        {
            return _rotor;
        }
        set
        {
            if (value != _rotor)
            {
                _rotor = value;
                if (_rotor != null)
                {
                    _lastOrientation = _rotor.WorldMatrix;
                }
            }
        }
    }

    double CalculateAngularVelocity(double dt)
    {
        var axis = Vector3D.Cross(_lastOrientation.Forward, Rotor.WorldMatrix.Forward);
        double mag = axis.Length();
        double angle = MathHelper.Clamp(Math.Asin(mag), -1.0, 1.0);
        axis = mag < 1e-12 ? Vector3D.Zero : axis / mag * angle / dt;
        return Vector3D.Dot(axis, Rotor.WorldMatrix.Up);
    }

    public float Update(double dt)
    {
        if (Rotor == null)
        {
            Velocity = 0f;
        }
        else
        {
            Velocity = (float)(CalculateAngularVelocity(dt) * MathHelper.RadiansPerSecondToRPM);
            _lastOrientation = Rotor.WorldMatrix.GetOrientation();
        }
        return Velocity;
    }
}

class CustomTurretController
{
    class ConfigRotorAngle : ConfigFloat
    {
        const string DefaultString = "none";
        public bool HasValue { get; private set; } = false;

        public ConfigRotorAngle(string section, string name) : base(section, name, -1f, null) { }

        protected override string GetIniString()
        {
            return HasValue ? Value.ToString() : DefaultString;
        }

        protected override void SetValue(ref MyIniValue val)
        {
            HasValue = true;
            base.SetValue(ref val);
        }
        protected override void SetDefault()
        {
            HasValue = false;
        }
    }

    IConfigValue[] _rotorConfig;

    ConfigBool _enableStabilization = new ConfigBool(IniSectionRotor, IniKeyEnableStabilization, true);
    ConfigRotorAngle _restAngle = new ConfigRotorAngle(IniSectionRotor, IniKeyRestAngle);

    const float RotorStopThresholdRad = 1f * (MathHelper.Pi / 180f);
    List<IMyFunctionalBlock> _tools = new List<IMyFunctionalBlock>();
    List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
    List<IMyFunctionalBlock> _mainTools = new List<IMyFunctionalBlock>();
    List<IMyFunctionalBlock> _otherTools = new List<IMyFunctionalBlock>();

    List<IMyMotorStator> _extraRotors = new List<IMyMotorStator>();
    StabilizedRotor _azimuthStabilizer = new StabilizedRotor();
    StabilizedRotor _elevationStabilizer = new StabilizedRotor();
    float? _azimuthRestAngle = null;
    float? _elevationRestAngle = null;
    bool _stabilizeAzimuth = true;
    bool _stabilizeElevation = true;
    IMyTurretControlBlock _controller;

    Program _p;
    IMyBlockGroup _group;
    readonly string _groupName;

    Dictionary<IMyCubeGrid, IMyFunctionalBlock> _gridToToolDict = new Dictionary<IMyCubeGrid, IMyFunctionalBlock>();
    long _updateCount = 0;

    bool _wasActive = false;
    bool _wasShooting = false;
    bool _wasManuallyControlled = false;
    bool _shouldRest = false;
    MyIni _ini = new MyIni();
    float _idleTime = 0f;

    const float RestSpeed = 10f;
    const float PlayerInputMultiplier = 1f/50f; // Magic number from: SpaceEngineers.ObjectBuilders.ObjectBuilders.Definitions.MyObjectBuilder_TurretControlBlockDefinition.PlayerInputDivider

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

    public bool IsManuallyControlled
    {
        get
        {
            return _controller != null && _controller.IsUnderControl;
        }
    }

    bool IsActive
    {
        get
        {
            return _controller != null && (_controller.HasTarget || _controller.IsUnderControl);
        }
    }

    public CustomTurretController(Program p, IMyBlockGroup group)
    {
        _rotorConfig = new IConfigValue[]
        {
            _restAngle,
            _enableStabilization,
        };

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

    static float MouseInputToRotorVelocityRpm(float input, float multiplierRpm, IMyMotorStator rotor)
    {   
        return input * PlayerInputMultiplier * multiplierRpm;
    }

    public void Update1()
    {

        if (IsManuallyControlled)
        {
            if (_stabilizeAzimuth) 
            { 
                _azimuthStabilizer.Update(1f / 60f); 
            }
            _controller.AzimuthRotor = null;
            if (BlockValid(_azimuthStabilizer.Rotor))
            {
                _azimuthStabilizer.Rotor.TargetVelocityRPM =
                    MouseInputToRotorVelocityRpm(_controller.RotationIndicator.Y, _controller.VelocityMultiplierAzimuthRpm, _azimuthStabilizer.Rotor) +
                    (_stabilizeAzimuth ? _azimuthStabilizer.Velocity : 0);
            }

            _controller.ElevationRotor = null;
            if (BlockValid(_elevationStabilizer.Rotor))
            {
                if (_stabilizeElevation)
                {
                    _elevationStabilizer.Update(1f / 60f);
                }
                _elevationStabilizer.Rotor.TargetVelocityRPM =
                    MouseInputToRotorVelocityRpm(_controller.RotationIndicator.X, _controller.VelocityMultiplierElevationRpm, _elevationStabilizer.Rotor) +
                    (_stabilizeElevation ? _elevationStabilizer.Velocity : 0);
            }
            _wasManuallyControlled = true;
        }
        else
        {
            if (_wasManuallyControlled)
            {
                if (BlockValid(_azimuthStabilizer.Rotor))
                {
                    _controller.AzimuthRotor = _azimuthStabilizer.Rotor;
                    _azimuthStabilizer.Rotor.TargetVelocityRad = 0;
                }
                if (BlockValid(_elevationStabilizer.Rotor))
                {
                    _controller.ElevationRotor = _elevationStabilizer.Rotor;
                    _elevationStabilizer.Rotor.TargetVelocityRad = 0;
                }
            }

            _wasManuallyControlled = false;
        }

    }

    public void Update10()
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

        Update1();

        if (IsActive)
        {
            _idleTime = 0f;

            if (_shouldRest)
            {
                if (BlockValid(_azimuthStabilizer.Rotor))
                {
                    _controller.AzimuthRotor = _azimuthStabilizer.Rotor;
                    _azimuthStabilizer.Rotor.TargetVelocityRad = 0;
                }
                if (BlockValid(_elevationStabilizer.Rotor))
                {
                    _controller.ElevationRotor = _elevationStabilizer.Rotor;
                    _elevationStabilizer.Rotor.TargetVelocityRad = 0;
                }
                _shouldRest = false;
            }
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

        if (_shouldRest)
        {
            bool done = HandleAzimuthAndElevationRestAngles();
            HandleExtraRotors();
            _shouldRest = !done;
        }
        else if (_extraRotors.Count > 0)
        {
            HandleExtraRotors();
            bool shouldShoot = IsShooting();
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
        }


        _wasActive = IsActive;
    }

    #region Setup
    void Setup()
    {
        _setupReturnCode = ReturnCode.None;

        _controller = null;
        _azimuthStabilizer.Rotor = null;
        _elevationStabilizer.Rotor = null;
        _extraRotors.Clear();
        _tools.Clear();
        _cameras.Clear();
        _gridToToolDict.Clear();
        _azimuthRestAngle = null;
        _elevationRestAngle = null;

        _group.GetBlocks(null, CollectBlocks);

        if (_controller == null)
        {
            _setupReturnCode |= ReturnCode.MissingController;
        }
        if (_azimuthStabilizer.Rotor == null)
        {
            _setupReturnCode |= ReturnCode.MissingAzimuth;
        }
        if (_elevationStabilizer.Rotor == null)
        {
            _setupReturnCode |= ReturnCode.MissingElevation;
        }
        if (_extraRotors.Count == 0)
        {
            _setupReturnCode |= ReturnCode.NoExtraRotors;
        }
        if (_cameras.Count == 0)
        {
            _setupReturnCode |= ReturnCode.MissingCamera;
        }
        if (_tools.Count == 0)
        {
            _setupReturnCode |= ReturnCode.MissingTools;
        }
    }

    void ParseRotorIni(IMyMotorStator r, out float? restAngle, out bool stabilize)
    {
        _ini.Clear();
        if (!_ini.TryParse(r.CustomData) && !string.IsNullOrWhiteSpace(r.CustomData))
        {
            _ini.EndContent = r.CustomData;
        }

        foreach (IConfigValue c in _rotorConfig)
        {
            c.Update(_ini);
        }

        string output = _ini.ToString();
        if (output != r.CustomData)
        {
            r.CustomData = output;
        }

        restAngle = _restAngle.HasValue ? MathHelper.ToRadians(_restAngle.Value) : (float?)null;
        stabilize = _enableStabilization;
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
            if (StringExtensions.Contains(b.CustomName, _p.AzimuthName))
            {
                if (_azimuthStabilizer.Rotor != null)
                {
                    _extraRotors.Add(rotor);
                }
                else
                {
                    _azimuthStabilizer.Rotor = rotor;
                    ParseRotorIni(_azimuthStabilizer.Rotor, out _azimuthRestAngle, out _stabilizeAzimuth);
                }
            }
            else if (StringExtensions.Contains(b.CustomName, _p.ElevationName))
            {
                if (_elevationStabilizer.Rotor != null)
                {
                    _extraRotors.Add(rotor);
                }
                else
                {
                    _elevationStabilizer.Rotor = rotor;
                    ParseRotorIni(_elevationStabilizer.Rotor, out _elevationRestAngle, out _stabilizeElevation);
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
            _cameras.Add(cam);
            _gridToToolDict[cam.CubeGrid] = cam;
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

    bool BlockValid(IMyTerminalBlock b)
    {
        return (b != null) && !b.Closed;
    }

    void SetBlocks()
    {
        if (_controller == null)
        {
            return;
        }
        if (_cameras.Count > 0)
        {
            foreach (var c in _cameras)
            {
                if (!c.Closed)
                {
                    _controller.Camera = c;
                    c.Enabled = true;
                    break;
                }
            }
        }
        if (!_shouldRest)
        {
            if (BlockValid(_azimuthStabilizer.Rotor))
            {
                _controller.AzimuthRotor = _azimuthStabilizer.Rotor;
            }
            if (BlockValid(_elevationStabilizer.Rotor))
            {
                _controller.ElevationRotor = _elevationStabilizer.Rotor;
            }
        }
        _controller.ClearTools();
        _otherTools.Clear();
        _mainTools.Clear();
        foreach (var t in _tools)
        {
            if (BlockValid(t))
            {
                if (_extraRotors.Count > 0) // Special behavior
                {

                    if ((_azimuthStabilizer.Rotor != null && _azimuthStabilizer.Rotor.IsAttached && t.CubeGrid == _azimuthStabilizer.Rotor.TopGrid) ||
                        (_elevationStabilizer.Rotor != null && _elevationStabilizer.Rotor.IsAttached && t.CubeGrid == _elevationStabilizer.Rotor.TopGrid))
                    {
                        _mainTools.Add(t);
                        _controller.AddTool(t);
                    }
                    else
                    {
                        _otherTools.Add(t);
                    }
                }
                else // Default behavior
                {
                    _controller.AddTool(t);
                }
            }
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
            else
            {
                Echo($"> INFO: {_cameras.Count} cameras.");
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
        bool done = TryMoveRotorToRestAngle(_azimuthStabilizer.Rotor, _azimuthRestAngle);
        if (done)
        {
            if (BlockValid(_azimuthStabilizer.Rotor))
            {
                _controller.AzimuthRotor = _azimuthStabilizer.Rotor;
            }
            done = TryMoveRotorToRestAngle(_elevationStabilizer.Rotor, _elevationRestAngle);
            if (done)
            {
                if (BlockValid(_elevationStabilizer.Rotor))
                {
                    _controller.ElevationRotor = _elevationStabilizer.Rotor;
                }
            }
            else
            {
                _controller.ElevationRotor = null;
            }
        }
        else
        {
            _controller.AzimuthRotor = null;
            if (BlockValid(_elevationStabilizer.Rotor))
            {
                _controller.ElevationRotor = _elevationStabilizer.Rotor;
            }
        }
        return done;
    }

    bool TryMoveRotorToRestAngle(IMyMotorStator r, float? restAngle)
    {
        if (!BlockValid(r) || !restAngle.HasValue)
        {
            return true;
        }
        return MoveRotorToEquilibrium(r, restAngle.Value);
    }

    bool IsShooting()
    {
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
        return isShooting;
    }

    void HandleExtraRotors()
    {
        if (_extraRotors.Count == 0)
        {
            return;
        }
        
        var directionSource = _controller.GetDirectionSource();
        if (directionSource == null)
        {
            return;
        }

        Vector3D totalVelocityCommand = Vector3D.Zero;
        if (_azimuthStabilizer.Rotor != null)
        {
            totalVelocityCommand += _azimuthStabilizer.Rotor.WorldMatrix.Up * _azimuthStabilizer.Rotor.TargetVelocityRad;
        }
        if (_elevationStabilizer.Rotor != null)
        {
            totalVelocityCommand += _elevationStabilizer.Rotor.WorldMatrix.Up * _elevationStabilizer.Rotor.TargetVelocityRad;
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
            float commandedVelocity = (float)Vector3D.Dot(totalVelocityCommand, r.WorldMatrix.Up);
            r.TargetVelocityRad += commandedVelocity;
        }
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
    _config = new IConfigValue[]
    {
        _groupNameTag,
        AzimuthName,
        ElevationName,
        AutomaticRest,
        AutomaticRestDelay,
        _drawTitleScreen,
    };

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
    if (!_ini.TryParse(Me.CustomData) && !string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    foreach (IConfigValue c in _config)
    {
        c.Update(_ini);
    }

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

bool CollectGroups(IMyBlockGroup g)
{
    if (!g.Name.Contains(_groupNameTag))
    {
        return false;
    }
    _turretControllers.Add(new CustomTurretController(this, g));
    return false;
}

void Main(string arg, UpdateType updateSource)
{
    try
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
        else if ((updateSource & UpdateType.Update1) != 0)
        {
            OnUpdate1();
        }

        _runtimeTracker.AddInstructions();
    }
    catch (Exception e)
    {
        BlueScreenOfDeath.Show(Me.GetSurface(0), "TCES", Version, e);
        throw e;
    }
}

void OnUpdate1()
{
    foreach (var c in _turretControllers)
    {
        c.Update1();
    }
}

void OnUpdate10()
{
    bool needsFastUpdate = false;
    foreach (var c in _turretControllers)
    {
        c.Update10();
        needsFastUpdate |= c.IsManuallyControlled;
    }

    if (_drawTitleScreen)
    {
        _titleScreen.Draw();
    }

    UpdateFrequency desiredFrequency;
    if (needsFastUpdate)
    {
        desiredFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update1;
    }
    else
    {
        desiredFrequency = UpdateFrequency.Update10;
    }

    if (Runtime.UpdateFrequency != desiredFrequency)
    {
        Runtime.UpdateFrequency = desiredFrequency;
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
    Echo($"Custom Turret Groups: {_turretControllers.Count}");
    Echo($"Run frequency: {Runtime.UpdateFrequency}\n");

    foreach (var controller in _turretControllers)
    {
        controller.WriteStatus();
    }

    Echo(_runtimeTracker.Write());

    WriteEcho();

    _titleScreen.RestartDraw();
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

        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -90f - sin * 0f, sin * -90f + cos * 0f) * scale + centerPos, new Vector2(90f, 80f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // gun body
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(cos * -175f - sin * -25f, sin * -175f + cos * -25f) * scale + centerPos, new Vector2(30f, 80f) * scale, _white, null, TextAlignment.CENTER, -1.5708f + rotation)); // gun front slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -150f - sin * 12f, sin * -150f + cos * 12f) * scale + centerPos, new Vector2(50f, 45f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // gun bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -195f - sin * 2f, sin * -195f + cos * 2f) * scale + centerPos, new Vector2(40f, 25f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // gun mid
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(cos * -185f - sin * 24f, sin * -185f + cos * 24f) * scale + centerPos, new Vector2(20f, 20f) * scale, _white, null, TextAlignment.CENTER, 3.1416f + rotation)); // gun bottom slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -345f - sin * 2f, sin * -345f + cos * 2f) * scale + centerPos, new Vector2(200f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // barrel
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -230f - sin * 2f, sin * -230f + cos * 2f) * scale + centerPos, new Vector2(20f, 16f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // barrel base
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * 2f, sin * -88f + cos * 2f) * scale + centerPos, new Vector2(60f, 60f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // conveyor outline
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * 2f, sin * -88f + cos * 2f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // conveyor center
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * -3f, sin * -88f + cos * -3f) * scale + centerPos, new Vector2(50f, 5f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // conveyor crossCopy
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -88f - sin * 7f, sin * -88f + cos * 7f) * scale + centerPos, new Vector2(50f, 5f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // conveyor cross
        frame.Add(new MySprite(SpriteType.TEXTURE, "RightTriangle", new Vector2(cos * -134f - sin * 50f, sin * -134f + cos * 50f) * scale + centerPos, new Vector2(10f, 10f) * scale, _white, null, TextAlignment.CENTER, -1.5708f + rotation)); // gun bottom slope
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -90f - sin * 50f, sin * -90f + cos * 50f) * scale + centerPos, new Vector2(80f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f + rotation)); // gun bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -174f - sin * 10f, sin * -174f + cos * 10f) * scale + centerPos, new Vector2(60f, 5f) * scale, _black, null, TextAlignment.CENTER, -0.3491f + rotation)); // stripe diag
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -141f - sin * 0f, sin * -141f + cos * 0f) * scale + centerPos, new Vector2(12f, 5f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // stripe horizontal
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * -134f - sin * 0f, sin * -134f + cos * 0f) * scale + centerPos, new Vector2(5f, 82f) * scale, _black, null, TextAlignment.CENTER, 0f + rotation)); // stripe vertical

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
#endregion

#region INCLUDES

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
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

public interface IConfigValue
{
    void WriteToIni(MyIni ini);
    void ReadFromIni(MyIni ini);
    void Update(MyIni ini);
}

public abstract class ConfigValue<T> : IConfigValue
{
    public T Value;
    public string Section { get; set; }
    public string Name { get; set; }
    T DefaultValue { get; }
    readonly string _comment;

    public static implicit operator T(ConfigValue<T> cfg)
    {
        return cfg.Value;
    }

    public ConfigValue(string section, string name, T defaultValue = default(T), string comment = null)
    {
        Section = section;
        Name = name;
        Value = defaultValue;
        DefaultValue = defaultValue;
        _comment = comment;
    }

    protected virtual string GetIniString()
    {
        return Value.ToString();
    }

    public void Update(MyIni ini)
    {
        ReadFromIni(ini);
        WriteToIni(ini);
    }

    public void WriteToIni(MyIni ini)
    {
        ini.Set(Section, Name, GetIniString());
        if (!string.IsNullOrWhiteSpace(_comment))
        {
            ini.SetComment(Section, Name, _comment);
        }
    }

    protected abstract void SetValue(ref MyIniValue val);

    protected virtual void SetDefault() 
    {
        Value = DefaultValue;
    }

    public void ReadFromIni(MyIni ini)
    {
        MyIniValue val = ini.Get(Section, Name);
        if (!val.IsEmpty)
        {
            SetValue(ref val);
        }
        else
        {
            SetDefault();
        }
    }
}

public class ConfigString : ConfigValue<string>
{
    public ConfigString(string section, string name, string value = "", string comment = null) : base(section, name, value, comment) { }
    protected override void SetValue(ref MyIniValue val) { if (!val.TryGetString(out Value)) SetDefault(); }
}

public class ConfigBool : ConfigValue<bool>
{
    public ConfigBool(string section, string name, bool value = false, string comment = null) : base(section, name, value, comment) { }
    protected override void SetValue(ref MyIniValue val) { if (!val.TryGetBoolean(out Value)) SetDefault(); }
}

public class ConfigFloat : ConfigValue<float>
{
    public ConfigFloat(string section, string name, float value = 0, string comment = null) : base(section, name, value, comment) { }
    protected override void SetValue(ref MyIniValue val) { if (!val.TryGetSingle(out Value)) SetDefault(); }
}
#endregion
