
#region This goes in the programmable block
const string VERSION = "121.5.5";
const string DATE = "2021/10/08";

/*
/ //// / Whip's Turret Slaver / //// /
_______________________________________________________________________

README: https://steamcommunity.com/sharedfiles/filedetails/?id=672678005

Read the bloody online instructions. I'm out of space in this script.
Post any questions, suggestions, or issues you have on the workshop page.

FOR THE LOVE OF GOD, DON'T EDIT VARIABLES IN THE CODE!
USE THE CUSTOM DATA OF THE PROGRAMMABLE BLOCK!

_______________________________________________________________________

NOTE:

This code has been compressed and is likely very ugly to try and read in the
script editor.

You can paste this into Visual Studio and it will auto-format it for you, or 
you can use a site like https://codebeautify.org/csharpviewer to uncompress it.

I did not obfuscate my code because I still want it to be readable.

_______________________________________________________________________

- Whiplash141








=================================================
        No touchey anything below here!
=================================================









*/

#region Variables that you should not touch
readonly TurretVariables _defaultVars = new TurretVariables()
{
    ToleranceAngle = 2.5,
    ConvergenceRange = 800,
    EquilibriumRotationSpeed = 10,
    ProportionalGain = 75,
    IntegralGain = 0,
    IntegralDecayRatio = 0.25,
    DerivativeGain = 0,
    MaxRotationSpeed = 60,
    GameMaxSpeed = 100,
    TargetRefreshInterval = 2,
    RotorTurretGroupNameTag = "Turret Group",
    RotorGimbalGroupNameTag = "Gimbal Group",
    AiTurretGroupNameTag = "Slaved Group",
    DesignatorNameTag = "Designator",
    OnlyShootWhenDesignatorShoots = false,
};

class TurretVariables
{
    public double ToleranceAngle;
    public double ConvergenceRange;
    public double EquilibriumRotationSpeed;
    public double ProportionalGain;
    public double IntegralGain;
    public double DerivativeGain;
    public double IntegralDecayRatio;
    public double MaxRotationSpeed;
    public double GameMaxSpeed;
    public double TargetRefreshInterval;
    public string RotorTurretGroupNameTag;
    public string RotorGimbalGroupNameTag;
    public string AiTurretGroupNameTag;
    public string DesignatorNameTag;
    public bool OnlyShootWhenDesignatorShoots;
}

const string IgcTag = "IGC_IFF_MSG";

const string
    IniGeneral = "Turret Slaver - General Parameters",
    IniDrawTitleScreen = "- Draw title screen",
    IniMuzzleVelocity = "- Muzzle velocity (m/s)",
    IniIsRocket = "- Is rocket",
    IniRocketAccel = "- Rocket acceleration (m/s^2)",
    IniRocketInitVel = "- Rocket initial velocity (m/s)",
    IniAvoidFriendly = "- Avoid friendly fire (own ship)",
    IniAvoidFriendlyOtherShips = "- Avoid friendly fire (other ships)",
    IniTolerance = "- Fire tolerance angle (deg)",
    IniConvergence = "- Manual convergence range (m)",
    IniKp = "- Proportional constant",
    IniKi = "- Integral constant",
    IniKd = "- Derivative constant",
    IniMaxRotorSpeed = "- Max rotor speed (rpm)",
    IniIntegralDecayRatio = "- Integral decay ratio",
    IniRestRpm = "- Return to rest position speed (rpm)",
    IniGameMaxSpeed = "- Game max speed (m/s)",
    IniRotorTurretName = "- Rotor turret group name tag",
    IniAiTurretName = "- Slaved AI turret group name tag",
    IniDesignatorName = "- Designator name tag",
    IniEngagementRange = "- Autofire range (m)",
    IniRestTime = "- Return to rest delay (s)",
    IniShootWhenDesignatorDoes = "- Only shoot when designator shoots",
    IniGravityMult = "- Gravity multiplier (for mods)",
    IniTimerSection = "Turret Slaver - Timer Config",
    IniTimerTriggerState = "Trigger on state",
    IniCommentTimerTriggerState = " Valid trigger states: Idle, Firing, Targeting, NotIdle, NotFiring, NotTargeting",
    IniTimerRetrigger = "Should retrigger",
    IniTimerRetriggerInterval = "Retrigger interval (s)",
    IniRotorSection = "Turret Slaver - Rotor Config",
    IniRotorManualRestAngle = "Use manual rest angle",
    IniRotorRestAngle = "Manual rest angle (deg)",
    IniLightSectionTargeting = "Turret Slaver - Light Config - Targeting",
    IniLightSectionIdle = "Turret Slaver - Light Config - Idle",
    IniLightEnable = "Turn on",
    IniLightColor = "Color (R,G,B)",
    IniLightBlinkInterval = "Blink interval (s)",
    IniLightBlinkLength = "Blink length (%)";

const string _iniMigrationKey = "[General Parameters]";
Dictionary<string, string> _iniMigrationDictionary = new Dictionary<string, string>()
{
{"General Parameters", IniGeneral},
{"muzzle_velocity", IniMuzzleVelocity},
{"is_rocket", IniIsRocket},
{"rocket_acceleration", IniRocketAccel},
{"rocket_initial_velocity", IniRocketInitVel},
{"avoid_friendly_fire", IniAvoidFriendly},
{"fire_tolerance_deg", IniTolerance},
{"manual_convergence_range", IniConvergence},
{"proportional_gain", IniKp},
{"integral_gain", IniKi},
{"derivative_gain", IniKd},
{"integral_decay_ratio", IniIntegralDecayRatio},
{"return_to_rest_rpm", IniRestRpm},
{"max_game_speed", IniGameMaxSpeed},
{"rotor_turret_group_tag", IniRotorTurretName},
{"ai_turret_group_tag", IniAiTurretName},
{"designator_name_tag", IniDesignatorName},
{"auto_fire_range", IniEngagementRange},
{"return_to_rest_delay", IniRestTime},
{"only_shoot_when_designator_shoots", IniShootWhenDesignatorDoes},
{"gravity_multiplier", IniGravityMult}
};

const double UpdatesPerSecond = 10;
const double MainUpdateInterval = 1.0 / UpdatesPerSecond;
const double Tick = 1.0 / 60.0;
const int MaxBlocksToCheckForFF = 50;
Dictionary<long, FriendlyData>
    _friendlyData = new Dictionary<long, FriendlyData>(),
    _friendlyDataBuffer = new Dictionary<long, FriendlyData>();
List<IMyLargeTurretBase> _designators = new List<IMyLargeTurretBase>();
List<IMyShipController> _shipControllers = new List<IMyShipController>();
List<IMyTextPanel> _debugPanels = new List<IMyTextPanel>();
List<IMyBlockGroup>
    _currentTurretGroups = new List<IMyBlockGroup>(),
    _lastTurretGroups = new List<IMyBlockGroup>();
List<TurretGroupBase> _turretList = new List<TurretGroupBase>();
HashSet<IMyCubeGrid> _allShipGrids = new HashSet<IMyCubeGrid>();
MyIni _generalIni = new MyIni();
Scheduler _scheduler;
StringBuilder
    _iniOutput = new StringBuilder(),
    _echoOutput = new StringBuilder(),
    _turretEchoBuilder = new StringBuilder(),
    _turretErrorBuilder = new StringBuilder(),
    _turretEchoOutput = new StringBuilder(),
    _turretErrorOutput = new StringBuilder();
RuntimeTracker _runtimeTracker;
CircularBuffer<Action> _turretBuffer;
IMyBroadcastListener _broadcastListener;
IMyShipController _reference = null;
Vector3D _lastGridPosition = Vector3D.Zero,
    _gridVelocity = Vector3D.Zero,
    _gravity = Vector3D.Zero;
IMyCubeGrid _biggestGrid = null;
double _biggestGridRadius = 0;
bool _debugMode = false,
    _writtenTurretEcho = false,
    _drawTitleScreen = true;
int _rotorTurretCount = 0,
    _aiTurretCount = 0,
    _rotorGimbalCount = 0;
Log _setupLog = new Log();
TurretSlaverScreenManager _screenManager;
#endregion

#region Entrypoints
Program()
{
    MigrateConfig();

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    _runtimeTracker = new RuntimeTracker(this, 5 * 60);
    _screenManager = new TurretSlaverScreenManager(VERSION, this);

    double step = UpdatesPerSecond / 60.0;
    _turretBuffer = new CircularBuffer<Action>(6);
    _turretBuffer.Add(() => UpdateTurrets(0 * step, 1 * step));
    _turretBuffer.Add(() => UpdateTurrets(1 * step, 2 * step));
    _turretBuffer.Add(() => UpdateTurrets(2 * step, 3 * step));
    _turretBuffer.Add(() => UpdateTurrets(3 * step, 4 * step));
    _turretBuffer.Add(() => UpdateTurrets(4 * step, 5 * step));
    _turretBuffer.Add(() => UpdateTurrets(5 * step, 6 * step));

    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(CalculateShooterVelocity, UpdatesPerSecond);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(NetworkTargets, 6);
    _scheduler.AddScheduledAction(RefreshDesignatorTargeting, 0.25, timeOffset: 0.66);
    _scheduler.AddScheduledAction(MoveNextTurrets, 60);
    _scheduler.AddScheduledAction(DrawRunningScreen, 6);
    _scheduler.AddScheduledAction(_screenManager.Animate, 0.2);
    _scheduler.Init();

    // IGC Register
    _broadcastListener = IGC.RegisterBroadcastListener(IgcTag);
    _broadcastListener.SetMessageCallback(IgcTag);

    MainSetup();

    base.Echo("Initializing...");
}

void DrawRunningScreen()
{
    if (_drawTitleScreen)
    {
        _screenManager.Draw();
    }
}

void Main(string arg, UpdateType updateType)
{
    try 
    {
        _runtimeTracker.AddRuntime();

        if (arg.Equals(IgcTag))
            ProcessNetworkMessage();
        else if (!string.IsNullOrWhiteSpace(arg))
            ArgumentHandling(arg);

        _scheduler.Update();

        _runtimeTracker.AddInstructions();
    }
    catch (Exception e)
    {
        PrintBsod(Me.GetSurface(0), "Whip's Turret Slaver", VERSION, 0.5f, e);
        throw e;
    }
}
#endregion

#region Main Routines
void MoveNextTurrets()
{
    _turretBuffer.MoveNext().Invoke();
}

void UpdateTurrets(double startProportion, double endProportion)
{
    int startInt = (int)Math.Round(startProportion * _turretList.Count);
    int endInt = (int)Math.Round(endProportion * _turretList.Count);

    for (int i = startInt; i < endInt; ++i)
    {
        var turretToUpdate = _turretList[i];
        turretToUpdate.DoWork(ref _gridVelocity, ref _gravity, _allShipGrids, _friendlyData, _debugMode);

        if (turretToUpdate.Log.HasContent)
        {
            _turretErrorBuilder.Append($"_____________________________\n{turretToUpdate.Group.Name} Errors/Warnings\n\n");
            turretToUpdate.Log.Write(_turretErrorBuilder);
        }
        if (_debugMode)
        {
            _turretEchoBuilder.Append($"_____________________________\n{turretToUpdate.Group.Name} Info\n\n");
            _turretEchoBuilder.Append(turretToUpdate.EchoOutput);
        }
    }

    // End of cycle
    if (endInt == _turretList.Count && !_writtenTurretEcho)
    {
        _writtenTurretEcho = true;

        if (_debugMode)
        {
            _turretEchoOutput.Clear();
            _turretEchoOutput.Append(_turretEchoBuilder);
            _turretEchoBuilder.Clear();
        }

        _turretErrorOutput.Clear();
        _turretErrorOutput.Append(_turretErrorBuilder);
        _turretErrorBuilder.Clear();
    }
    else
    {
        _writtenTurretEcho = false;
    }
}

void MainSetup()
{
    _setupLog.Clear();
    ParseGeneralIni();
    GetBlockGroups();
    GetVelocityReference();
    BuildIniOutput();
}

void CalculateShooterVelocity()
{
    _gravity = Vector3D.Zero;
    if (_reference != null && !_reference.Closed)
    {
        _gridVelocity = _reference.GetShipVelocities().LinearVelocity;
        _gravity = _reference.GetNaturalGravity();
    }
    else
    {
        var currentGridPosition = Me.CubeGrid.WorldAABB.Center;
        _gridVelocity = (currentGridPosition - _lastGridPosition) * UpdatesPerSecond;
        _lastGridPosition = currentGridPosition;
        GetVelocityReference();
    }
}
#endregion

#region Detailed Info Printing
new void Echo(string text)
{
    _echoOutput.Append(text).Append("\n");
}

void PrintEcho()
{
    base.Echo(_echoOutput.ToString());
}

void PrintDetailedInfo()
{
    Echo($"Whip's Turret Slaver\n(Version {VERSION} - {DATE})");
    Echo("\nYou can customize turrets \nindividually in the Custom Data\nof this block.");
    Echo($"\nTo refresh blocks and reprocess\nblock groups, run the argument:\n\"setup\".\n");
    Echo($"Turret Summary:\n  {_rotorTurretCount} rotor turret group(s) found");
    Echo($"  {_rotorGimbalCount} rotor gimbal group(s) found");
    Echo($"  {_aiTurretCount} slaved AI turret group(s) found\n");

    Echo($"Debug mode is: {(_debugMode ? "ON" : "OFF")}\n> Toggle debug output with the\n  argument: \"debug_toggle\".");
    if (_debugMode)
        Echo($"> {_debugPanels.Count} debug panel(s) found\n  Name a text panel \"DEBUG\" to\n  see debug text.");
    _echoOutput.Append("\n");

    if (_setupLog.HasContent)
    {
        _echoOutput.Append($"_____________________________\nMain Setup\n\n");
        _setupLog.Write(_echoOutput);
    }

    _echoOutput.Append(_turretErrorOutput);
    Echo(_runtimeTracker.Write());

    if (_debugMode)
    {
        string finalOutput = _echoOutput.ToString() + _turretEchoOutput.ToString();
        foreach (var block in _debugPanels)
        {
            block.WriteText(finalOutput);
            block.ContentType = ContentType.TEXT_AND_IMAGE;
        }
    }

    PrintEcho();
    _echoOutput.Clear();
}
#endregion

