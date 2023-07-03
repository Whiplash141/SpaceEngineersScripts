
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

public const string Version = "1.9.3",
                    Date = "2023/07/03",
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
public ConfigString AzimuthName = new ConfigString(IniSectionGeneral, IniKeyAzimuthName, "Azimuth");
public ConfigString ElevationName = new ConfigString(IniSectionGeneral, IniKeyElevationName, "Elevation");
public ConfigBool AutomaticRest = new ConfigBool(IniSectionGeneral, IniKeyAutoRestAngle, true);
public ConfigFloat AutomaticRestDelay = new ConfigFloat(IniSectionGeneral, IniKeyAutoRestDelay, 2f);
ConfigBool _drawTitleScreen = new ConfigBool(IniSectionGeneral, IniKeyDrawTitleScreen, true);

TCESTitleScreen _titleScreen;

List<CustomTurretController> _turretControllers = new List<CustomTurretController>();

MyIni _ini = new MyIni();

class CustomTurretController
{
    class ConfigRotorAngle : ConfigFloat
    {
        const string DefaultString = "none";
        public bool HasValue { get; private set; } = false;

        public ConfigRotorAngle(string section, string name) : base(section, name, -1f, null) { }

        public override string ToString()
        {
            return HasValue ? Value.ToString() : DefaultString;
        }

        protected override bool SetValue(ref MyIniValue val)
        {
            HasValue = true;
            return base.SetValue(ref val);
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

    bool _wasShooting = false;
    bool _wasManuallyControlled = false;
    MyIni _ini = new MyIni();
    float _idleTime = 0f;
    float _cachedElevationBrakingTorque = 0;

    const float RestSpeed = 10f;
    const float PlayerInputMultiplier = 1f/50f; // Magic number from: SpaceEngineers.ObjectBuilders.ObjectBuilders.Definitions.MyObjectBuilder_TurretControlBlockDefinition.PlayerInputDivider

    enum ControlState
    {
        ManualControl,
        AiControl,
        WaitForRest,
        MoveToRest,
        Idle
    }

    enum AiTargetingState
    {
        Idle,
        BothRotors,
        AzimuthOnly,
    }

    StateMachine _rotorControlSM = new StateMachine();
    StateMachine _aiControlSM = new StateMachine();

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

    ControlState _lastState = ControlState.Idle;

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
            return _controller != null && (_controller.HasTarget || _controller.IsUnderControl || _controller.IsSunTrackerEnabled);
        }
    }

    public CustomTurretController(Program p, IMyBlockGroup group)
    {
        _rotorControlSM.AddState(new State(ControlState.ManualControl, onUpdate: OnUpdateManualControl));
        _rotorControlSM.AddState(new State(ControlState.AiControl, onUpdate: OnUpdateAiControl, onEnter: OnEnterAiControl, onLeave: OnAiControlBothRotors));
        _rotorControlSM.AddState(new State(ControlState.WaitForRest, onUpdate: OnUpdateRestWait, onEnter: OnEnterRestWait));
        _rotorControlSM.AddState(new State(ControlState.MoveToRest, onUpdate: OnUpdateMoveToRest));
        _rotorControlSM.AddState(new State(ControlState.Idle, onEnter: ResetRotors));
        _rotorControlSM.Initialize(ControlState.MoveToRest);

        _aiControlSM.AddState(new State(AiTargetingState.Idle));
        _aiControlSM.AddState(new State(AiTargetingState.BothRotors, onEnter: OnAiControlBothRotors));
        _aiControlSM.AddState(new State(AiTargetingState.AzimuthOnly, onEnter: OnAiControlAzimuthOnly));
        _aiControlSM.Initialize(AiTargetingState.Idle);

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

        if (_elevationStabilizer.Rotor != null)
        {
            _cachedElevationBrakingTorque = _elevationStabilizer.Rotor.BrakingTorque;
        }
    }

    public void GoToRest()
    {
        if (_p.AutomaticRest && _controller != null && !IsActive)
        {
            _rotorControlSM.SetState(ControlState.MoveToRest);
        }
    }

