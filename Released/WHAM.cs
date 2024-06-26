
#region WHAM
const string Version = "170.22.2";
const string Date = "2024/05/03";
const string CompatVersion = "95.0.0";

/*
/ //// / (WHAM) Whip's Homing Advanced Missile Script / //// /
_______________________________
    INSTRUCTIONS

See workshop page for instructions, there is no room left in this script!

_______________________________
    NOTE

This code has been minified so that it will fit in the programmable block.
I have NOT obfuscated any of the code, so that if you copy paste this into an
IDE like Visual Studio or use a website like https://codebeautify.org/csharpviewer,
you can uncompress this and it will be human readable.




=================================================
    DO NOT MODIFY VARIABLES IN THE SCRIPT!

    USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!

*/

#region Global Fields

enum GuidanceAlgoType { ProNav, WhipNav, HybridNav, QuadraticIntercept };

GuidanceBase _selectedGuidance;
Dictionary<GuidanceAlgoType, GuidanceBase> _guidanceAlgorithms;

const string MissileNamePattern = "({0} {1})";
const string MissileGroupPattern = "{0} {1}";

Vector3D
    _shooterForwardVec,
    _shooterLeftVec,
    _shooterUpVec,
    _shooterPos,
    _shooterVel,
    _lastShooterPos,
    _shooterPosCached,
    _randomizedHeadingVector,
    _targetPos,
    _targetVel,
    _aimDispersion;

string
    _missileGroupNameTag = "",
    _missileNameTag = "";

double
    _timeSinceLastLock = 0,
    _distanceFromShooter = 0,
    _timeTotal = 0,
    _timeSinceLastIngest = 0,
    _fuelConservationCos;

int _setupTicks = 0;

bool
    _shouldFire = false,
    _shouldKill = false,
    _hasPassed = false,
    _killAllowed = false,
    _shouldDive = false,
    _shouldStealth = true,
    _shouldProximityScan = false,
    _enableGuidance = false,
    _broadcastListenersRegistered = false,
    _foundLampAntennas = false,
    _markedForDetonation = false,
    _canSetup = true,
    _preSetupFailed = false,
    _topDownAttack = false,
    _enableEvasion = false,
    _precisionMode = false,
    _retask = false;

#region Meme Mode Stuff
enum AntennaNameMode { Meme, Empty, Custom, MissileName, MissileStatus };

int _memeIndex;
string[] _antennaMemeMessages = new string[]
{
    "All your base are belong to us",
    "Pucker up buttercup",
    "You are screwed",
    "Are you my mommy?",
    "You feeling lucky?",
    "Herpes",
    "Pootis",
    "From Whip with love!",
    "Run!",
    "General Distress",
    "Unknown Signal",
    "Here comes the pain",
    "Bend over",
    "Private Shipment",
    "Argentavis",
    "Cargo Hauler",
    "Art thou feeling it now Mr. Krabs?",
    "It's commin' right for us!",
    "Nothing personal kid...",
    "*Fortnite dancing in public*",
    "*Dabbing intensifies*",
    "*Heavy breathing*",
    "A surprise to be sure, but a welcome one",
    "Hello there",
    "General Kenobi",
    "You underestimate my power",
    "I am the SENATE!",
    "Did you ever hear the tragedy of Darth Plagueis The Wise?",
    "I thought not. It's not a story the Jedi would tell you.",
    "It's a Sith legend.",
    "Darth Plagueis was a Dark Lord of the Sith...",
    "...so powerful and so wise he could use the Force...",
    "...to influence the midichlorians to create life...",
    "*Evil head turn*",
    "He had such a knowledge of the dark side...",
    "...that he could even keep the ones he cared about from dying.",
    "The dark side of the Force is a pathway to many abilities...",
    "...some consider to be unnatural.",
    "He became so powerful, the only thing he was afraid of...",
    "...was losing his power, which eventually, of course, he did.",
    "Unfortunately, he taught his apprentice everything he knew...",
    "...then his apprentice killed him in his sleep.",
    "Ironic.",
    "He could save others from death, but not himself.",
    "Another happy landing!",
    "*Internalized Oppression*",
    "Area is not secure!",
    "Perfectly balanced, as all things should be",
    "It's super effective!",
    "Ah yes, an old friend of mine is here",
    "Anything else you'd like to order?",
    "Hi! Welcome to Chili's!",
    "Platinum",
    "Gold",
    "Dinner is served!",
    "UwU",
    "* Arrow to the knee *",
    "Atmospheric Lander",
    "Just like mother used to make!",
    "Big Chungus",
    "Hecking Bamboozolled",
    "Time to take out the trash",
    "BUT WAIT!! THERE'S MORE!!",
    "Is this the Krusty Krab?",
    "No this is PATRICK!!!",
    "I'm about to end this whole man's career..",
    "Do you get to the Cloud District very often?",
    "It's over Anakin!",
    "I have the high ground!",
    "You underestimate my POWER!",
    "You were the chosen one!",
    "It was said you would destroy the sith not join them...",
    "...Bring balance to the force, not leave it in darkness!",
    "We've been trying to reach you about your car's extended warranty",
};
#endregion
Random RNGesus = new Random();
BatesDistributionRandom _bellCurveRandom = new BatesDistributionRandom(3);

//So many lists...
List<IMyTerminalBlock> _missileBlocks = new List<IMyTerminalBlock>();
List<IMyFunctionalBlock> _funcBlocks = new List<IMyFunctionalBlock>();
List<IMyThrust>
    _unsortedThrusters = new List<IMyThrust>(),
    _mainThrusters = new List<IMyThrust>(),
    _sideThrusters = new List<IMyThrust>(),
    _detachThrusters = new List<IMyThrust>();
List<IMyVirtualMass> _artMasses = new List<IMyVirtualMass>();
List<IMyShipMergeBlock> _mergeBlocks = new List<IMyShipMergeBlock>();
List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>();
List<IMyShipController> _shipControllers = new List<IMyShipController>();
List<IMyGyro> _gyros = new List<IMyGyro>();
List<IMyShipConnector> _connectors = new List<IMyShipConnector>();
List<IMyMotorStator> _rotors = new List<IMyMotorStator>();
List<IMyReactor> _reactors = new List<IMyReactor>();
List<IMyRadioAntenna> _antennas = new List<IMyRadioAntenna>();
List<IMySensorBlock> _sensors = new List<IMySensorBlock>();
List<IMyWarhead> _warheads = new List<IMyWarhead>();
List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> _homingCameras = new List<IMyCameraBlock>();
List<IMyGasTank> _gasTanks = new List<IMyGasTank>();
List<MyDetectedEntityInfo> _sensorEntities = new List<MyDetectedEntityInfo>();

ImmutableArray<MyTuple<byte, long, Vector3D, double>>.Builder _messageBuilder = ImmutableArray.CreateBuilder<MyTuple<byte, long, Vector3D, double>>();
List<MyTuple<Vector3D, long>> _remoteFireRequests = new List<MyTuple<Vector3D, long>>();

long _senderKeycode = 0;

IMyShipController _missileReference = null;

enum PostSetupAction { None = 0, Fire = 1, FireRequestResponse = 2 };
PostSetupAction _postSetupAction = PostSetupAction.None;

const int MaxInstructionsPerRun = 5000;

const double
    Tick = 1.0 / 60.0,
    UpdatesPerSecond = 10.0,
    SecondsPerUpdate = 1.0 / UpdatesPerSecond,
    DegToRad = Math.PI / 180,
    RpmToRad = Math.PI / 30,
    TopdownDescentAngle = Math.PI / 6,
    AntennaEnableDelay = 2, // To prevent HUD bug where they show up when they shouldn't
    MaxGuidanceTime = 180,
    RuntimeToRealtime = Tick / 0.0166666,
    GyroSlowdownAngle = Math.PI / 36;

const float MinThrust = 1e-9f;

readonly MyIni _guidanceIni = new MyIni();
readonly StringBuilder _saveSB = new StringBuilder();
readonly RaycastHoming _raycastHoming;

enum GuidanceMode : int { BeamRiding = 1, SemiActive = 2, Active = 4, Homing = SemiActive | Active };

PID _yawPID = new PID(1, 0, 0, SecondsPerUpdate),
    _pitchPID = new PID(1, 0, 0, SecondsPerUpdate);
IMyBlockGroup _missileGroup;
Scheduler _scheduler;
GuidanceMode _guidanceMode = GuidanceMode.SemiActive;
RuntimeTracker _runtimeTracker;
IMyBroadcastListener
    _broadcastListenerHoming,
    _broadcastListenerBeamRiding,
    _broadcastListenerParameters,
    _broadcastListenerRemoteFire;
IMyUnicastListener _unicastListener;

const string
    IgcTagParams = "IGC_MSL_PAR_MSG",
    IgcTagHoming = "IGC_MSL_HOM_MSG",
    IgcTagBeamRiding = "IGC_MSL_OPT_MSG",
    IgcTagIff = "IGC_IFF_PKT",
    IgcTagFire = "IGC_MSL_FIRE_MSG",
    IgcTagRemoteFireRequest = "IGC_MSL_REM_REQ",
    IgcTagRemoteFireResponse = "IGC_MSL_REM_RSP",
    IgcTagRemoteFireNotification = "IGC_MSL_REM_NTF",
    IgcTagRegister = "IGC_MSL_REG_MSG",
    IgcTagUnicast = "UNICAST",
    IniSectionMisc = "Misc.",
    IniCompatMemeMode = "Antenna meme mode";

ScheduledAction
    _guidanceActivateAction,
    _randomHeadingVectorAction;

enum LaunchStage { None = 0, Intiate = 1, Detach = 2, Drift = 3, Flight = 4, Idle = None }
StateMachine _launchSM = new StateMachine();
LaunchState _initiateState, _detachState, _driftState;
State _flightState;

bool InFlight
{
    get
    {
        return (LaunchStage)_launchSM.StateId == LaunchStage.Flight;
    }
}

#region Custom Data Ini
MyIni _ini = new MyIni();

ConfigSection _namesConfig = new ConfigSection("Names");
ConfigBool _autoConfigure = new ConfigBool("Auto-configure missile name", true);
ConfigString _missileTag = new ConfigString("Missile name tag", "Missile");
ConfigInt _missileNumber = new ConfigInt("Missile number", 1);
ConfigString _fireControlGroupNameTag = new ConfigString("Fire control group name", "Fire Control");
ConfigString _detachThrustTag = new ConfigString("Detach thruster name tag", "Detach");

ConfigSection _delaysConfig = new ConfigSection("Delays");
ConfigDouble
    _guidanceDelay = new ConfigDouble("Guidance delay (s)", 1),
    _disconnectDelay = new ConfigDouble("Stage 1: Disconnect delay (s)", 0),
    _detachDuration = new ConfigDouble("Stage 2: Detach duration (s)", 0),
    _mainIgnitionDelay = new ConfigDouble("Stage 3: Main ignition delay (s)", 0);

ConfigSection _gyrosConfig = new ConfigSection("Gyros");
ConfigDouble
    _gyroProportionalGain = new ConfigDouble("Proportional gain", 10),
    _gyroIntegralGain = new ConfigDouble("Integral gain", 0),
    _gyroDerivativeGain = new ConfigDouble("Derivative gain", 10);

ConfigSection _homingConfig = new ConfigSection("Homing Parameters");
ConfigEnum<GuidanceAlgoType> _guidanceAlgoType = new ConfigEnum<GuidanceAlgoType>("Guidance algorithm", GuidanceAlgoType.ProNav, " Valid guidance algorithms:\n ProNav, WhipNav, HybridNav, QuadraticIntercept");
ConfigDouble
    _navConstant = new ConfigDouble("Navigation constant", 3),
    _accelNavConstant = new ConfigDouble("Acceleration constant", 1.5),
    _maxAimDispersion = new ConfigDouble("Max aim dispersion (m)", 0),
    _topDownAttackHeight = new ConfigDouble("Topdown attack height (m)", 1500);

ConfigSection _beamRideConfig = new ConfigSection("Beam Riding Parameters");
ConfigDouble
    _offsetUp = new ConfigDouble("Hit offset up (m)", 0),
    _offsetLeft = new ConfigDouble("Hit offset left (m)", 0);

ConfigSection _evasionConfig = new ConfigSection("Evasion Parameters");
ConfigDouble
    _randomVectorInterval = new ConfigDouble("Direction change interval (sec)", 0.5),
    _maxRandomAccelRatio = new ConfigDouble("Max acceleration ratio", 0.25);

ConfigSection _raycastConfig = new ConfigSection("Raycast/Sensors");
ConfigBool _useCamerasForHoming = new ConfigBool("Use cameras for homing", true);
ConfigDouble
    _raycastRange = new ConfigDouble("Tripwire range (m)", 2.5),
    _raycastMinimumTargetSize = new ConfigDouble("Minimum target size (m)", 0),
    _minimumArmingRange = new ConfigDouble("Minimum warhead arming range (m)", 100);
ConfigBool
    _raycastIgnoreFriends = new ConfigBool("Ignore friendlies", false),
    _raycastIgnorePlanetSurface = new ConfigBool("Ignore planets", true),
    _ignoreIdForDetonation = new ConfigBool("Ignore target ID for detonation", false);

ConfigSection _miscConfig = new ConfigSection(IniSectionMisc);
ConfigDouble _missileSpinRPM = new ConfigDouble("Spin rate (RPM)", 0);
ConfigBool
    _allowRemoteFire = new ConfigBool("Allow remote firing", false),
    _requireAntenna = new ConfigBool("Require antenna on missile", true,
        " Recommended value is true.\n" +
        " Setting this to false *will* result in degraded reliability\n" +
        " and will prevent missiles from receiving mid-course corrections\n" +
        " or commands from the firing ship.\n" +
        " If remote fire is enabled, this will be forced to true.");
ConfigEnum<AntennaNameMode> _antennaMode = new ConfigEnum<AntennaNameMode>("Antenna name mode", AntennaNameMode.Meme, " Valid antenna name modes:\n Meme, Empty, Custom, MissileName, MissileStatus");

ConfigSection _fuelConservationConfig = new ConfigSection("Fuel Conservation");
ConfigBool _conserveFuel = new ConfigBool("Conserve fuel", false, " If enabled, the missile will cut thrust when near max speed to attempt\n to save fuel/power. This will make the missile LESS ACCURATE!");
ConfigDouble _fuelConservationMaxSpeed = new ConfigDouble("Max speed (m/s)", 95);
ConfigDouble _fuelConservationAngle = new ConfigDouble("Angle tolerance (deg)", 2.5, " Smaller angles make the missile more accurate but will use more fuel");

