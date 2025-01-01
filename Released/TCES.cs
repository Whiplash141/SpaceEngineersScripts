
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

public const string
    Version = "1.14.2",
    Date = "2024/12/28",
    IniSectionGeneral = "TCES - General",
    IniKeyGroupNameTag = "Group name tag",
    IniKeySyncGroupNameTag = "Synced group name tag",
    IniKeyAzimuthName = "Azimuth rotor name tag",
    IniKeyElevationName = "Elevation rotor name tag",
    IniKeyAutoRestAngle = "Should auto return to rest angle",
    IniKeyAutoRestDelay = "Auto return to rest angle delay (s)",
    IniKeyAutoDeviation = "Auto compute deviation angle",
    IniKeyDrawTitleScreen = "Draw title screen",
    IniSectionRotor = "TCES - Rotor",
    IniKeyRestAngle = "Rest angle (deg)",
    IniKeyRestSpeed = "Rest speed multiplier",
    IniKeyEnableStabilization = "Enable stabilization";

RuntimeTracker _runtimeTracker;
long _runCount = 0;

ConfigSection _config = new ConfigSection(IniSectionGeneral);
ConfigString _groupNameTag = new ConfigString(IniKeyGroupNameTag, "TCES");
ConfigString _syncGroupNameTag = new ConfigString(IniKeySyncGroupNameTag, "SYNC");
public ConfigString AzimuthName = new ConfigString(IniKeyAzimuthName, "Azimuth");
public ConfigString ElevationName = new ConfigString(IniKeyElevationName, "Elevation");
public ConfigBool AutomaticRest = new ConfigBool(IniKeyAutoRestAngle, true);
public ConfigFloat AutomaticRestDelay = new ConfigFloat(IniKeyAutoRestDelay, 2f);
public ConfigBool AutomaticDeviationAngle = new ConfigBool(IniKeyAutoDeviation, true);
ConfigBool _drawTitleScreen = new ConfigBool(IniKeyDrawTitleScreen, true);

TCESTitleScreen _titleScreen;

GridConnectionSolver ConnectionSolver { get; } = new GridConnectionSolver();
List<TCESTurret> _tcesTurrets = new List<TCESTurret>();
List<TCESSynced> _syncedTurrets = new List<TCESSynced>();
Dictionary<IMyTurretControlBlock, TCESTurret> _tcesTurretMap = new Dictionary<IMyTurretControlBlock, TCESTurret>();

MyIni _ini = new MyIni();

class InvertibleRotor
{
    public IMyMotorStator Rotor;
    public float Multiplier { get; }

    public InvertibleRotor(IMyMotorStator rotor, bool invert)
    {
        Rotor = rotor;
        Multiplier = invert ? -1f : 1f;
    }
}

class TCESTurret
{
    class ConfigMinMax : ConfigValue<Vector2>
    {
        public float Min
        {
            get
            {
                return (float)Math.Min(Value.X, Value.Y);
            }
        }

        public float Max
        {
            get
            {
                return (float)Math.Max(Value.X, Value.Y);
            }
        }

        public ConfigMinMax(string name, Vector2 value = default(Vector2), string comment = null) : base(name, value, comment) { }

        protected override bool SetValue(ref MyIniValue val)
        {
            // Source formatting example: {min:-60 max:30}
            string source = val.ToString("");
            int xIndex = source.IndexOf("min:");
            int yIndex = source.IndexOf("max:");
            int closingBraceIndex = source.IndexOf("}");
            if (xIndex == -1 || yIndex == -1 || closingBraceIndex == -1)
            {
                SetDefault();
                return false;
            }

            Vector2 vec = default(Vector2);
            string xStr = source.Substring(xIndex + 4, yIndex - (xIndex + 4));
            if (!float.TryParse(xStr, out vec.X))
            {
                SetDefault();
                return false;
            }
            string yStr = source.Substring(yIndex + 4, closingBraceIndex - (yIndex + 4));
            if (!float.TryParse(yStr, out vec.Y))
            {
                SetDefault();
                return false;
            }
            _value = vec;
            return true;
        }

        public override string ToString()
        {
            return $"{{min:{Min} max:{Max}}}";
        }
    }

    class WeaponDeadzone
    {
        public ConfigMinMax _azimuthDeadzoneRange = new ConfigMinMax("Azimuth angle range (deg)");
        public ConfigMinMax _elevationDeadzoneRange = new ConfigMinMax("Elevation angle range (deg)");

        readonly string _sectionName;

        public WeaponDeadzone(int index)
        {
            _sectionName = $"TCES - Weapon Deadzone {index + 1}";
        }

        public void Update(MyIni ini, IMyMotorStator azimuth, IMyMotorStator elevation)
        {
            if (azimuth != null)
            {
                _azimuthDeadzoneRange.Update(ini, _sectionName);
            }
            else
            {
                ini.Delete(_sectionName, _azimuthDeadzoneRange.Name);
            }

            if (elevation != null)
            {
                _elevationDeadzoneRange.Update(ini, _sectionName);
            }
            else
            {
                ini.Delete(_sectionName, _elevationDeadzoneRange.Name);
            }
        }

        bool WithinDeadzone(IMyMotorStator rotor, float min, float max)
        {
            if (max <= min)
            {
                return false;
            }

            float angleDeg = MathHelper.ToDegrees(rotor.Angle);

            float delta = 0;
            if (angleDeg < min)
            {
                delta = min - angleDeg;
            }
            else if (angleDeg > max)
            {
                delta = max - angleDeg;
            }

            float wraps = (float)Math.Round(delta / 360f);
            float wrappedAngle = angleDeg + wraps * 360f;

            return wrappedAngle > min && wrappedAngle < max;
        }

        public bool ShouldSafe(IMyMotorStator azimuth, IMyMotorStator elevation)
        {
            bool shouldSafe = true;

            if (azimuth != null)
            {
                shouldSafe &= WithinDeadzone(azimuth, _azimuthDeadzoneRange.Min, _azimuthDeadzoneRange.Max);
            }

            if (elevation != null)
            {
                shouldSafe &= WithinDeadzone(elevation, _elevationDeadzoneRange.Min, _elevationDeadzoneRange.Max);
            }

            return shouldSafe;
        }
    }