    /*
     * Since we are nulling out the rotors in order to apply stabilization,
     * the GetAzimuth/ElevationSpeedMax methods will return 30 rpm always. However,
     * when a rotor is actually assigned, small grid rotors will return 60 rpm. The
     * multipliers below account for this.
     */
    static float GetRotorMultiplier(IMyMotorStator rotor)
    {
        return (rotor.CubeGrid.GridSizeEnum == MyCubeSize.Small) ? 2f : 1f;
    }

    static float MouseInputToRotorVelocityRpm(float input, float multiplierRpm, IMyMotorStator rotor)
    {
        return input * PlayerInputMultiplier * multiplierRpm * GetRotorMultiplier(rotor);
    }

    #region Rotor Control State Machine
    void ResetRotors()
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

        foreach (var r in _extraRotors)
        {
            r.TargetVelocityRad = 0f;
        }
    }

    void OnUpdateManualControl()
    {
        _controller.AzimuthRotor = null;
        if (BlockValid(_azimuthStabilizer.Rotor))
        {
            if (_stabilizeAzimuth)
            {
                _azimuthStabilizer.Update(1f / 60f);
            }
            _azimuthStabilizer.Rotor.TargetVelocityRPM =
                MouseInputToRotorVelocityRpm(_controller.RotationIndicator.Y, _controller.VelocityMultiplierAzimuthRpm, _azimuthStabilizer.Rotor) +
                (_stabilizeAzimuth && _wasManuallyControlled ? _azimuthStabilizer.Velocity : 0);
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
                (_stabilizeElevation && _wasManuallyControlled ? _elevationStabilizer.Velocity : 0);
        }

        _wasManuallyControlled = true;
    }

    void OnEnterAiControl()
    {
        ResetRotors();
        _aiControlSM.SetState(AiTargetingState.Idle);
    }

    void OnUpdateAiControl()
    {
        MyDetectedEntityInfo info = _controller.GetTargetedEntity();
        if (info.IsEmpty() || !info.HitPosition.HasValue)
        {
            return;
        }

        IMyTerminalBlock aimRef = _controller.GetDirectionSource();
        if (aimRef == null)
        {
            return;
        }

        Vector3D aimDirection = aimRef.WorldMatrix.Forward;
        Vector3D targetDirection = info.HitPosition.Value - aimRef.WorldMatrix.Translation;
        double cosBtwn = VectorMath.CosBetween(aimDirection, targetDirection);
        if (cosBtwn > 0.7071)
        {
            _aiControlSM.SetState(AiTargetingState.BothRotors);
        }
        else
        {
            _aiControlSM.SetState(AiTargetingState.AzimuthOnly);
        }
    }
    
    void OnAiControlBothRotors()
    {
        if (BlockValid(_elevationStabilizer.Rotor))
        {
            _elevationStabilizer.Rotor.BrakingTorque = _cachedElevationBrakingTorque;
            _elevationStabilizer.Rotor.Enabled = true;
        }
    }

    void OnAiControlAzimuthOnly()
    {
        if (BlockValid(_elevationStabilizer.Rotor))
        {
            _elevationStabilizer.Rotor.BrakingTorque = _elevationStabilizer.Rotor.Torque;
            _elevationStabilizer.Rotor.Enabled = false;
        }
    }

    void OnEnterRestWait()
    {
        ResetRotors();
        _idleTime = 0f;
    }

    void OnUpdateRestWait()
    {
        _idleTime += (1f / 6f);
        if (_idleTime >= _p.AutomaticRestDelay)
        {
            _rotorControlSM.SetState(ControlState.MoveToRest);
        }
    }

    void OnUpdateMoveToRest()
    {
        bool done = HandleAzimuthAndElevationRestAngles();
        if (done)
        {
            _rotorControlSM.SetState(ControlState.Idle);
        }
    }
    #endregion

    public void Update1()
    {
        if (_controller == null)
        {
            return;
        }

        if (IsManuallyControlled)
        {
            _rotorControlSM.SetState(ControlState.ManualControl);
        }
        else if (IsActive)
        {
            _rotorControlSM.SetState(ControlState.AiControl);
        }
        else if (_lastState == ControlState.ManualControl || _lastState == ControlState.AiControl)
        {
            if (_p.AutomaticRest)
            {
                _rotorControlSM.SetState(ControlState.WaitForRest);
            }
            else
            {
                _rotorControlSM.SetState(ControlState.Idle);
            }
        }

        _rotorControlSM.Update();
        _lastState = (ControlState)_rotorControlSM.StateId;
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

        if (_extraRotors.Count > 0)
        {
            HandleExtraRotors();
            SyncWeaponFiring();
        }
    }

    void SyncWeaponFiring()
    {
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
        IMyMotorStator r = b as IMyMotorStator;
        if (r != null && r.TopGrid == null) return false;
        return (b != null) &&  _p.Me.IsSameConstructAs(b);
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
        if ((ControlState)_rotorControlSM.StateId != ControlState.MoveToRest)
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
    const int MAX_BSOD_WIDTH = 50;
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
        surface.FontSize = scaleFactor * surface.TextureSize.X / (19.5f * MAX_BSOD_WIDTH);
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
                        bsodBuilder.Append("\n");
                        lineLength = word.Length;
                    }
                    bsodBuilder.Append(word).Append(" ");
                    lineLength += 1;
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
    bool ReadFromIni(MyIni ini);
    bool Update(MyIni ini);
}