#region Ini
void WriteGeneralIni()
{
    _generalIni.Clear();
    _generalIni.TryParse(Me.CustomData, IniGeneral);
    _generalIni.Set(IniGeneral, IniGameMaxSpeed, _defaultVars.GameMaxSpeed);
    _generalIni.Set(IniGeneral, IniRotorTurretName, _defaultVars.RotorTurretGroupNameTag);
    _generalIni.Set(IniGeneral, IniAiTurretName, _defaultVars.AiTurretGroupNameTag);
    _generalIni.Set(IniGeneral, IniDesignatorName, _defaultVars.DesignatorNameTag);
    _generalIni.Set(IniGeneral, IniDrawTitleScreen, _drawTitleScreen);
}

void ParseGeneralIni()
{
    _generalIni.Clear();
    bool parsed = _generalIni.TryParse(Me.CustomData, IniGeneral);
    if (!parsed)
        return;
    _defaultVars.GameMaxSpeed = _generalIni.Get(IniGeneral, IniGameMaxSpeed).ToDouble(_defaultVars.GameMaxSpeed);
    _defaultVars.RotorTurretGroupNameTag = _generalIni.Get(IniGeneral, IniRotorTurretName).ToString(_defaultVars.RotorTurretGroupNameTag);
    _defaultVars.AiTurretGroupNameTag = _generalIni.Get(IniGeneral, IniAiTurretName).ToString(_defaultVars.AiTurretGroupNameTag);
    _defaultVars.DesignatorNameTag = _generalIni.Get(IniGeneral, IniDesignatorName).ToString(_defaultVars.DesignatorNameTag);
    _drawTitleScreen = _generalIni.Get(IniGeneral, IniDrawTitleScreen).ToBoolean(_drawTitleScreen);
}

void BuildIniOutput()
{
    _iniOutput.Clear();
    WriteGeneralIni();
    _iniOutput.AppendLine(_generalIni.ToString());

    foreach (TurretGroupBase turret in _turretList)
    {
        _iniOutput.Append(turret.IniOutput).Append("\n");
    }

    Me.CustomData = _iniOutput.ToString();
}
#endregion

#region Setup
bool CollectDesignatorsDebugAndMech(IMyTerminalBlock b)
{
    if (!b.IsSameConstructAs(Me))
        return false;

    _allShipGrids.Add(b.CubeGrid);
    double rad = b.CubeGrid.WorldVolume.Radius;
    if (rad > _biggestGridRadius)
    {
        _biggestGridRadius = rad;
        _biggestGrid = b.CubeGrid;
    }

    var t = b as IMyLargeTurretBase;
    if (t != null && StringExtensions.Contains(b.CustomName, _defaultVars.DesignatorNameTag))
    {
        _designators.Add(t);
        return false;
    }

    var sc = b as IMyShipController;
    if (sc != null)
    {
        _shipControllers.Add(sc);
        return false;
    }

    var mech = b as IMyMechanicalConnectionBlock;
    if (mech != null)
    {
        if (mech.IsAttached)
            _allShipGrids.Add(mech.TopGrid);
        return false;
    }

    var text = b as IMyTextPanel;
    if (_debugMode && b != null && b.CustomName.Contains("DEBUG"))
    {
        _debugPanels.Add(text);
        return false;
    }

    return false;
}

readonly char[] IllegalIniSectionChars = new char[] { '[', ']', '\r', '\n' };
bool HasIllegalIniSectionChars(string s)
{
    return s.IndexOfAny(IllegalIniSectionChars) >= 0;
}

bool CollectTurretGroups(IMyBlockGroup g)
{
    bool create = true;
    if (_lastTurretGroups.Contains(g))
    {
        create = false;
    }

    bool illegal = HasIllegalIniSectionChars(g.Name);
    bool collect = false;

    if (StringExtensions.Contains(g.Name, _defaultVars.AiTurretGroupNameTag))
    {
        collect = true;
        if (!illegal)
        {
            if (create)
            {
                _turretList.Add(new SlavedAIGroup(g, _defaultVars, this, _allShipGrids, _friendlyData));
            }
            _currentTurretGroups.Add(g);
            _aiTurretCount++;
        }
    }
    else if (StringExtensions.Contains(g.Name, _defaultVars.RotorTurretGroupNameTag))
    {
        collect = true;
        if (!illegal)
        {
            if (create)
            {
                _turretList.Add(new DualAxisRotorTurretGroup(g, _defaultVars, this, _allShipGrids, _friendlyData));
            }
            _currentTurretGroups.Add(g);
            _rotorTurretCount++;
        }
    }
    else if (StringExtensions.Contains(g.Name, _defaultVars.RotorGimbalGroupNameTag))
    {
        collect = true;
        if (!illegal)
        {
            if (create)
            {
                _turretList.Add(new SingleAxisRotorTurretGroup(g, _defaultVars, this, _allShipGrids, _friendlyData));
            }
            _currentTurretGroups.Add(g);
            _rotorGimbalCount++;
        }
    }

    if (collect && illegal)
    {
        _setupLog.Warning($"Group '{g.Name}'\nhas illegal characters! Characters\n[ ] \\r and \\n are not allowed!");
    }

    return false;
}

void GetBlockGroups()
{
    _rotorTurretCount = 0;
    _aiTurretCount = 0;
    _rotorGimbalCount = 0;
    _biggestGridRadius = 0;
    _biggestGrid = null;

    _shipControllers.Clear();
    _designators.Clear();
    _debugPanels.Clear();
    _currentTurretGroups.Clear();
    _allShipGrids.Clear();
    _allShipGrids.Add(Me.CubeGrid);

    // Update existing
    foreach (var turret in _turretList)
    {
        turret.GetTurretGroupBlocks(defaultVars: _defaultVars);
    }

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectDesignatorsDebugAndMech);
    GridTerminalSystem.GetBlockGroups(null, CollectTurretGroups);

    // Remove dead groups
    _turretList.RemoveAll(x => !_currentTurretGroups.Contains(x.Group));

    _lastTurretGroups.Clear();
    foreach (var tg in _turretList)
    {
        _lastTurretGroups.Add(tg.Group);
    }
}

void GetVelocityReference()
{
    _reference = _shipControllers.Count > 0 ? _shipControllers[0] : null;
}

void RefreshDesignatorTargeting()
{
    foreach (var t in _designators)
    {
        if (t.HasTarget)
        {
            // Force a new target selection
            if (t.TargetMeteors)
            {
                t.TargetMeteors = false;
                t.TargetMeteors = true;
            }

            if (t.TargetMissiles)
            {
                t.TargetMissiles = false;
                t.TargetMissiles = true;
            }

            if (t.TargetSmallGrids)
            {
                t.TargetSmallGrids = false;
                t.TargetSmallGrids = true;
            }

            if (t.TargetLargeGrids)
            {
                t.TargetLargeGrids = false;
                t.TargetLargeGrids = true;
            }

            if (t.TargetCharacters)
            {
                t.TargetCharacters = false;
                t.TargetCharacters = true;
            }

            if (t.TargetStations)
            {
                t.TargetStations = false;
                t.TargetStations = true;
            }
        }
    }
}

void ArgumentHandling(string arg)
{
    switch (arg.ToLowerInvariant())
    {
        case "reset_targeting":
            ResetAllDesignatorTargeting();
            break;

        case "debug_toggle":
            _debugMode = !_debugMode;
            if (_debugMode)
            {
                GetBlockGroups();
            }
            break;

        case "setup":
            MainSetup();
            break;

        default:
            break;
    }
}

void ResetAllDesignatorTargeting()
{
    foreach (var t in _designators)
    {
        t.ResetTargetingToDefault();
        t.Range = float.MaxValue;
    }
}
#endregion

#region Inter-Grid Comms
struct FriendlyData
{
    public Vector3D Position;
    public double Radius;

    public FriendlyData(Vector3D pos, double rad)
    {
        Position = pos;
        Radius = rad;
    }
}

enum TargetRelation : byte { Neutral = 0, Other = 0, Enemy = 1, Friendly = 2, Locked = 4, LargeGrid = 8, SmallGrid = 16, RelationMask = Neutral | Enemy | Friendly, TypeMask = LargeGrid | SmallGrid | Other }
void ProcessNetworkMessage()
{
    byte relationship = 0;
    long entityId = 0;
    Vector3D position = default(Vector3D);
    double targetRadius = 0;

    while (_broadcastListener.HasPendingMessage)
    {
        object messageData = _broadcastListener.AcceptMessage().Data;

        if (messageData is MyTuple<byte, long, Vector3D, double>)
        {
            if (_biggestGrid == null)
                continue;
            var payload = (MyTuple<byte, long, Vector3D, double>)messageData;
            relationship = payload.Item1;
            if ((relationship & (byte)TargetRelation.Friendly) == 0)
                continue; // Ignore IFF message if not friendly
            entityId = payload.Item2;
            if (entityId == _biggestGrid.EntityId)
                continue; // Ignore if source ship is the same
            position = payload.Item3;
            targetRadius = payload.Item4;
            if (Vector3D.DistanceSquared(position, _biggestGrid.GetPosition()) < targetRadius)
                continue; // Ignore if we are within the bounding sphere

            FriendlyData friendlyData;
            _friendlyDataBuffer.TryGetValue(entityId, out friendlyData);

            if (targetRadius > friendlyData.Radius)
            {
                friendlyData.Radius = targetRadius;
                friendlyData.Position = position;
                _friendlyDataBuffer[entityId] = friendlyData;
            }
        }
    }
}

void NetworkTargets()
{
    var myType = _biggestGrid.GridSizeEnum == MyCubeSize.Large ? TargetRelation.LargeGrid : TargetRelation.SmallGrid;
    var payload = new MyTuple<byte, long, Vector3D, double>((byte)(TargetRelation.Friendly | myType), _biggestGrid.EntityId, _biggestGrid.WorldVolume.Center, _biggestGridRadius * _biggestGridRadius);
    IGC.SendBroadcastMessage(IgcTag, payload);

    _friendlyData.Clear();
    foreach (var kvp in _friendlyDataBuffer)
    {
        _friendlyData[kvp.Key] = kvp.Value;
    }
    _friendlyDataBuffer.Clear();
}
#endregion

#region Turret Group Classes
#region Rotor Turrets
class DualAxisRotorTurretGroup : RotorTurretGroupBase
{
    HashSet<IMyCubeGrid> _elevationRotorHeadGrids = new HashSet<IMyCubeGrid>();
    List<IMyMotorStator> _secondaryElevationRotors = new List<IMyMotorStator>();
    IMyMotorStator _mainElevationRotor;
    DecayingIntegralPID _elevationPID = new DecayingIntegralPID(0, 0, 0, MainUpdateInterval, 0);
    MatrixD _lastElevationMatrix;

    public DualAxisRotorTurretGroup(IMyBlockGroup group, TurretVariables defaultVars, Program program, HashSet<IMyCubeGrid> shipGrids, Dictionary<long, FriendlyData> friendlyData)
        : base(group, defaultVars, program, shipGrids, friendlyData)
    {

    }

    protected override void ClearBlocks()
    {
        base.ClearBlocks();
        _elevationRotorHeadGrids.Clear();
        _secondaryElevationRotors.Clear();
        _azimuthTopGrid = null;
        _mainElevationRotor = null;
    }

    protected override bool SortBlocks(List<IMyTerminalBlock> _groupBlocks, bool verbose)
    {
        ClearBlocks();
        Log.Clear();

        foreach (var block in _groupBlocks)
        {
            CollectBlocks(block);
        }

        // Find azimuth rotors by finding which rotors have rotors atop them
        for (int i = _unsortedRotors.Count - 1; i >= 0; --i)
        {
            var rotor = _unsortedRotors[i];
            if (!rotor.IsAttached)
            {
                Log.Warning($"No rotor head for rotor\n named '{rotor.CustomName}'\n Skipping this rotor...");
                continue;
            }

            // Found a azimuth rotor
            if (_rotorGrids.Contains(rotor.TopGrid))
            {
                if (_azimuthRotor == null)
                {
                    _azimuthRotor = rotor;
                    _azimuthTopGrid = rotor.TopGrid;
                    GetRotorRestAngle(rotor);
                    AddRotorGridsToHash(rotor, false);
                }
                else
                {
                    _duplicateAzimuths.Add(rotor);
                }
                _unsortedRotors.RemoveAt(i);
            }
        }

        // Find elevation rotors
        for (int i = _unsortedRotors.Count - 1; i >= 0; --i)
        {
            var rotor = _unsortedRotors[i];
            if (!rotor.IsAttached)
            {
                continue;
            }

            // Check that this rotor resides on the azimuth's top grid
            if (rotor.CubeGrid != _azimuthTopGrid)
            {
                continue;
            }

            // Check if elevation rotor has weapons
            if (!_weaponGrids.Contains(rotor.TopGrid))
            {
                continue;
            }

            // This is an elevation rotor
            if (_mainElevationRotor == null)
            {
                _rotorTurretReference = GetTurretReferenceOnRotorHead(rotor);
                if (_rotorTurretReference != null)
                {
                    _mainElevationRotor = rotor;
                }
            }
            _secondaryElevationRotors.Add(rotor);
            _elevationRotorHeadGrids.Add(rotor.TopGrid);
            GetRotorRestAngle(rotor);
            AddRotorGridsToHash(rotor, true);

            _unsortedRotors.RemoveAt(i);
        }

        // Check for orphaned rotors
        if (_unsortedRotors.Count > 0)
        {
            Log.Warning("Unsorted rotor(s) found:", false);
            foreach (var r in _unsortedRotors)
            {
                Log.WarningOutput.Append($"  - \"{r.CustomName}\"\n");
            }
            Log.WarningOutput.Append("\n");
        }

        if (_duplicateAzimuths.Count > 0)
        {
            Log.Warning("Only one base rotor is\n allowed per turret. Additional ones\n will be ignored:", false);
            foreach (var r in _duplicateAzimuths)
            {
                Log.WarningOutput.Append($"  - \"{r.CustomName}\"\n");
            }
            Log.WarningOutput.Append("\n");
        }

        bool noErrors = true;
        if (_guns.Count == 0 && _tools.Count == 0 && _cameras.Count == 0 && _lightConfigs.Count == 0)
        {
            Log.Error("No weapons, tools, lights or\ncameras found");
            noErrors = false;
        }

        if (_designators.Count == 0)
        {
            Log.Error("No designators found");
            noErrors = false;
        }

        if (_azimuthRotor == null)
        {
            Log.Error("No base rotor found");
            noErrors = false;
        }

        if (_mainElevationRotor == null)
        {
            if (_secondaryElevationRotors.Count == 0)
                Log.Error("No secondary rotor(s) found");
            else
                Log.Error($"None of the {_secondaryElevationRotors.Count} secondary\nrotor(s) has weapons/tools attached to them");
            noErrors = false;
        }
        else
        {
            _secondaryElevationRotors.Remove(_mainElevationRotor); // Remove main elevation rotor from the list so it isnt double counted.
        }

        _vitalBlocks.Add(_mainElevationRotor);
        _vitalBlocks.Add(_azimuthRotor);
        _vitalBlocks.Add(_rotorTurretReference);
        _vitalBlocks.AddRange(_guns);
        _vitalBlocks.AddRange(_tools);
        _vitalBlocks.AddRange(_cameras);
        foreach (var lightConfig in _lightConfigs)
        {
            _vitalBlocks.Add(lightConfig.Light);
        }

        return noErrors;
    }