    class DeadzoneConfig : ConfigSection
    {
        public ConfigInt DeadzoneCount = new ConfigInt("Weapon deadzone count", 0);
        public List<WeaponDeadzone> Deadzones = new List<WeaponDeadzone>();
        public DeadzoneConfig() : base("TCES - Weapon Deadzone Config")
        {
            AddValues(DeadzoneCount);
        }
    }

    class RotorConfig : ConfigSection
    {
        private ConfigNullable<float> _restAngleDeg = new ConfigNullable<float>(new ConfigFloat(IniKeyRestAngle), "none");
        public ConfigFloat RestSpeedRatio = new ConfigFloat(IniKeyRestSpeed, 1f);
        public ConfigBool EnableStabilization = new ConfigBool(IniKeyEnableStabilization, true);

        public float? RestAngleRad
        {
            get
            {
                if (_restAngleDeg.HasValue)
                {
                    return MathHelper.ToRadians(_restAngleDeg.Value);
                }
                return null;
            }
        }

        public RotorConfig() : base(IniSectionRotor)
        {
            AddValues(
                _restAngleDeg,
                RestSpeedRatio,
                EnableStabilization
            );
        }
    }

    const float RotorStopThresholdRad = 1f * (MathHelper.Pi / 180f);
    List<IMyFunctionalBlock> _tools = new List<IMyFunctionalBlock>();
    List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
    List<IMyFunctionalBlock> _mainTools = new List<IMyFunctionalBlock>();
    List<IMyFunctionalBlock> _otherTools = new List<IMyFunctionalBlock>();

    List<IMyMotorStator> _extraRotors = new List<IMyMotorStator>();
    List<InvertibleRotor> _controlledExtraRotors = new List<InvertibleRotor>();
    List<IMyMotorStator> _unusedRotors = new List<IMyMotorStator>();
    StabilizedRotor _azimuthStabilizer = new StabilizedRotor();
    StabilizedRotor _elevationStabilizer = new StabilizedRotor();

    public IMyMotorStator AzimuthRotor => _azimuthStabilizer.Rotor;
    public IMyMotorStator ElevationRotor => _elevationStabilizer.Rotor;
    public IMyTurretControlBlock TurretController => _controller;

    DeadzoneConfig _deadzoneConfig = new DeadzoneConfig();
    RotorConfig _azimuthConfig = new RotorConfig();
    RotorConfig _elevationConfig = new RotorConfig();
    List<IMyTurretControlBlock> _foundControllers = new List<IMyTurretControlBlock>();
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
    bool _wasSafed = false;

    const float RestSpeed = 10f;
    const float PlayerInputMultiplier = 1f / 50f; // Magic number from: SpaceEngineers.ObjectBuilders.ObjectBuilders.Definitions.MyObjectBuilder_TurretControlBlockDefinition.PlayerInputDivider

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

    public TCESTurret(Program p, IMyBlockGroup group)
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

        _p = p;
        _group = group;
        _groupName = group.Name;
        Setup();
        SetBlocks();