ConfigSection _stageTriggerConfig = new ConfigSection("WHAM - Stage Trigger");
ConfigEnum<LaunchStage> _triggerOnStage = new ConfigEnum<LaunchStage>("Trigger on launch stage", LaunchStage.None, " Valid launch stages are:\n Intiate, Detach, Drift, Flight");

ConfigSection _rangeTriggerConfig = new ConfigSection("WHAM - Range Trigger");
ConfigNullable<double> _triggerAtRange = new ConfigNullable<double>(new ConfigDouble("Trigger at range (m)", 200, " Range from target to trigger this timer"));

class RangeTimer
{
    IMyTimerBlock _timer;
    double _rangeSq;
    bool _latched = false;

    public RangeTimer(IMyTimerBlock t, double r)
    {
        _timer = t;
        _rangeSq = r * r;
    }

    public void Update(double rSq)
    {
        if (_latched)
        {
            return;
        }

        if (rSq <= _rangeSq)
        {
            _timer.Trigger();
            _latched = true;
        }
    }
}

class StageTimer
{
    IMyTimerBlock _timer;
    LaunchStage _stage;
    bool _latched = false;

    public StageTimer(IMyTimerBlock t, LaunchStage s)
    {
        _timer = t;
        _stage = s;
    }

    public void Update(LaunchStage s)
    {
        if (_latched)
        {
            return;
        }
        
        if (_stage <= s)
        {
            _timer.Trigger();
            _latched = true;
        }
    }
}

List<RangeTimer> _rangeTimers = new List<RangeTimer>();
List<StageTimer> _stageTimers = new List<StageTimer>();

ConfigSection[] _config;

enum LogLevel { Info, Warning, Error, Fail, Success }
Logger _logger;

void SetupConfig()
{
    _config = new ConfigSection[]
    {
        _namesConfig,
        _delaysConfig,
        _gyrosConfig,
        _homingConfig,
        _beamRideConfig,
        _evasionConfig,
        _raycastConfig,
        _fuelConservationConfig,
        _miscConfig,
    };

    _namesConfig.AddValues(
        _autoConfigure,
        _missileTag,
        _missileNumber,
        _fireControlGroupNameTag,
        _detachThrustTag
    );

    _delaysConfig.AddValues(
        _guidanceDelay,
        _disconnectDelay,
        _detachDuration,
        _mainIgnitionDelay
    );

    _gyrosConfig.AddValues(
        _gyroProportionalGain,
        _gyroIntegralGain,
        _gyroDerivativeGain
    );

    _homingConfig.AddValues(
        _guidanceAlgoType,
        _navConstant,
        _accelNavConstant,
        _maxAimDispersion,
        _topDownAttackHeight
    );

    _beamRideConfig.AddValues(
        _offsetUp,
        _offsetLeft
    );

    _evasionConfig.AddValues(
        _randomVectorInterval,
        _maxRandomAccelRatio
    );

    _raycastConfig.AddValues(
        _useCamerasForHoming,
        _raycastRange,
        _raycastMinimumTargetSize,
        _minimumArmingRange,
        _raycastIgnoreFriends,
        _raycastIgnorePlanetSurface,
        _ignoreIdForDetonation
    );

    _fuelConservationConfig.AddValues(
        _conserveFuel,
        _fuelConservationMaxSpeed,
        _fuelConservationAngle
    );

    _miscConfig.AddValues(
        _missileSpinRPM,
        _allowRemoteFire,
        _antennaMode,
        _requireAntenna
    );

    _stageTriggerConfig.AddValues(
        _triggerOnStage
    );

    _rangeTriggerConfig.AddValues(
        _triggerAtRange
    );
}
#endregion

#endregion

#region Main Methods
Program()
{
    _scheduler = new Scheduler(this, true);

    _logger = new Logger(_setupBuilder);
    _logger.RegisterType(LogLevel.Info, "> INFO:", new Color(0, 170, 255));
    _logger.RegisterType(LogLevel.Warning, "> WARN:", new Color(255, 255, 0));
    _logger.RegisterType(LogLevel.Error, "> ERROR:", new Color(255, 0, 0));
    _logger.RegisterType(LogLevel.Fail, "", null, new Color(255, 0, 0));
    _logger.RegisterType(LogLevel.Success, "", null, new Color(0, 250, 0));

    SetupConfig();
    SetupLaunchStages();

    _memeIndex = RNGesus.Next(_antennaMemeMessages.Length);

    _unicastListener = IGC.UnicastListener;
    _unicastListener.SetMessageCallback(IgcTagUnicast);

    _broadcastListenerRemoteFire = IGC.RegisterBroadcastListener(IgcTagRemoteFireRequest);
    _broadcastListenerRemoteFire.SetMessageCallback(IgcTagRemoteFireRequest);

    _guidanceActivateAction = new ScheduledAction(ActivateGuidance, 0, true);
    _randomHeadingVectorAction = new ScheduledAction(ComputeRandomHeadingVector, 0, false);

    // Setting up scheduled tasks
    _scheduler.AddScheduledAction(LaunchStaging, UpdatesPerSecond);
    _scheduler.AddScheduledAction(_guidanceActivateAction);
    _scheduler.AddScheduledAction(GuidanceNavAndControl, UpdatesPerSecond);
    _scheduler.AddScheduledAction(CheckProximity, UpdatesPerSecond);
    _scheduler.AddScheduledAction(PrintEcho, 1);
    _scheduler.AddScheduledAction(NetworkTargets, 6);
    _scheduler.AddScheduledAction(_randomHeadingVectorAction);

    _runtimeTracker = new RuntimeTracker(this, 120, 0.005);

    _raycastHoming = new RaycastHoming(5000, 3, 0, Me.CubeGrid.EntityId);
    _raycastHoming.AddEntityTypeToFilter(MyDetectedEntityType.FloatingObject, MyDetectedEntityType.Planet, MyDetectedEntityType.Asteroid);

    // Populate guidance algos
    _guidanceAlgorithms = new Dictionary<GuidanceAlgoType, GuidanceBase>()
    {
        { GuidanceAlgoType.ProNav, new ProNavGuidance(UpdatesPerSecond, _navConstant) },
        { GuidanceAlgoType.WhipNav, new WhipNavGuidance(UpdatesPerSecond, _navConstant) },
        { GuidanceAlgoType.HybridNav, new HybridNavGuidance(UpdatesPerSecond, _navConstant) },
        { GuidanceAlgoType.QuadraticIntercept, new QuadraticInterceptGuidance(new ProNavGuidance(UpdatesPerSecond, _navConstant)) },
    };

    // Enable raycast spooling
    GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(null, camera =>
    {
        if (camera.IsSameConstructAs(Me))
            camera.EnableRaycast = true;
        return false;
    });

    //load targeting data from last save
    ParseStorage();
    if (_postSetupAction != PostSetupAction.Fire)
    {
        InitiateSetup();
    }
}

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & (UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script)) != 0)
        ArgumentHandling(arg);

    if ((updateSource & UpdateType.Once) != 0)
    {
        RunSetupStateMachine();
    }

    if ((updateSource & UpdateType.Update10) != 0)
    {
        SendRemoteFireResponse();
        Runtime.UpdateFrequency &= ~UpdateFrequency.Update10;
    }

    bool igcMsg = (updateSource & UpdateType.IGC) != 0;
    if (igcMsg)
    {
        IgcMessageHandling(_shouldFire);
    }

    if (!_shouldFire)
    {
        return;
    }

    _scheduler.Update();

    var lastRuntime = Math.Min(RuntimeToRealtime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0), SecondsPerUpdate);
    _timeTotal += lastRuntime;
    _timeSinceLastIngest += lastRuntime;

    if (_shouldKill)
        KillPower();

    _runtimeTracker.AddRuntime();
    _runtimeTracker.AddInstructions();
}

void ActiveHomingScans()
{
    if (_homingCameras.Count == 0 || _shipControllers.Count == 0)
        return;

    _raycastHoming.Update(SecondsPerUpdate, _homingCameras, _shipControllers);

    if (_raycastHoming.Status == RaycastHoming.TargetingStatus.Locked)
    {
        // handoff
        _guidanceMode = GuidanceMode.Active;

        _targetPos = _raycastHoming.TargetPosition;
        _targetVel = _raycastHoming.TargetVelocity;
        _timeSinceLastLock = _raycastHoming.TimeSinceLastLock;

        // Force time since last ingest to be zero since there is no transmission time for active homing scans
        _timeSinceLastIngest = 0;
    }
    else if (_raycastHoming.LockLost)
    {
        _guidanceMode = GuidanceMode.SemiActive;
    }
}

void LaunchStaging()
{
    _launchSM.Update();

    foreach (var t in _stageTimers)
    {
        t.Update((LaunchStage)_launchSM.StateId);
    }
}

void PrintEcho()
{
    Echo(GetTitle());
    Echo($"Time Active: {_timeTotal:n0} sec");
    Echo(_runtimeTracker.Write());
}
#endregion

#region Logging
void LogInfo(string text)
{
    _logger.Log(LogLevel.Info, text);
}

void LogWarning(string text)
{
    _logger.Log(LogLevel.Warning, text);
}

void LogError(string text)
{
    _logger.Log(LogLevel.Error, text);
}
#endregion

#region Ini Configuration
void LoadIniConfig()
{
    _ini.Clear();

    bool parsed = _ini.TryParse(Me.CustomData);
    if (!parsed)
    {
        SaveIniConfig();
        _setupBuilder.Append("Wrote default missile config!\n");
        return;
    }

    foreach (var c in _config)
    {
        c.ReadFromIni(_ini);
    }

    _fuelConservationCos = Math.Cos(MathHelper.ToRadians(_fuelConservationAngle));

    if (_allowRemoteFire && !_requireAntenna)
    {
        _requireAntenna.Value = true;
    }

    // For backwards compat
    bool antennaMemeMode;
    if (_ini.Get(IniSectionMisc, IniCompatMemeMode).TryGetBoolean(out antennaMemeMode))
    {
        _antennaMode.Value = antennaMemeMode ? AntennaNameMode.Meme : AntennaNameMode.Empty;
    }

    _setupBuilder.Append("Loaded missile config!\n");
}

void SaveIniConfig()
{
    _ini.Clear();

    _missileGroupNameTag = string.Format(MissileGroupPattern, _missileTag, _missileNumber);
    _missileNameTag = string.Format(MissileNamePattern, _missileTag, _missileNumber);
    _maxRandomAccelRatio.Value = MathHelper.Clamp(_maxRandomAccelRatio, 0, 1);

    foreach (var c in _config)
    {
        c.WriteToIni(_ini);
    }

    _guidanceActivateAction.RunInterval = _guidanceDelay;
    _randomHeadingVectorAction.RunInterval = _randomVectorInterval;
    _initiateState.Duration = _disconnectDelay;
    _detachState.Duration = _detachDuration;
    _driftState.Duration = _mainIgnitionDelay;

    Me.CustomData = _ini.ToString();
}
#endregion

#region Argument Handling and IGC Processing
void ProcessRemoteFireRequests()
{
    Runtime.UpdateFrequency |= UpdateFrequency.Update10;
    // On 10, send
    float antennaRange = 1f;
    foreach (MyTuple<Vector3D, long> request in _remoteFireRequests)
    {
        Vector3D requestPos = request.Item1;
        float dSq = (float)Vector3D.DistanceSquared(requestPos, Me.GetPosition());
        if (dSq > antennaRange)
        {
            antennaRange = dSq;
        }
    }
    antennaRange = (float)Math.Sqrt(antennaRange) + 100f;

    foreach (var a in _antennas)
    {
        if (a.Closed)
            continue;
        a.Radius = (float)antennaRange;
        a.EnableBroadcasting = true;
        a.Enabled = true;
        break;
    }
}

void SendRemoteFireResponse()
{
    foreach (MyTuple<Vector3D, long> request in _remoteFireRequests)
    {
        long programId = request.Item2;

        var response = new MyTuple<Vector3D, long>();
        response.Item1 = Me.GetPosition();
        response.Item2 = Me.EntityId;

        IGC.SendUnicastMessage(programId, IgcTagRemoteFireResponse, response);
    }

    _remoteFireRequests.Clear();

    foreach (var a in _antennas)
    {
        if (a.Closed)
            continue;
        a.Radius = 1f;
        a.EnableBroadcasting = false;
        a.Enabled = _allowRemoteFire && !_foundLampAntennas;
        break;
    }
}