    protected override void SetPidValues()
    {
        base.SetPidValues();
        _elevationPID.Kp = _proportionalGain;
        _elevationPID.Ki = _integralGain;
        _elevationPID.Kd = _derivativeGain;
        _elevationPID.DecayRatio = _integralDecayRatio;
    }

    public override void UpdateGeneralSettings(TurretVariables defaultVars)
    {
        if (defaultVars == null)
            return;

        base.UpdateGeneralSettings(defaultVars);
    }

    protected override void RotorTurretTargeting()
    {
        if (!_mainElevationRotor.IsAttached)
            return;

        Echo($"Rotor turret is targeting");

        Vector3D aimPosition = GetTargetPoint(ref _averageWeaponPos, _designator);
        Vector3D targetDirection = aimPosition - _averageWeaponPos;
        Vector3D turretFrontVec = _rotorTurretReference.WorldMatrix.Forward;

        MatrixD turretBaseMatrix = GetBaseMatrix(turretFrontVec, _azimuthRotor.WorldMatrix.Up);
        int elevationSign = Math.Sign(Vector3D.Dot(turretBaseMatrix.Left, _mainElevationRotor.WorldMatrix.Up));

        /*
         * We need 2 sets of angles to be able to prevent the turret from trying to rotate over 90 deg
         * vertical to get to a target behind it. This ensures that the elevation angle is always
         * lies in the domain: -90 deg <= elevation <= 90 deg.
         */
        double desiredAzimuthAngle, desiredElevationAngle, currentElevationAngle, azimuthAngle, elevationAngle;
        GetRotationAngles(ref targetDirection, ref turretBaseMatrix, out desiredAzimuthAngle, out desiredElevationAngle);
        GetElevationAngle(ref turretFrontVec, ref turretBaseMatrix, out currentElevationAngle);
        elevationAngle = (desiredElevationAngle - currentElevationAngle) * elevationSign;
        azimuthAngle = GetAllowedRotationAngle(desiredAzimuthAngle, _azimuthRotor);

        double azimuthSpeed = _azimuthPID.Control(azimuthAngle);
        double elevationSpeed = _elevationPID.Control(elevationAngle);

        azimuthSpeed = MathHelper.Clamp(azimuthSpeed, -_maxRotorSpeed, _maxRotorSpeed);
        elevationSpeed = MathHelper.Clamp(elevationSpeed, -_maxRotorSpeed, _maxRotorSpeed);

        /*
         * Get angular error rate due to ship rotation
         */
        double azimuthError, elevationError, azimuthErrorRate, elevationErrorRate;
        azimuthError = ComputeRotorHeadingError(_mainElevationRotor, ref _lastAzimuthMatrix);
        elevationError = ComputeRotorHeadingError(_mainElevationRotor, ref _lastElevationMatrix);
        azimuthErrorRate = azimuthError / MainUpdateInterval * MathHelper.RadiansPerSecondToRPM;
        elevationErrorRate = elevationError / MainUpdateInterval * MathHelper.RadiansPerSecondToRPM;

        _azimuthRotor.TargetVelocityRPM = (float)(azimuthSpeed + azimuthErrorRate);
        _mainElevationRotor.TargetVelocityRPM = (float)(elevationSpeed + elevationErrorRate);


        if (!_azimuthRotor.Enabled)
            _azimuthRotor.Enabled = true;

        if (!_mainElevationRotor.Enabled)
            _mainElevationRotor.Enabled = true;

        bool inRange = _autoEngagementRange * _autoEngagementRange >= targetDirection.LengthSquared();
        bool angleWithinTolerance = VectorMath.IsDotProductWithinTolerance(turretFrontVec, targetDirection, _toleranceDotProduct);
        bool shootWeapons = false;

        if (angleWithinTolerance)
        {
            if (_designator.IsUnderControl && _designator.IsShooting) // If manually controlled
            {
                shootWeapons = true;
            }
            else if (_designator.HasTarget && inRange) // If AI controlled
            {
                if (!_onlyShootWhenDesignatorShoots)
                    shootWeapons = true;
                else if (_onlyShootWhenDesignatorShoots && _designator.IsShooting)
                    shootWeapons = true;
            }
        }

        _intersection = false;
        if (shootWeapons)
        {
            if (_avoidFriendlyFireOtherShips)
            {
                _intersection = IsOccludedByFriendlyShip(ref _averageWeaponPos, ref turretFrontVec);
            }
            if (_avoidFriendlyFire && !_intersection)
            {
                _intersection = CheckForFF(ref _averageWeaponPos, ref turretFrontVec, _azimuthRotor);
            }
            shootWeapons = !_intersection;
        }

        ToggleWeaponsAndTools(shootWeapons, _isTargeting, _guns);

        foreach (var rotor in _secondaryElevationRotors)
        {
            HandleSecondaryElevationRotors(rotor, elevationSpeed, ref turretFrontVec);
        }
    }

    void HandleSecondaryElevationRotors(IMyMotorStator rotor, double elevationSpeed, ref Vector3D turretFrontVec)
    {
        IMyTerminalBlock reference = GetTurretReferenceOnRotorHead(rotor);
        if (reference == null)
        {
            Log.Warning($"No weapons, tools, cameras, or lights\non secondary rotor named\n'{rotor.CustomName}'\nSkipping this rotor...");
            return;
        }

        if (!rotor.Enabled)
            rotor.Enabled = true;

        Vector3D up = rotor.WorldMatrix.Up;
        Vector3D desiredFrontVec = reference.WorldMatrix.Forward;
        Vector3D cross = Vector3D.Cross(turretFrontVec, desiredFrontVec);
        double diff = 100f * VectorMath.AngleBetween(desiredFrontVec, turretFrontVec) * Math.Sign(Vector3D.Dot(cross, up));
        int multiplier = Math.Sign(Vector3D.Dot(up, _mainElevationRotor.WorldMatrix.Up));
        rotor.TargetVelocityRPM = (float)(multiplier * elevationSpeed + diff);

        if (!rotor.Enabled)
            rotor.Enabled = true;
    }

    protected override void StopRotorMovement()
    {
        base.StopRotorMovement();

        if (_mainElevationRotor != null)
            _mainElevationRotor.TargetVelocityRPM = 0;

        foreach (var rotor in _secondaryElevationRotors)
        {
            rotor.TargetVelocityRPM = 0f;
        }
    }

    protected override void ReturnToEquilibrium()
    {
        base.ReturnToEquilibrium();

        MoveRotorToEquilibrium(_mainElevationRotor);
        foreach (var block in _secondaryElevationRotors)
        {
            MoveRotorToEquilibrium(block);
        }
    }

    protected override void PrintDebugInfo()
    {
        var num = _mainElevationRotor == null ? 0 : 1;
        Echo($"Targeting: {_isTargeting}" +
            $"\nGrid intersection: {_intersection}" +
            $"\nBase rotor: {(_azimuthRotor == null ? "null" : _azimuthRotor.CustomName)}" +
            $"\nMain secondary rotor: {(_mainElevationRotor == null ? "null" : _mainElevationRotor.CustomName)}" +
            $"\nSecondary rotors: {_secondaryElevationRotors.Count + num}" +
            $"\nWeapons: {_guns.Count}" +
            $"\nTools: {_tools.Count}" +
            $"\nLights: {_lightConfigs.Count}" +
            $"\nTimers: {_timerConfigs.Count}" +
            $"\nCameras: {_cameras.Count}" +
            $"\nDesignators: {_designators.Count}" +
            $"\nMuzzle velocity: {_muzzleVelocity} m/s" +
            $"\nIs Firing: {_isShooting}"
        );
    }

    public override void OnNewTarget()
    {
        base.OnNewTarget();
        _elevationPID.Reset();
    }
}

class SingleAxisRotorTurretGroup : RotorTurretGroupBase
{
    public SingleAxisRotorTurretGroup(IMyBlockGroup group, TurretVariables defaultVars, Program program, HashSet<IMyCubeGrid> shipGrids, Dictionary<long, FriendlyData> friendlyData)
        : base(group, defaultVars, program, shipGrids, friendlyData)
    {

    }

    protected override bool SortBlocks(List<IMyTerminalBlock> _groupBlocks, bool verbose)
    {
        ClearBlocks();
        Log.Clear();

        foreach (var block in _groupBlocks)
        {
            CollectBlocks(block);
        }

        foreach (var rotor in _unsortedRotors)
        {
            if (!rotor.IsAttached)
            {
                continue;
            }

            if (_azimuthRotor != null)
            {
                _duplicateAzimuths.Add(rotor);
                continue;
            }

            _rotorTurretReference = GetTurretReferenceOnRotorHead(rotor);
            if (_rotorTurretReference != null)
            {
                _azimuthRotor = rotor;
                GetRotorRestAngle(rotor);
                AddRotorGridsToHash(rotor, false);
                break;
            }
        }

        if (_duplicateAzimuths.Count > 0)
        {
            Log.Warning("Only one rotor is\n allowed per gimbal. Additional ones\n will be ignored.", false);
            foreach (var r in _duplicateAzimuths)
            {
                Log.WarningOutput.Append($"  - \"{r.CustomName}\"\n");
            }
            Log.WarningOutput.Append("\n");
        }

        bool noErrors = true;
        if (_guns.Count == 0 && _tools.Count == 0 && _cameras.Count == 0 && _lightConfigs.Count == 0)
        {
            if (verbose)
                Log.Error("No weapons, tools, lights or\ncameras found");
            noErrors = false;
        }

        if (_designators.Count == 0)
        {
            if (verbose)
                Log.Error("No designators found");
            noErrors = false;
        }

        if (_azimuthRotor == null)
        {
            if (verbose)
                Log.Error("No rotor found");
            noErrors = false;
        }

        _vitalBlocks.Add(_azimuthRotor);
        _vitalBlocks.Add(_rotorTurretReference);
        _vitalBlocks.AddRange(_guns);
        _vitalBlocks.AddRange(_tools);
        _vitalBlocks.AddRange(_cameras);
        foreach (var lightConfig in _lightConfigs)
        {
            _vitalBlocks.Add(lightConfig.Light);
        }

        return noErrors;
    }

    protected override void RotorTurretTargeting()
    {
        if (!_azimuthRotor.IsAttached)
            return;

        Echo($"Rotor gimbal is targeting");
        Vector3D aimPosition = GetTargetPoint(ref _averageWeaponPos, _designator);
        Vector3D targetDirection = aimPosition - _averageWeaponPos;
        Vector3D turretFrontVec = _rotorTurretReference.WorldMatrix.Forward;
        MatrixD turretBaseMatrix = GetBaseMatrix(turretFrontVec, _azimuthRotor.WorldMatrix.Up);

        double azimuthAngle;
        GetAzimuthAngle(ref targetDirection, ref turretBaseMatrix, out azimuthAngle);
        azimuthAngle = GetAllowedRotationAngle(azimuthAngle, _azimuthRotor);
        double azimuthError = ComputeRotorHeadingError(_azimuthRotor, ref _lastAzimuthMatrix);
        double azimuthErrorRate = azimuthError / MainUpdateInterval * MathHelper.RadiansPerSecondToRPM;
        double azimuthSpeed = _azimuthPID.Control(azimuthAngle);
        azimuthSpeed = MathHelper.Clamp(azimuthSpeed, -_maxRotorSpeed, _maxRotorSpeed);

        _azimuthRotor.TargetVelocityRPM = (float)(azimuthSpeed + azimuthErrorRate);

        if (!_azimuthRotor.Enabled)
            _azimuthRotor.Enabled = true;

        bool inRange = _autoEngagementRange * _autoEngagementRange >= targetDirection.LengthSquared();
        bool angleWithinTolerance = VectorMath.IsDotProductWithinTolerance(turretFrontVec, targetDirection, _toleranceDotProduct);
        bool shootWeapons = false;

        if (angleWithinTolerance)
        {
            if (_designator.IsUnderControl && _designator.IsShooting) // If manually controlled
            {
                shootWeapons = true;
            }
            else if (_designator.HasTarget && inRange) // If AI controlled
            {
                if (!_onlyShootWhenDesignatorShoots)
                    shootWeapons = true;
                else if (_onlyShootWhenDesignatorShoots && _designator.IsShooting)
                    shootWeapons = true;
            }
        }

        _intersection = false;
        if (shootWeapons)
        {
            if (_avoidFriendlyFireOtherShips)
            {
                _intersection = IsOccludedByFriendlyShip(ref _averageWeaponPos, ref turretFrontVec);
            }
            if (_avoidFriendlyFire && !_intersection)
            {
                _intersection = CheckForFF(ref _averageWeaponPos, ref turretFrontVec, _azimuthRotor);
            }
            shootWeapons = !_intersection;
        }

        ToggleWeaponsAndTools(shootWeapons, _isTargeting, _guns);
    }