public abstract class ConfigValue<T> : IConfigValue
{
    public string Section;
    public string Name;
    protected T _value;
    public T Value
    {
        get { return _value; }
        set
        {
            _value = value;
            _skipRead = true;
        }
    }
    readonly T _defaultValue;
    readonly string _comment;
    bool _skipRead = false;

    public static implicit operator T(ConfigValue<T> cfg)
    {
        return cfg.Value;
    }

    public ConfigValue(string section, string name, T defaultValue, string comment)
    {
        Section = section;
        Name = name;
        _value = defaultValue;
        _defaultValue = defaultValue;
        _comment = comment;
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public bool Update(MyIni ini)
    {
        bool read = ReadFromIni(ini);
        WriteToIni(ini);
        return read;
    }

    public bool ReadFromIni(MyIni ini)
    {
        if (_skipRead)
        {
            _skipRead = false;
            return true;
        }
        MyIniValue val = ini.Get(Section, Name);
        bool read = !val.IsEmpty;
        if (read)
        {
            read = SetValue(ref val);
        }
        else
        {
            SetDefault();
        }
        return read;
    }

    public void WriteToIni(MyIni ini)
    {
        ini.Set(Section, Name, this.ToString());
        if (!string.IsNullOrWhiteSpace(_comment))
        {
            ini.SetComment(Section, Name, _comment);
        }
        _skipRead = false;
    }

    public void Reset()
    {
        SetDefault();
        _skipRead = false;
    }

    protected abstract bool SetValue(ref MyIniValue val);

    protected virtual void SetDefault()
    {
        _value = _defaultValue;
    }
}

public class ConfigString : ConfigValue<string>
{
    public ConfigString(string section, string name, string value = "", string comment = null) : base(section, name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetString(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigBool : ConfigValue<bool>
{
    public ConfigBool(string section, string name, bool value = false, string comment = null) : base(section, name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetBoolean(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigFloat : ConfigValue<float>
{
    public ConfigFloat(string section, string name, float value = 0, string comment = null) : base(section, name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetSingle(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

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

    public StabilizedRotor(IMyMotorStator rotor = null)
    {
        Rotor = rotor;
    }

    double CalculateAngularVelocity(double dt)
    {
        var axis = Vector3D.Cross(_lastOrientation.Forward, Rotor.WorldMatrix.Forward);
        double mag = axis.Length();
        double angle = Math.Asin(MathHelper.Clamp(mag, -1.0, 1.0));
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

public class StateMachine
{
    public Enum StateId
    {
        get
        {
            return State.Id;
        }
    }
    public IState State { get; private set; } = null;

    Dictionary<Enum, IState> _states = new Dictionary<Enum, IState>();
    bool _initialized = false;

    public void AddStates(params IState[] states)
    {
        foreach (IState state in states)
        {
            AddState(state);
        }
    }

    public void AddState(IState state)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("StateMachine.AddState can not be called after initialization");
        }
        bool uniqueState = !_states.ContainsKey(state.Id);
        if (uniqueState)
        {
            _states[state.Id] = state;
        }
        else
        {
            throw new ArgumentException($"Input state does not have a unique id (id: {state.Id})");
        }
    }

    public bool SetState(Enum stateID)
    {
        IState oldState = State;
        IState newState;
        bool validState = _states.TryGetValue(stateID, out newState) && (oldState == null || oldState.Id != newState.Id);
        if (validState)
        {
            oldState?.OnLeave?.Invoke();
            newState?.OnEnter?.Invoke();
            State = newState;
        }
        return validState;
    }

    public void Initialize(Enum stateId)
    {
        _initialized = SetState(stateId);
        if (!_initialized)
        {
            throw new ArgumentException($"stateId {stateId} does not correspond to any registered state");
        }
    }

    public void Update()
    {
        if (!_initialized)
        {
            throw new Exception($"StateMachine has not been initialized");
        }
        State?.OnUpdate?.Invoke();
    }
}

public interface IState
{
    Enum Id { get; }
    Action OnUpdate { get; }
    Action OnEnter { get; }
    Action OnLeave { get; }
}

class State : IState
{
    public Enum Id { get; }
    public Action OnUpdate { get; }
    public Action OnEnter { get; }
    public Action OnLeave { get; }
    public State(Enum id, Action onUpdate = null, Action onEnter = null, Action onLeave = null)
    {
        Id = id;
        OnUpdate = onUpdate;
        OnEnter = onEnter;
        OnLeave = onLeave;
    }
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
        Vector3D proj = Projection(a, b);
        Vector3D rej = a - proj;
        return proj - rej * rejectionFactor;
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
        
        if (Vector3D.IsUnit(ref b))
            return a.Dot(b) * b;

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
    /// Computes angle between 2 vectors in radians.
    /// </summary>
    public static double AngleBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }

    /// <summary>
    /// Computes cosine of the angle between 2 vectors.
    /// </summary>
    public static double CosBetween(Vector3D a, Vector3D b)
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
    /// <param name="a">First vector</param>
    /// <param name="b">Second vector</param>
    /// <param name="tolerance">Cosine of maximum angle</param>
    /// <returns></returns>
    public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
    {
        double dot = Vector3D.Dot(a, b);
        double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
        return Math.Abs(dot) * dot > num;
    }
}

public static double GetAllowedRotationAngle(double desiredDelta, IMyMotorStator rotor)
{
    double desiredAngle = rotor.Angle + desiredDelta;
    if ((desiredAngle < rotor.LowerLimitRad && desiredAngle + MathHelper.TwoPi < rotor.UpperLimitRad)
        || (desiredAngle > rotor.UpperLimitRad && desiredAngle - MathHelper.TwoPi > rotor.LowerLimitRad))
    {
        return -Math.Sign(desiredDelta) * (MathHelper.TwoPi - Math.Abs(desiredDelta));
    }
    return desiredDelta;
}

public static void AimRotorAtPosition(IMyMotorStator rotor, Vector3D desiredDirection, Vector3D currentDirection, float rotationScale = 1f, float timeStep = 1f/6f)
{
    Vector3D desiredDirectionFlat = VectorMath.Rejection(desiredDirection, rotor.WorldMatrix.Up);
    Vector3D currentDirectionFlat = VectorMath.Rejection(currentDirection, rotor.WorldMatrix.Up);
    double angle = VectorMath.AngleBetween(desiredDirectionFlat, currentDirectionFlat);
    Vector3D axis = Vector3D.Cross(desiredDirection, currentDirection);
    angle *= Math.Sign(Vector3D.Dot(axis, rotor.WorldMatrix.Up));
    angle = GetAllowedRotationAngle(angle, rotor);
    rotor.TargetVelocityRad = rotationScale * (float)angle / timeStep;
}
#endregion