void IgcMessageHandling(bool shouldFire)
{
    if (!shouldFire)
    {
        if (_allowRemoteFire)
        {
            // Handle remote fire requests
            bool remoteFireRequest = false;
            while (_broadcastListenerRemoteFire.HasPendingMessage)
            {
                object messageData = _broadcastListenerRemoteFire.AcceptMessage().Data;
                if (messageData is MyTuple<Vector3D, long>)
                {
                    var payload = (MyTuple<Vector3D, long>)messageData;
                    _remoteFireRequests.Add(payload);
                    remoteFireRequest = true;
                }
            }

            if (remoteFireRequest)
            {
                _postSetupAction = PostSetupAction.FireRequestResponse;
                InitiateSetup(true);
            }
        }

        // Handle unicast messages
        bool locallyFired = false;
        bool remotelyFired = false;
        while (_unicastListener.HasPendingMessage)
        {
            MyIGCMessage message = _unicastListener.AcceptMessage();
            if (message.Tag == IgcTagFire)
            {
                if (GridTerminalSystem.GetBlockWithId(message.Source) != null)
                {
                    locallyFired = true;
                    _senderKeycode = message.Source;
                    break;
                }
            }
            else if (message.Tag == IgcTagRegister)
            {
                if (_allowRemoteFire)
                {
                    remotelyFired = true;
                    _senderKeycode = message.Source;
                    break;
                }
            }
        }

        if (locallyFired || remotelyFired)
        {
            _postSetupAction = PostSetupAction.Fire;
            InitiateSetup(remotelyFired);
            if (remotelyFired)
            {
                IGC.SendBroadcastMessage(IgcTagRemoteFireNotification, _missileNumber.Value, TransmissionDistance.CurrentConstruct);
            }
        }

        return;
    }
    else
    {
        // Handle broadcast listeners
        while (_broadcastListenerParameters.HasPendingMessage)
        {
            MyIGCMessage message = _broadcastListenerParameters.AcceptMessage();
            long keycode = message.Source; //payload.Item2;
            if (_senderKeycode != keycode)
                continue;

            object messageData = message.Data;
            if (!(messageData is MyTuple<byte, long>))
                continue;

            var payload = (MyTuple<byte, long>)messageData;

            byte packedBools = payload.Item1;
            if (_killAllowed && !_shouldKill)
                _shouldKill = (packedBools & (1)) != 0;

            _shouldStealth = (packedBools & (1 << 1)) != 0;
            _enableEvasion = (packedBools & (1 << 2)) != 0;
            _topDownAttack = (packedBools & (1 << 3)) != 0;
            _precisionMode = (packedBools & (1 << 4)) != 0;
            _retask = (packedBools & (1 << 5)) != 0;

            if (_shouldKill)
                _shouldStealth = true;

            _raycastHoming.OffsetTargeting = _precisionMode;
        }

        while (_broadcastListenerBeamRiding.HasPendingMessage)
        {
            MyIGCMessage message = _broadcastListenerBeamRiding.AcceptMessage();
            long keycode = message.Source; //payload.Item5;
            if (_senderKeycode != keycode)
                continue;

            object messageData = message.Data;
            if (_guidanceMode == GuidanceMode.Active && !_retask)
                continue;

            if (!(messageData is MyTuple<Vector3, Vector3, Vector3, Vector3, long>))
                continue;

            var payload = (MyTuple<Vector3, Vector3, Vector3, Vector3, long>)messageData;

            _retask = false;
            _shooterForwardVec = payload.Item1;
            _shooterLeftVec = payload.Item2;
            _shooterUpVec = payload.Item3;
            _shooterPosCached = payload.Item4;

            _guidanceMode = GuidanceMode.BeamRiding;
        }

        // Item1.Col0: Hit position
        // Item1.Col1: Target position
        // Item1.Col2: Target velocity
        // Item2.Col0: Precision offset
        // Item2.Col1: Shooter position
        // Item2.Col2: <NOT USED>
        // Item3:      Time since last lock
        // Item4:      Target ID
        // Item5:      Key code
        while (_broadcastListenerHoming.HasPendingMessage)
        {
            MyIGCMessage message = _broadcastListenerHoming.AcceptMessage();
            long keycode = message.Source; //payload.Item5;
            if (_senderKeycode != keycode)
                continue;

            object messageData = message.Data;
            if (!(messageData is MyTuple<Matrix3x3, Matrix3x3, float, long, long>))
                continue;

            var payload = (MyTuple<Matrix3x3, Matrix3x3, float, long, long>)messageData;

            _shooterPosCached = payload.Item2.Col1;
            double timeSinceLock = payload.Item3 + Tick;
            long targetId = payload.Item4;

            if (_guidanceMode == GuidanceMode.Active)
            {
                if (_retask)
                {
                    _guidanceMode = GuidanceMode.SemiActive;
                }
                else if (targetId != _raycastHoming.TargetId ||
                    timeSinceLock > _raycastHoming.TimeSinceLastLock)
                {
                    continue;
                }
            }
            else
            {
                _guidanceMode = GuidanceMode.SemiActive;
            }

            _retask = false;
            Vector3D hitPos = payload.Item1.Col0;
            _targetPos = payload.Item1.Col1;
            _targetVel = payload.Item1.Col2;
            _timeSinceLastIngest = Tick; // IGC messages are always a tick delayed
            _timeSinceLastLock = timeSinceLock;

            if (_guidanceMode == GuidanceMode.Active)
            {
                _raycastHoming.UpdateTargetStateVectors(_targetPos, hitPos, _targetVel, _timeSinceLastLock);
            }
            else
            {
                Vector3D offset = payload.Item2.Col0;
                _raycastHoming.SetInitialLockParameters(hitPos, _targetVel, offset, _timeSinceLastLock, targetId);
            }
        }
    }
}

void RegisterBroadcastListeners()
{
    if (_broadcastListenersRegistered)
        return;

    _broadcastListenerHoming = IGC.RegisterBroadcastListener(IgcTagHoming);
    _broadcastListenerHoming.SetMessageCallback(IgcTagHoming);

    _broadcastListenerBeamRiding = IGC.RegisterBroadcastListener(IgcTagBeamRiding);
    _broadcastListenerBeamRiding.SetMessageCallback(IgcTagBeamRiding);

    _broadcastListenerParameters = IGC.RegisterBroadcastListener(IgcTagParams);
    _broadcastListenerParameters.SetMessageCallback(IgcTagParams);

    _broadcastListenersRegistered = true;
}

void ArgumentHandling(string arg)
{
    if (arg.ToLower().Equals("_fire"))
    {
        _postSetupAction = PostSetupAction.Fire;
        InitiateSetup();
    }
    else if (arg.ToLower().Equals("setup") && !_shouldFire)
    {
        _postSetupAction = PostSetupAction.None;
        InitiateSetup();
    }
}
#endregion

#region Setup
List<IMyBlockGroup> _allGroups = new List<IMyBlockGroup>();
List<IMyProgrammableBlock> _groupPrograms = new List<IMyProgrammableBlock>();
int _autoConfigureMissileNumber;
IEnumerator<SetupStatus> _setupStateMachine;
StringBuilder _setupBuilder = new StringBuilder(),
    _workingBuilder = new StringBuilder();

void ClearLists()
{
    _missileBlocks.Clear();
    _funcBlocks.Clear();
    _unsortedThrusters.Clear();
    _mainThrusters.Clear();
    _sideThrusters.Clear();
    _detachThrusters.Clear();
    _artMasses.Clear();
    _mergeBlocks.Clear();
    _batteries.Clear();
    _shipControllers.Clear();
    _gyros.Clear();
    _connectors.Clear();
    _rotors.Clear();
    _reactors.Clear();
    _antennas.Clear();
    _sensors.Clear();
    _warheads.Clear();
    _cameras.Clear();
    _homingCameras.Clear();
    _gasTanks.Clear();
}

string GetTitle()
{
    return $"Whip's Homing Adv. Missile Script\n(Version {Version} - {Date})\n\nFor use with LAMP v{CompatVersion} or later.\n";
}

List<IMyRadioAntenna> _broadcasters = new List<IMyRadioAntenna>();
List<IMyBlockGroup> _foundGroups = new List<IMyBlockGroup>();
List<IMyMechanicalConnectionBlock> _mechConnections = new List<IMyMechanicalConnectionBlock>();

enum SetupStatus { None = 0, Running = 1, Done = 2 }
IEnumerator<SetupStatus> SetupStateMachine(bool reload = false)
{
    string title = GetTitle();
    _setupBuilder.Append(title).Append("\n");

    LoadIniConfig();
    #region Auto-Configuration
    if (_autoConfigure)
    {
        _allGroups.Clear();
        _foundGroups.Clear();
        _autoConfigureMissileNumber = -1;
        _missileGroup = null;

        _setupBuilder.Append("\nRunning Autosetup...\n");
        GridTerminalSystem.GetBlockGroups(_allGroups);

        foreach (var group in _allGroups)
        {
            // Group collect
            string groupName = group.Name;

            if (groupName.StartsWith(_missileTag))
            {
                int spaceIndex = groupName.IndexOf(' ', _missileTag.Value.Length);
                if (spaceIndex == -1)
                    continue;

                // Check if this program exists in the group
                _groupPrograms.Clear();
                group.GetBlocksOfType(_groupPrograms);
                bool foundSelf = false;
                foreach (var pb in _groupPrograms)
                {
                    if (pb == Me)
                    {
                        foundSelf = true;
                        break;
                    }
                    if (AtInstructionLimit()) { yield return SetupStatus.Running; }
                }

                if (!foundSelf)
                    continue;
                string missileNumberStr = groupName.Substring(spaceIndex + 1);
                int missileNumber;
                bool parsed = int.TryParse(missileNumberStr, out missileNumber);
                if (!parsed)
                    continue;

                _autoConfigureMissileNumber = missileNumber;

                if (_missileGroup == null)
                    _missileGroup = group;
                _foundGroups.Add(group);
            }
            if (AtInstructionLimit()) { yield return SetupStatus.Running; }
        }

        if (_missileGroup == null)
        {
            LogWarning("No groups containing this program found.");
            _missileGroup = GridTerminalSystem.GetBlockGroupWithName(_missileGroupNameTag); // Default
        }
        else if (_foundGroups.Count > 1) // Too many
        {
            _workingBuilder.Clear();
            _workingBuilder.Append("MULTIPLE groups containing this program found:\n");
            for (int i = 0; i < _foundGroups.Count; ++i)
            {
                var thisGroup = _foundGroups[i];
                _workingBuilder.Append($"    {i + 1}: {thisGroup.Name}");
                if ((i + 1) != _foundGroups.Count)
                {
                    _workingBuilder.Append("\n");
                }
            }
            LogWarning(_workingBuilder.ToString());
        }
        else
        {
            LogInfo($"Missile group found: '{_missileTag} {_autoConfigureMissileNumber}'");
            _missileNumber.Value = _autoConfigureMissileNumber;
        }
    }
    else // Default: Read from custom data
    {
        _missileGroup = GridTerminalSystem.GetBlockGroupWithName(_missileGroupNameTag);
    }
    #endregion
    SaveIniConfig();

    #region Ignore own grids with raycast
    _mechConnections.Clear();
    GridTerminalSystem.GetBlocksOfType(_mechConnections);
    _raycastHoming.ClearIgnoredGridIDs();
    _raycastHoming.AddIgnoredGridID(Me.CubeGrid.EntityId);
    foreach (var mc in _mechConnections)
    {
        _raycastHoming.AddIgnoredGridID(mc.CubeGrid.EntityId);
        if (mc.TopGrid != null)
            _raycastHoming.AddIgnoredGridID(mc.TopGrid.EntityId);

        if (AtInstructionLimit()) { yield return SetupStatus.Running; }
    }
    #endregion

    #region Guidance and PID config
    foreach (var guid in _guidanceAlgorithms)
    {
        var relNav = guid.Value as RelNavGuidance;
        if (relNav != null)
        {
            relNav.NavConstant = _navConstant;
            relNav.NavAccelConstant = _accelNavConstant;
            continue;
        }

        var interceptGuid = guid.Value as InterceptPointGuidance;
        if (interceptGuid != null)
        {
            var relNavImpl = interceptGuid.Implementation as RelNavGuidance;
            if (relNavImpl != null)
            {
                relNavImpl.NavConstant = _navConstant;
                relNavImpl.NavAccelConstant = _accelNavConstant;
            }
        }
    }
    _selectedGuidance = _guidanceAlgorithms[_guidanceAlgoType]; // TODO: Ensure value in bounds or just let it crash?

    _yawPID.Kp = _gyroProportionalGain;
    _yawPID.Ki = _gyroIntegralGain;
    _yawPID.Kd = _gyroDerivativeGain;
    _pitchPID.Kp = _gyroProportionalGain;
    _pitchPID.Ki = _gyroIntegralGain;
    _pitchPID.Kd = _gyroDerivativeGain;
    #endregion

    _preSetupFailed = false; // TODO rename
    ClearLists();

    #region Grab Key Codes
    _setupBuilder.Append($"\nChecking for firing ship...\n");
    _broadcasters.Clear();
    _foundLampAntennas = false;
    var fcsGroup = GridTerminalSystem.GetBlockGroupWithName(_fireControlGroupNameTag);
    if (fcsGroup != null)
    {
        fcsGroup.GetBlocksOfType(_broadcasters);
        if (_broadcasters.Count == 0)
        {
            if (_allowRemoteFire)
            {
                LogWarning($"No antennas in group named '{_fireControlGroupNameTag}', but remote fire is active.");
            }
            else if (!reload)
            {
                _preSetupFailed = true;
                LogError($"No antennas in group named '{_fireControlGroupNameTag}'! This missile MUST be attached to a configured firing ship to fire!");
            }
        }
        else
        {
            LogInfo($"Found antenna(s) on firing ship");
            _foundLampAntennas = true;
        }
    }
    else if (_allowRemoteFire)
    {
        LogWarning($"No group named '{_fireControlGroupNameTag}' found, but remote fire is active.");
    }
    else if (!reload)
    {
        _preSetupFailed = true;
        LogError($"No group named '{_fireControlGroupNameTag}' found! This missile MUST be attached to a configured firing ship to fire!");
    }
    #endregion

    #region Get Missile Blocks
    _setupBuilder.Append($"\nSetup for group named \"{_missileGroupNameTag}\"...\n");

    if (_missileGroup != null)
    {
        _missileGroup.GetBlocks(_missileBlocks);
    }
    else
    {
        LogError($"No block group named '{_missileGroupNameTag}' found!");
        _preSetupFailed = true;
    }

    for (int i = 0; i < _missileBlocks.Count; ++i)
    {
        CollectBlocks(_missileBlocks[i]);
        if (AtInstructionLimit()) { yield return SetupStatus.Running; }
    }
    #endregion

    yield return SetupStatus.Done;
}

public void RunSetupStateMachine()
{
    _setupTicks++;
    if (_setupStateMachine != null)
    {
        bool moreInstructions = _setupStateMachine.MoveNext();

        if (_setupStateMachine.Current == SetupStatus.Running)
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;
        }
        else // Done with block fetching
        {
            _canSetup = true;

            // Dispose of setup state machine
            _setupStateMachine.Dispose();
            _setupStateMachine = null;

            // Post-block fetch
            bool setupPassed = SetupErrorChecking();
            LogInfo($"Setup took {_setupTicks} tick(s)");

            _setupBuilder.Append("\nSetup Result: ");
            if (!setupPassed || _preSetupFailed)
            {
                _logger.Log(LogLevel.Fail, "[[FAILED]]\n");
                Echo(_setupBuilder.ToString());
                return;
            }
            // Implied else
            _logger.Log(LogLevel.Success, "[[SUCCESS]]\n");
            _missileReference = _shipControllers[0];

            if ((_postSetupAction & PostSetupAction.Fire) != 0)
            {
                _shouldFire = true;
                RegisterBroadcastListeners();
                _launchSM.SetState(LaunchStage.Intiate);
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
            if ((_postSetupAction & PostSetupAction.FireRequestResponse) != 0)
            {
                ProcessRemoteFireRequests();
            }

            Echo(_setupBuilder.ToString());
        }
    }
    else // Setup failed
    {
        _setupBuilder.Append($"> Info: Setup took {_setupTicks} tick(s)\n");
        _setupBuilder.Append("\n>>> Setup Failed! <<<\n");
        _canSetup = true;
        Echo(_setupBuilder.ToString());
    }
}

void InitiateSetup(bool reload = false)
{
    if (!_canSetup)
        return;

    _canSetup = false;
    _setupTicks = 0;
    _setupBuilder.Clear();

    if (_setupStateMachine != null)
    {
        _setupStateMachine.Dispose();
        _setupStateMachine = null;
    }
    _setupStateMachine = SetupStateMachine(reload);

    RunSetupStateMachine();
}