        if (ElevationRotor != null)
        {
            _cachedElevationBrakingTorque = ElevationRotor.BrakingTorque;
        }
    }

    public void GoToRest()
    {
        if (_controller != null && !IsActive)
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
        if (BlockValid(AzimuthRotor))
        {
            _controller.AzimuthRotor = AzimuthRotor;
            AzimuthRotor.TargetVelocityRad = 0;
        }

        if (BlockValid(ElevationRotor))
        {
            _controller.ElevationRotor = ElevationRotor;
            ElevationRotor.TargetVelocityRad = 0;
        }

        foreach (var r in _controlledExtraRotors)
        {
            r.Rotor.TargetVelocityRad = 0f;
        }
    }

    void OnUpdateManualControl()
    {
        _controller.AzimuthRotor = null;
        if (BlockValid(AzimuthRotor))
        {
            if (_azimuthConfig.EnableStabilization)
            {
                _azimuthStabilizer.Update(1f / 60f);
            }

            AzimuthRotor.TargetVelocityRPM =
                MouseInputToRotorVelocityRpm(_controller.RotationIndicator.Y, _controller.VelocityMultiplierAzimuthRpm, AzimuthRotor) +
                (_azimuthConfig.EnableStabilization && _wasManuallyControlled ? _azimuthStabilizer.Velocity : 0);
        }

        _controller.ElevationRotor = null;
        if (BlockValid(ElevationRotor))
        {
            if (_elevationConfig.EnableStabilization)
            {
                _elevationStabilizer.Update(1f / 60f);
            }

            ElevationRotor.TargetVelocityRPM =
                MouseInputToRotorVelocityRpm(_controller.RotationIndicator.X, _controller.VelocityMultiplierElevationRpm, ElevationRotor) +
                (_elevationConfig.EnableStabilization && _wasManuallyControlled ? _elevationStabilizer.Velocity : 0);
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

        Vector3D targetDirection = info.HitPosition.Value - aimRef.WorldMatrix.Translation;

        if (AzimuthRotor == null)
        {
            _aiControlSM.SetState(AiTargetingState.BothRotors);
        }
        else
        {
            Vector3D flattenedAimDirection = VectorMath.Rejection(aimRef.WorldMatrix.Forward, AzimuthRotor.WorldMatrix.Up);
            Vector3D flattenedTargetDirection = VectorMath.Rejection(targetDirection, AzimuthRotor.WorldMatrix.Up);
            double cosBtwn = VectorMath.CosBetween(flattenedAimDirection, flattenedTargetDirection);
            if (cosBtwn > 0.7071)
            {
                _aiControlSM.SetState(AiTargetingState.BothRotors);
            }
            else
            {
                _aiControlSM.SetState(AiTargetingState.AzimuthOnly);
            }
        }

        if (_p.AutomaticDeviationAngle)
        {
            double targetRadSq = info.BoundingBox.HalfExtents.LengthSquared() * 0.25;
            double distSq = targetDirection.LengthSquared();

            double angleRad = Math.Atan(Math.Sqrt(targetRadSq / distSq));

            _controller.AngleDeviation = (float)MathHelper.ToDegrees(angleRad);
        }
    }

    void OnAiControlBothRotors()
    {
        if (BlockValid(ElevationRotor))
        {
            ElevationRotor.BrakingTorque = _cachedElevationBrakingTorque;
            ElevationRotor.Enabled = true;
        }
    }

    void OnAiControlAzimuthOnly()
    {
        if (BlockValid(ElevationRotor))
        {
            ElevationRotor.BrakingTorque = ElevationRotor.Torque;
            ElevationRotor.Enabled = false;
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
        EvaluateDeadzones();

        if (_controlledExtraRotors.Count > 0)
        {
            HandleExtraRotors();
            SyncWeaponFiring();
        }
    }

    void EvaluateDeadzones()
    {
        bool safed = false;

        foreach (var deadzone in _deadzoneConfig.Deadzones)
        {
            safed = deadzone.ShouldSafe(AzimuthRotor, ElevationRotor);

            if (safed)
            {
                break;
            }
        }

        if (safed == _wasSafed)
        {
            return;
        }

        _wasSafed = safed;

        foreach (var b in _tools)
        {
            var gun = b as IMyUserControllableGun;
            if (gun != null)
            {
                gun.Enabled = !safed;
            }
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
        _foundControllers.Clear();
        _extraRotors.Clear();
        _tools.Clear();
        _cameras.Clear();
        _gridToToolDict.Clear();

        _group.GetBlocks(null, CollectBlocks);

        _controlledExtraRotors.Clear();
        _unusedRotors.Clear();
        _otherTools.Clear();
        _mainTools.Clear();

        foreach (var r in _extraRotors)
        {
            if (r.TopGrid != null && _gridToToolDict.ContainsKey(r.TopGrid))
            {
                _controlledExtraRotors.Add(new InvertibleRotor(r, false));
            }
            else if (r.TopGrid != null && _gridToToolDict.ContainsKey(r.CubeGrid))
            {
                _controlledExtraRotors.Add(new InvertibleRotor(r, true));
            }
            else
            {
                _unusedRotors.Add(r);
            }
        }

        if (_foundControllers.Count == 0)
        {
            _setupReturnCode |= ReturnCode.MissingController;
        }
        else
        {
            _controller = _foundControllers[0];
            ParseCTCIni(_controller);

            if (_foundControllers.Count > 1)
            {
                _setupReturnCode |= ReturnCode.MultipleTurretControllers;
            }


            _controller.ClearTools();

            foreach (var t in _tools)
            {
                if (BlockValid(t))
                {
                    if (_controlledExtraRotors.Count > 0) // Special behavior
                    {

                        if ((AzimuthRotor != null && AzimuthRotor.IsAttached && (t.CubeGrid == AzimuthRotor.TopGrid || t.CubeGrid == AzimuthRotor.CubeGrid)) ||
                            (ElevationRotor != null && ElevationRotor.IsAttached && (t.CubeGrid == ElevationRotor.TopGrid || t.CubeGrid == ElevationRotor.CubeGrid)))
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
                        _mainTools.Add(t);
                        _controller.AddTool(t);
                    }
                }
            }

            var reference = _controller.GetDirectionSource();
            _azimuthStabilizer.IsInverted = DetermineIfRotorInverted(reference, AzimuthRotor, _p.ConnectionSolver);
            _elevationStabilizer.IsInverted = DetermineIfRotorInverted(reference, ElevationRotor, _p.ConnectionSolver);
        }
        if (AzimuthRotor == null)
        {
            _setupReturnCode |= ReturnCode.MissingAzimuth;
        }
        if (ElevationRotor == null)
        {
            _setupReturnCode |= ReturnCode.MissingElevation;
        }
        if (_controlledExtraRotors.Count == 0)
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

    public static bool DetermineIfRotorInverted(IMyTerminalBlock reference, IMyMotorStator rotor, GridConnectionSolver solver)
    {
        if (reference != null && rotor != null)
        {
            if (rotor.TopGrid != null)
            {
                GridConnectionSolver.GridNode node;
                if (solver.FindConnectionBetween(reference.CubeGrid, (g) => g == rotor.CubeGrid || g == rotor.TopGrid, out node))
                {
                    if (node.Grid == rotor.CubeGrid)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }


    void PreparseIni(IMyTerminalBlock b)
    {
        _ini.Clear();
        if (!_ini.TryParse(b.CustomData) && !string.IsNullOrWhiteSpace(b.CustomData))
        {
            _ini.EndContent = b.CustomData;
        }
    }

    void PostparseIni(IMyTerminalBlock b)
    {
        string output = _ini.ToString();
        if (output != b.CustomData)
        {
            b.CustomData = output;
        }
    }

    void ParseRotorIni(IMyMotorStator r, RotorConfig config)
    {
        PreparseIni(r);

        config.Update(_ini);

        PostparseIni(r);
    }

    void ParseCTCIni(IMyTurretControlBlock tcb)
    {
        PreparseIni(tcb);

        _deadzoneConfig.Update(_ini);

        _deadzoneConfig.Deadzones.Clear();
        for (int ii = 0; ii < _deadzoneConfig.DeadzoneCount; ++ii)
        {
            var deadzone = new WeaponDeadzone(ii);
            deadzone.Update(_ini, AzimuthRotor, ElevationRotor);
            _deadzoneConfig.Deadzones.Add(deadzone);
        }

        PostparseIni(tcb);
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
                if (AzimuthRotor != null)
                {
                    _extraRotors.Add(rotor);
                }
                else
                {
                    _azimuthStabilizer.Rotor = rotor;
                    ParseRotorIni(AzimuthRotor, _azimuthConfig);
                }
            }
            else if (StringExtensions.Contains(b.CustomName, _p.ElevationName))
            {
                if (ElevationRotor != null)
                {
                    _extraRotors.Add(rotor);
                }
                else
                {
                    _elevationStabilizer.Rotor = rotor;
                    ParseRotorIni(ElevationRotor, _elevationConfig);
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
            _foundControllers.Add(tcb);
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
        return (b != null) && _p.Me.IsSameConstructAs(b);
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
            if (BlockValid(AzimuthRotor))
            {
                _controller.AzimuthRotor = AzimuthRotor;
            }
            if (BlockValid(ElevationRotor))
            {
                _controller.ElevationRotor = ElevationRotor;
            }
        }

        _controller.ClearTools();
        foreach (var t in _mainTools)
        {
            if (BlockValid(t))
            {
                _controller.AddTool(t); 
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
            for (int ii = 0; ii < _foundControllers.Count; ++ii)
            {
                Echo($"    {ii + 1}: \"{_foundControllers[ii].CustomName}\"");
            }
        }

        if (!missingRotors)
        {
            if ((_setupReturnCode & ReturnCode.MissingAzimuth) != 0)
            {
                Echo("> INFO: No azimuth rotor.");
            }
            else
            {
                Echo($"> INFO: Azimuth inverted: {_azimuthStabilizer.IsInverted}");
            }

            if ((_setupReturnCode & ReturnCode.MissingElevation) != 0)
            {
                Echo("> INFO: No elevation rotor.");
            }
            else
            {
                Echo($"> INFO: Elevation inverted: {_elevationStabilizer.IsInverted}");
            }
        }

        if ((_setupReturnCode & ReturnCode.NoExtraRotors) != 0)
        {
            Echo("> INFO: No extra rotors.");
        }
        else
        {
            Echo($"> INFO: {_controlledExtraRotors.Count} extra rotor(s).");
        }

        if (_unusedRotors.Count > 0)
        {
            Echo($"> WARN: {_unusedRotors.Count} unused rotor(s).");
            for (int ii = 0; ii < _unusedRotors.Count; ++ii)
            {
                Echo($"    {ii + 1}: \"{_unusedRotors[ii].CustomName}\"");
            }
        }

        if (!missingTools)
        {
            if ((_setupReturnCode & ReturnCode.MissingCamera) != 0)
            {
                Echo("> INFO: No camera.");
            }
            else
            {
                Echo($"> INFO: {_cameras.Count} camera(s).");
            }
            if ((_setupReturnCode & ReturnCode.MissingTools) != 0)
            {
                Echo("> INFO: No tools.");
            }
            else
            {
                Echo($"> INFO: {_tools.Count} weapon(s)/tool(s).");
            }
        }
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
        bool done = TryMoveRotorToRestAngle(AzimuthRotor, _azimuthConfig.RestAngleRad, _azimuthConfig.RestSpeedRatio);
        if (done)
        {
            if (BlockValid(AzimuthRotor))
            {
                _controller.AzimuthRotor = AzimuthRotor;
            }
            done = TryMoveRotorToRestAngle(ElevationRotor, _elevationConfig.RestAngleRad, _elevationConfig.RestSpeedRatio);
            if (done)
            {
                if (BlockValid(ElevationRotor))
                {
                    _controller.ElevationRotor = ElevationRotor;
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
            if (BlockValid(ElevationRotor))
            {
                _controller.ElevationRotor = ElevationRotor;
            }
        }
        return done;
    }

    bool TryMoveRotorToRestAngle(IMyMotorStator r, float? restAngleRad, float restSpeed)
    {
        if (!BlockValid(r) || !restAngleRad.HasValue)
        {
            return true;
        }
        return MoveRotorToEquilibrium(r, restAngleRad.Value, restSpeed);
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

    public Vector3D TotalCommandedVelocity
    {
        get
        {
            Vector3D totalVelocityCommand = Vector3D.Zero;
            if (AzimuthRotor != null)
            {
                totalVelocityCommand += _azimuthStabilizer.RotationAxis * AzimuthRotor.TargetVelocityRad;
            }
            if (ElevationRotor != null)
            {
                totalVelocityCommand += _elevationStabilizer.RotationAxis * ElevationRotor.TargetVelocityRad;
            }

            return totalVelocityCommand;
        }
    }

    void HandleExtraRotors()
    {
        if (_controlledExtraRotors.Count == 0)
        {
            return;
        }

        var directionSource = _controller.GetDirectionSource();
        if (directionSource == null)
        {
            return;
        }

        Vector3D totalVelocityCommand = TotalCommandedVelocity;

        foreach (var r in _controlledExtraRotors)
        {
            if (!r.Rotor.IsAttached)
            {
                continue;
            }

            IMyFunctionalBlock reference;
            if (r.Rotor.TopGrid == null || !_gridToToolDict.TryGetValue(r.Rotor.TopGrid, out reference))
            {
                if (!_gridToToolDict.TryGetValue(r.Rotor.CubeGrid, out reference))
                {
                    // Not theoretically possible, but just to be safe
                    continue;
                }
            }

            AimRotorAtPosition(r.Rotor, _controller.GetShootDirection(), reference.WorldMatrix.Forward, r.Multiplier);
            float commandedVelocity = (float)Vector3D.Dot(totalVelocityCommand, r.Rotor.WorldMatrix.Up * r.Multiplier);
            r.Rotor.TargetVelocityRad += commandedVelocity;
        }
    }

    bool MoveRotorToEquilibrium(IMyMotorStator rotor, float restAngleRad, float restSpeed)
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
            if (restAngleRad > upperLimitRad)
            {
                restAngleRad -= MathHelper.TwoPi;
            }
            else if (restAngleRad < lowerLimitRad)
            {
                restAngleRad += MathHelper.TwoPi;
            }
        }
        else
        {
            if (restAngleRad > currentAngle + MathHelper.Pi)
            {
                restAngleRad -= MathHelper.TwoPi;
            }
            else if (restAngleRad < currentAngle - MathHelper.Pi)
            {
                restAngleRad += MathHelper.TwoPi;
            }
        }

        float angularDeviation = (restAngleRad - currentAngle);
        float targetVelocity = (float)Math.Round(angularDeviation * RestSpeed, 2) * restSpeed;

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

class TCESSynced
{
    Program _program;
    IMyBlockGroup _group;
    IMyTurretControlBlock _controller;
    List<IMyTurretControlBlock> _foundControllers = new List<IMyTurretControlBlock>();
    List<InvertibleRotor> _syncedRotors = new List<InvertibleRotor>();
    List<IMyMotorStator> _stagedRotors = new List<IMyMotorStator>();
    List<IMyFunctionalBlock> _syncedTools = new List<IMyFunctionalBlock>();
    List<IMyFunctionalBlock> _controllerTools = new List<IMyFunctionalBlock>();
    IMyMotorStator ParentAzimuthRotor;
    IMyMotorStator ParentElevationRotor;
    const float VelocityMultiplier = 1f / 6f;
    bool _tcesTurretFound = false;
    bool _invertAzimuth = false;
    bool _invertElevation = false;
    TCESTurret _tcesTurret = null;

    IMyTerminalBlock AimReference
    {
        get
        {
            return _syncedTools.Count == 0 ? null : _syncedTools[0];
        }
    }

    enum ReturnCode
    {
        None = 0,
        NoSyncedRotors = 1 << 0,
        NoSyncedTools = 1 << 1,
        NoCTC = 1 << 2,
        MultipleCTCs = 1 << 3,
        NoDirectionSource = 1 << 3,
        SetupError = NoSyncedRotors | NoSyncedTools | NoCTC,
    }

    ReturnCode _setupCode;
    ReturnCode _updateCode;

    bool _isShooting = true; // True because this will force shoot off when evaluated the first time
    bool IsShooting
    {
        get
        {
            return _isShooting;
        }
        set
        {
            if (value != _isShooting)
            {
                _isShooting = value;

                foreach (var func in _syncedTools)
                {
                    var gun = func as IMyUserControllableGun;
                    if (gun != null)
                    {
                        gun.Shoot = _isShooting;
                    }
                    else if (func is IMyShipToolBase || func is IMyLightingBlock)
                    {
                        func.Enabled = _isShooting;
                    }
                }
            }
        }
    }

    public TCESSynced(Program program, IMyBlockGroup group)
    {
        _program = program;
        _group = group;

        _setupCode = Setup();
    }

    ReturnCode Setup()
    {
        ReturnCode setupCode = ReturnCode.None;

        _controller = null;
        _stagedRotors.Clear();
        _syncedTools.Clear();
        _foundControllers.Clear();

        _group.GetBlocks(null, CollectBlocks);

        _syncedTools.Sort((a, b) => a.CustomName.CompareTo(b.CustomName));



        _syncedRotors.Clear();
        if (_syncedTools.Count == 0)
        {
            setupCode |= ReturnCode.NoSyncedTools;
        }
        else
        {
            var reference = AimReference;
            foreach (var rotor in _stagedRotors)
            {
                GridConnectionSolver.GridNode node;
                bool solved = _program.ConnectionSolver.FindConnectionBetween(reference.CubeGrid, (g) => g == rotor.TopGrid || g == rotor.CubeGrid, out node);
                if (solved)
                {
                    bool inverted = node.Grid == rotor.CubeGrid;
                    _syncedRotors.Add(new InvertibleRotor(rotor, inverted));
                }
            }
        }

        if (_syncedRotors.Count == 0)
        {
            setupCode |= ReturnCode.NoSyncedRotors;
        }

        if (_foundControllers.Count == 0)
        {
            setupCode |= ReturnCode.NoCTC;
        }
        else
        {
            _controller = _foundControllers[0];
            _controller.GetTools(_controllerTools);

            if (_foundControllers.Count > 1)
            {
                setupCode |= ReturnCode.MultipleCTCs;
            }
        }

        return setupCode;
    }

    bool CollectBlocks(IMyTerminalBlock b)
    {
        var tcb = b as IMyTurretControlBlock;
        if (tcb != null)
        {
            _foundControllers.Add(tcb);
        }

        var r = b as IMyMotorStator;
        if (r != null)
        {
            _stagedRotors.Add(r);
        }

        var func = b as IMyFunctionalBlock;
        if (func != null &&
                ((b is IMyUserControllableGun && !(b is IMyLargeTurretBase)) ||
                    b is IMyCameraBlock ||
                    b is IMyShipToolBase ||
                    b is IMyLightingBlock ||
                    b is IMyShipConnector))
        {
            _syncedTools.Add(func);
        }

        return false;
    }

    public void Init(Dictionary<IMyTurretControlBlock, TCESTurret> turretMap)
    {
        if ((_setupCode & ReturnCode.SetupError) != 0)
        {
            return;
        }

        _tcesTurretFound = turretMap.TryGetValue(_controller, out _tcesTurret);
        if (_tcesTurretFound)
        {
            ParentAzimuthRotor = _tcesTurret.AzimuthRotor;
            ParentElevationRotor = _tcesTurret.ElevationRotor;
        }
        else
        {
            ParentAzimuthRotor = _controller.AzimuthRotor;
            ParentElevationRotor = _controller.ElevationRotor;
        }

        var reference = _controller.GetDirectionSource();
        _invertAzimuth = TCESTurret.DetermineIfRotorInverted(reference, ParentAzimuthRotor, _program.ConnectionSolver);
        _invertElevation = TCESTurret.DetermineIfRotorInverted(reference, ParentElevationRotor, _program.ConnectionSolver);
    }

    float GetInversionMultiplier(bool invert) => invert ? -1f : 1f;

    public void Update10()
    {
        _updateCode = ReturnCode.None;

        if ((_setupCode & ReturnCode.SetupError) != 0)
        {
            return;
        }

        IMyTerminalBlock directionSource = _controller.GetDirectionSource();
        if (directionSource == null)
        {
            _updateCode |= ReturnCode.NoDirectionSource;
            return;
        }

        Vector3D totalVelocityCommand = Vector3D.Zero;
        if (_tcesTurretFound)
        {
            totalVelocityCommand = _tcesTurret.TotalCommandedVelocity;
        }
        else
        {
            if (ParentAzimuthRotor != null)
            {
                totalVelocityCommand += GetInversionMultiplier(_invertAzimuth) * ParentAzimuthRotor.WorldMatrix.Up * ParentAzimuthRotor.TargetVelocityRad;
            }
            if (ParentElevationRotor != null)
            {
                totalVelocityCommand += GetInversionMultiplier(_invertElevation) * ParentElevationRotor.WorldMatrix.Up * ParentElevationRotor.TargetVelocityRad;
            }
        }

        IMyTerminalBlock reference = _syncedTools[0];
        foreach (var r in _syncedRotors)
        {
            AimRotorAtPosition(r.Rotor, _controller.GetShootDirection(), reference.WorldMatrix.Forward, r.Multiplier);
            float commandedVelocity = (float)Vector3D.Dot(totalVelocityCommand, r.Rotor.WorldMatrix.Up * r.Multiplier);
            r.Rotor.TargetVelocityRad += commandedVelocity * VelocityMultiplier;
        }

        bool shouldShoot = GetControllerIsShooting();
        IsShooting = shouldShoot;
    }

    bool GetControllerIsShooting()
    {
        foreach (var func in _controllerTools)
        {
            var gun = func as IMyUserControllableGun;
            if (gun != null)
            {
                if (gun.IsShooting)
                {
                    return true;
                }
                continue;
            }

            if (func.Enabled)
            {
                return true;
            }
        }

        return false;
    }

    void Echo(string msg)
    {
        _program.Echo(msg);
    }

    public void WriteStatus()
    {
        Echo(_group.Name);

        if ((_setupCode & ReturnCode.NoCTC) != 0)
        {
            Echo("> ERROR: No custom turret controller found");
        }
        else if ((_setupCode & ReturnCode.MultipleCTCs) != 0)
        {
            Echo("> WARN: Multiple custom turret controllers found");
            for (int ii = 0; ii < _foundControllers.Count; ++ii)
            {
                Echo($"    {ii + 1}: \"{_foundControllers[ii].CustomName}\"");
            }
        }

        if ((_setupCode & ReturnCode.NoSyncedRotors) != 0)
        {
            Echo("> ERROR: No rotors found");
        }
        else
        {
            Echo($"> INFO: {_syncedRotors.Count} synced rotor(s)");
        }

        if ((_setupCode & ReturnCode.NoSyncedTools) != 0)
        {
            Echo("> ERROR: No weapons, tools, or cameras found");
        }
        else
        {
            Echo($"> INFO: {_syncedTools.Count} synced weapons, tools, and/or cameras");
            Echo($"> INFO: Using \"{_syncedTools[0].CustomName}\" as aim reference");
        }

        if ((_updateCode & ReturnCode.NoDirectionSource) != 0)
        {
            Echo($"> ERROR: No direction source for CTC \"{_controller}\"");
        }

        if (_tcesTurretFound)
        {
            Echo("> INFO: Found TCES turret");
        }
    }
}

Program()
{
    try
    {
        _config.AddValues(
            _groupNameTag,
            _syncGroupNameTag,
            AzimuthName,
            ElevationName,
            AutomaticRest,
            AutomaticRestDelay,
            AutomaticDeviationAngle,
            _drawTitleScreen
        );

        Runtime.UpdateFrequency = UpdateFrequency.Update10;

        Setup();
        _titleScreen = new TCESTitleScreen(Version, this);

        _runtimeTracker = new RuntimeTracker(this);
    }
    catch (Exception e)
    {
        BlueScreenOfDeath.Show(Me.GetSurface(0), "TCES", Version, e);
        throw;
    }
}

void Setup()
{
    _tcesTurrets.Clear();
    _syncedTurrets.Clear();
    _tcesTurretMap.Clear();

    ProcessIni();
    ConnectionSolver.Initialize(GridTerminalSystem);
    GridTerminalSystem.GetBlockGroups(null, CollectGroups);

    foreach (var s in _syncedTurrets)
    {
        s.Init(_tcesTurretMap);
    }
}

void ProcessIni()
{
    _ini.Clear();
    if (!_ini.TryParse(Me.CustomData) && !string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _config.Update(_ini);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

bool CollectGroups(IMyBlockGroup g)
{
    if (g.Name.Contains(_groupNameTag))
    {
        var tcesTurret = new TCESTurret(this, g);
        _tcesTurrets.Add(tcesTurret);
        if (tcesTurret.TurretController != null)
        {
            _tcesTurretMap[tcesTurret.TurretController] = tcesTurret;
        }
    }
    else if (g.Name.Contains(_syncGroupNameTag))
    {
        _syncedTurrets.Add(new TCESSynced(this, g));
    }
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
                foreach (var c in _tcesTurrets)
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
        throw;
    }
}

void OnUpdate1()
{
    foreach (var c in _tcesTurrets)
    {
        c.Update1();
    }
}

void OnUpdate10()
{
    bool needsFastUpdate = false;
    foreach (var c in _tcesTurrets)
    {
        c.Update10();
        needsFastUpdate |= c.IsManuallyControlled;
    }

    foreach (var s in _syncedTurrets)
    {
        s.Update10();
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
    Echo($"TCES groups: {_tcesTurrets.Count}");
    Echo($"SYNC groups: {_syncedTurrets.Count}");
    Echo($"Run frequency: {Runtime.UpdateFrequency}\n");

    Echo(_runtimeTracker.Write());
    Echo("");

    foreach (var controller in _tcesTurrets)
    {
        controller.WriteStatus();
        Echo("");
    }

    foreach (var synced in _syncedTurrets)
    {
        synced.WriteStatus();
        Echo("");
    }

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
    const float TextSize = 1.5f;
    const float CTCSpriteScale = 0.4f;
    const float TurretSpriteScale = 0.8f;
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

    readonly AnimationParams[] _animSequence = new AnimationParams[]
    {
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

        var frame = _surface.DrawFrame();

        if (_clearSpriteCache)
        {
            frame.Add(new MySprite());
        }

        DrawCTC(frame, screenCenter + _ctcPos * minScale, CTCSpriteScale * minScale);
        DrawTurret(frame, screenCenter + _turretPos * minScale, TurretSpriteScale * minScale, anim.ElevationAngle, anim.DrawMuzzleFlash);
        DrawTitleBar(ref frame, _surface, _topBarColor, _white, _titleText, minScale, TextSize);

        frame.Dispose();
    }

    public void RestartDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    #region Draw Helper Functions
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
    void WriteToIni(MyIni ini, string section);
    bool ReadFromIni(MyIni ini, string section);
    bool Update(MyIni ini, string section);
    void Reset();
    string Name { get; set; }
    string Comment { get; set; }
}

public interface IConfigValue<T> : IConfigValue
{
    T Value { get; set; }
}

public abstract class ConfigValue<T> : IConfigValue<T>
{
    public string Name { get; set; }
    public string Comment { get; set; }
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
    protected T DefaultValue => _defaultValue;
    bool _skipRead = false;

    public static implicit operator T(ConfigValue<T> cfg)
    {
        return cfg.Value;
    }

    protected virtual void InitializeValue()
    {
        _value = default(T);
    }

    public ConfigValue(string name, T defaultValue, string comment)
    {
        Name = name;
        InitializeValue();
        _defaultValue = defaultValue;
        Comment = comment;
        SetDefault();
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    public bool Update(MyIni ini, string section)
    {
        bool read = ReadFromIni(ini, section);
        WriteToIni(ini, section);
        return read;
    }

    public bool ReadFromIni(MyIni ini, string section)
    {
        if (_skipRead)
        {
            _skipRead = false;
            return true;
        }
        MyIniValue val = ini.Get(section, Name);
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

    public void WriteToIni(MyIni ini, string section)
    {
        ini.Set(section, Name, this.ToString());
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            ini.SetComment(section, Name, Comment);
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

public class ConfigBool : ConfigValue<bool>
{
    public ConfigBool(string name, bool value = false, string comment = null) : base(name, value, comment) { }
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
    public ConfigFloat(string name, float value = 0, string comment = null) : base(name, value, comment) { }
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

public class ConfigInt : ConfigValue<int>
{
    public ConfigInt(string name, int value = 0, string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetInt32(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigNullable<T> : IConfigValue<T> where T : struct
{
    public string Name
    {
        get { return _impl.Name; }
        set { _impl.Name = value; }
    }

    public string Comment
    {
        get { return _impl.Comment; }
        set { _impl.Comment = value; }
    }

    public string NullString;

    public T Value
    {
        get { return _impl.Value; }
        set
        {
            _impl.Value = value;
            HasValue = true;
            _skipRead = true;
        }
    }
    
    public bool HasValue { get; private set; }
    readonly IConfigValue<T> _impl;
    bool _skipRead = false;

    public ConfigNullable(IConfigValue<T> impl, string nullString = "none")
    {
        _impl = impl;
        NullString = nullString;
        HasValue = false;
    }

    public void Reset()
    {
        HasValue = false;
        _skipRead = true;
    }

    public bool ReadFromIni(MyIni ini, string section)
    {
        if (_skipRead)
        {
            _skipRead = false;
            return true;
        }
        bool read = _impl.ReadFromIni(ini, section);
        if (read)
        {
            HasValue = true;
        }
        else
        {
            HasValue = false;
        }
        return read;
    }

    public void WriteToIni(MyIni ini, string section)
    {
        _impl.WriteToIni(ini, section);
        if (!HasValue)
        {
            ini.Set(section, _impl.Name, NullString);
        }
    }

    public bool Update(MyIni ini, string section)
    {
        bool read = ReadFromIni(ini, section);
        WriteToIni(ini, section);
        return read;
    }

    public override string ToString()
    {
        return HasValue ? Value.ToString() : NullString;
    }
}

public class ConfigSection
{
    public string Section { get; set; }
    public string Comment { get; set; }
    List<IConfigValue> _values = new List<IConfigValue>();

    public ConfigSection(string section, string comment = null)
    {
        Section = section;
        Comment = comment;
    }

    public void AddValue(IConfigValue value)
    {
        _values.Add(value);
    }

    public void AddValues(List<IConfigValue> values)
    {
        _values.AddRange(values);
    }

    public void AddValues(params IConfigValue[] values)
    {
        _values.AddRange(values);
    }

    void SetComment(MyIni ini)
    {
        if (!string.IsNullOrWhiteSpace(Comment))
        {
            ini.SetSectionComment(Section, Comment);
        }
    }

    public void ReadFromIni(MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.ReadFromIni(ini, Section);
        }
    }

    public void WriteToIni(MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.WriteToIni(ini, Section);
        }
        SetComment(ini);
    }

    public void Update(MyIni ini)
    {    
        foreach (IConfigValue c in _values)
        {
            c.Update(ini, Section);
        }
        SetComment(ini);
    }
}
public class ConfigString : ConfigValue<string>
{
    public ConfigString(string name, string value = "", string comment = null) : base(name, value, comment) { }
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

public class ConfigVector2 : ConfigValue<Vector2>
{
    public ConfigVector2(string name, Vector2 value = default(Vector2), string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        // Source formatting example: {X:2.75 Y:-14.4}
        string source = val.ToString("");
        int xIndex = source.IndexOf("X:");
        int yIndex = source.IndexOf("Y:");
        int closingBraceIndex = source.IndexOf("}");
        if (xIndex == -1 || yIndex == -1 || closingBraceIndex == -1)
        {
            SetDefault();
            return false;
        }

        Vector2 vec = default(Vector2);
        string xStr = source.Substring(xIndex + 2, yIndex - (xIndex + 2));
        if (!float.TryParse(xStr, out vec.X))
        {
            SetDefault();
            return false;
        }
        string yStr = source.Substring(yIndex + 2, closingBraceIndex - (yIndex + 2));
        if (!float.TryParse(yStr, out vec.Y))
        {
            SetDefault();
            return false;
        }
        _value = vec;
        return true;
    }
}

/// <summary>
/// Simple class for determing the shortest sequence of mechanical connections between a grid and a desired target
/// </summary>
public class GridConnectionSolver
{
    Dictionary<IMyCubeGrid, List<IMyCubeGrid>> _gridPeerMap = new Dictionary<IMyCubeGrid, List<IMyCubeGrid>> ();
    Queue<GridNode> _queue = new Queue<GridNode>();
    List<IMyCubeGrid> _explored = new List<IMyCubeGrid>();

    public class GridNode
    {
        public IMyCubeGrid Grid { get; }
        public GridNode Parent { get; set; }

        public GridNode(IMyCubeGrid grid, GridNode parent = null)
        {
            Grid = grid;
            Parent = parent;
        }
    }

    void AddGridLinkage(IMyCubeGrid from, IMyCubeGrid to)
    {
        List<IMyCubeGrid> peers;
        if (!_gridPeerMap.TryGetValue(from, out peers))
        {
            peers = new List<IMyCubeGrid>();
            _gridPeerMap[from] = peers;
        }

        if (!peers.Contains(to))
        {
            peers.Add(to);
        }
    }

    /// <summary>
    /// Initializes the solver with a map of the current mechanical connections
    /// </summary>
    /// <param name="gts">GridTerminalSystem instance</param>
    public void Initialize(IMyGridTerminalSystem gts)
    {
        _gridPeerMap.Clear();

        gts.GetBlocksOfType<IMyMechanicalConnectionBlock>(null, mech =>
        {
            if (mech.TopGrid != null)
            {
                AddGridLinkage(mech.TopGrid, mech.CubeGrid);
                AddGridLinkage(mech.CubeGrid, mech.TopGrid);
            }

            return false;
        });
    }

    /// <summary>
    /// Breadth-first search of grid connection tree
    /// </summary>
    /// <param name="start">Grid to begin from</param>
    /// <param name="evaluate">Function to evaluate if traversal is successful</param>
    /// <param name="endNode">End node that can be traversed back to start</param>
    /// <returns>Boolean indicating if a path was found</returns>
    public bool FindConnectionBetween(IMyCubeGrid start, Func<IMyCubeGrid, bool> evaluate, out GridNode endNode)
    {
        endNode = null;
        _queue.Clear();
        _explored.Clear();

        var startNode = new GridNode(start);
        _queue.Enqueue(startNode);

        while (_queue.Count > 0)
        {
            GridNode currentNode = _queue.Dequeue();
            if (evaluate.Invoke(currentNode.Grid))
            {
                endNode = currentNode;
                return true;
            }

            _explored.Add(currentNode.Grid);

            List<IMyCubeGrid> peers;
            if (_gridPeerMap.TryGetValue(currentNode.Grid, out peers))
            {
                foreach (IMyCubeGrid peer in peers)
                {
                    if (!_explored.Contains(peer))
                    {
                        var peerNode = new GridNode(peer, currentNode);
                        _queue.Enqueue(peerNode);
                    }
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Class that tracks runtime history.
/// </summary>
public class RuntimeTracker
{
    public int Capacity;
    public double Sensitivity;
    public double MaxRuntime;
    public double MaxInstructions;
    public double AverageRuntime;
    public double AverageInstructions;
    public double LastRuntime;
    public double LastInstructions;
    
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

public class StabilizedRotor
{
    public bool IsInverted { get; set; }
    
    public float Velocity { get; private set; }

    MatrixD _lastOrientation = MatrixD.Identity;

    public Vector3D RotationAxis
    {
        get
        {
            if (IsInverted)
            {
                return _rotor.WorldMatrix.Down;
            }
            else
            {
                return _rotor.WorldMatrix.Up;
            }
        }
    }

    public MatrixD CurrentOrientation
    {
        get
        {
            if (IsInverted)
            {
                return _rotor.TopGrid?.WorldMatrix ?? MatrixD.Identity;
            }
            else
            {
                return _rotor.WorldMatrix;
            }
        }
    }

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
                    _lastOrientation = CurrentOrientation;
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
        var axis = Vector3D.Cross(_lastOrientation.Forward, CurrentOrientation.Forward);
        double mag = axis.Length();
        double angle = Math.Asin(MathHelper.Clamp(mag, -1.0, 1.0));
        axis = mag < 1e-12 ? Vector3D.Zero : axis / mag * angle / dt;
        return Vector3D.Dot(axis, RotationAxis);
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
            _lastOrientation = CurrentOrientation.GetOrientation();
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
            return CurrentState.Id;
        }
    }
    public State CurrentState { get; private set; } = null;

    Dictionary<Enum, State> _states = new Dictionary<Enum, State>();
    bool _initialized = false;

    public void AddStates(params State[] states)
    {
        foreach (State state in states)
        {
            AddState(state);
        }
    }

    public void AddState(State state)
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
        State oldState = CurrentState;
        State newState;
        bool validState = _states.TryGetValue(stateID, out newState) && (oldState == null || oldState.Id != newState.Id);
        if (validState)
        {
            oldState?.OnLeave?.Invoke();
            newState?.OnEnter?.Invoke();
            CurrentState = newState;
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
        CurrentState?.OnUpdate?.Invoke();
    }
}

public class State
{
    public Enum Id { get; }
    public Action OnUpdate;
    public Action OnEnter;
    public Action OnLeave;
    public State(Enum id, Action onUpdate = null, Action onEnter = null, Action onLeave = null)
    {
        Id = id;
        OnUpdate = onUpdate;
        OnEnter = onEnter;
        OnLeave = onLeave;
    }
}

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

public static class VectorMath
{
    /// <summary>
    /// Computes cosine of the angle between 2 vectors.
    /// </summary>
    public static double CosBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        {
            return 0;
        }
        return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
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
    /// Rejects vector a on vector b
    /// </summary>
    public static Vector3D Rejection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    /// <summary>
    /// Computes angle between 2 vectors in radians.
    /// </summary>
    /// <remarks>
    /// This uses atan2 to avoid numerical precision issues associated
    /// with acos based dot-product backsolving.
    /// </remarks>
    public static double AngleBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        {
            return 0;
        }
        return Math.Atan2(Vector3D.Cross(a, b).Length(), Vector3D.Dot(a, b));
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

public static Vector2 DrawTitleBar(ref MySpriteDrawFrame frame, IMyTextSurface surf, Color barColor, Color textColor, string text, float scale, float textSize = 1.5f, float barHeightPx = 64f, float baseTextHeightPx = 28.8f, string font = "Debug")
{
    float titleBarHeight = scale * barHeightPx;
    float scaledTextSize = textSize * scale;
    Vector2 topLeft = 0.5f * (surf.TextureSize - surf.SurfaceSize);
    Vector2 titleBarSize = new Vector2(surf.SurfaceSize.X, titleBarHeight);
    Vector2 titleBarPos = topLeft + titleBarSize * 0.5f;
    Vector2 titleBarTextPos = titleBarPos - Vector2.UnitY * (0.5f * scaledTextSize * baseTextHeightPx);

    frame.Add(new MySprite(
        SpriteType.TEXTURE,
        "SquareSimple",
        titleBarPos,
        titleBarSize,
        barColor,
        null,
        TextAlignment.CENTER));

    frame.Add(new MySprite(
        SpriteType.TEXT,
        text,
        titleBarTextPos,
        null,
        textColor,
        font,
        TextAlignment.CENTER,
        scaledTextSize));

    return titleBarPos;
}
#endregion