    protected override void PrintDebugInfo()
    {
        Echo($"Targeting: {_isTargeting}" +
            $"\nGrid intersection: {_intersection}" +
            $"\nWeapons: {_guns.Count}" +
            $"\nTools: {_tools.Count}" +
            $"\nLights: {_lightConfigs.Count}" +
            $"\nTimers: {_timerConfigs.Count}" +
            $"\nCameras: {_cameras.Count}" +
            $"\nDesignators: {_designators.Count}" +
            $"\nMuzzle velocity: {_muzzleVelocity} m/s" +
            $"\nIs Firing: {_isShooting}"
        );
    }
}

abstract class RotorTurretGroupBase : TurretGroupBase
{
    protected List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
    protected List<IMyFunctionalBlock> _tools = new List<IMyFunctionalBlock>();
    protected List<IMyUserControllableGun> _guns = new List<IMyUserControllableGun>();
    protected Dictionary<long, float> _rotorRestAngles = new Dictionary<long, float>();
    protected Dictionary<IMyCubeGrid, IMyTerminalBlock>
        _gunGridDict = new Dictionary<IMyCubeGrid, IMyTerminalBlock>(),
        _toolGridDict = new Dictionary<IMyCubeGrid, IMyTerminalBlock>(),
        _lightGridDict = new Dictionary<IMyCubeGrid, IMyTerminalBlock>();
    protected HashSet<IMyCubeGrid>
        _weaponGrids = new HashSet<IMyCubeGrid>(),
        _rotorGrids = new HashSet<IMyCubeGrid>();
    protected List<IMyMotorStator>
        _unsortedRotors = new List<IMyMotorStator>(),
        _duplicateAzimuths = new List<IMyMotorStator>();
    protected IMyCubeGrid _azimuthTopGrid;
    protected IMyMotorStator _azimuthRotor;
    protected IMyTerminalBlock _rotorTurretReference;
    protected double
        _proportionalGain,
        _integralGain,
        _derivativeGain,
        _maxRotorSpeed,
        _equilibriumRotationSpeed,
        _integralDecayRatio;
    protected int _returnToRestDelay = 20;
    protected MatrixD _lastAzimuthMatrix;
    protected DecayingIntegralPID _azimuthPID = new DecayingIntegralPID(0, 0, 0, MainUpdateInterval, 0);

    const double RotorStopThresholdRad = 0.5 / 180.0 * Math.PI;

    public RotorTurretGroupBase(IMyBlockGroup group, TurretVariables defaultVars, Program program, HashSet<IMyCubeGrid> shipGrids, Dictionary<long, FriendlyData> friendlyData)
        : base(group, defaultVars, program, shipGrids, friendlyData)
    {

    }

    public override void UpdateGeneralSettings(TurretVariables defaultVars)
    {
        if (defaultVars == null)
            return;

        base.UpdateGeneralSettings(defaultVars);
        _proportionalGain = defaultVars.ProportionalGain;
        _integralGain = defaultVars.IntegralGain;
        _derivativeGain = defaultVars.DerivativeGain;
        _maxRotorSpeed = defaultVars.MaxRotationSpeed;
        _equilibriumRotationSpeed = defaultVars.EquilibriumRotationSpeed;
        _integralDecayRatio = defaultVars.IntegralDecayRatio;
        SetPidValues();
    }

    protected bool CollectBlocks(IMyTerminalBlock block)
    {
        if (!block.IsSameConstructAs(_program.Me))
            return false;

        var turret = block as IMyLargeTurretBase;
        if (turret != null && StringExtensions.Contains(block.CustomName, _designatorName))
        {
            if (!turret.IsFunctional)
                return false;
            _designators.Add(turret);
            EnableTurretAI(turret);
            return false;
        }

        var weapon = block as IMyUserControllableGun;
        if (weapon != null)
        {
            if (weapon is IMyLargeTurretBase)
                return false;
            _guns.Add(weapon);
            _gunGridDict[block.CubeGrid] = block;
            _weaponGrids.Add(block.CubeGrid);
            return false;
        }

        var cam = block as IMyCameraBlock;
        if (cam != null)
        {
            _cameras.Add(cam);
            _toolGridDict[block.CubeGrid] = block;
            _weaponGrids.Add(block.CubeGrid);
            return false;
        }

        var tool = block as IMyShipToolBase;
        if (tool != null)
        {
            _tools.Add(tool);
            _toolGridDict[block.CubeGrid] = block;
            _weaponGrids.Add(block.CubeGrid);
            return false;
        }

        var drill = block as IMyShipDrill;
        if (drill != null)
        {
            _tools.Add(drill);
            _toolGridDict[block.CubeGrid] = block;
            _weaponGrids.Add(block.CubeGrid);
            return false;
        }

        var light = block as IMyLightingBlock;
        if (light != null)
        {
            CollectLight(light);
            _lightGridDict[block.CubeGrid] = block;
            _weaponGrids.Add(block.CubeGrid);
            return false;
        }

        var timer = block as IMyTimerBlock;
        if (timer != null)
        {
            CollectTimer(timer);
            return false;
        }

        var rotor = block as IMyMotorStator;
        if (rotor != null && rotor.IsFunctional)
        {
            _unsortedRotors.Add(rotor);
            _rotorGrids.Add(rotor.CubeGrid);
            return false;
        }
        return false;
    }

    protected void GetRotorRestAngle(IMyMotorStator rotor)
    {
        bool useManual = false;
        float restAngle = 0;

        // Migrate old configs
        if (!rotor.CustomData.Contains(IniRotorSection) && !string.IsNullOrWhiteSpace(IniRotorSection))
        {
            useManual = float.TryParse(rotor.CustomData, out restAngle);
        }

        Ini.Clear();
        if (Ini.TryParse(rotor.CustomData))
        {
            useManual = Ini.Get(IniRotorSection, IniRotorManualRestAngle).ToBoolean(useManual);
            restAngle = Ini.Get(IniRotorSection, IniRotorRestAngle).ToSingle(restAngle);
        }

        Ini.Set(IniRotorSection, IniRotorManualRestAngle, useManual);
        Ini.Set(IniRotorSection, IniRotorRestAngle, restAngle);

        string output = Ini.ToString();
        if (!string.Equals(output, rotor.CustomData))
        {
            rotor.CustomData = output;
        }

        if (useManual)
        {
            _rotorRestAngles[rotor.EntityId] = MathHelper.ToRadians(restAngle) % MathHelper.TwoPi;
        }
    }

    protected void AddRotorGridsToHash(IMyMotorStator rotor, bool addBase = true)
    {
        if (addBase)
            _thisTurretGrids.Add(rotor.CubeGrid);

        if (rotor.IsAttached)
            _thisTurretGrids.Add(rotor.TopGrid);
    }

    protected IMyTerminalBlock GetTurretReferenceOnRotorHead(IMyMotorStator rotor)
    {
        IMyTerminalBlock block = null;
        IMyCubeGrid rotorHeadGrid = rotor.TopGrid;
        if (rotorHeadGrid == null)
            return null;

        if (_gunGridDict.TryGetValue(rotorHeadGrid, out block))
            return block;

        if (_toolGridDict.TryGetValue(rotorHeadGrid, out block))
            return block;

        if (_lightGridDict.TryGetValue(rotorHeadGrid, out block))
            return block;

        return null;
    }

    protected override void ClearBlocks()
    {
        base.ClearBlocks();
        _cameras.Clear();
        _tools.Clear();
        _guns.Clear();
        _rotorRestAngles.Clear();
        _gunGridDict.Clear();
        _toolGridDict.Clear();
        _lightGridDict.Clear();
        _weaponGrids.Clear();
        _unsortedRotors.Clear();
        _rotorGrids.Clear();
        _duplicateAzimuths.Clear();
        _rotorTurretReference = null;
        _designator = null;
        _azimuthTopGrid = null;
        _azimuthRotor = null;
    }

    protected virtual void SetPidValues()
    {
        _azimuthPID.Kp = _proportionalGain;
        _azimuthPID.Ki = _integralGain;
        _azimuthPID.Kd = _derivativeGain;
        _azimuthPID.DecayRatio = _integralDecayRatio;
    }

    protected override void ParseIni()
    {
        base.ParseIni();

        double kp = _proportionalGain, ki = _integralGain, kd = _derivativeGain, decay = _integralDecayRatio;
        string name = Group.Name;
        _proportionalGain = Ini.Get(name, IniKp).ToDouble(_proportionalGain);
        _integralGain = Ini.Get(name, IniKi).ToDouble(_integralGain);
        _derivativeGain = Ini.Get(name, IniKd).ToDouble(_derivativeGain);
        _integralDecayRatio = Ini.Get(name, IniIntegralDecayRatio).ToDouble(_integralDecayRatio);
        _equilibriumRotationSpeed = Ini.Get(name, IniRestRpm).ToDouble(_equilibriumRotationSpeed);
        _maxRotorSpeed = Ini.Get(name, IniMaxRotorSpeed).ToDouble(_maxRotorSpeed);

        if (kp != _proportionalGain || ki != _integralGain || kd != _derivativeGain || decay != _integralDecayRatio)
        {
            SetPidValues();
        }

        _returnToRestDelay = (int)(Ini.Get(name, IniRestTime).ToDouble(_returnToRestDelay / UpdatesPerSecond) * UpdatesPerSecond);
    }

    protected override void WriteIni()
    {
        base.WriteIni();
        string name = Group.Name;
        Ini.Set(name, IniMaxRotorSpeed, _maxRotorSpeed);
        Ini.Set(name, IniKp, _proportionalGain);
        Ini.Set(name, IniKi, _integralGain);
        Ini.Set(name, IniIntegralDecayRatio, _integralDecayRatio);
        Ini.Set(name, IniKd, _derivativeGain);
        Ini.Set(name, IniRestRpm, _equilibriumRotationSpeed);
        Ini.Set(name, IniRestTime, _returnToRestDelay / UpdatesPerSecond);

        IniOutput.Clear();
        IniOutput.Append(Ini.ToString());
    }

    protected override void SetInitialWeaponParameters()
    {
        DominantWeaponType weaponType = GetDominantWeaponType(_guns);
        SetWeaponParameters(weaponType);
    }

    protected abstract void RotorTurretTargeting();

    protected override void HandleTargeting()
    {
        if (_designator == null)
        {
            ToggleWeaponsAndTools(false, false, _guns);
            return;
        }

        if (_isTargeting && _designator.IsWorking)
        {
            RotorTurretTargeting();
            _framesSinceLastLock = 0;
        }
        else
        {
            ToggleWeaponsAndTools(false, false, _guns);

            if (_framesSinceLastLock < _returnToRestDelay)
            {
                _framesSinceLastLock++;
                StopRotorMovement();
            }
            else
            {
                ReturnToEquilibrium();
            }
        }
    }

    protected virtual void StopRotorMovement()
    {
        if (_azimuthRotor != null)
            _azimuthRotor.TargetVelocityRPM = 0;
    }

    protected virtual void ReturnToEquilibrium()
    {
        MoveRotorToEquilibrium(_azimuthRotor);
    }

    protected void MoveRotorToEquilibrium(IMyMotorStator rotor)
    {
        if (!rotor.Enabled)
            rotor.Enabled = true;

        float restAngle = 0;
        float currentAngle = rotor.Angle;
        float lowerLimitRad = rotor.LowerLimitRad;
        float upperLimitRad = rotor.UpperLimitRad;

        if (_rotorRestAngles.TryGetValue(rotor.EntityId, out restAngle))
        {
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
        }
        else
        {
            if (lowerLimitRad >= -MathHelper.TwoPi && upperLimitRad <= MathHelper.TwoPi)
                restAngle = (lowerLimitRad + upperLimitRad) * 0.5f;
            else
                restAngle = currentAngle;
        }

        float angularDeviation = (restAngle - currentAngle);
        float targetVelocity = (float)Math.Round(angularDeviation * _equilibriumRotationSpeed, 2);

        if (Math.Abs(angularDeviation) < RotorStopThresholdRad)
        {
            rotor.TargetVelocityRPM = 0;
        }
        else
        {
            rotor.TargetVelocityRPM = targetVelocity;
        }
    }

    protected override void OnNotFunctional()
    {
        StopRotorMovement();
    }

    protected override void ToggleWeaponsAndTools<T>(bool isShooting, bool isTargeting, List<T> guns)
    {
        base.ToggleWeaponsAndTools(isShooting, isTargeting, guns);
        if (_toolsOn != isTargeting)
        {
            ChangePowerState(_tools, isTargeting);
            _toolsOn = isTargeting;
        }
    }

    static void ChangePowerState<T>(List<T> list, bool stateToSet) where T : class, IMyFunctionalBlock
    {
        foreach (IMyFunctionalBlock block in list)
        {
            if (block.Enabled != stateToSet)
                block.Enabled = stateToSet;
        }
    }

    public override void OnNewTarget()
    {
        base.OnNewTarget();
        _azimuthPID.Reset();
    }

    protected override Vector3D GetAverageWeaponPosition()
    {
        Vector3D positionSum = Vector3D.Zero;

        if (_guns.Count != 0)
        {
            foreach (var block in _guns) { positionSum += block.GetPosition(); }
            return positionSum / _guns.Count;
        }

        /*
         * This is a fall-through in case the user has no guns. The code will use the
         * tools for alignment instead.
         */
        int toolCount = _lightConfigs.Count + _cameras.Count + _tools.Count;
        if (toolCount == 0)
            return positionSum;
        foreach (var lightConfig in _lightConfigs) { positionSum += lightConfig.Light.GetPosition(); }
        foreach (var block in _cameras) { positionSum += block.GetPosition(); }
        foreach (var block in _tools) { positionSum += block.GetPosition(); }
        return positionSum / toolCount;
    }

    protected double ComputeRotorHeadingError(IMyMotorStator rotor, ref MatrixD previous)
    {
        Vector3D forward = rotor.WorldMatrix.Forward;
        double angle = CalculateRotorDeviationAngle(ref forward, ref previous);
        previous = rotor.WorldMatrix;
        return angle;
    }