public bool AtInstructionLimit()
{
    return Runtime.CurrentInstructionCount >= MaxInstructionsPerRun;
}

bool SetupErrorChecking()
{
    bool setupFailed = false;

    setupFailed |= LogIfTrue(_requireAntenna ? LogLevel.Error : LogLevel.Warning , _antennas.Count == 0, "No antennas found") && _requireAntenna;
    setupFailed |= LogIfTrue(LogLevel.Error, _gyros.Count == 0, "No gyros found");
    setupFailed |= LogIfTrue(LogLevel.Error, _shipControllers.Count == 0, "No remotes found");

    if (_shipControllers.Count > 0)
    {
        GetThrusterOrientation(_shipControllers[0]);
        setupFailed |= _mainThrusters.Count == 0;
        if (_mainThrusters.Count == 0)
        {
            if (_sideThrusters.Count != 0)
            {
                LogError("No main thrusters found. Make sure that your remote is pointed forwards!");
            }
            else
            {
                LogError("No main thrusters found.");
            }
        }
    }
    setupFailed |= LogIfTrue(LogLevel.Error, _batteries.Count == 0 && _reactors.Count == 0, "No batteries or reactors found");

    if (!LogIfTrue(LogLevel.Warning, _mergeBlocks.Count == 0 && _rotors.Count == 0 && _connectors.Count == 0, "No merge blocks, rotors, or connectors found for detaching"))
    {
        EchoBlockCount(_mergeBlocks.Count, "merge");
        EchoBlockCount(_rotors.Count, "rotor");
        EchoBlockCount(_connectors.Count, "connector");
    }

    EchoBlockCount(_antennas.Count, "antenna");
    EchoBlockCount(_artMasses.Count, "art. mass block");
    EchoBlockCount(_sensors.Count, "sensor");
    EchoBlockCount(_warheads.Count, "warhead");
    EchoBlockCount(_cameras.Count, "camera");
    EchoBlockCount(_stageTimers.Count, "stage timer trigger");
    EchoBlockCount(_rangeTimers.Count, "range timer trigger");
    EchoBlockCount(_unsortedThrusters.Count, "total thruster");
    EchoBlockCount(_mainThrusters.Count, "main thruster");
    EchoBlockCount(_sideThrusters.Count, "side thruster");
    EchoBlockCount(_detachThrusters.Count, "detach thruster");
    EchoBlockCount(_gasTanks.Count, "gas tank");

    return !setupFailed;
}

void EchoBlockCount(int count, string name)
{
    LogInfo($"Found {count} {name}{(count == 1 ? "" : "s")}");
}

bool CollectBlocks(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    /*
    Here we extract the missile prefix from block names to ensure that
    we don't write multiple name tags to the same block.
    Expected block name pattern is: "(Missile X) BLOCK_NAME"
    */
    var name = block.CustomName;
    var splitName = name.Split('(', ')');
    if (splitName.Length >= 2)
    {
        var temp = splitName[1];
        var cleanName = name.Replace($"({temp})", "").TrimStart();
        block.CustomName = $"{_missileNameTag} {cleanName}";
    }
    else
    {
        block.CustomName = $"{_missileNameTag} {name}";
    }

    var timer = block as IMyTimerBlock;
    if (timer != null)
    {
        _ini.Clear();
        string cd = timer.CustomData;
        if (!_ini.TryParse(cd) && !string.IsNullOrWhiteSpace(cd))
        {
            _ini.EndContent = cd;
        }

        _stageTriggerConfig.Update(_ini);
        if (_triggerOnStage != LaunchStage.None)
        {
            _stageTimers.Add(new StageTimer(timer, _triggerOnStage));
        }

        _rangeTriggerConfig.Update(_ini);
        if (_triggerAtRange.HasValue)
        {
            _rangeTimers.Add(new RangeTimer(timer, _triggerAtRange.Value));
        }

        string output = _ini.ToString();
        if (output != cd)
        {
            timer.CustomData = output;
        }
    }

    IMyThrust thrust;
    IMyRadioAntenna antenna;
    IMyCameraBlock camera;
    IMyWarhead warhead;

    AddToListIfType(block, _funcBlocks);

    if (AddToListIfType(block, _unsortedThrusters, out thrust))
    {
        if (thrust.CustomName.Contains(_detachThrustTag))
            _detachThrusters.Add(thrust);
    }
    else if (AddToListIfType(block, _antennas, out antenna))
    {
        antenna.Radius = 1f;
        antenna.EnableBroadcasting = false;
        antenna.Enabled = _allowRemoteFire && !_foundLampAntennas;
    }
    else if (AddToListIfType(block, _warheads, out warhead))
    {
        warhead.IsArmed = false;
    }
    else if (AddToListIfType(block, _cameras, out camera))
    {
        camera.Enabled = true;
        camera.EnableRaycast = true;
        if (_useCamerasForHoming)
            _homingCameras.Add(camera);
    }
    else if (AddToListIfType(block, _artMasses)
            || AddToListIfType(block, _batteries)
            || AddToListIfType(block, _gyros)
            || AddToListIfType(block, _mergeBlocks)
            || AddToListIfType(block, _shipControllers)
            || AddToListIfType(block, _connectors)
            || AddToListIfType(block, _rotors)
            || AddToListIfType(block, _reactors)
            || AddToListIfType(block, _sensors)
            || AddToListIfType(block, _gasTanks))
    {
        /* Nothing to do here */
    }


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

bool LogIfTrue(LogLevel severity, bool state, string msg)
{
    if (state)
    {
        _logger.Log(severity, msg);
    }
    return state;
}

MatrixD MissileMatrix
{
    get
    {
        if (_missileReference != null)
        {
            return _missileReference.WorldMatrix;
        }
        return MatrixD.Identity;
    }
}

void GetThrusterOrientation(IMyTerminalBlock reference)
{
    if (reference == null)
    {
        return;
    }

    _mainThrusters.Clear();
    _sideThrusters.Clear();

    foreach (IMyThrust t in _unsortedThrusters)
    {
        var thrustDirn = Base6Directions.GetFlippedDirection(t.Orientation.Forward);
        if (thrustDirn == reference.Orientation.Forward)
        {
            _mainThrusters.Add(t);
        }
        else
        {
            _sideThrusters.Add(t);
        }
    }
}
#endregion

#region Missile Launch Sequence
class LaunchState : IState
{
    public Enum Id { get; }
    public Action OnUpdate { get; }
    public Action OnEnter { get; }
    public Action OnLeave { get; }
    public double Duration;
    public double ElapsedTime = 0;

    readonly StateMachine _parent;
    readonly Enum _nextState;
    readonly Action _update;
    readonly double _timeStep;

    public LaunchState(Enum id, Enum nextStateId, StateMachine sm, double duration, double dt, Action onUpdate = null, Action onEnter = null, Action onLeave = null)
    {
        _parent = sm;
        Id = id;
        _nextState = nextStateId;
        Duration = duration;
        _timeStep = dt;
        _update = onUpdate;
        OnEnter = onEnter;
        OnLeave = onLeave;

        OnUpdate = DoUpdate;
    }

    void DoUpdate()
    {
        _update?.Invoke();
        ElapsedTime += _timeStep;
        if (ElapsedTime >= Duration)
        {
            _parent.SetState(_nextState);
        }
    }
}

void SetupLaunchStages()
{
    _launchSM.AddState(new State(LaunchStage.Idle));
    _initiateState = new LaunchState(LaunchStage.Intiate, LaunchStage.Detach, _launchSM, _disconnectDelay, SecondsPerUpdate, onEnter: OnInitiate);
    _detachState = new LaunchState(LaunchStage.Detach, LaunchStage.Drift, _launchSM, _detachDuration, SecondsPerUpdate, onEnter: OnDetach);
    _driftState = new LaunchState(LaunchStage.Drift, LaunchStage.Flight, _launchSM, _mainIgnitionDelay, SecondsPerUpdate, onEnter: OnDrift);
    _flightState = new State(LaunchStage.Flight, onEnter: OnFlight);
    _launchSM.AddStates(_initiateState, _detachState, _driftState, _flightState);
    _launchSM.Initialize(LaunchStage.Idle);
}

// Prepares missile for launch by activating power sources.
void OnInitiate()
{
    foreach (var b in _batteries)
    {
        b.Enabled = true;
        b.ChargeMode = ChargeMode.Discharge;
    }

    foreach (var r in _reactors)
    {
        r.Enabled = true;
    }

    foreach (var s in _sensors)
    {
        s.Enabled = false;
    }

    foreach (var w in _warheads)
    {
        w.IsArmed = false;
    }

    foreach (var g in _gyros)
    {
        g.Enabled = true;
    }

    foreach (var t in _gasTanks)
    {
        t.Stockpile = false;
    }
}

// Detaches missile from the firing ship.
void OnDetach()
{
    foreach (var m in _artMasses)
    {
        m.Enabled = true;
    }

    foreach (var b in _mergeBlocks)
    {
        b.Enabled = false;
    }

    foreach (var c in _connectors)
    {
        c.Disconnect();
        c.Enabled = false;
    }

    foreach (var r in _rotors)
    {
        r.Detach();
    }

    foreach (var a in _antennas)
    {
        a.Radius = 1f;
        a.EnableBroadcasting = !_shouldStealth;
        a.Enabled = false;
        a.HudText = GetAntennaName(a.HudText);
    }

    ApplyThrustOverride(_sideThrusters, MinThrust, false);
    ApplyThrustOverride(_detachThrusters, 1f);

    _scheduler.AddQueuedAction(EnableAntennas, AntennaEnableDelay);
}

// Disables missile thrust for drifting.
void OnDrift()
{
    ApplyThrustOverride(_detachThrusters, MinThrust);
}

// Ignites main thrust.
void OnFlight()
{
    foreach (var m in _artMasses)
    {
        m.Enabled = false;
    }

    foreach (var c in _cameras)
    {
        c.EnableRaycast = true;
        c.Enabled = true;
    }

    ApplyThrustOverride(_detachThrusters, MinThrust);
    ApplyThrustOverride(_sideThrusters, MinThrust);
    ApplyThrustOverride(_mainThrusters, 1f);

    Me.CubeGrid.CustomName = _missileGroupNameTag;

    _killAllowed = true;

    _scheduler.AddQueuedAction(KillPower, MaxGuidanceTime);
}

void EnableAntennas()
{
    foreach (var a in _antennas)
    {
        a.Enabled = true;
    }
}
#endregion

#region Missile Nav, Guidance, and Control
// Delays GuideMissile until the guidance delay is finished.
void ActivateGuidance()
{
    _enableGuidance = true;
}

void GuidanceNavAndControl()
{
    if ((_guidanceMode & GuidanceMode.Homing) != 0)
        ActiveHomingScans();

    if (!_enableGuidance)
        return;

    Vector3D missilePos, missileVel, gravityVec;
    MatrixD missileMatrix;
    double missileMass, missileAccel;
    bool pastArmingRange;

    Navigation(_minimumArmingRange,
        out missileMatrix,
        out missilePos,
        out missileVel,
        out _shooterPos,
        out _shooterVel,
        out gravityVec,
        out missileMass,
        out missileAccel,
        out _distanceFromShooter,
        out pastArmingRange);

    Vector3D accelCmd = GuidanceMain(
        _guidanceMode,
        missileMatrix,
        missilePos,
        missileVel,
        gravityVec,
        missileAccel,
        pastArmingRange,
        out _shouldProximityScan);

    ScaleAntennaRange(_distanceFromShooter + 100);

    Control(missileMatrix, accelCmd, gravityVec, missileVel, missileMass);
}

void Navigation(
    double minArmingRange,
    out MatrixD missileMatrix,
    out Vector3D missilePos,
    out Vector3D missileVel,
    out Vector3D shooterPos,
    out Vector3D shooterVel,
    out Vector3D gravity,
    out double missileMass,
    out double missileAcceleration,
    out double distanceFromShooter,
    out bool pastMinArmingRange)
{
    missilePos = _missileReference.CenterOfMass;
    missileVel = _missileReference.GetShipVelocities().LinearVelocity;
    missileMatrix = MissileMatrix;

    shooterPos = _shooterPosCached + _offsetLeft * _shooterLeftVec + _offsetUp * _shooterUpVec;
    shooterVel = (shooterPos - _lastShooterPos) * UpdatesPerSecond;
    _lastShooterPos = shooterPos;

    gravity = _missileReference.GetNaturalGravity();

    distanceFromShooter = Vector3D.Distance(_shooterPos, missilePos);

    // Computing mass, thrust, and acceleration
    double missileThrust = CalculateMissileThrust(_mainThrusters);
    missileMass = _missileReference.CalculateShipMass().PhysicalMass;
    missileAcceleration = missileThrust / missileMass;

    pastMinArmingRange = Vector3D.DistanceSquared(missilePos, shooterPos) >= minArmingRange * minArmingRange;
}

Vector3D GuidanceMain(
    GuidanceMode guidanceMode,
    MatrixD missileMatrix,
    Vector3D missilePos,
    Vector3D missileVel,
    Vector3D gravity,
    double missileAcceleration,
    bool pastMinArmingRange,
    out bool shouldProximityScan)
{
    Vector3D accelCmd;
    if (guidanceMode == GuidanceMode.BeamRiding)
    {
        accelCmd = BeamRideGuidance(
            missilePos,
            missileVel,
            gravity,
            missileMatrix.Forward,
            missileAcceleration);

        shouldProximityScan = pastMinArmingRange;
    }
    else
    {
        Vector3D adjustedTargetPos;

        accelCmd = HomingGuidance(
            missilePos,
            missileVel,
            gravity,
            missileAcceleration,
            out adjustedTargetPos);

        double distanceToTgt = Math.Max(0.0, Vector3D.Distance(missilePos, adjustedTargetPos) - _raycastHoming.TargetSize * 0.5);
        double distanceToTgtSq = distanceToTgt * distanceToTgt;
        double closingSpeedSq = (missileVel - _targetVel).LengthSquared();
        shouldProximityScan = pastMinArmingRange && (closingSpeedSq > distanceToTgtSq); // Roughly 1 second till impact

        foreach (var t in _rangeTimers)
        {
            t.Update(distanceToTgtSq);
        }
    }

    if (_enableEvasion)
    {
        accelCmd += missileAcceleration * _randomizedHeadingVector;
    }

    return accelCmd;
}

Vector3D BeamRideGuidance(
    Vector3D missilePos,
    Vector3D missileVel,
    Vector3D gravity,
    Vector3D missileForwardVec,
    double missileAcceleration)
{
    var shooterToMissileVec = missilePos - _shooterPos;

    if (Vector3D.IsZero(_shooterForwardVec)) //this is to avoid NaN cases when the shooterForwardVec isnt cached yet
        _shooterForwardVec = missileForwardVec;

    var projectionVec = VectorMath.Projection(shooterToMissileVec, _shooterForwardVec);

    double missileSpeed = missileVel.Length();
    Vector3D destinationVec = _shooterPos + projectionVec + _shooterForwardVec * Math.Max(2 * missileSpeed, 200);

    if (_shooterForwardVec.Dot(shooterToMissileVec) > 0) // Missile is in front of the shooter
    {
        if (!_hasPassed)
        {
            _hasPassed = true;
        }
    }
    else if (_hasPassed) // If behind shooter and we have already passed the shooter before
    {
        int signLeft = Math.Sign(shooterToMissileVec.Dot(_shooterLeftVec));
        int signUp = Math.Sign(shooterToMissileVec.Dot(_shooterUpVec));

        destinationVec += signLeft * 100 * _shooterLeftVec + signUp * 100 * _shooterUpVec;
    }

    Vector3D accelCmd = _selectedGuidance.Update(missilePos, missileVel, missileAcceleration, destinationVec, _shooterVel, gravity);

    return accelCmd * missileAcceleration;
}

Vector3D HomingGuidance(
    Vector3D missilePos,
    Vector3D missileVel,
    Vector3D gravityVec,
    double missileAcceleration,
    out Vector3D adjustedTargetPos)
{
    adjustedTargetPos = _targetPos + _targetVel * (_timeSinceLastLock + _timeSinceLastIngest);

    if (_topDownAttack && gravityVec.LengthSquared() > 1e-3 && !_shouldDive)
    {
        if (VectorMath.AngleBetween(adjustedTargetPos - missilePos, gravityVec) < TopdownDescentAngle)
        {
            _shouldDive = true;
        }
        else
        {
            adjustedTargetPos += Vector3D.Normalize(gravityVec) * -_topDownAttackHeight;
        }
    }

    if (_maxAimDispersion > 0.5)
    {
        if (Vector3D.IsZero(_aimDispersion))
        {
            _aimDispersion = ComputeRandomDispersion();
        }
        adjustedTargetPos += VectorMath.Rejection(_aimDispersion, adjustedTargetPos - missilePos);
    }

    Vector3D accelCmd = _selectedGuidance.Update(missilePos, missileVel, missileAcceleration, adjustedTargetPos, _targetVel, gravityVec);
    return accelCmd * missileAcceleration;
}

void Control(MatrixD missileMatrix, Vector3D accelCmd, Vector3D gravityVec, Vector3D velocityVec, double mass)
{
    if (InFlight)
    {
        if (_conserveFuel &&
            _fuelConservationMaxSpeed > 0 &&
            (velocityVec.LengthSquared() >= _fuelConservationMaxSpeed * _fuelConservationMaxSpeed) &&
            VectorMath.CosBetween(accelCmd, velocityVec) >= _fuelConservationCos
        )
        {
            ApplyThrustOverride(_mainThrusters, MinThrust, false);
        }
        else
        {
            var headingDeviation = VectorMath.CosBetween(accelCmd, missileMatrix.Forward);
            ApplyThrustOverride(_mainThrusters, (float)MathHelper.Clamp(headingDeviation, 0.1f, 1f));
        }
        var sideVelocity = VectorMath.Rejection(velocityVec, accelCmd);
        ApplySideThrust(_sideThrusters, sideVelocity, gravityVec, mass);
    }

    Vector3D rotationVectorPYR = GetRotationVector(accelCmd, -gravityVec, missileMatrix);

    // Angle controller
    Vector3D rotationSpeedPYR;
    rotationSpeedPYR.X = _pitchPID.Control(rotationVectorPYR.X);
    rotationSpeedPYR.Y = _yawPID.Control(rotationVectorPYR.Y);

    // Handle roll more simply
    if (Math.Abs(_missileSpinRPM) > 1e-3 && InFlight)
    {
        rotationSpeedPYR.Z = _missileSpinRPM * RpmToRad;
    }
    else
    {
        rotationSpeedPYR.Z = rotationVectorPYR.Z;
    }

    // Yaw and pitch slowdown to avoid overshoot
    if (Math.Abs(rotationVectorPYR.X) < GyroSlowdownAngle)
    {
        rotationSpeedPYR.X = UpdatesPerSecond * .5 * rotationVectorPYR.X;
    }

    if (Math.Abs(rotationVectorPYR.Y) < GyroSlowdownAngle)
    {
        rotationSpeedPYR.Y = UpdatesPerSecond * .5 * rotationVectorPYR.Y;
    }

    ApplyGyroOverride(rotationSpeedPYR, _gyros, missileMatrix);
}

#endregion

#region Broadcast Missile IFF
void NetworkTargets()
{
    bool hasTarget = _guidanceMode == GuidanceMode.Active && _raycastHoming.Status == RaycastHoming.TargetingStatus.Locked;

    int capacity = hasTarget ? 2 : 1;
    _messageBuilder.Capacity = capacity;

    var myType = TargetRelation.Missile | (Me.CubeGrid.GridSizeEnum == MyCubeSize.Large ? TargetRelation.LargeGrid : TargetRelation.SmallGrid);
    var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)(TargetRelation.Friendly | myType), Me.CubeGrid.EntityId, Me.WorldAABB.Center, Me.CubeGrid.WorldAABB.HalfExtents.LengthSquared());
    _messageBuilder.Add(myTuple);

    if (hasTarget)
    {
        TargetRelation relation = TargetRelation.Locked;
        switch (_raycastHoming.TargetRelation)
        {
            case MyRelationsBetweenPlayerAndBlock.Owner:
            case MyRelationsBetweenPlayerAndBlock.Friends:
            case MyRelationsBetweenPlayerAndBlock.FactionShare:
                relation |= TargetRelation.Friendly;
                break;

            case MyRelationsBetweenPlayerAndBlock.Enemies:
                relation |= TargetRelation.Enemy;
                break;

                // Neutral is assumed if not friendly or enemy
        }

        switch (_raycastHoming.TargetType)
        {
            case MyDetectedEntityType.LargeGrid:
                relation |= TargetRelation.LargeGrid;
                break;
            case MyDetectedEntityType.SmallGrid:
                relation |= TargetRelation.SmallGrid;
                break;
        }

        myTuple = new MyTuple<byte, long, Vector3D, double>((byte)relation, _raycastHoming.TargetId, _raycastHoming.TargetCenter, 0);
        _messageBuilder.Add(myTuple);
    }

    IGC.SendBroadcastMessage(IgcTagIff, _messageBuilder.MoveToImmutable());
}
#endregion

#region Block Property Functions
string GetAntennaName(string customName)
{
    switch (_antennaMode.Value)
    {
        case AntennaNameMode.Meme:
            return _antennaMemeMessages[_memeIndex];
        case AntennaNameMode.MissileName:
            return _missileNameTag;
        case AntennaNameMode.MissileStatus:
            return $"{_missileNameTag} / Mode: {_guidanceMode} / Age: {(_guidanceMode == GuidanceMode.BeamRiding ? 0 : _timeSinceLastLock):0.000}";
        case AntennaNameMode.Custom:
            return customName;
        default:
        case AntennaNameMode.Empty:
            return " ";
    }
}

void ScaleAntennaRange(double dist)
{
    foreach (IMyRadioAntenna a in _antennas)
    {
        if (a.Closed)
            continue;
        a.Radius = (float)dist;
        a.EnableBroadcasting = !_shouldStealth;
        a.HudText = GetAntennaName(a.HudText);
    }
}

double CalculateMissileThrust(List<IMyThrust> mainThrusters)
{
    double thrust = 0;
    foreach (var block in mainThrusters)
    {
        thrust += block.IsFunctional && !block.Closed ? block.MaxEffectiveThrust : 0; // TODO: IsWorking?
    }
    return thrust;
}

void ApplyThrustOverride(List<IMyThrust> thrusters, float overrideValue, bool turnOn = true)
{
    float thrustProportion = overrideValue;
    foreach (IMyThrust t in thrusters)
    {
        if (t.Closed)
            continue;

        if (t.Enabled != turnOn)
            t.Enabled = turnOn;

        if (thrustProportion != t.ThrustOverridePercentage)
            t.ThrustOverridePercentage = thrustProportion;
    }
}

void ApplySideThrust(List<IMyThrust> thrusters, Vector3D controlAccel, Vector3D gravity, double mass)
{
    var desiredThrust = mass * (2 * controlAccel + gravity);
    var thrustToApply = desiredThrust;
    foreach (IMyThrust t in thrusters)
    {
        if (!t.IsWorking)
        {
            continue;
        }

        double neededThrust = Vector3D.Dot(t.WorldMatrix.Forward, thrustToApply);
        if (neededThrust > 0)
        {
            var outputProportion = MathHelper.Clamp(neededThrust / t.MaxEffectiveThrust, 0, 1);
            t.ThrustOverridePercentage = (float)outputProportion;
            thrustToApply -= t.WorldMatrix.Forward * outputProportion * t.MaxEffectiveThrust;
        }
        else
        {
            t.ThrustOverridePercentage = MinThrust;
        }
    }
}

void CheckProximity()
{
    if (!_shouldProximityScan || _markedForDetonation)
        return;

    Vector3D adjustedTargetPos = _targetPos + _targetVel * (_timeSinceLastLock + _timeSinceLastIngest);
    Vector3D missileVelocity = _missileReference.GetShipVelocities().LinearVelocity;
    Vector3D closingVelocity = _guidanceMode == GuidanceMode.BeamRiding ? missileVelocity : missileVelocity - _targetVel;
    Vector3D missilePos = _missileReference.GetPosition();

    Vector3D missileToTarget = adjustedTargetPos - missilePos;
    double distanceToTarget = missileToTarget.Length();
    Vector3D missileToTargetNorm = missileToTarget / distanceToTarget;

    double closingSpeed = Math.Max(1e-9, Vector3D.Dot(closingVelocity, missileToTargetNorm));

    // If we have no cameras or sensors for detonation, use some approximations
    if (_cameras.Count == 0 && _sensors.Count == 0)
    {
        if (_guidanceMode == GuidanceMode.BeamRiding) // Arm warheads if in beam ride mode
        {
            foreach (var thisWarhead in _warheads)
            {
                if (thisWarhead.Closed)
                    continue;

                if (!thisWarhead.IsArmed)
                    thisWarhead.IsArmed = true;
            }
        }
        else
        {
            // Use bounding box estimation for detonation
            double adjustedDetonationRange = _missileReference.CubeGrid.WorldVolume.Radius + _raycastRange;
            
            if (distanceToTarget < adjustedDetonationRange + closingSpeed * SecondsPerUpdate)
            {
                Detonate((distanceToTarget - adjustedDetonationRange) / closingSpeed);
                return;
            }
        }

        return;
    }

    // Try raycast detonation methods

    Vector3D targetScanPos = missilePos + missileToTargetNorm * (_raycastRange + closingSpeed * SecondsPerUpdate);

    double raycastHitDistance = 0;
    double trueClosingSpeed = 0;
    // Do one scan in the direction of the target (if applicable)
    if ((_guidanceMode & GuidanceMode.Homing) != 0 && RaycastTripwire(targetScanPos, closingVelocity, out raycastHitDistance, out trueClosingSpeed))
    {
        Detonate((raycastHitDistance - _raycastRange) / trueClosingSpeed);
        return;
    }

    /*
    Do one scan in the direction of the relative velocity vector
    If that fails, we will scan a cross pattern that traces the radius of the missile to try and catch
    complex geometry and avoid missed detonations
    */
    double apparentRadius = CalculateGridRadiusFromAxis(_missileReference.CubeGrid, closingVelocity) + _raycastRange;

    var displacement = closingVelocity * UpdatesPerSecond;
    var perp1 = Vector3D.CalculatePerpendicularVector(closingVelocity) * apparentRadius;
    var perp2 = VectorMath.SafeNormalize(Vector3D.Cross(perp1, closingVelocity)) * apparentRadius;

    var travelDirection = VectorMath.SafeNormalize(closingVelocity);
    closingSpeed = Vector3D.Dot(travelDirection, closingVelocity);
    targetScanPos = missilePos + travelDirection * (_raycastRange + closingSpeed * SecondsPerUpdate);

    if (RaycastTripwire(targetScanPos, closingVelocity, out raycastHitDistance, out trueClosingSpeed) ||
        RaycastTripwire(targetScanPos + perp1, closingVelocity, out raycastHitDistance, out trueClosingSpeed) ||
        RaycastTripwire(targetScanPos - perp1, closingVelocity, out raycastHitDistance, out trueClosingSpeed) ||
        RaycastTripwire(targetScanPos + perp2, closingVelocity, out raycastHitDistance, out trueClosingSpeed) ||
        RaycastTripwire(targetScanPos - perp2, closingVelocity, out raycastHitDistance, out trueClosingSpeed))
    {
        Detonate((raycastHitDistance - _raycastRange) / trueClosingSpeed);
        return;
    }

    foreach (IMySensorBlock sensor in _sensors)
    {
        if (sensor.Closed)
            continue;

        if (!sensor.Enabled)
            sensor.Enabled = true;

        if (sensor.IsActive)
        {
            _sensorEntities.Clear();
            sensor.DetectedEntities(_sensorEntities);

            foreach (var targetInfo in _sensorEntities)
            {
                if (IsValidTarget(targetInfo))
                {
                    Detonate(0);
                    return;
                }
            }
        }
    }
}

bool TryGetRaycastCamera(List<IMyCameraBlock> cameras, Vector3D worldPosition, out IMyCameraBlock selectedCamera)
{
    selectedCamera = null;

    foreach (var c in cameras)
    {
        if (c.Closed) { continue; }

        if (c.CanScan(worldPosition))
        {
            selectedCamera = c; 
            break;
        }
    }

    return selectedCamera != null;
}

bool RaycastTripwire(Vector3D scanPosition, Vector3D closingVelocity, out double raycastHitDistance, out double closingSpeed)
{
    raycastHitDistance = 0;
    closingSpeed = 0;

    IMyCameraBlock camera;
    if (!TryGetRaycastCamera(_cameras, scanPosition, out camera))
    {
        return false;
    }

    MyDetectedEntityInfo targetInfo = camera.Raycast(scanPosition);
    bool valid = IsValidTarget(targetInfo);
    if (valid)
    {
        var hitDirection = targetInfo.HitPosition.Value - camera.GetPosition();
        var hitDirectionNorm = VectorMath.SafeNormalize(hitDirection);
        raycastHitDistance = Vector3D.Dot(hitDirection, hitDirectionNorm);
        closingSpeed = Math.Max(1e-9, Vector3D.Dot(closingVelocity, hitDirectionNorm));
    }
    return valid;
}