    static double CalculateRotorDeviationAngle(ref Vector3D forwardVector, ref MatrixD lastOrientation)
    {
        Vector3D up = lastOrientation.Up, forward = lastOrientation.Forward;
        Vector3D flattenedForward = VectorMath.Rejection(forwardVector, up);
        return VectorMath.AngleBetween(flattenedForward, forward) * Math.Sign(flattenedForward.Dot(lastOrientation.Left));
    }

    protected double GetAllowedRotationAngle(double desiredDelta, IMyMotorStator rotor)
    {
        double desiredAngle = rotor.Angle + desiredDelta;
        if ((desiredAngle < rotor.LowerLimitRad && desiredAngle + MathHelper.TwoPi < rotor.UpperLimitRad)
            || (desiredAngle > rotor.UpperLimitRad && desiredAngle - MathHelper.TwoPi > rotor.LowerLimitRad))
        {
            return -Math.Sign(desiredDelta) * (MathHelper.TwoPi - Math.Abs(desiredDelta));
        }
        return desiredDelta;
    }

    protected MatrixD GetBaseMatrix(Vector3D front, Vector3D up)
    {
        var upNorm = VectorMath.SafeNormalize(up);
        var left = Vector3D.Cross(up, front);
        var leftNorm = VectorMath.SafeNormalize(left);
        var forwardNorm = Vector3D.Cross(leftNorm, upNorm); // By definition, should be normalized already

        return new MatrixD
        {
            Forward = forwardNorm,
            Left = leftNorm,
            Up = upNorm
        };
    }
}
#endregion

class SlavedAIGroup : TurretGroupBase
{
    List<IMyLargeTurretBase> _slavedTurrets = new List<IMyLargeTurretBase>();

    public SlavedAIGroup(IMyBlockGroup group, TurretVariables defaultVars, Program program, HashSet<IMyCubeGrid> shipGrids, Dictionary<long, FriendlyData> friendlyData)
        : base(group, defaultVars, program, shipGrids, friendlyData)
    {

    }

    protected override void ClearBlocks()
    {
        base.ClearBlocks();
        _slavedTurrets.Clear();
        _designators.Clear();
        _timerConfigs.Clear();
        _lightConfigs.Clear();
    }

    protected override bool SortBlocks(List<IMyTerminalBlock> _groupBlocks, bool verbose)
    {
        ClearBlocks();
        Log.Clear();

        foreach (IMyTerminalBlock block in _groupBlocks)
        {
            if (!block.IsSameConstructAs(_program.Me))
                continue;

            var light = block as IMyLightingBlock;
            if (light != null)
            {
                CollectLight(light);
                continue;
            }

            var timer = block as IMyTimerBlock;
            if (timer != null)
            {
                CollectTimer(timer);
                continue;
            }

            var turret = block as IMyLargeTurretBase;
            if (turret == null)
                continue;

            if (StringExtensions.Contains(turret.CustomName, _designatorName) && turret.IsFunctional)
            {
                _designators.Add(turret);
                EnableTurretAI(turret);
            }
            else
            {
                turret.Range = 1f;
                if (turret.EnableIdleRotation)
                    turret.EnableIdleRotation = false;
                _slavedTurrets.Add(turret);
            }
        }

        bool setupError = false;
        if (_slavedTurrets.Count == 0)
        {
            if (verbose)
                Log.Error($"No slaved AI turrets found");
            setupError = true;
        }

        if (_designators.Count == 0) /* second null check (If STILL null) */
        {
            if (verbose)
                Log.Error($"No designators found");
            setupError = true;
        }

        _vitalBlocks.AddRange(_slavedTurrets);

        return !setupError;
    }

    protected override void SetInitialWeaponParameters()
    {
        DominantWeaponType weaponType = GetDominantWeaponType(_slavedTurrets);
        SetWeaponParameters(weaponType);
    }

    protected override void HandleTargeting()
    {
        if (_designator == null)
        {
            _isShooting = false;
            ToggleWeaponsAndTools(false, false, _slavedTurrets);
            return;
        }

        if (_isTargeting && _designator.IsWorking)
        {
            _isShooting = false;
            foreach (IMyLargeTurretBase t in _slavedTurrets)
                SlaveAITurret(t);

            Echo($"Slaved turret(s) targeting");
        }
        else
        {
            if (_isShooting != false)
            {
                foreach (IMyLargeTurretBase t in _slavedTurrets)
                {
                    t.Shoot = false;
                }
                _isShooting = false;
            }
            Echo($"Slaved turret(s) idle");
        }

        foreach (var timerConfig in _timerConfigs)
        {
            timerConfig.Update(MainUpdateInterval, _isShooting, _isTargeting);
        }
    }

    void SlaveAITurret(IMyLargeTurretBase turret)
    {
        Vector3D turretPos = turret.GetPosition();
        Vector3D aimPosition = GetTargetPoint(ref turretPos, _designator);
        MatrixD turretMatrix = turret.WorldMatrix;
        Vector3D turretDirection = VectorAzimuthElevation(turret);
        Vector3D targetDirectionNorm = Vector3D.Normalize(aimPosition - turretMatrix.Translation);

        double az = 0; double el = 0;
        GetRotationAngles(ref targetDirectionNorm, ref turretMatrix, out az, out el);
        turret.Azimuth = (float)-az; // AZ is reverse of r.h.r. convention
        turret.Elevation = (float)el;
        turret.SyncAzimuth(); // this syncs both angles

        bool inRange = _autoEngagementRange * _autoEngagementRange > Vector3D.DistanceSquared(aimPosition, turretMatrix.Translation);
        var toleranceRads = MathHelper.ToRadians(_toleranceAngle);
        bool withinAngleTolerance = Math.Abs(turret.Azimuth + az) < toleranceRads && Math.Abs(turret.Elevation - el) < toleranceRads;
        bool shouldShoot = false;

        if (withinAngleTolerance)
        {
            if (_designator.IsUnderControl && _designator.IsShooting)
                shouldShoot = true;
            else if (_designator.HasTarget)
            {
                if (inRange)
                {
                    if (!_onlyShootWhenDesignatorShoots)
                        shouldShoot = true;
                    else if (_designator.IsShooting)
                        shouldShoot = true;
                }
            }
        }

        _intersection = false;
        if (shouldShoot)
        {
            if (_avoidFriendlyFireOtherShips)
            {
                _intersection = IsOccludedByFriendlyShip(ref turretPos, ref targetDirectionNorm);
            }
            if (_avoidFriendlyFire && !_intersection)
            {
                _intersection = CheckForFF(ref turretPos, ref targetDirectionNorm, turret);
            }
            shouldShoot = !_intersection;
        }

        turret.Shoot = shouldShoot;

        _isShooting |= shouldShoot;

        if (turret.EnableIdleRotation)
            turret.EnableIdleRotation = false;
    }

    protected override void PrintDebugInfo()
    {
        Echo($"Targeting: {_isTargeting}" +
            $"\nGrid intersection: {_intersection}" +
            $"\nSlaved turrets: {_slavedTurrets.Count}" +
            $"\nDesignators: {_designators.Count}" +
            $"\nLights: {_lightConfigs.Count}" +
            $"\nTimers: {_timerConfigs.Count}"
        );
    }

    protected override void OnNotFunctional()
    {
        ResetTurretTargeting(_slavedTurrets);
    }

    static void ResetTurretTargeting(List<IMyLargeTurretBase> turrets)
    {
        foreach (var t in turrets)
        {
            if (!t.AIEnabled)
            {
                t.ResetTargetingToDefault();
                t.EnableIdleRotation = false;
                t.Shoot = false;
                t.Range = float.MaxValue;
            }
        }
    }

    protected override Vector3D GetAverageWeaponPosition()
    {
        Vector3D positionSum = Vector3D.Zero;

        if (_slavedTurrets.Count != 0)
        {
            foreach (var block in _slavedTurrets) { positionSum += block.GetPosition(); }
            return positionSum / _slavedTurrets.Count;
        }

        return positionSum;
    }
}

abstract class TurretGroupBase
{
    #region Member Fields
    public Log Log = new Log();
    public StringBuilder
        EchoOutput = new StringBuilder(),
        IniOutput = new StringBuilder();
    public MyIni Ini = new MyIni();
    public IMyBlockGroup Group { get; private set; }
    protected Program _program;
    protected Dictionary<Vector3D, bool> _scannedBlocks = new Dictionary<Vector3D, bool>();
    protected Dictionary<long, FriendlyData> _friendlyData = new Dictionary<long, FriendlyData>();
    protected List<IMyTerminalBlock>
        _groupBlocks = new List<IMyTerminalBlock>(),
        _vitalBlocks = new List<IMyTerminalBlock>();
    protected List<IMyLargeTurretBase> _designators = new List<IMyLargeTurretBase>();
    protected HashSet<IMyCubeGrid>
        _shipGrids = new HashSet<IMyCubeGrid>(),
        _thisTurretGrids = new HashSet<IMyCubeGrid>();
    protected Vector3D _gridVelocity,
        _targetVec,
        _averageWeaponPos,
        _gravity,
        _lastTargetVelocity = Vector3D.Zero;
    protected double _muzzleVelocity,
        _toleranceDotProduct,
        _toleranceAngle,
        _convergenceRange,
        _gameMaxSpeed,
        _autoEngagementRange = 800,
        _gravityMultiplier,
        _rocketInitVelocity,
        _rocketAcceleration;
    protected bool _isSetup = false,
        _firstRun = true,
        _isRocket,
        _intersection = false,
        _avoidFriendlyFire = true,
        _avoidFriendlyFireOtherShips = true,
        _isShooting = false,
        _isTargeting = false,
        _toolsOn = true,
        _onlyShootWhenDesignatorShoots = false,
        _init = false;
    protected long _lastTargetEntityId = 0;
    protected int _framesSinceLastLock = 141,
        _errorCount = 0,
        _warningCount = 0;
    protected string _designatorName;
    protected IMyLargeTurretBase _designator;
    protected List<TimerConfig> _timerConfigs = new List<TimerConfig>();
    protected List<LightConfig> _lightConfigs = new List<LightConfig>();
    protected enum DominantWeaponType { None = 0, Projectile = 1, Rocket = 2 };
    private bool _isInoperable = false;
    #endregion

    #region Constructor
    public TurretGroupBase(IMyBlockGroup group, TurretVariables defaultVars, Program program, HashSet<IMyCubeGrid> shipGrids, Dictionary<long, FriendlyData> friendlyData)
    {
        Group = group;
        _program = program;
        _shipGrids = shipGrids;
        _friendlyData = friendlyData;

        UpdateGeneralSettings(defaultVars);
        GetTurretGroupBlocks(true, defaultVars);
    }
    #endregion

    #region Ini Config
    protected virtual void WriteIni()
    {
        Ini.Clear();
        string name = Group.Name;
        Ini.TryParse(_program.Me.CustomData, Group.Name);
        Ini.Set(name, IniMuzzleVelocity, _muzzleVelocity);
        Ini.Set(name, IniIsRocket, _isRocket);
        Ini.Set(name, IniRocketInitVel, _rocketInitVelocity);
        Ini.Set(name, IniRocketAccel, _rocketAcceleration);
        Ini.Set(name, IniAvoidFriendly, _avoidFriendlyFire);
        Ini.Set(name, IniAvoidFriendlyOtherShips, _avoidFriendlyFireOtherShips);
        Ini.Set(name, IniTolerance, _toleranceAngle);
        Ini.Set(name, IniConvergence, _convergenceRange);
        Ini.Set(name, IniEngagementRange, _autoEngagementRange);
        Ini.Set(name, IniShootWhenDesignatorDoes, _onlyShootWhenDesignatorShoots);
        Ini.Set(name, IniGravityMult, _gravityMultiplier);

        IniOutput.Clear();
        IniOutput.Append(Ini.ToString());
    }

    protected virtual void ParseIni()
    {
        Ini.Clear();
        string name = Group.Name;
        Ini.TryParse(_program.Me.CustomData, name);
        _muzzleVelocity = Ini.Get(name, IniMuzzleVelocity).ToDouble(_muzzleVelocity);
        _isRocket = Ini.Get(name, IniIsRocket).ToBoolean(_isRocket);
        _rocketInitVelocity = Ini.Get(name, IniRocketInitVel).ToDouble(_rocketInitVelocity);
        _rocketAcceleration = Ini.Get(name, IniRocketAccel).ToDouble(_rocketAcceleration);
        _avoidFriendlyFire = Ini.Get(name, IniAvoidFriendly).ToBoolean(_avoidFriendlyFire);
        _avoidFriendlyFireOtherShips = Ini.Get(name, IniAvoidFriendlyOtherShips).ToBoolean(_avoidFriendlyFireOtherShips);
        _convergenceRange = Ini.Get(name, IniConvergence).ToDouble(_convergenceRange);
        _autoEngagementRange = Ini.Get(name, IniEngagementRange).ToDouble(_autoEngagementRange);
        _onlyShootWhenDesignatorShoots = Ini.Get(name, IniShootWhenDesignatorDoes).ToBoolean(_onlyShootWhenDesignatorShoots);
        _gravityMultiplier = Ini.Get(name, IniGravityMult).ToDouble(_gravityMultiplier);

        double t = _toleranceAngle;
        _toleranceAngle = Ini.Get(name, IniTolerance).ToDouble(_toleranceAngle);
        if (t != _toleranceAngle)
            _toleranceDotProduct = Math.Cos(_toleranceAngle * Math.PI / 180);
    }
    #endregion

    #region Grabbing Blocks    
    public virtual void UpdateGeneralSettings(TurretVariables defaultVars)
    {
        if (defaultVars == null)
            return;

        _designatorName = defaultVars.DesignatorNameTag;
        _gameMaxSpeed = defaultVars.GameMaxSpeed;
        _convergenceRange = defaultVars.ConvergenceRange;
        _onlyShootWhenDesignatorShoots = defaultVars.OnlyShootWhenDesignatorShoots;

        double t = _toleranceAngle;
        _toleranceAngle = defaultVars.ToleranceAngle;
        if (t != _toleranceAngle)
            _toleranceDotProduct = Math.Cos(_toleranceAngle * Math.PI / 180);
    }