bool IsValidTarget(MyDetectedEntityInfo targetInfo)
{
    if (targetInfo.IsEmpty() ||
        (targetInfo.EntityId == Me.CubeGrid.EntityId) ||
        (!_ignoreIdForDetonation && ((_guidanceMode & GuidanceMode.Homing) != 0) && (targetInfo.EntityId != _raycastHoming.TargetId)) ||
        (_raycastIgnorePlanetSurface && targetInfo.Type == MyDetectedEntityType.Planet) ||
        (_raycastIgnoreFriends && (targetInfo.Relationship & (MyRelationsBetweenPlayerAndBlock.FactionShare | MyRelationsBetweenPlayerAndBlock.Owner)) != 0) ||
        (targetInfo.BoundingBox.Size.LengthSquared() < _raycastMinimumTargetSize * _raycastMinimumTargetSize))
    {
        return false;
    }

    return true;
}

void Detonate(double fuzeTime)
{
    _markedForDetonation = true;
    foreach (var thisWarhead in _warheads)
    {
        if (thisWarhead.Closed)
            continue;
        thisWarhead.DetonationTime = Math.Max(0f, (float)fuzeTime - 1f / 60f);
        thisWarhead.StartCountdown();
    }
}

void KillPower()
{
    foreach (var b in _funcBlocks)
    {
        if (!b.Closed)
            b.Enabled = false;
    }
    Detonate(0);
    Runtime.UpdateFrequency = UpdateFrequency.None;
}
#endregion

#region Vector Math Functions
void ComputeRandomHeadingVector()
{
    if (!_enableEvasion || !_enableGuidance || _missileReference == null)
    {
        return;
    }

    double angle = RNGesus.NextDouble() * Math.PI * 2.0;
    var missileMatrix = MissileMatrix;
    _randomizedHeadingVector = Math.Sin(angle) * missileMatrix.Up + Math.Cos(angle) * missileMatrix.Right;
    _randomizedHeadingVector *= _maxRandomAccelRatio;
}

Vector3D ComputeRandomDispersion()
{
    Vector3D direction = new Vector3D(2 * _bellCurveRandom.NextDouble() - 1,
                                        2 * _bellCurveRandom.NextDouble() - 1,
                                        2 * _bellCurveRandom.NextDouble() - 1);
    return _maxAimDispersion * direction;
}

double CalculateGridRadiusFromAxis(IMyCubeGrid grid, Vector3D axis)
{
    var axisLocal = VectorMath.SafeNormalize(Vector3D.Rotate(axis, MatrixD.Transpose(grid.WorldMatrix)));
    var min = ((Vector3D)grid.Min - 0.5) * grid.GridSize;
    var max = ((Vector3D)grid.Max + 0.5) * grid.GridSize;
    var bb = new BoundingBoxD(min, max);

    double maxLenSq = 0;
    for (int ii = 0; ii < 8; ++ii)
    {
        var corner = bb.GetCorner(ii);
        var dirn = corner - bb.Center;
        var rej = dirn - dirn.Dot(axisLocal) * axisLocal;
        var lenSq = rej.LengthSquared();
        if (lenSq > maxLenSq)
        {
            maxLenSq = lenSq;
        }
    }
    return Math.Sqrt(maxLenSq);
}
#endregion

#region Storage Parsing/Saving
const string StorageKey = "WHAM";
void ParseStorage()
{
    _ini.Clear();
    _ini.TryParse(Storage);
    int i = 0;
    var launchStage = (LaunchStage)_ini.Get(StorageKey, $"{i++}").ToInt32();
    if (launchStage == LaunchStage.Idle)
    {
        return;
    }
    _launchSM.SetState(launchStage);

    _enableGuidance = _ini.Get(StorageKey, $"{i++}").ToBoolean();
    _senderKeycode = _ini.Get(StorageKey, $"{i++}").ToInt64();

    if (!_ini.Get(StorageKey, $"{i++}").ToBoolean())
    {
        return;
    }

    Vector3D pos, vel, offset;
    pos.X = _ini.Get(StorageKey, $"{i++}").ToDouble();
    pos.Y = _ini.Get(StorageKey, $"{i++}").ToDouble();
    pos.Z = _ini.Get(StorageKey, $"{i++}").ToDouble();
    vel.X = _ini.Get(StorageKey, $"{i++}").ToDouble();
    vel.Y = _ini.Get(StorageKey, $"{i++}").ToDouble();
    vel.Z = _ini.Get(StorageKey, $"{i++}").ToDouble();
    offset.X = _ini.Get(StorageKey, $"{i++}").ToDouble();
    offset.Y = _ini.Get(StorageKey, $"{i++}").ToDouble();
    offset.Z = _ini.Get(StorageKey, $"{i++}").ToDouble();
    var age = _ini.Get(StorageKey, $"{i++}").ToDouble();
    var id = _ini.Get(StorageKey, $"{i++}").ToInt64();

    _raycastHoming.SetInitialLockParameters(pos, vel, offset, age, id);
    _postSetupAction = PostSetupAction.Fire;
    InitiateSetup(true);

    Echo("Storage parsed");
}

void Save()
{
    if ((LaunchStage)_launchSM.StateId == LaunchStage.Idle)
    {
        Storage = "";
        return;
    }

    _ini.Clear();
    int i = 0;
    _ini.Set(StorageKey, $"{i++}", (int)(LaunchStage)_launchSM.StateId);
    _ini.Set(StorageKey, $"{i++}", _enableGuidance);
    _ini.Set(StorageKey, $"{i++}", _senderKeycode);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.IsScanning);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.HitPosition.X);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.HitPosition.Y);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.HitPosition.Z);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.TargetVelocity.X);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.TargetVelocity.Y);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.TargetVelocity.Z);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.PreciseModeOffset.X);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.PreciseModeOffset.Y);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.PreciseModeOffset.Z);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.TimeSinceLastLock);
    _ini.Set(StorageKey, $"{i++}", _raycastHoming.TargetId);
    Storage = _ini.ToString();
}
#endregion
#endregion

#region INCLUDES

class BatesDistributionRandom
{
    Random _rnd = new Random();
    readonly int _count;

    public BatesDistributionRandom(int count)
    {
        if (count < 1)
        {
            throw new Exception($"count must be greater than 1");
        }
        _count = count;
    }

    public double NextDouble()
    {
        double num = 0;
        for (int i = 0; i < _count; ++i)
        {
            num += _rnd.NextDouble();
        }
        return num / _count;
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

public class ConfigDouble : ConfigValue<double>
{
    public ConfigDouble(string name, double value = 0, string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        if (!val.TryGetDouble(out _value))
        {
            SetDefault();
            return false;
        }
        return true;
    }
}

public class ConfigEnum<TEnum> : ConfigValue<TEnum> where TEnum : struct
{
    public ConfigEnum(string name, TEnum defaultValue = default(TEnum), string comment = null)
    : base (name, defaultValue, comment)
    {}

    protected override bool SetValue(ref MyIniValue val)
    {
        string enumerationStr;
        if (!val.TryGetString(out enumerationStr) ||
            !Enum.TryParse(enumerationStr, true, out _value) ||
            !Enum.IsDefined(typeof(TEnum), _value))
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

/// <summary>
/// A simple, generic circular buffer class with a variable capacity.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DynamicCircularBuffer<T>
{
    public int Count
    {
        get
        {
            return _list.Count;
        }
    }
    
    List<T> _list = new List<T>();
    int _getIndex = 0;

    /// <summary>
    /// Adds an item to the buffer.
    /// </summary>
    /// <param name="item"></param>
    public void Add(T item)
    {
        _list.Add(item);
    }
    
    /// <summary>
    /// Clears the buffer.
    /// </summary>
    public void Clear()
    {
        _list.Clear();
        _getIndex = 0;
    }

    /// <summary>
    /// Retrieves the current item in the buffer and increments the buffer index.
    /// </summary>
    /// <returns></returns>
    public T MoveNext()
    {
        if (_list.Count == 0)
            return default(T);
        T val = _list[_getIndex];
        _getIndex = ++_getIndex % _list.Count;
        return val;
    }

    /// <summary>
    /// Retrieves the current item in the buffer without incrementing the buffer index.
    /// </summary>
    /// <returns></returns>
    public T Peek()
    {
        if (_list.Count == 0)
            return default(T);
        return _list[_getIndex];
    }
}

abstract class GuidanceBase
{
    public double DeltaTime { get; private set; }
    public double UpdatesPerSecond { get; private set; }

    Vector3D? _lastVelocity;

    public GuidanceBase(double updatesPerSecond)
    {
        UpdatesPerSecond = updatesPerSecond;
        DeltaTime = 1.0 / UpdatesPerSecond;
    }

    public void ClearAcceleration()
    {
        _lastVelocity = null;
    }

    public Vector3D Update(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D? gravity = null)
    {
        Vector3D targetAcceleration = Vector3D.Zero;
        if (_lastVelocity.HasValue)
            targetAcceleration = (targetVelocity - _lastVelocity.Value) * UpdatesPerSecond;
        _lastVelocity = targetVelocity;

        Vector3D pointingVector = GetPointingVector(missilePosition, missileVelocity, missileAcceleration, targetPosition, targetVelocity, targetAcceleration);

        if (gravity.HasValue && gravity.Value.LengthSquared() > 1e-3)
        {
            pointingVector = GravityCompensation(missileAcceleration, pointingVector, gravity.Value);
        }
        return VectorMath.SafeNormalize(pointingVector);
    }
    
    public static Vector3D GravityCompensation(double missileAcceleration, Vector3D desiredDirection, Vector3D gravity)
    {
        Vector3D directionNorm = VectorMath.SafeNormalize(desiredDirection);
        Vector3D gravityCompensationVec = -(VectorMath.Rejection(gravity, desiredDirection));
        
        double diffSq = missileAcceleration * missileAcceleration - gravityCompensationVec.LengthSquared();
        if (diffSq < 0) // Impossible to hover
        {
            return desiredDirection - gravity; // We will sink, but at least approach the target.
        }
        
        return directionNorm * Math.Sqrt(diffSq) + gravityCompensationVec;
    }

    public abstract Vector3D GetPointingVector(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration);
}

abstract class RelNavGuidance : GuidanceBase
{
    public double NavConstant;
    public double NavAccelConstant;

    public RelNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond)
    {
        NavConstant = navConstant;
        NavAccelConstant = navAccelConstant;
    }

    protected abstract Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration);

    public override Vector3D GetPointingVector(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration)
    {
        Vector3D missileToTarget = targetPosition - missilePosition;
        Vector3D missileToTargetNorm = Vector3D.Normalize(missileToTarget);
        Vector3D relativeVelocity = targetVelocity - missileVelocity;
        Vector3D lateralTargetAcceleration = (targetAcceleration - Vector3D.Dot(targetAcceleration, missileToTargetNorm) * missileToTargetNorm);

        Vector3D lateralAcceleration = GetLatax(missileToTarget, missileToTargetNorm, relativeVelocity, lateralTargetAcceleration);

        double missileAccelSq = missileAcceleration * missileAcceleration;
        double diff = missileAccelSq - Math.Min(missileAccelSq, lateralAcceleration.LengthSquared());
        return lateralAcceleration + Math.Sqrt(diff) * missileToTargetNorm;
    }
}


class HybridNavGuidance : RelNavGuidance
{
    public HybridNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond, navConstant, navAccelConstant) { }

    protected override Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        Vector3D omega = Vector3D.Cross(missileToTarget, relativeVelocity) / Math.Max(missileToTarget.LengthSquared(), 1); // to combat instability at close range
        Vector3D parallelVelocity = relativeVelocity.Dot(missileToTargetNorm) * missileToTargetNorm;
        Vector3D normalVelocity = (relativeVelocity - parallelVelocity);
        return NavConstant * (relativeVelocity.Length() * Vector3D.Cross(omega, missileToTargetNorm) + 0.1 * normalVelocity)
             + NavAccelConstant * lateralTargetAcceleration;
    }
}

abstract class InterceptPointGuidance : GuidanceBase
{

    public readonly GuidanceBase Implementation;

    public InterceptPointGuidance(GuidanceBase implementation) : base(implementation.UpdatesPerSecond)
    {
        Implementation = implementation;
    }

    protected abstract Vector3D GetInterceptPoint(Vector3D missilePosition, Vector3D missileVelocity, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration);

    public override Vector3D GetPointingVector(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration)
    {
        Vector3D interceptPoint = GetInterceptPoint(missilePosition, missileVelocity, targetPosition, targetVelocity, targetAcceleration);

        return Implementation.GetPointingVector(missilePosition, missileVelocity, missileAcceleration, interceptPoint, Vector3D.Zero, targetAcceleration);
    }
}

/// <summary>
/// Whip's Proportional Navigation Intercept
/// Derived from: https://en.wikipedia.org/wiki/Proportional_navigation
/// And: http://www.moddb.com/members/blahdy/blogs/gamedev-introduction-to-proportional-navigation-part-i
/// And: http://www.dtic.mil/dtic/tr/fulltext/u2/a556639.pdf
/// And: http://nptel.ac.in/courses/101108054/module8/lecture22.pdf
/// </summary>
class ProNavGuidance : RelNavGuidance
{
    public ProNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond, navConstant, navAccelConstant) { }

    protected override Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        Vector3D omega = Vector3D.Cross(missileToTarget, relativeVelocity) / Math.Max(missileToTarget.LengthSquared(), 1); //to combat instability at close range
        return NavConstant * relativeVelocity.Length() * Vector3D.Cross(omega, missileToTargetNorm)
             + NavAccelConstant * lateralTargetAcceleration;
    }
}

/// <summary>
/// Solves a quadratic in the form: 0 = a*x^2 + b*x + c.
/// If only one solution exists, xMin = xMax.
/// </summary>
/// <param name="a">Coefficient of the x^2 term</param>
/// <param name="b">Coefficient of the x term</param>
/// <param name="c">Constant term</param>
/// <param name="xMax">Larger of the two solutions</param>
/// <param name="xMin">Smaller of the two solutions</param>
/// <param name="epsilon">Small floating point epsilon to prevent division by zero.</param>
/// <returns>True if a solution exists.</returns>
public static bool SolveQuadratic(double a, double b, double c, out double xMax, out double xMin, double epsilon = 1e-12)
{
    xMin = xMax = 0;

    // Linear
    if (Math.Abs(a) < epsilon)
    {
        if (Math.Abs(b) < epsilon)
        {
            return false;
        }
        xMax = xMin = -c / b;
        return true;
    }

    // Quadratic
    double d = b * b - 4.0 * a * c;
    if (d < 0 || Math.Abs(a) < epsilon)
    {
        return false;
    }
    double sqrtD = Math.Sqrt(d);
    double inv2a = 1.0 / (2.0 * a);
    double x1 = (-b + sqrtD) * inv2a;
    double x2 = (-b - sqrtD) * inv2a;
    xMax = Math.Max(x1, x2);
    xMin = Math.Min(x1, x2);
    return true;
}

class QuadraticInterceptGuidance : InterceptPointGuidance
{
    public QuadraticInterceptGuidance(GuidanceBase implementation) : base(implementation) {}
    
    protected override Vector3D GetInterceptPoint(Vector3D missilePosition, Vector3D missileVelocity, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration)
    {
        Vector3D relativePosition = targetPosition - missilePosition;
        double missileSpeed = missileVelocity.Length();

        double a = targetVelocity.LengthSquared() - missileSpeed * missileSpeed;
        double b = 2 * Vector3D.Dot(relativePosition, targetVelocity);
        double c = relativePosition.LengthSquared();

        double interceptTime = 0;

        double timeMin, timeMax;
        if (SolveQuadratic(a, b, c, out timeMax, out timeMin))
        {
            if (timeMin > 0)
            {
                interceptTime = timeMin;
            }
            else if (timeMax > 0)
            {
                interceptTime = timeMax;
            }
        }

        return targetPosition + targetVelocity * interceptTime;
    }
}

class WhipNavGuidance : RelNavGuidance
{
    public WhipNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond, navConstant, navAccelConstant) { }

    protected override Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        Vector3D parallelVelocity = relativeVelocity.Dot(missileToTargetNorm) * missileToTargetNorm;
        Vector3D normalVelocity = (relativeVelocity - parallelVelocity);
        return NavConstant * 0.1 * normalVelocity
             + NavAccelConstant * lateralTargetAcceleration;
    }
}
public class Logger
{
    public static string GetHexColor(Color c)
    {
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }
    
    struct LogType
    {
        public static LogType Default = new LogType("", null, null);
        
        const string ColorFormat = "[color={1}]{0}[/color]";
        const string NoColorFormat = "{0}";
        
        public readonly string Prefix;
        public readonly string PrefixColorHex;
        public readonly string TextColorHex;
        
        readonly string _prefixFormat;
        readonly string _textFormat;

        public LogType(string prefix, Color? prefixColor, Color? textColor)
        {
            Prefix = prefix;
            PrefixColorHex = prefixColor.HasValue ? GetHexColor(prefixColor.Value) : null;
            _prefixFormat = prefixColor.HasValue ? ColorFormat : NoColorFormat;
            TextColorHex = textColor.HasValue ? GetHexColor(textColor.Value) : null;
            _textFormat = textColor.HasValue ? ColorFormat : NoColorFormat;
        }

        public void Write(StringBuilder buffer, string text)
        {
            if (!string.IsNullOrWhiteSpace(Prefix))
            {
                buffer.Append(string.Format(_prefixFormat, Prefix, PrefixColorHex)).Append(" ");
            }
            buffer.AppendLine(string.Format(_textFormat, text, TextColorHex));
        }
    }

    Dictionary<Enum, LogType> _logTypes = new Dictionary<Enum, LogType>();
    
    StringBuilder _buffer;

    public Logger(StringBuilder buffer)
    {
        _buffer = buffer;
    }

    public void RegisterType(Enum type, string prefix, Color? prefixColor = null, Color? textColor = null)
    {
        _logTypes[type] = new LogType(prefix, prefixColor, textColor);
    }

    public void Log(Enum type, string text)
    {
        LogType logType;
        if (!_logTypes.TryGetValue(type, out logType))
        {
            logType = LogType.Default;
        }
        logType.Write(_buffer, text);
    }
}

/// <summary>
/// Discrete time PID controller class.
/// Last edited: 2022/08/11 - Whiplash141
/// </summary>
public class PID
{
    public double Kp { get; set; } = 0;
    public double Ki { get; set; } = 0;
    public double Kd { get; set; } = 0;
    public double Value { get; private set; }

    double _timeStep = 0;
    double _inverseTimeStep = 0;
    double _errorSum = 0;
    double _lastError = 0;
    bool _firstRun = true;

    public PID(double kp, double ki, double kd, double timeStep)
    {
        Kp = kp;
        Ki = ki;
        Kd = kd;
        _timeStep = timeStep;
        _inverseTimeStep = 1 / _timeStep;
    }

    protected virtual double GetIntegral(double currentError, double errorSum, double timeStep)
    {
        return errorSum + currentError * timeStep;
    }

    public double Control(double error)
    {
        //Compute derivative term
        double errorDerivative = (error - _lastError) * _inverseTimeStep;

        if (_firstRun)
        {
            errorDerivative = 0;
            _firstRun = false;
        }

        //Get error sum
        _errorSum = GetIntegral(error, _errorSum, _timeStep);

        //Store this error as last error
        _lastError = error;

        //Construct output
        Value = Kp * error + Ki * _errorSum + Kd * errorDerivative;
        return Value;
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

    public virtual void Reset()
    {
        _errorSum = 0;
        _lastError = 0;
        _firstRun = true;
    }
}

/// <summary>
/// Selects the active controller from a list using the following priority:
/// Main controller > Oldest controlled ship controller > Any controlled ship controller.
/// </summary>
/// <param name="controllers">List of ship controlers</param>
/// <param name="lastController">Last actively controlled controller</param>
/// <returns>Actively controlled ship controller or null if none is controlled</returns>
public static IMyShipController GetControlledShipController(List<IMyShipController> controllers, IMyShipController lastController = null)
{
    IMyShipController currentlyControlled = null;
    foreach (IMyShipController ctrl in controllers)
    {
        if (ctrl.IsMainCockpit)
        {
            return ctrl;
        }

        // Grab the first seat that has a player sitting in it
        // and save it away in-case we don't have a main contoller
        if (currentlyControlled == null && ctrl != lastController && ctrl.IsUnderControl && ctrl.CanControlShip)
        {
            currentlyControlled = ctrl;
        }
    }

    // We did not find a main controller, so if the first controlled controller
    // from last cycle if it is still controlled
    if (lastController != null && lastController.IsUnderControl)
    {
        return lastController;
    }

    // Otherwise we return the first ship controller that we
    // found that was controlled.
    if (currentlyControlled != null)
    {
        return currentlyControlled;
    }

    // Nothing is under control, return the controller from last cycle.
    return lastController;
}

class RaycastHoming
{
    public TargetingStatus Status { get; private set; } = TargetingStatus.NotLocked;
    public Vector3D TargetPosition
    {
        get
        {
            return OffsetTargeting ? OffsetTargetPosition : TargetCenter;
        }
    }
    public double SearchScanSpread { get; set; } = 0;
    public Vector3D TargetCenter { get; private set; } = Vector3D.Zero;
    public Vector3D OffsetTargetPosition
    {
        get
        {
            return TargetCenter + Vector3D.TransformNormal(PreciseModeOffset, _targetOrientation);
        }
    }
    public Vector3D TargetVelocity { get; private set; } = Vector3D.Zero;
    public Vector3D HitPosition { get; private set; } = Vector3D.Zero;
    public Vector3D PreciseModeOffset { get; private set; } = Vector3D.Zero;
    public bool OffsetTargeting = false;
    public bool MissedLastScan { get; private set; } = false;
    public bool LockLost { get; private set; } = false;
    public bool IsScanning { get; private set; } = false;
    public double TimeSinceLastLock { get; private set; } = 0;
    public double TargetSize { get; private set; } = 0;
    public double MaxRange { get; private set; }
    public double MinRange { get; private set; }
    public long TargetId { get; private set; } = 0;
    public double AutoScanInterval { get; private set; } = 0;
    public double MaxTimeForLockBreak { get; private set; }
    public MyRelationsBetweenPlayerAndBlock TargetRelation { get; private set; }
    public MyDetectedEntityType TargetType { get; private set; }

    public enum TargetingStatus { NotLocked, Locked, TooClose };
    enum AimMode { Center, Offset, OffsetRelative };

    AimMode _currentAimMode = AimMode.Center;

    readonly HashSet<MyDetectedEntityType> _targetFilter = new HashSet<MyDetectedEntityType>();
    readonly List<IMyCameraBlock> _availableCameras = new List<IMyCameraBlock>();
    readonly Random _rngeesus = new Random();

    MatrixD _targetOrientation;
    HashSet<long> _gridIDsToIgnore = new HashSet<long>();
    double _timeSinceLastScan = 0;
    bool _manualLockOverride = false;
    bool _fudgeVectorSwitch = false;

    double AutoScanScaleFactor
    {
        get
        {
            return MissedLastScan ? 0.8 : 1.1;
        }
    }

    public RaycastHoming(double maxRange, double maxTimeForLockBreak, double minRange = 0, long gridIDToIgnore = 0)
    {
        MinRange = minRange;
        MaxRange = maxRange;
        MaxTimeForLockBreak = maxTimeForLockBreak;
        AddIgnoredGridID(gridIDToIgnore);
    }

    public void SetInitialLockParameters(Vector3D hitPosition, Vector3D targetVelocity, Vector3D offset, double timeSinceLastLock, long targetId)
    {
        TargetCenter = hitPosition;
        HitPosition = hitPosition;
        PreciseModeOffset = offset;
        TargetVelocity = targetVelocity;
        TimeSinceLastLock = timeSinceLastLock;
        _manualLockOverride = true;
        IsScanning = true;
        TargetId = targetId;
    }

    public void AddIgnoredGridID(long id)
    {
        _gridIDsToIgnore.Add(id);
    }

    public void ClearIgnoredGridIDs()
    {
        _gridIDsToIgnore.Clear();
    }

    public void AddEntityTypeToFilter(params MyDetectedEntityType[] types)
    {
        foreach (var type in types)
        {
            _targetFilter.Add(type);
        }
    }

    public void AcknowledgeLockLost()
    {
        LockLost = false;
    }

    public void LockOn()
    {
        ClearLockInternal();
        LockLost = false;
        IsScanning = true;
    }

    public void ClearLock()
    {
        ClearLockInternal();
        LockLost = false;
    }

    void ClearLockInternal()
    {
        IsScanning = false;
        Status = TargetingStatus.NotLocked;
        MissedLastScan = false;
        TimeSinceLastLock = 0;
        TargetSize = 0;
        HitPosition = Vector3D.Zero;
        TargetId = 0;
        _timeSinceLastScan = 141;
        _currentAimMode = AimMode.Center;
        TargetRelation = MyRelationsBetweenPlayerAndBlock.NoOwnership;
        TargetType = MyDetectedEntityType.None;
    }

    double RndDbl()
    {
        return 2 * _rngeesus.NextDouble() - 1;
    }

    double GaussRnd()
    {
        return (RndDbl() + RndDbl() + RndDbl()) / 3.0;
    }

    Vector3D CalculateFudgeVector(Vector3D targetDirection, double fudgeFactor = 5)
    {
        _fudgeVectorSwitch = !_fudgeVectorSwitch;

        if (!_fudgeVectorSwitch)
            return Vector3D.Zero;

        var perpVector1 = Vector3D.CalculatePerpendicularVector(targetDirection);
        var perpVector2 = Vector3D.Cross(perpVector1, targetDirection);
        if (!Vector3D.IsUnit(ref perpVector2))
            perpVector2.Normalize();

        var randomVector = GaussRnd() * perpVector1 + GaussRnd() * perpVector2;
        return randomVector * fudgeFactor * TimeSinceLastLock;
    }

    Vector3D GetSearchPos(Vector3D origin, Vector3D direction, IMyCameraBlock camera)
    {
        Vector3D scanPos = origin + direction * MaxRange;
        if (SearchScanSpread < 1e-2)
        {
            return scanPos;
        }
        return scanPos + (camera.WorldMatrix.Left * GaussRnd() + camera.WorldMatrix.Up * GaussRnd()) * SearchScanSpread;
    }

    IMyTerminalBlock GetReference(List<IMyCameraBlock> cameraList, List<IMyShipController> shipControllers, IMyTerminalBlock referenceBlock)
    {
        /*
         * References are prioritized in this order:
         * 1. Currently used camera
         * 2. Reference block
         * 3. Currently used control seat
         */
        IMyTerminalBlock controlledCam = GetControlledCamera(cameraList);
        if (controlledCam != null)
            return controlledCam;

        if (referenceBlock != null)
            return referenceBlock;

        return GetControlledShipController(shipControllers);
    }

    IMyCameraBlock SelectCamera()
    {
        // Check for transition between faces
        if (_availableCameras.Count == 0)
        {
            _timeSinceLastScan = 100000;
            MissedLastScan = true;
            return null;
        }

        return GetCameraWithMaxRange(_availableCameras);
    }

    void SetAutoScanInterval(double scanRange, IMyCameraBlock camera)
    {
        AutoScanInterval = scanRange / (1000.0 * camera.RaycastTimeMultiplier) / _availableCameras.Count * AutoScanScaleFactor;
    }

    bool DoLockScan(List<IMyCameraBlock> cameraList, out MyDetectedEntityInfo info, out IMyCameraBlock camera)
    {
        info = default(MyDetectedEntityInfo);

        #region Scan position selection
        Vector3D scanPosition;
        switch (_currentAimMode)
        {
            case AimMode.Offset:
                scanPosition = HitPosition;
                break;
            case AimMode.OffsetRelative:
                scanPosition = OffsetTargetPosition;
                break;
            default:
                scanPosition = TargetCenter;
                break;
        }
        scanPosition += TargetVelocity * TimeSinceLastLock;

        if (MissedLastScan)
        {
            scanPosition += CalculateFudgeVector(scanPosition - cameraList[0].GetPosition());
        }
        #endregion

        #region Camera selection
        GetCamerasInDirection(cameraList, _availableCameras, scanPosition, true);

        camera = SelectCamera();
        if (camera == null)
        {
            return false;
        }
        #endregion

        #region Scanning
        // We adjust the scan position to scan a bit past the target so we are more likely to hit if it is moving away
        Vector3D adjustedTargetPos = scanPosition + Vector3D.Normalize(scanPosition - camera.GetPosition()) * 2 * TargetSize;
        double scanRange = (adjustedTargetPos - camera.GetPosition()).Length();

        SetAutoScanInterval(scanRange, camera);

        if (camera.AvailableScanRange >= scanRange &&
            _timeSinceLastScan >= AutoScanInterval)
        {
            info = camera.Raycast(adjustedTargetPos);
            return true;
        }
        return false;
        #endregion
    }