    public void GetTurretGroupBlocks(bool verbose = false, TurretVariables defaultVars = null)
    {
        UpdateGeneralSettings(defaultVars);
        Group.GetBlocks(_groupBlocks); //TODO optimize this away

        _isSetup = SortBlocks(_groupBlocks, verbose);

        if (!_isSetup)
            return;

        if (_isInoperable)
            _isInoperable = false;

        if (!_init)
            SetInitialWeaponParameters();

        ParseIni();
        WriteIni();
    }

    protected abstract void SetInitialWeaponParameters();

    protected void CollectLight(IMyLightingBlock light)
    {
        LightConfig lightConfig = null;

        bool tgtEnable = true;
        Color tgtColor = light.Color;
        float tgtBlinkInterval = light.BlinkIntervalSeconds;
        float tgtBlinkLength = light.BlinkLength;

        bool idleEnable = false;
        Color idleColor = light.Color;
        float idleBlinkInterval = light.BlinkIntervalSeconds;
        float idleBlinkLength = light.BlinkLength;

        Ini.Clear();
        if (Ini.TryParse(light.CustomData))
        {
            tgtEnable = Ini.Get(IniLightSectionTargeting, IniLightEnable).ToBoolean(tgtEnable);
            tgtColor = MyIniHelper.GetColor(IniLightSectionTargeting, IniLightColor, Ini, tgtColor);
            tgtBlinkInterval = Ini.Get(IniLightSectionTargeting, IniLightBlinkInterval).ToSingle(tgtBlinkInterval);
            tgtBlinkLength = Ini.Get(IniLightSectionTargeting, IniLightBlinkLength).ToSingle(tgtBlinkLength);

            idleEnable = Ini.Get(IniLightSectionIdle, IniLightEnable).ToBoolean(idleEnable);
            idleColor = MyIniHelper.GetColor(IniLightSectionIdle, IniLightColor, Ini, idleColor);
            idleBlinkInterval = Ini.Get(IniLightSectionIdle, IniLightBlinkInterval).ToSingle(idleBlinkInterval);
            idleBlinkLength = Ini.Get(IniLightSectionIdle, IniLightBlinkLength).ToSingle(idleBlinkLength);
        }

        Ini.Set(IniLightSectionTargeting, IniLightEnable, tgtEnable);
        MyIniHelper.SetColor(IniLightSectionTargeting, IniLightColor, tgtColor, Ini);
        Ini.Set(IniLightSectionTargeting, IniLightBlinkInterval, tgtBlinkInterval);
        Ini.Set(IniLightSectionTargeting, IniLightBlinkLength, tgtBlinkLength);

        Ini.Set(IniLightSectionIdle, IniLightEnable, idleEnable);
        MyIniHelper.SetColor(IniLightSectionIdle, IniLightColor, idleColor, Ini);
        Ini.Set(IniLightSectionIdle, IniLightBlinkInterval, idleBlinkInterval);
        Ini.Set(IniLightSectionIdle, IniLightBlinkLength, idleBlinkLength);

        lightConfig = new LightConfig(light, tgtEnable, tgtColor, tgtBlinkInterval, tgtBlinkLength, idleEnable, idleColor, idleBlinkInterval, idleBlinkLength);

        string output = Ini.ToString();
        if (!string.Equals(output, light.CustomData))
        {
            light.CustomData = output;
        }

        if (lightConfig != null)
        {
            _lightConfigs.Add(lightConfig);
        }
    }

    protected void CollectTimer(IMyTimerBlock timer)
    {
        TimerConfig timerConfig = null;

        TimerConfig.TurretState triggerState = TimerConfig.TurretState.Firing;

        bool shouldRetrigger = true;
        double retriggerInterval = 0.1;

        Ini.Clear();
        if (Ini.TryParse(timer.CustomData))
        {
            string triggerStateString = Ini.Get(IniTimerSection, IniTimerTriggerState).ToString();
            if (!Enum.TryParse(triggerStateString, true, out triggerState))
            {
                triggerState = TimerConfig.TurretState.None;
            }

            shouldRetrigger = Ini.Get(IniTimerSection, IniTimerRetrigger).ToBoolean(shouldRetrigger);
            retriggerInterval = Ini.Get(IniTimerSection, IniTimerRetriggerInterval).ToDouble(retriggerInterval);
        }

        Ini.Set(IniTimerSection, IniTimerTriggerState, triggerState.ToString());
        Ini.SetComment(IniTimerSection, IniTimerTriggerState, IniCommentTimerTriggerState);
        Ini.Set(IniTimerSection, IniTimerRetrigger, shouldRetrigger);
        Ini.Set(IniTimerSection, IniTimerRetriggerInterval, retriggerInterval);

        timerConfig = new TimerConfig(timer, triggerState, shouldRetrigger, retriggerInterval);

        string output = Ini.ToString();
        if (!string.Equals(output, timer.CustomData))
        {
            timer.CustomData = output;
        }

        if (timerConfig != null)
        {
            _timerConfigs.Add(timerConfig);
        }
    }

    protected virtual void ClearBlocks()
    {
        _timerConfigs.Clear();
        _lightConfigs.Clear();
        _thisTurretGrids.Clear();
        _designators.Clear();
        _vitalBlocks.Clear();
        _designator = null;
    }

    protected abstract bool SortBlocks(List<IMyTerminalBlock> _groupBlocks, bool verbose);

    protected DominantWeaponType GetDominantWeaponType<T>(List<T> weapons)
    {
        int projectiles = 0;
        int rockets = 0;

        foreach (var b in weapons)
        {
            if (b is IMySmallGatlingGun || b is IMyLargeGatlingTurret || b is IMyLargeInteriorTurret)
                projectiles++;
            else if (b is IMySmallMissileLauncher || b is IMyLargeMissileTurret)
                rockets++;
        }

        if (projectiles == 0 && rockets == 0)
            return DominantWeaponType.None;
        else if (rockets > projectiles)
            return DominantWeaponType.Rocket;
        return DominantWeaponType.Projectile;
    }

    protected void SetWeaponParameters(DominantWeaponType weaponType)
    {
        switch (weaponType)
        {
            case DominantWeaponType.None:
                _muzzleVelocity = 3e8;
                _isRocket = false;
                break;

            case DominantWeaponType.Projectile:
                _muzzleVelocity = 400;
                _isRocket = false;
                break;

            case DominantWeaponType.Rocket:
                _muzzleVelocity = 200; //212.8125;
                _isRocket = true;
                _rocketAcceleration = 600;
                _rocketInitVelocity = 100;
                break;
        }
    }
    #endregion

    #region Main Entrypoint
    public void DoWork(ref Vector3D gridVelocity, ref Vector3D gravity, HashSet<IMyCubeGrid> allShipGrids, Dictionary<long, FriendlyData> friendlyData, bool debugMode)
    {
        EchoOutput.Clear();

        if (_isInoperable)
            return;

        if (_isSetup) // Verify that all vital blocks are working
            _isSetup = VerifyBlocks(_vitalBlocks);

        // If the turret group is not functional, grab blocks and return
        if (!_isSetup)
        {
            GetTurretGroupBlocks(true);
            OnNotFunctional();
            if (!_isSetup)
                _isInoperable = true;
            return;
        }

        _gridVelocity = gridVelocity;
        _gravity = gravity;
        _shipGrids = allShipGrids;
        _friendlyData = friendlyData;

        _averageWeaponPos = GetAverageWeaponPosition();
        _designator = GetDesignatorTurret(_designators, ref _averageWeaponPos);
        _isTargeting = _designator.IsUnderControl || _designator.HasTarget;

        HandleTargeting();
        if (debugMode)
        {
            PrintDebugInfo();
            Echo($"Instruction sum: {_program.Runtime.CurrentInstructionCount}");
        }

    }

    protected abstract void HandleTargeting();

    protected abstract void PrintDebugInfo();

    protected abstract void OnNotFunctional();
    #endregion