    bool DoSearchScan(List<IMyCameraBlock> cameraList, IMyTerminalBlock reference, out MyDetectedEntityInfo info, out IMyCameraBlock camera)
    {
        info = default(MyDetectedEntityInfo);

        #region Camera selection
        if (reference != null)
        {
            GetCamerasInDirection(cameraList, _availableCameras, reference.WorldMatrix.Forward);
        }
        else
        {
            _availableCameras.Clear();
            _availableCameras.AddRange(cameraList);
        }

        camera = SelectCamera();
        if (camera == null)
        {
            return false;
        }
        #endregion

        #region Scanning
        SetAutoScanInterval(MaxRange, camera);

        if (camera.AvailableScanRange >= MaxRange &&
            _timeSinceLastScan >= AutoScanInterval)
        {
            if (reference != null)
            {
                info = camera.Raycast(GetSearchPos(reference.GetPosition(), reference.WorldMatrix.Forward, camera));
            }
            else
            {
                info = camera.Raycast(MaxRange);
            }

            return true;
        }
        return false;
        #endregion
    }

    public void UpdateTargetStateVectors(Vector3D position, Vector3D hitPosition, Vector3D velocity, double timeSinceLock = 0)
    {
        TargetCenter = position;
        HitPosition = hitPosition;
        TargetVelocity = velocity;
        TimeSinceLastLock = timeSinceLock;
    }

    void ProcessScanData(MyDetectedEntityInfo info, IMyTerminalBlock reference, Vector3D scanOrigin)
    {
        // Validate target and assign values
        if (info.IsEmpty() ||
            _targetFilter.Contains(info.Type) ||
            _gridIDsToIgnore.Contains(info.EntityId))
        {
            MissedLastScan = true;
            CycleAimMode();
        }
        else
        {
            if (Vector3D.DistanceSquared(info.Position, scanOrigin) < MinRange * MinRange && Status != TargetingStatus.Locked)
            {
                Status = TargetingStatus.TooClose;
                return;
            }

            if (info.EntityId != TargetId)
            {
                if (Status == TargetingStatus.Locked)
                {
                    MissedLastScan = true;
                    CycleAimMode();
                    return;
                }
                else if (_manualLockOverride)
                {
                    MissedLastScan = true;
                    return;
                }
            }

            MissedLastScan = false;
            UpdateTargetStateVectors(info.Position, info.HitPosition.Value, info.Velocity);
            TargetSize = info.BoundingBox.Size.Length();
            _targetOrientation = info.Orientation;

            if (Status != TargetingStatus.Locked) // Initial lockon
            {
                Status = TargetingStatus.Locked;
                TargetId = info.EntityId;
                TargetRelation = info.Relationship;
                TargetType = info.Type;

                // Compute aim offset
                if (!_manualLockOverride)
                {
                    Vector3D hitPosOffset = reference == null ? Vector3D.Zero : VectorMath.Rejection(reference.GetPosition() - scanOrigin, HitPosition - scanOrigin);
                    PreciseModeOffset = Vector3D.TransformNormal(info.HitPosition.Value + hitPosOffset - TargetCenter, MatrixD.Transpose(_targetOrientation));
                }
            }

            _manualLockOverride = false;
        }
    }

    void CycleAimMode()
    {
        _currentAimMode = (AimMode)((int)(_currentAimMode + 1) % 3);
    }

    public void Update(double timeStep, List<IMyCameraBlock> cameraList, List<IMyShipController> shipControllers, IMyTerminalBlock referenceBlock = null)
    {
        _timeSinceLastScan += timeStep;

        if (!IsScanning)
            return;

        TimeSinceLastLock += timeStep;

        if (cameraList.Count == 0)
            return;

        // Check for lock lost
        if (TimeSinceLastLock > (MaxTimeForLockBreak + AutoScanInterval) && (Status == TargetingStatus.Locked || _manualLockOverride))
        {
            LockLost = true; // TODO: Change this to a callback
            ClearLockInternal();
            return;
        }

        IMyTerminalBlock reference = GetReference(cameraList, shipControllers, referenceBlock);

        MyDetectedEntityInfo info;
        IMyCameraBlock camera;
        bool scanned;
        if (Status == TargetingStatus.Locked || _manualLockOverride)
        {
            scanned = DoLockScan(cameraList, out info, out camera);
        }
        else
        {
            scanned = DoSearchScan(cameraList, reference, out info, out camera);
        }

        if (!scanned)
        {
            return;
        }
        _timeSinceLastScan = 0;

        ProcessScanData(info, reference, camera.GetPosition());
    }

    void GetCamerasInDirection(List<IMyCameraBlock> allCameras, List<IMyCameraBlock> availableCameras, Vector3D testVector, bool vectorIsPosition = false)
    {
        availableCameras.Clear();

        foreach (var c in allCameras)
        {
            if (c.Closed)
                continue;

            if (TestCameraAngles(c, vectorIsPosition ? testVector - c.GetPosition() : testVector))
                availableCameras.Add(c);
        }
    }

    bool TestCameraAngles(IMyCameraBlock camera, Vector3D direction)
    {
        Vector3D local = Vector3D.Rotate(direction, MatrixD.Transpose(camera.WorldMatrix));

        if (local.Z > 0)
            return false;

        var yawTan = Math.Abs(local.X / local.Z);
        var localSq = local * local;
        var pitchTanSq = localSq.Y / (localSq.X + localSq.Z);

        return yawTan <= 1 && pitchTanSq <= 1;
    }

    IMyCameraBlock GetCameraWithMaxRange(List<IMyCameraBlock> cameras)
    {
        double maxRange = 0;
        IMyCameraBlock maxRangeCamera = null;
        foreach (var c in cameras)
        {
            if (c.Closed)
                continue;

            if (c.AvailableScanRange > maxRange)
            {
                maxRangeCamera = c;
                maxRange = maxRangeCamera.AvailableScanRange;
            }
        }

        return maxRangeCamera;
    }

    IMyCameraBlock GetControlledCamera(List<IMyCameraBlock> cameras)
    {
        foreach (var c in cameras)
        {
            if (c.Closed)
                continue;

            if (c.IsActive)
                return c;
        }
        return null;
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

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    public double CurrentTimeSinceLastRun { get; private set; } = 0;
    public long CurrentTicksSinceLastRun { get; private set; } = 0;

    QueuedAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<QueuedAction> _queuedActions = new Queue<QueuedAction>();
    readonly Program _program;

    public const long TicksPerSecond = 60;
    public const double TickDurationSeconds = 1.0 / TicksPerSecond;
    const long ClockTicksPerGameTick = 166666L;

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
        long deltaTicks = Math.Max(0, _program.Runtime.TimeSinceLastRun.Ticks / ClockTicksPerGameTick);

        if (_firstRun)
        {
            if (_ignoreFirstRun)
            {
                deltaTicks = 0;
            }
            _firstRun = false;
        }

        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTicksSinceLastRun = action.TicksSinceLastRun + deltaTicks;
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTicks * TickDurationSeconds;
            action.Update(deltaTicks);
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
            _currentlyQueuedAction.Update(deltaTicks);
            if (_currentlyQueuedAction.JustRan)
            {
                if (!_currentlyQueuedAction.DisposeAfterRun)
                {
                    _queuedActions.Enqueue(_currentlyQueuedAction);
                }
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
    public void AddQueuedAction(Action action, double updateInterval, bool removeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        QueuedAction scheduledAction = new QueuedAction(action, updateInterval, removeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    public void AddQueuedAction(QueuedAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class QueuedAction : ScheduledAction
{
    public QueuedAction(Action action, double runInterval, bool removeAfterRun = false)
        : base(action, 1.0 / runInterval, removeAfterRun: removeAfterRun, timeOffset: 0)
    { }
}

public class ScheduledAction
{
    public bool JustRan { get; private set; } = false;
    public bool DisposeAfterRun { get; private set; } = false;
    public double TimeSinceLastRun { get { return TicksSinceLastRun * Scheduler.TickDurationSeconds; } }
    public long TicksSinceLastRun { get; private set; } = 0;
    public double RunInterval
    {
        get
        {
            return RunIntervalTicks * Scheduler.TickDurationSeconds;
        }
        set
        {
            RunIntervalTicks = (long)Math.Round(value * Scheduler.TicksPerSecond);
        }
    }
    public long RunIntervalTicks
    {
        get
        {
            return _runIntervalTicks;
        }
        set
        {
            if (value == _runIntervalTicks)
                return;

            _runIntervalTicks = value < 0 ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : Scheduler.TicksPerSecond / _runIntervalTicks;
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
                RunIntervalTicks = long.MaxValue;
            else
                RunIntervalTicks = (long)Math.Round(Scheduler.TicksPerSecond / value);
        }
    }

    long _runIntervalTicks;
    double _runFrequency;
    readonly Action _action;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TicksSinceLastRun = (long)Math.Round(timeOffset * Scheduler.TicksPerSecond);
    }

    public void Update(long deltaTicks)
    {
        TicksSinceLastRun += deltaTicks;

        if (TicksSinceLastRun >= RunIntervalTicks)
        {
            _action.Invoke();
            TicksSinceLastRun = 0;

            JustRan = true;
        }
        else
        {
            JustRan = false;
        }
    }
}
#endregion

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
    /// Normalizes a vector only if it is non-zero and non-unit
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
}

enum TargetRelation : byte { Neutral = 0, Other = 0, Enemy = 1, Friendly = 2, Locked = 4, LargeGrid = 8, SmallGrid = 16, Missile = 32, Asteroid = 64, RelationMask = Neutral | Enemy | Friendly, TypeMask = LargeGrid | SmallGrid | Other | Missile | Asteroid }

/*
Whip's ApplyGyroOverride - Last modified: 2023/11/19

Takes pitch, yaw, and roll speeds relative to the gyro's backwards
ass rotation axes. 
*/
void ApplyGyroOverride(Vector3D rotationSpeedPYR, List<IMyGyro> gyros, MatrixD worldMatrix)
{
    var worldRotationPYR = Vector3D.TransformNormal(rotationSpeedPYR, worldMatrix);

    foreach (var g in gyros)
    {
        var gyroRotationPYR = Vector3D.TransformNormal(worldRotationPYR, Matrix.Transpose(g.WorldMatrix));

        g.Pitch = (float)gyroRotationPYR.X;
        g.Yaw = (float)gyroRotationPYR.Y;
        g.Roll = (float)gyroRotationPYR.Z;
        g.GyroOverride = true;
    }
}

void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, List<IMyGyro> gyros, MatrixD worldMatrix)
{
    var rotationVec = new Vector3D(pitchSpeed, yawSpeed, rollSpeed);
    ApplyGyroOverride(rotationVec, gyros, worldMatrix);
}

/// Whip's GetRotationVector - Last modified: 2023/11/19
/// <summary>
/// <para>
///     This method computes the axis-angle rotation required to align the
///     reference world matrix with the desired forward and up vectors.
/// </para>
/// <para>
///     The desired forward and up vectors are used to construct the desired
///     target orientation relative to the current world matrix orientation.
///     The current orientation of the craft with respect to itself will be the
///     identity matrix, thus the error between our desired orientation and our
///     target orientation is simply the target orientation itself:
///     M_target = M_current * M_error =>
///     M_target = I * M_error =>
///     M_target = M_error
/// </para>
/// <para>
///     This is designed for use with Keen's gyroscopes where:
///     + pitch = -X rotation,
///     + yaw   = -Y rotation,
///     + roll  = -Z rotation
/// </para>
/// </summary>
/// <remarks>
///     Dependencies: <c>VectorMath.SafeNormalize</c>
/// </remarks>
/// <param name="desiredForwardVector">
///     Desired forward direction in world frame.
///     This is the primary constraint used to allign pitch and yaw.
/// </param>
/// <param name="desiredUpVector">
///     Desired up direction in world frame.
///     This is the secondary constraint used to align roll. 
///     Set to <c>null</c> if roll control is not desired.
/// </param>
/// <param name="worldMatrix">
///     World matrix describing current orientation.
///     The translation part of the matrix is ignored; only the orientation matters.
/// </param>
/// <returns>
///     Pitch-Yaw-Roll rotation vector to the desired orientation (rads). 
/// </returns>
public static Vector3D GetRotationVector(Vector3D desiredForwardVector, Vector3D? desiredUpVector, MatrixD worldMatrix)
{
    var transposedWm = MatrixD.Transpose(worldMatrix);
    var forwardVector = Vector3D.Rotate(VectorMath.SafeNormalize(desiredForwardVector), transposedWm);

    Vector3D leftVector = Vector3D.Zero;
    if (desiredUpVector.HasValue)
    {
        desiredUpVector = Vector3D.Rotate(desiredUpVector.Value, transposedWm);
        leftVector = Vector3D.Cross(desiredUpVector.Value, forwardVector);
    }

    Vector3D axis;
    double angle;
    if (!desiredUpVector.HasValue || Vector3D.IsZero(leftVector))
    {
        /*
         * Simple case where we have no valid roll constraint:
         * We merely cross the current forward vector (Vector3D.Forward) on the 
         * forwardVector.
         */
        axis = new Vector3D(-forwardVector.Y, forwardVector.X, 0);
        angle = Math.Acos(MathHelper.Clamp(-forwardVector.Z, -1.0, 1.0));
    }
    else
    {
        /*
         * Here we need to construct the target orientation matrix so that we
         * can extract the error from it in axis-angle representation.
         */
        leftVector = VectorMath.SafeNormalize(leftVector);
        var upVector = Vector3D.Cross(forwardVector, leftVector);
        var targetOrientation = new MatrixD()
        {
            Forward = forwardVector,
            Left = leftVector,
            Up = upVector,
        };

        axis = new Vector3D(targetOrientation.M32 - targetOrientation.M23,
                            targetOrientation.M13 - targetOrientation.M31,
                            targetOrientation.M21 - targetOrientation.M12);

        double trace = targetOrientation.M11 + targetOrientation.M22 + targetOrientation.M33;
        angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1.0, 1.0));
    }

    Vector3D rotationVectorPYR;
    if (Vector3D.IsZero(axis))
    {
        /*
         * Degenerate case where we get a zero axis. This means we are either
         * exactly aligned or exactly anti-aligned. In the latter case, we just
         * assume the yaw is PI to get us away from the singularity.
         */
        angle = forwardVector.Z < 0 ? 0 : Math.PI;
        rotationVectorPYR = new Vector3D(0, angle, 0);
    }
    else
    {
        rotationVectorPYR = VectorMath.SafeNormalize(axis) * angle;
    }

    return rotationVectorPYR;
}
#endregion