    #region Check Friendly Data
    protected bool IsOccludedByFriendlyShip(ref Vector3D position, ref Vector3D direction)
    {
        foreach (var kvp in _friendlyData)
        {
            var friendly = kvp.Value;
            // Friendly out of range
            if (Vector3D.DistanceSquared(friendly.Position, position) > _autoEngagementRange * _autoEngagementRange)
                continue;

            Vector3D toFriendly = friendly.Position - position;
            // Friendly is behind you
            if (Vector3D.Dot(direction, toFriendly) < 0)
                continue;

            Vector3D rejFriendly = VectorMath.Rejection(toFriendly, direction);
            double perpDist = rejFriendly.LengthSquared();
            if (perpDist < friendly.Radius)
            {
                Echo($"> Occluded by friendly ship: {kvp.Key}");
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Helper Functions
    private bool VerifyBlocks(List<IMyTerminalBlock> blocks)
    {
        foreach (var x in blocks)
        {
            if (x.Closed)
                return false;
        }
        return true;
    }

    protected void Echo(string data)
    {
        EchoOutput.AppendLine(data);
    }

    #endregion

    #region Targeting Functions
    protected Vector3D GetTargetPoint(ref Vector3D shooterPos, IMyLargeTurretBase designator)
    {
        if (designator.IsUnderControl)
        {
            _targetVec = designator.GetPosition() + VectorAzimuthElevation(designator) * _convergenceRange;
            _lastTargetEntityId = 0;
        }
        else if (designator.HasTarget)
        {
            var targetInfo = designator.GetTargetedEntity();

            /*
             * We reset our PID controllers and make acceleration compute to zero to handle switching off targets.
             */
            if (targetInfo.EntityId != _lastTargetEntityId)
            {
                _lastTargetVelocity = targetInfo.Velocity;
                OnNewTarget();
            }
            _lastTargetEntityId = targetInfo.EntityId;

            double timeToIntercept = 0;
            double projectileInitSpeed = 0;
            double projectileAccel = 0;
            Vector3D gridVel;
            /*
            ** Predict a cycle ahead to overlead a slight bit. We want to overlead rather
            ** than under lead because the aim point is computed at the beginning of a 0.1 s
            ** time tick. This instead aims near the middle of the current time tick and the
            ** next predicted time tick.
            */
            Vector3D targetPos = 0.5 * MainUpdateInterval * ((Vector3D)targetInfo.Velocity - _gridVelocity) + targetInfo.Position;

            if (_isRocket)
            {
                projectileInitSpeed = _rocketInitVelocity;
                projectileAccel = _rocketAcceleration;
                gridVel = Vector3D.Zero;
            }
            else
            {
                gridVel = _gridVelocity;
            }
            Vector3D targetVel = targetInfo.Velocity;
            timeToIntercept = CalculateTimeToIntercept(_muzzleVelocity, ref gridVel, ref shooterPos, ref targetVel, ref targetPos);
            Vector3D targetAccel = UpdatesPerSecond * (targetInfo.Velocity - _lastTargetVelocity);
            _targetVec = TrajectoryEstimation(timeToIntercept, ref targetPos, ref targetVel, ref targetAccel, _gameMaxSpeed,
                ref shooterPos, ref _gridVelocity, _muzzleVelocity, projectileInitSpeed, projectileAccel, _gravityMultiplier);
                
            if (targetInfo.HitPosition.HasValue) // Not aim at center
            {
                _targetVec += (targetInfo.HitPosition.Value - targetInfo.Position);
            }

            _lastTargetVelocity = targetInfo.Velocity;
        }
        else
        {
            _lastTargetEntityId = 0;
        }

        return _targetVec;
    }

    public virtual void OnNewTarget() { }

    /*
    ** Whip's Projectile Time To Intercept - Modified 07/21/2019
    */
    double CalculateTimeToIntercept(
        double projectileSpeed,
        ref Vector3D shooterVelocity,
        ref Vector3D shooterPosition,
        ref Vector3D targetVelocity,
        ref Vector3D targetPosition)
    {
        double timeToIntercept = -1;

        Vector3D deltaPos = targetPosition - shooterPosition;
        Vector3D deltaVel = targetVelocity - shooterVelocity;
        Vector3D deltaPosNorm = VectorMath.SafeNormalize(deltaPos);

        double closingSpeed = Vector3D.Dot(deltaVel, deltaPosNorm);
        Vector3D closingVel = closingSpeed * deltaPosNorm;
        Vector3D lateralVel = deltaVel - closingVel;

        double diff = projectileSpeed * projectileSpeed - lateralVel.LengthSquared();
        if (diff < 0)
        {
            return 0;
        }

        double projectileClosingSpeed = Math.Sqrt(diff) - closingSpeed;
        double closingDistance = Vector3D.Dot(deltaPos, deltaPosNorm);
        timeToIntercept = closingDistance / projectileClosingSpeed;
        return timeToIntercept;
    }

    Vector3D TrajectoryEstimation(
        double timeToIntercept,
        ref Vector3D targetPos,
        ref Vector3D targetVel,
        ref Vector3D targetAcc,
        double targetMaxSpeed,
        ref Vector3D shooterPos,
        ref Vector3D shooterVel,
        double projectileMaxSpeed,
        double projectileInitSpeed = 0,
        double projectileAccMag = 0,
        double gravityMultiplier = 0)
    {
        bool projectileAccelerates = projectileAccMag > 1e-6;
        bool hasGravity = gravityMultiplier > 1e-6;

        double shooterVelScaleFactor = 1;
        if (projectileAccelerates)
        {
            /*
            This is a rough estimate to smooth out our initial guess based upon the missile parameters.
            The reasoning is that the longer it takes to reach max velocity, the more the initial velocity
            has an overall impact on the estimated impact point.
            */
            shooterVelScaleFactor = Math.Min(1, (projectileMaxSpeed - projectileInitSpeed) / projectileAccMag);
        }

        /*
        Estimate our predicted impact point and aim direction
        */
        Vector3D estimatedImpactPoint = targetPos + timeToIntercept * (targetVel - shooterVel * shooterVelScaleFactor);

        if (!projectileAccelerates && !hasGravity && targetAcc.LengthSquared() < 1e-2)
        {
            return estimatedImpactPoint; // No need to simulate
        }

        Vector3D aimDirection = estimatedImpactPoint - shooterPos;
        Vector3D aimDirectionNorm = VectorMath.SafeNormalize(aimDirection);
        Vector3D projectileVel = shooterVel;
        Vector3D projectilePos = shooterPos;

        if (projectileAccelerates)
        {
            projectileVel += aimDirectionNorm * projectileInitSpeed;
        }
        else
        {
            projectileVel += aimDirectionNorm * projectileMaxSpeed;
        }

        /*
        Target trajectory estimation. We do only 10 steps since PBs are instruction limited.
        */
        double dt = Math.Max(1.0 / 60.0, timeToIntercept * 0.1); // TODO: This can be a const somewhere
        double timeSum = 0;
        double maxSpeedSq = targetMaxSpeed * targetMaxSpeed;
        double projectileMaxSpeedSq = projectileMaxSpeed * projectileMaxSpeed;
        Vector3D targetAccStep = targetAcc * dt;
        Vector3D projectileAccStep = aimDirectionNorm * projectileAccMag * dt;
        Vector3D gravityStep = _gravity * gravityMultiplier * dt;

        Vector3D aimOffset = Vector3D.Zero;
        double minDiff = double.MaxValue;

        for (int i = 0; i < 10; ++i)
        {
            targetVel += targetAccStep;
            if (targetVel.LengthSquared() > maxSpeedSq)
                targetVel = Vector3D.Normalize(targetVel) * targetMaxSpeed;
            targetPos += targetVel * dt;

            if (projectileAccelerates)
            {
                projectileVel += projectileAccStep;
                if (projectileVel.LengthSquared() > projectileMaxSpeedSq)
                {
                    projectileVel = Vector3D.Normalize(projectileVel) * projectileMaxSpeed;
                }
            }

            if (hasGravity)
            {
                projectileVel += gravityStep;
            }

            projectilePos += projectileVel * dt;

            Vector3D diff = (targetPos - projectilePos);
            double diffLenSq = diff.LengthSquared();
            if (diffLenSq < minDiff)
            {
                minDiff = diffLenSq;
                aimOffset = diff;
            }

            timeSum += dt;
            if (timeSum > timeToIntercept)
            {
                break;
            }
        }

        Vector3D lateralOffset = VectorMath.Rejection(aimOffset, aimDirectionNorm);
        return estimatedImpactPoint + lateralOffset;
    }

    protected abstract Vector3D GetAverageWeaponPosition();

    protected static void EnableTurretAI(IMyLargeTurretBase turret)
    {
        if (!turret.AIEnabled)
            turret.ResetTargetingToDefault();

        turret.EnableIdleRotation = false;
    }
    #endregion

    #region Weapon Control
    protected virtual void ToggleWeaponsAndTools<T>(bool isShooting, bool isTargeting, List<T> guns) where T : IMyUserControllableGun
    {
        if (_isShooting != isShooting)
        {
            foreach (var weapon in guns)
            {
                weapon.Shoot = isShooting;
            }
            _isShooting = isShooting;
        }

        foreach (var lightConfig in _lightConfigs)
        {
            lightConfig.Update(isTargeting);
        }

        foreach (var timerConfig in _timerConfigs)
        {
            timerConfig.Update(MainUpdateInterval, isShooting, isTargeting);
        }
    }
    #endregion

    #region Designator Selection
    IMyLargeTurretBase GetDesignatorTurret(List<IMyLargeTurretBase> turretDesignators, ref Vector3D referencePos)
    {
        IMyLargeTurretBase closestTurret = null;
        double closestDistanceSq = double.MaxValue;
        foreach (var block in turretDesignators)
        {
            if (block.IsUnderControl)
                return block;

            if (block.HasTarget)
            {
                var distanceSq = Vector3D.DistanceSquared(block.GetPosition(), referencePos);
                if (distanceSq + 1e-3 < closestDistanceSq)
                {
                    closestDistanceSq = distanceSq;
                    closestTurret = block;
                }
            }
        }

        if (closestTurret == null)
        {
            closestTurret = turretDesignators.Count == 0 ? null : turretDesignators[0];
        }
        return closestTurret;
    }
    #endregion

    #region Vector Math Functions
    protected static Vector3D VectorAzimuthElevation(IMyLargeTurretBase turret)
    {
        double el = turret.Elevation;
        double az = turret.Azimuth;
        Vector3D targetDirection;
        Vector3D.CreateFromAzimuthAndElevation(az, el, out targetDirection);
        return Vector3D.TransformNormal(targetDirection, turret.WorldMatrix);
    }

    protected static void GetRotationAngles(ref Vector3D targetVector, ref MatrixD matrix, out double yaw, out double pitch)
    {
        MatrixD matrixTpose;
        MatrixD.Transpose(ref matrix, out matrixTpose);
        Vector3D localTargetVector;
        Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
        Vector3D flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

        yaw = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); //left is positive
        if (Math.Abs(yaw) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
            yaw = Math.PI;

        if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
            pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
        else
            pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
    }

    protected static void GetAzimuthAngle(ref Vector3D targetVector, ref MatrixD matrix, out double azimuth)
    {
        MatrixD matrixTpose;
        MatrixD.Transpose(ref matrix, out matrixTpose);
        Vector3D localTargetVector;
        Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
        var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

        azimuth = VectorMath.AngleBetween(Vector3D.Forward, flattenedTargetVector) * Math.Sign(localTargetVector.X); //left is positive
        if (Math.Abs(azimuth) < 1E-6 && localTargetVector.Z > 0) //check for straight back case
            azimuth = Math.PI;
    }

    protected static void GetElevationAngle(ref Vector3D targetVector, ref MatrixD matrix, out double pitch)
    {
        MatrixD matrixTpose;
        MatrixD.Transpose(ref matrix, out matrixTpose);
        Vector3D localTargetVector;
        Vector3D.TransformNormal(ref targetVector, ref matrixTpose, out localTargetVector);
        var flattenedTargetVector = new Vector3D(localTargetVector.X, 0, localTargetVector.Z);

        if (Vector3D.IsZero(flattenedTargetVector)) //check for straight up case
            pitch = MathHelper.PiOver2 * Math.Sign(localTargetVector.Y);
        else
            pitch = VectorMath.AngleBetween(localTargetVector, flattenedTargetVector) * Math.Sign(localTargetVector.Y); //up is positive
    }
    #endregion

    #region Intersection Checks
    protected bool CheckForFF(ref Vector3D startPosWorld, ref Vector3D dirnNorm, IMyTerminalBlock ignoredBlock)
    {
        Vector3D endPosWorld = startPosWorld; //This may not be precise if target is off axis by a bunch

        if (_isRocket)
            endPosWorld += (dirnNorm * (_muzzleVelocity - _gridVelocity.Dot(dirnNorm)) - _gridVelocity) * 5;
        else
            endPosWorld += dirnNorm * 1000;

        IMySlimBlock slim = ignoredBlock.CubeGrid.GetCubeBlock(ignoredBlock.Position);

        foreach (var cubeGrid in _shipGrids)
        {
            if (_thisTurretGrids.Contains(cubeGrid))
                continue;

            // Inlined because profiler costs were too damn high
            double radSq = cubeGrid.WorldVolume.Radius + 2.5;
            radSq *= radSq;
            Vector3D startToCenter = cubeGrid.WorldVolume.Center - startPosWorld;

            // Fast check for when we are outside the bounding sphere
            if (startToCenter.LengthSquared() > radSq)
            {
                Vector3D startToEnd = endPosWorld - startPosWorld;
                bool behindSphere = Vector3D.Dot(startToEnd, startToCenter) > 0;
                if (!behindSphere)
                    continue;
                Vector3D perpDist = VectorMath.Rejection(startToCenter, startToEnd);
                if (perpDist.LengthSquared() < radSq)
                    return true;
            }

            Vector3D startPosGrid = WorldToGridVec(ref startPosWorld, cubeGrid);
            Vector3D endPosGrid = WorldToGridVec(ref endPosWorld, cubeGrid);
            var line = new LineD(startPosGrid, endPosGrid);

            Vector3D boxMin = cubeGrid.Min - Vector3I.One;
            Vector3D boxMax = cubeGrid.Max + Vector3I.One;
            var box = new BoundingBoxD(boxMin, boxMax);

            var intersectedLine = new LineD();
            if (!box.Intersect(ref line, out intersectedLine))
            {
                Echo($"> No intersection");
                continue;
            }

            Vector3I startInt = Vector3I.Round(intersectedLine.From);
            Vector3I endInt = Vector3I.Round(intersectedLine.To);

            Vector3D diff = endInt - startInt;
            if (Vector3D.IsZero(diff))
                continue;

            Vector3I sign = Vector3I.Sign((Vector3)diff);
            Vector3D dirn = VectorMath.SafeNormalize(diff);
            Vector3D dirnAbs = dirn * (Vector3D)sign;
            Vector3D tMaxVec = 0.5 / dirnAbs;
            Vector3D tDelta = 2.0 * tMaxVec;

            Vector3I scanPos = startInt;
            for (int i = 0; i < MaxBlocksToCheckForFF; ++i)
            {
                if (i != 0 && BlockExistsAtPoint(cubeGrid, ref scanPos, slim))
                {
                    Echo($"> Intersection at {scanPos}");
                    return true;
                }

                if (!PointInBoxInt(ref scanPos, ref boxMin, ref boxMax))
                    break;

                int idx = GetMinIndex(ref tMaxVec);
                switch (idx)
                {
                    case 0:
                        scanPos.X += sign.X;
                        tMaxVec.X += tDelta.X;
                        break;
                    case 1:
                        scanPos.Y += sign.Y;
                        tMaxVec.Y += tDelta.Y;
                        break;
                    case 2:
                        scanPos.Z += sign.Z;
                        tMaxVec.Z += tDelta.Z;
                        break;
                }
            }

        }
        return false;
    }

    int GetMinIndex(ref Vector3D vec)
    {
        var min = vec.AbsMin();
        if (min == vec.X) return 0;
        if (min == vec.Y) return 1;
        return 2;
    }

    static Vector3D WorldToGridVec(ref Vector3D position, IMyCubeGrid cubeGrid)
    {
        var direction = position - cubeGrid.GetPosition();
        return Vector3D.TransformNormal(direction, MatrixD.Transpose(cubeGrid.WorldMatrix)) / cubeGrid.GridSize;
    }

    static bool BlockExistsAtPoint(IMyCubeGrid cubeGrid, ref Vector3I point, IMySlimBlock blockToIgnore = null)
    {
        if (!cubeGrid.CubeExists(point))
            return false;
        return cubeGrid.GetCubeBlock(point) != blockToIgnore;
    }

    static bool PointInBoxInt(ref Vector3I point, ref Vector3D min, ref Vector3D max)
    {
        return min.X <= point.X && point.X <= max.X &&
            min.Y <= point.Y && point.Y <= max.Y &&
            min.Z <= point.Z && point.Z <= max.Z;
    }

    static bool PointInBox(ref Vector3D point, ref Vector3D min, ref Vector3D max)
    {
        return min.X <= point.X && point.X <= max.X &&
            min.Y <= point.Y && point.Y <= max.Y &&
            min.Z <= point.Z && point.Z <= max.Z;
    }
    #endregion
}
#endregion

#region Helper Classes/Functions
#region General Utilities
void MigrateConfig()
{
    if (!Me.CustomData.Contains(_iniMigrationKey))
        return;

    // Hijack our INI builder for a bit...
    _iniOutput.Clear();
    _iniOutput.Append(Me.CustomData);

    foreach (var keyValue in _iniMigrationDictionary)
    {
        _iniOutput.Replace(keyValue.Key, keyValue.Value);
    }

    Me.CustomData = _iniOutput.ToString();
    _iniOutput.Clear();

    Echo("Config Migrated!\n");
}

const int MAX_BSOD_WIDTH = 40;
const string BSOD_TEMPLATE =
"\n" +
"{0} - v{1}\n" +
"A fatal exception has occured at\n" +
"{2}. The current\n" +
"program will be terminated.\n" +
"\n" +
"EXCEPTION:\n" +
"{3}\n" +
"\n" +
"* Please REPORT this crash message to\n" +
"the Bug Reports discussion of this script\n" +
"\n" +
"* Press RECOMPILE to restart the program";

StringBuilder bsodBuilder = new StringBuilder();
void PrintBsod(IMyTextSurface s, string scriptName, string version, float fontSize, Exception e)
{
    s.ContentType = ContentType.TEXT_AND_IMAGE;
    s.Alignment = TextAlignment.LEFT;
    s.Font = "Monospace";
    s.FontSize = fontSize;
    s.FontColor = Color.White;
    s.BackgroundColor = Color.Blue;
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

    s.WriteText(string.Format(BSOD_TEMPLATE,
                                    scriptName.ToUpperInvariant(),
                                    version,
                                    DateTime.Now,
                                    bsodBuilder));
}

#endregion

#region Turret Group Config Classes
public class TimerConfig
{
    IMyTimerBlock _timer;
    double _retriggerInterval;
    double _timeSinceTrigger;
    bool _shouldRetrigger;
    bool _hasTriggered;

    public enum TurretState { None = 0, Idle = 1, Firing = 2, Targeting = 4, NotIdle = ~Idle, NotFiring = ~Firing, NotTargeting = ~Targeting }
    TurretState _triggerStates;

    public TimerConfig(IMyTimerBlock timer, TurretState triggerState, bool shouldRetrigger, double interval)
    {
        _shouldRetrigger = shouldRetrigger;
        _hasTriggered = false;
        _timer = timer;
        _timeSinceTrigger = 10000;
        _retriggerInterval = interval;
        _triggerStates = triggerState;
    }

    public void Update(double deltaT, bool isFiring, bool isTargeting)
    {
        _timeSinceTrigger += deltaT;

        TurretState state = TurretState.None;
        if (!isFiring && !isTargeting)
        {
            state = TurretState.Idle;
        }
        else if (isFiring)
        {
            state = TurretState.Firing;
        }
        else // isTargeting
        {
            state = TurretState.Targeting;
        }

        if (((_triggerStates & state) != 0))
        {
            if ((!_hasTriggered) || // Any time where the last state was NOT triggered, we want to trigger
                (_shouldRetrigger && _timeSinceTrigger >= _retriggerInterval))
            {
                _timer.Trigger();
                _timeSinceTrigger = 0;
                _hasTriggered = true;
            }
        }
        else
        {
            _hasTriggered = false;
        }
    }
}

public class LightConfig
{
    public IMyLightingBlock Light;

    bool _targetingTurnOn;
    Color _targetingColor;
    float _targetingBlinkInterval;
    float _targetingBlinkLength;

    bool _idleTurnOn;
    Color _idleColor;
    float _idleBlinkInterval;
    float _idleBlinkLength;

    bool _init = false;
    bool _lastState = false;

    public LightConfig(IMyLightingBlock light, bool targetingTurnOn, Color targetingColor, float targetingBlinkInterval, float targetingBlinkLength, bool idleTurnOn, Color idleColor, float idleBlinkInterval, float idleBlinkLength)
    {
        Light = light;
        _targetingTurnOn = targetingTurnOn;
        _targetingColor = targetingColor;
        _targetingBlinkInterval = targetingBlinkInterval;
        _targetingBlinkLength = targetingBlinkLength;
        _idleTurnOn = idleTurnOn;
        _idleColor = idleColor;
        _idleBlinkInterval = idleBlinkInterval;
        _idleBlinkLength = idleBlinkLength;
    }

    public void Update(bool isTargeting)
    {
        if (isTargeting == _lastState && !_init)
            return;

        if (isTargeting)
        {
            Light.Enabled = _targetingTurnOn;
            Light.Color = _targetingColor;
            Light.BlinkIntervalSeconds = _targetingBlinkInterval;
            Light.BlinkLength = _targetingBlinkLength;
        }
        else
        {
            Light.Enabled = _idleTurnOn;
            Light.Color = _idleColor;
            Light.BlinkIntervalSeconds = _idleBlinkInterval;
            Light.BlinkLength = _idleBlinkLength;
        }

        _lastState = isTargeting;
    }
}
#endregion

#region Circular Buffer
public class CircularBuffer<T>
{
    public readonly int Capacity;

    readonly T[] _array = null;
    int _setIndex = 0;
    int _getIndex = 0;

    public CircularBuffer(int capacity)
    {
        if (capacity < 1)
            throw new Exception($"Capacity of CircularBuffer ({capacity}) can not be less than 1");
        Capacity = capacity;
        _array = new T[Capacity];
    }

    public void Add(T item)
    {
        _array[_setIndex] = item;
        _setIndex = ++_setIndex % Capacity;
    }

    public T MoveNext()
    {
        T val = _array[_getIndex];
        _getIndex = ++_getIndex % Capacity;
        return val;
    }

    public T Peek()
    {
        return _array[_getIndex];
    }
}
#endregion


#region Scheduler
public class Scheduler
{
    public double CurrentTimeSinceLastRun = 0;

    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    readonly Program _program;

    const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666;

    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

    public void Init()
    {
        _scheduledActions.Reverse();
    }

    public void Update()
    {
        _inUpdate = true;
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);

        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;

        for (int i = _scheduledActions.Count - 1; i >= 0; --i)
        {
            ScheduledAction action = _scheduledActions[i];
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTime;
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _scheduledActions.RemoveAt(i);
            }
        }

        if (_currentlyQueuedAction == null)
        {
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTime);
            if (_currentlyQueuedAction.JustRan)
            {
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

    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        if (!_inUpdate)
            _scheduledActions.Add(scheduledAction);
        else
            _actionsToAdd.Add(scheduledAction);
    }

    public void AddQueuedAction(Action action, double updateInterval)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001;
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true);
        _queuedActions.Enqueue(scheduledAction);
    }

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

    public ScheduledAction(Action action, double runFrequency = 0, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency;
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

#region PID Class
/// <summary>
/// Discrete time PID controller class.
/// </summary>
public class PID
{
    public double Kp = 0;
    public double Ki = 0;
    public double Kd = 0;

    double _timeStep = 0;
    double _inverseTimeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;

    public double Value { get; private set; }

    public double TimeStep
    {
        get
        {
            return _timeStep;
        }
        set
        {
            if (value == _timeStep)
                return;
            _timeStep = value;
            _inverseTimeStep = 1 / _timeStep;
        }
    }

    public PID(double kP, double kI, double kD, double timeStep)
    {
        Kp = kP;
        Ki = kI;
        Kd = kD;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
    }

    protected virtual double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        return errorSum + currentError * timeStep;
    }

    public double Control(double error)
    {
        var errorDerivative = (error - _lastError) * _inverseTimeStep;

        if (_firstRun)
        {
            errorDerivative = 0;
            _firstRun = false;
        }

        _errorSum = GetIntegral(error, _errorSum, _timeStep);
        _lastError = error;

        this.Value = Kp * error + Ki * _errorSum + Kd * errorDerivative;
        return this.Value;
    }

    public double Control(double error, double timeStep)
    {
        if (timeStep != _timeStep)
        {
            _timeStep = timeStep;
            _inverseTimeStep = 1 / _timeStep;
        }
        return Control(error);
    }

    public void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}

public class DecayingIntegralPID : PID
{
    public double DecayRatio;

    public DecayingIntegralPID(double kP, double kI, double kD, double timeStep, double decayRatio) : base(kP, kI, kD, timeStep)
    {
        DecayRatio = decayRatio;
    }

    protected override double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        return errorSum = errorSum * (1.0 - DecayRatio) + currentError * timeStep;
    }
}
#endregion

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

public static class VectorMath
{
    public static Vector3D SafeNormalize(Vector3D a)
    {
        if (Vector3D.IsZero(a))
            return Vector3D.Zero;

        if (Vector3D.IsUnit(ref a))
            return a;

        return Vector3D.Normalize(a);
    }

    public static Vector3D Rejection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    public static double AngleBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }

    public static double CosBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
    }

    public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
    {
        double dot = Vector3D.Dot(a, b);
        double num = a.LengthSquared() * b.LengthSquared() * tolerance * Math.Abs(tolerance);
        return Math.Abs(dot) * dot > num;
    }
}

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

public static class MyIniHelper
{
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini)
    {
        string colorString = string.Format("{0}, {1}, {2}", color.R, color.G, color.B);
        ini.Set(sectionName, itemName, colorString);
    }

    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0;
        if (rgbSplit.Length != 3)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);

        return new Color(r, g, b, 255);
    }
}

public class Log
{
    public StringBuilder ErrorOutput = new StringBuilder();
    public StringBuilder WarningOutput = new StringBuilder();
    public StringBuilder InfoOutput = new StringBuilder();

    public bool HasContent
    {
        get
        {
            return _errorCount != 0 || _warningCount != 0 || _infoCount != 0;
        }
    }

    int _errorCount;
    int _warningCount;
    int _infoCount;

    string _errorTag = "Error {0}:";
    string _warningTag = "Warning {0}:";
    string _infoTag = "Info {0}:";
    const string _prefix = "  ";

    public void Clear()
    {
        ErrorOutput.Clear();
        WarningOutput.Clear();
        InfoOutput.Clear();
        _errorCount = 0;
        _warningCount = 0;
        _infoCount = 0;
    }

    string IndentIfNeeded(string text)
    {
        if (text.Contains('\n'))
        {
            return text.Replace("\n", "\n" + _prefix);
        }
        return text;
    }

    public void Error(string text, bool lineBreak = true)
    {
        _errorCount++;
        ErrorOutput.Append(string.Format(_errorTag, _errorCount)).Append("\n");
        ErrorOutput.Append(_prefix).Append(IndentIfNeeded(text)).Append("\n");
        if (lineBreak)
            ErrorOutput.Append("\n");
    }

    public void Warning(string text, bool lineBreak = true)
    {
        _warningCount++;
        WarningOutput.Append(string.Format(_warningTag, _warningCount)).Append("\n");
        WarningOutput.Append(_prefix).Append(IndentIfNeeded(text)).Append("\n");
        if (lineBreak)
            WarningOutput.Append("\n");
    }

    public void Info(string text, bool lineBreak = true)
    {
        _infoCount++;
        InfoOutput.Append(string.Format(_infoTag, _infoCount)).Append("\n");
        InfoOutput.Append(_prefix).Append(IndentIfNeeded(text)).Append("\n");
        if (lineBreak)
            InfoOutput.Append("\n");
    }

    public void Write(StringBuilder sb)
    {
        sb.Append(ErrorOutput);
        sb.Append(WarningOutput);
        sb.Append(InfoOutput);
    }
}

class TurretSlaverScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float InteriorTurretSpriteScale = 0.7f;
    const float RotorTurretSpriteScale = 1f;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;
    const string Font = "DEBUG";
    const string TitleFormat = "Whip's Turret Slaver - v{0}";
    readonly string _titleText;

    readonly Vector2 _interiorTurretSpritePos = new Vector2(-100, -70);
    readonly Vector2 _rotorTurretSpritePos = new Vector2(0, 130);

    Program _program;

    int _idx = 100;
    float[] _angles = new float[] { -9, -18, -27, -36, -45, -45, -45, -45, -45, -45, -36, -27, -18, -9 };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public TurretSlaverScreenManager(string version, Program program)
    {
        _titleText = string.Format(TitleFormat, version);
        _program = program;
        _surface = _program.Me.GetSurface(0);
    }

    public void ForceDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    public bool Draw()
    {
        if (_surface == null)
            return false;

        float angle = 0f;
        bool framesLeft = _idx < _angles.Length;
        if (framesLeft)
        {
            angle = MathHelper.ToRadians(_angles[_idx]);
            _idx++;
        }

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
            DrawBaseFrame(frame, screenCenter, minScale, angle);
            DrawTitleBar(_surface, frame, minScale);
        }

        return framesLeft;
    }

    public void Animate()
    {
        ForceDraw();
        _idx = 0;
    }

    void DrawBaseFrame(MySpriteDrawFrame frame, Vector2 centerPos, float scale, float angle = 0f)
    {
        DrawRotorTurretElevation(frame, centerPos + _rotorTurretSpritePos * scale, RotorTurretSpriteScale * scale, angle);
        DrawRotorTurretBase(frame, centerPos + _rotorTurretSpritePos * scale, RotorTurretSpriteScale * scale);

        DrawInteriorTurretElevation(frame, centerPos + _interiorTurretSpritePos * scale, InteriorTurretSpriteScale * scale, angle);
        DrawInteriorTurretBase(frame, centerPos + _interiorTurretSpritePos * scale, InteriorTurretSpriteScale * scale);
    }

    #region Draw Helper Functions
    void DrawTitleBar(IMyTextSurface _surface, MySpriteDrawFrame frame, float scale)
    {
        float titleBarHeight = scale * TitleBarHeightPx;
        Vector2 topLeft = 0.5f * (_surface.TextureSize - _surface.SurfaceSize);
        Vector2 titleBarSize = new Vector2(_surface.TextureSize.X, titleBarHeight);
        Vector2 titleBarPos = topLeft + new Vector2(_surface.TextureSize.X * 0.5f, titleBarHeight * 0.5f);
        Vector2 titleBarTextPos = topLeft + new Vector2(_surface.TextureSize.X * 0.5f, 0.5f * (titleBarHeight - scale * BaseTextHeightPx));

        frame.Add(new MySprite(
            Texture,
            "SquareSimple",
            titleBarPos,
            titleBarSize,
            _topBarColor,
            null,
            Center));

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

    void DrawInteriorTurretBase(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 40f) * scale + centerPos, new Vector2(40f, 80f) * scale, _black, null, Center, 0f));
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(40f, 40f) * scale, _black, null, Center, 0f));
        frame.Add(new MySprite(Texture, "Triangle", new Vector2(0f, 74f) * scale + centerPos, new Vector2(100f, 30f) * scale, _white, null, Center, 0f));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 104f) * scale + centerPos, new Vector2(100f, 30f) * scale, _white, null, Center, 0f));
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(30f, 30f) * scale, _white, null, Center, 0f));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 40f) * scale + centerPos, new Vector2(30f, 80f) * scale, _white, null, Center, 0f));
    }

    void DrawInteriorTurretElevation(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(cos * 50f - sin * -30f, sin * 50f + cos * -30f) * scale + centerPos, new Vector2(50f, 40f) * scale, _white, null, Center, 0f + rotation));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(cos * 82f - sin * -37f, sin * 82f + cos * -37f) * scale + centerPos, new Vector2(14f, 10f) * scale, _white, null, Center, 0f + rotation));
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(cos * 62f - sin * -19f, sin * 62f + cos * -19f) * scale + centerPos, new Vector2(20f, 30f) * scale, _black, null, Center, -1.5708f + rotation));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(cos * -20f - sin * -25f, sin * -20f + cos * -25f) * scale + centerPos, new Vector2(90f, 50f) * scale, _white, null, Center, 0f + rotation));
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, Center, 0f + rotation));
    }

    void DrawRotorTurretBase(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(90f, 90f) * scale, _white, null, Center, 0f));
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(-200f, -50f) * scale + centerPos, new Vector2(200f, 96f) * scale, _white, null, Center, -1.5708f));
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(-2f, -64f) * scale + centerPos, new Vector2(96f, 200f) * scale, _black, null, Center, 0f));
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(0f, -50f) * scale + centerPos, new Vector2(96f, 200f) * scale, _white, null, Center, 0f));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-100f, 0f) * scale + centerPos, new Vector2(96f, 100f) * scale, _white, null, Center, 0f));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-100f, -105f) * scale + centerPos, new Vector2(210f, 110f) * scale, _black, null, Center, 0f));
    }

    void DrawRotorTurretElevation(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(cos * 149f - sin * 0f, sin * 149f + cos * 0f) * scale + centerPos, new Vector2(190f, 100f) * scale, _white, null, Center, 0f + rotation));
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(cos * 242f - sin * 43f, sin * 242f + cos * 43f) * scale + centerPos, new Vector2(16f, 16f) * scale, _black, null, Center, -1.5708f + rotation));
        frame.Add(new MySprite(Texture, "RightTriangle", new Vector2(cos * 242f - sin * -43f, sin * 242f + cos * -43f) * scale + centerPos, new Vector2(16f, 16f) * scale, _black, null, Center, 3.1416f + rotation));
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(cos * 25f - sin * 0f, sin * 25f + cos * 0f) * scale + centerPos, new Vector2(50f, 70f) * scale, _white, null, Center, 0f + rotation));
    }
    #endregion
}

#endregion
#endregion
