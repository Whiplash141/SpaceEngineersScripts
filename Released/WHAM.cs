
#region WHAM
const string Version = "170.8.6";
const string Date = "2023/03/20";
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

enum GuidanceAlgoType { ProNav, WhipNav, HybridNav, ZeroEffortMiss };

MissileGuidanceBase _selectedGuidance;
Dictionary<GuidanceAlgoType, MissileGuidanceBase> _guidanceAlgorithms;

const string MissileNamePattern = "({0} {1})";
const string MissileGroupPattern = "{0} {1}";

Vector3D
    _shooterForwardVec,
    _shooterLeftVec,
    _shooterUpVec,
    _shooterPos,
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
    _timeSinceLastIngest = 0;

int
    _setupTicks = 0,
    _missileStage = 0;

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
enum AntennaNameMode { Meme, Empty, MissileName, MissileStatus };

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
List<IMyBeacon> _beacons = new List<IMyBeacon>();
List<IMySensorBlock> _sensors = new List<IMySensorBlock>();
List<IMyWarhead> _warheads = new List<IMyWarhead>();
List<IMyTimerBlock> _timers = new List<IMyTimerBlock>();
List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
List<IMyCameraBlock> _homingCameras = new List<IMyCameraBlock>();
List<IMyGasTank> _gasTanks = new List<IMyGasTank>();
List<MyDetectedEntityInfo> _sensorEntities = new List<MyDetectedEntityInfo>();

List<DynamicCircularBuffer<IMyCameraBlock>> _cameraBufferList = new List<DynamicCircularBuffer<IMyCameraBlock>>();
DynamicCircularBuffer<IMyCameraBlock>
    _camerasFront = new DynamicCircularBuffer<IMyCameraBlock>(),
    _camerasBack = new DynamicCircularBuffer<IMyCameraBlock>(),
    _camerasLeft = new DynamicCircularBuffer<IMyCameraBlock>(),
    _camerasRight = new DynamicCircularBuffer<IMyCameraBlock>(),
    _camerasUp = new DynamicCircularBuffer<IMyCameraBlock>(),
    _camerasDown = new DynamicCircularBuffer<IMyCameraBlock>();

ImmutableArray<MyTuple<byte, long, Vector3D, double>>.Builder _messageBuilder = ImmutableArray.CreateBuilder<MyTuple<byte, long, Vector3D, double>>();
List<MyTuple<Vector3D, long>> _remoteFireRequests = new List<MyTuple<Vector3D, long>>();

HashSet<long> _savedKeycodes = new HashSet<long>();

IMyShipController _missileReference = null;

enum PostSetupAction { None = 0, Fire = 1, FireRequestResponse = 2 };
PostSetupAction _postSetupAction = PostSetupAction.None;

const int MAX_INSTRUCTIONS_PER_SETUP_RUN = 5000;

const double
    UpdatesPerSecond = 10.0,
    SecondsPerUpdate = 1.0 / UpdatesPerSecond,
    DegToRad = Math.PI / 180,
    RpmToRad = Math.PI / 30,
    TopdownDescentAngle = Math.PI / 6,
    MaxGuidanceTime = 180,
    RuntimeToRealtime = (1.0 / 60.0) / 0.0166666,
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
    IgcTagregister = "IGC_MSL_REG_MSG",
    IgcTagUnicast = "UNICAST";

QueuedAction
    _stage1Action,
    _stage2Action,
    _stage3Action,
    _stage4Action;

ScheduledAction
    _guidanceActivateAction,
    _randomHeadingVectorAction;

#region Custom Data Ini
readonly MyIni _myIni = new MyIni();

const string
    IniSectionNames = "Names",
    IniSectionDelays = "Delays",
    IniSectionGyro = "Gyros",
    IniSectionHoming = "Homing Parameters",
    IniSectionBeamRide = "Beam Riding Parameters",
    IniSectionEvasion = "Evasion Parameters",
    IniSectionSpiral = "Spiral Parameters",
    IniSectionRandom = "Random Fligh Path Parameters",
    IniSectionRaycast = "Raycast/Sensors",
    IniSectionMisc = "Misc.",
    IniCompatMemeMode = "Antenna meme mode";

ConfigBool _autoConfigure = new ConfigBool(IniSectionNames, "Auto-configure missile name", true);
ConfigString _missileTag = new ConfigString(IniSectionNames, "Missile name tag", "Missile");
ConfigInt _missileNumber = new ConfigInt(IniSectionNames, "Missile number", 1);
ConfigString _fireControlGroupNameTag = new ConfigString(IniSectionNames, "Fire control group name", "Fire Control");
ConfigString _detachThrustTag = new ConfigString(IniSectionNames, "Detach thruster name tag", "Detach");

ConfigDouble _disconnectDelay = new ConfigDouble(IniSectionDelays, "Stage 1: Disconnect delay (s)", 0);
ConfigDouble _guidanceDelay = new ConfigDouble(IniSectionDelays, "Guidance delay (s)", 1);
ConfigDouble _detachDuration = new ConfigDouble(IniSectionDelays, "Stage 2: Detach duration (s)", 0);
ConfigDouble _mainIgnitionDelay = new ConfigDouble(IniSectionDelays, "Stage 3: Main ignition delay (s)", 0);

ConfigDouble _gyroProportionalGain = new ConfigDouble(IniSectionGyro, "Proportional gain", 10);
ConfigDouble _gyroIntegralGain = new ConfigDouble(IniSectionGyro, "Integral gain", 0);
ConfigDouble _gyroDerivativeGain = new ConfigDouble(IniSectionGyro, "Derivative gain", 10);

ConfigEnum<GuidanceAlgoType> _guidanceAlgoType = new ConfigEnum<GuidanceAlgoType>(IniSectionHoming, "Guidance algorithm", GuidanceAlgoType.ProNav, " Valid guidance algorithms: ProNav, WhipNav, HybridNav, ZeroEffortMiss");
ConfigDouble _navConstant = new ConfigDouble(IniSectionHoming, "Navigation constant", 3);
ConfigDouble _accelNavConstant = new ConfigDouble(IniSectionHoming, "Acceleration constant", 1.5);
ConfigDouble _maxAimDispersion = new ConfigDouble(IniSectionHoming, "Max aim dispersion (m)", 0);
ConfigDouble _topDownAttackHeight = new ConfigDouble(IniSectionHoming, "Topdown attack height (m)", 1500);

ConfigDouble _offsetUp = new ConfigDouble(IniSectionBeamRide, "Hit offset up (m)", 0);
ConfigDouble _offsetLeft = new ConfigDouble(IniSectionBeamRide, "Hit offset left (m)", 0);

ConfigDouble _missileSpinRPM = new ConfigDouble(IniSectionEvasion, "Spin rate (RPM)", 0);
ConfigBool _evadeWithSpiral = new ConfigBool(IniSectionEvasion, "Use spiral", false);
ConfigBool _evadeWithRandomizedHeading = new ConfigBool(IniSectionEvasion, "Use random flight path", true, " AKA \"Drunken Missile Mode\"");

ConfigDouble _spiralDegrees = new ConfigDouble(IniSectionSpiral, "Spiral angle (deg)", 15);
ConfigDouble _timeMaxSpiral = new ConfigDouble(IniSectionSpiral, "Spiral time (sec)", 3);
ConfigDouble _spiralActivationRange = new ConfigDouble(IniSectionSpiral, "Spiral activation range (m)", 1000);

ConfigDouble _randomVectorInterval = new ConfigDouble(IniSectionRandom, "Direction change interval (sec)", 0.5);
ConfigDouble _maxRandomAccelRatio = new ConfigDouble(IniSectionRandom, "Max acceleration ratio", 0.25);

ConfigBool _useCamerasForHoming = new ConfigBool(IniSectionRaycast, "Use cameras for homing", true);
ConfigDouble _raycastRange = new ConfigDouble(IniSectionRaycast, "Tripwire range (m)", 0.25);
ConfigDouble _raycastMinimumTargetSize = new ConfigDouble(IniSectionRaycast, "Minimum target size (m)", 0);
ConfigDouble _minimumArmingRange = new ConfigDouble(IniSectionRaycast, "Minimum warhead arming range (m)", 100);
ConfigBool _raycastIgnoreFriends = new ConfigBool(IniSectionRaycast, "Ignore friendlies", false);
ConfigBool _raycastIgnorePlanetSurface = new ConfigBool(IniSectionRaycast, "Ignore planets", true);
ConfigBool _ignoreIdForDetonation = new ConfigBool(IniSectionRaycast, "Ignore target ID for detonation", false);

ConfigBool _allowRemoteFire = new ConfigBool(IniSectionMisc, "Allow remote firing", false);
ConfigEnum<AntennaNameMode> _antennaMode = new ConfigEnum<AntennaNameMode>(IniSectionMisc, "Antenna name mode", AntennaNameMode.Meme, " Valid antenna name modes: Meme, Empty, MissileName, MissileStatus");

IConfigValue[] _config;

void SetupConfig()
{
    _config = new IConfigValue[]
    {
        _autoConfigure,
        _missileTag,
        _missileNumber,
        _fireControlGroupNameTag,
        _detachThrustTag,

        _disconnectDelay,
        _guidanceDelay,
        _detachDuration,
        _mainIgnitionDelay,

        _gyroProportionalGain,
        _gyroIntegralGain,
        _gyroDerivativeGain,

        _guidanceAlgoType,
        _navConstant,
        _accelNavConstant,
        _maxAimDispersion,
        _topDownAttackHeight,

        _offsetUp,
        _offsetLeft,

        _missileSpinRPM,
        _evadeWithSpiral,
        _evadeWithRandomizedHeading,

        _spiralDegrees,
        _timeMaxSpiral,
        _spiralActivationRange,

        _randomVectorInterval,
        _maxRandomAccelRatio,

        _useCamerasForHoming,
        _raycastRange,
        _raycastMinimumTargetSize,
        _minimumArmingRange,
        _raycastIgnoreFriends,
        _raycastIgnorePlanetSurface,
        _ignoreIdForDetonation,

        _allowRemoteFire,
        _antennaMode,
    };
}
#endregion

#endregion

#region Main Methods
Program()
{
    SetupConfig();

    _memeIndex = RNGesus.Next(_antennaMemeMessages.Length);

    _unicastListener = IGC.UnicastListener;
    _unicastListener.SetMessageCallback(IgcTagUnicast);

    _broadcastListenerRemoteFire = IGC.RegisterBroadcastListener(IgcTagRemoteFireRequest);
    _broadcastListenerRemoteFire.SetMessageCallback(IgcTagRemoteFireRequest);

    _guidanceActivateAction = new ScheduledAction(ActivateGuidance, 0, true);
    _stage1Action = new QueuedAction(MissileStage1, 0);
    _stage2Action = new QueuedAction(MissileStage2, 0);
    _stage3Action = new QueuedAction(MissileStage3, 0);
    _stage4Action = new QueuedAction(MissileStage4, 0);
    _randomHeadingVectorAction = new ScheduledAction(ComputeRandomHeadingVector, 0, false);
    _stage1Action.RunInterval = 0;

    // Scheduler assignment
    _scheduler = new Scheduler(this, true);

    // Setting up scheduled tasks
    _scheduler.AddScheduledAction(_guidanceActivateAction);
    _scheduler.AddScheduledAction(GuidanceNavAndControl, UpdatesPerSecond);
    _scheduler.AddScheduledAction(CheckProximity, UpdatesPerSecond);
    _scheduler.AddScheduledAction(PrintEcho, 1);
    _scheduler.AddScheduledAction(NetworkTargets, 6);
    _scheduler.AddScheduledAction(_randomHeadingVectorAction);

    // Setting up sequential tasks
    _scheduler.AddQueuedAction(_stage1Action);
    _scheduler.AddQueuedAction(_stage2Action);
    _scheduler.AddQueuedAction(_stage3Action);
    _scheduler.AddQueuedAction(_stage4Action);
    _scheduler.AddQueuedAction(KillPower, MaxGuidanceTime);

    _runtimeTracker = new RuntimeTracker(this, 120, 0.005);

    _raycastHoming = new RaycastHoming(5000, 3, 0, Me.CubeGrid.EntityId);
    _raycastHoming.AddEntityTypeToFilter(MyDetectedEntityType.FloatingObject, MyDetectedEntityType.Planet, MyDetectedEntityType.Asteroid);

    // Populate guidance algos
    _guidanceAlgorithms = new Dictionary<GuidanceAlgoType, MissileGuidanceBase>()
{
    { GuidanceAlgoType.ProNav, new ProNavGuidance(UpdatesPerSecond, _navConstant) },
    { GuidanceAlgoType.WhipNav, new WhipNavGuidance(UpdatesPerSecond, _navConstant) },
    { GuidanceAlgoType.HybridNav, new HybridNavGuidance(UpdatesPerSecond, _navConstant) },
    { GuidanceAlgoType.ZeroEffortMiss, new ZeroEffortMissGuidance(UpdatesPerSecond, _navConstant) },
};

    // Enable raycast spooling
    GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(null, camera =>
    {
        if (camera.IsSameConstructAs(Me))
            camera.EnableRaycast = true;
        return false;
    });

    // Save camera buffer references to our list
    _cameraBufferList.Add(_camerasFront);
    _cameraBufferList.Add(_camerasRight);
    _cameraBufferList.Add(_camerasLeft);
    _cameraBufferList.Add(_camerasUp);
    _cameraBufferList.Add(_camerasDown);
    _cameraBufferList.Add(_camerasBack);

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
    _timeSpiral += lastRuntime;
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

void PrintEcho()
{
    Echo(GetTitle());
    Echo($"Time Active: {_timeTotal:n0} sec");
    Echo(_runtimeTracker.Write());
}
#endregion

#region Ini Configuration
void LoadIniConfig()
{
    _myIni.Clear();

    bool parsed = _myIni.TryParse(Me.CustomData);
    if (!parsed)
    {
        SaveIniConfig();
        _setupBuilder.Append("Wrote default missile config!\n");
        return;
    }

    foreach (IConfigValue c in _config)
    {
        c.ReadFromIni(_myIni);
    }

    // For backwards compat
    bool antennaMemeMode;
    if (_myIni.Get(IniSectionMisc, IniCompatMemeMode).TryGetBoolean(out antennaMemeMode))
    {
        _antennaMode.Value = antennaMemeMode ? AntennaNameMode.Meme : AntennaNameMode.Empty;
    }

    _setupBuilder.Append("Loaded missile config!\n");
}

void SaveIniConfig()
{
    _myIni.Clear();

    _missileGroupNameTag = string.Format(MissileGroupPattern, _missileTag, _missileNumber);
    _missileNameTag = string.Format(MissileNamePattern, _missileTag, _missileNumber);

    foreach (IConfigValue c in _config)
    {
        c.WriteToIni(_myIni);
    }

    _timeMaxSpiral.Value = Math.Max(_timeMaxSpiral, 0.1);
    _maxRandomAccelRatio.Value = MathHelper.Clamp(_maxRandomAccelRatio, 0, 1);

    _guidanceActivateAction.RunInterval = _guidanceDelay;
    _stage2Action.RunInterval = _disconnectDelay;
    _stage3Action.RunInterval = _detachDuration;
    _stage4Action.RunInterval = _mainIgnitionDelay;
    _randomHeadingVectorAction.RunInterval = _randomVectorInterval;

    Me.CustomData = _myIni.ToString();
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
        bool fireCommanded = false;
        bool remotelyFired = false;
        while (_unicastListener.HasPendingMessage)
        {
            MyIGCMessage message = _unicastListener.AcceptMessage();
            object data = message.Data;
            if (message.Tag == IgcTagFire)
            {
                fireCommanded = true;
            }
            else if (message.Tag == IgcTagregister)
            {
                if (data is long)
                {
                    long keycode = (long)data;
                    _savedKeycodes.Add(keycode);
                    remotelyFired = true;
                }
            }
        }

        if (fireCommanded)
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
            object messageData = _broadcastListenerParameters.AcceptMessage().Data;

            if (!(messageData is MyTuple<byte, long>))
                continue;

            var payload = (MyTuple<byte, long>)messageData;
            long keycode = payload.Item2;

            if (!_savedKeycodes.Contains(keycode))
                continue;

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
            object messageData = _broadcastListenerBeamRiding.AcceptMessage().Data;

            if (_guidanceMode == GuidanceMode.Active && !_retask)
                continue;

            if (!(messageData is MyTuple<Vector3, Vector3, Vector3, Vector3, long>))
                continue;

            var payload = (MyTuple<Vector3, Vector3, Vector3, Vector3, long>)messageData;
            long keycode = payload.Item5;
            if (!_savedKeycodes.Contains(keycode))
                continue;

            _retask = false;
            _shooterForwardVec = payload.Item1;
            _shooterLeftVec = payload.Item2;
            _shooterUpVec = payload.Item3;
            _shooterPosCached = payload.Item4;

            _guidanceMode = GuidanceMode.BeamRiding;
        }

        /* Item1.Col0: Hit position */
        /* Item1.Col1: Target position */
        /* Item1.Col2: Target velocity */
        /* Item2.Col0: Precision offset */
        /* Item2.Col1: Shooter position */
        /* Item2.Col2: <NOT USED> */
        /* Item3:      Time since last lock */
        /* Item4:      Target ID */
        /* Item5:      Key code */
        while (_broadcastListenerHoming.HasPendingMessage)
        {
            object messageData = _broadcastListenerHoming.AcceptMessage().Data;

            if (!(messageData is MyTuple<Matrix3x3, Matrix3x3, float, long, long>))
                continue;

            var payload = (MyTuple<Matrix3x3, Matrix3x3, float, long, long>)messageData;
            long keycode = payload.Item5;
            if (!_savedKeycodes.Contains(keycode))
                continue;

            _shooterPosCached = payload.Item2.Col1;

            if (_guidanceMode == GuidanceMode.Active && !_retask)
                continue;

            _retask = false;
            Vector3D hitPos = payload.Item1.Col0;
            Vector3D offset = payload.Item2.Col0;
            _targetPos = payload.Item1.Col1;
            _targetVel = payload.Item1.Col2;
            _timeSinceLastLock = payload.Item3;
            long targetId = payload.Item4;
            _timeSinceLastIngest = 1.0 / 60.0; // IGC messages are always a tick delayed

            _guidanceMode = GuidanceMode.SemiActive;

            _raycastHoming.SetInitialLockParameters(hitPos, _targetVel, offset, _timeSinceLastLock, targetId);
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
StringBuilder _setupBuilder = new StringBuilder();

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
    _beacons.Clear();
    _sensors.Clear();
    _warheads.Clear();
    _cameras.Clear();
    _camerasFront.Clear();
    _camerasBack.Clear();
    _camerasLeft.Clear();
    _camerasRight.Clear();
    _camerasUp.Clear();
    _camerasDown.Clear();
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
            _setupBuilder.Append("> WARN: No groups containing this\n  program found.\n");
            _missileGroup = GridTerminalSystem.GetBlockGroupWithName(_missileGroupNameTag); // Default
        }
        else if (_foundGroups.Count > 1) // Too many
        {
            _setupBuilder.Append("> WARN: MULTIPLE groups\n  containing this program\n  found:\n");
            for (int i = 0; i < _foundGroups.Count; ++i)
            {
                var thisGroup = _foundGroups[i];
                _setupBuilder.Append($"    {i + 1}: {thisGroup.Name}\n");
            }
        }
        else
        {
            _setupBuilder.Append($"> Missile group found:\n  '{_missileTag} {_autoConfigureMissileNumber}'\n");
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
                _setupBuilder.Append($"> WARN: No antennas in group named '{_fireControlGroupNameTag}', but remote fire is active.\n");
            }
            else if (!reload)
            {
                _preSetupFailed = true;
                _setupBuilder.Append($">> ERR: No antennas in group named '{_fireControlGroupNameTag}'! This missile MUST be attached to a configured firing ship to fire!\n");
            }
        }
        else
        {
            foreach (IMyRadioAntenna a in _broadcasters)
            {
                //x.IsSameConstructAs(Me))? Check if missile has connectors before this?
                _savedKeycodes.Add(a.EntityId);
                if (AtInstructionLimit()) { yield return SetupStatus.Running; }
            }
            _setupBuilder.Append($"> Info: Found antenna(s) on firing ship\n");
            _foundLampAntennas = true;
        }
    }
    else if (_allowRemoteFire)
    {
        _setupBuilder.Append($"> WARN: No group named '{_fireControlGroupNameTag}' found, but remote fire is active.\n");
    }
    else if (!reload)
    {
        _preSetupFailed = true;
        _setupBuilder.Append($">> ERR: No group named '{_fireControlGroupNameTag}' found! This missile MUST be attached to a configured firing ship to fire!\n");
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
        _setupBuilder.Append($">> ERR: No block group named '{_missileGroupNameTag}' found!\n");
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

            _setupBuilder.Append($"> Info: Setup took {_setupTicks} tick(s)\n");

            // Post-block fetch
            bool setupPassed = SetupErrorChecking();
            if (!setupPassed || _preSetupFailed)
            {
                _setupBuilder.Append("\n>>> Setup Failed! <<<\n");
                Echo(_setupBuilder.ToString());
                return;
            }
            // Implied else
            _setupBuilder.Append("\n>>> Setup Successful! <<<\n");
            _missileReference = _shipControllers[0];

            if ((_postSetupAction & PostSetupAction.Fire) != 0)
            {
                _shouldFire = true;
                RegisterBroadcastListeners();
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
    return Runtime.CurrentInstructionCount >= MAX_INSTRUCTIONS_PER_SETUP_RUN;
}

bool SetupErrorChecking()
{
    bool setupFailed = false;
    // ERRORS
    setupFailed |= EchoIfTrue(_antennas.Count == 0, ">> ERR: No antennas found");
    setupFailed |= EchoIfTrue(_gyros.Count == 0, ">> ERR: No gyros found");
    setupFailed |= EchoIfTrue(_shipControllers.Count == 0, ">> ERR: No remotes found");
    if (_shipControllers.Count > 0)
    {
        GetThrusterOrientation(_shipControllers[0]);
    }
    setupFailed |= EchoIfTrue(_mainThrusters.Count == 0, ">> ERR: No main thrusters found");
    setupFailed |= EchoIfTrue(_batteries.Count == 0 && _reactors.Count == 0, ">> ERR: No batteries or reactors found");

    // WARNINGS
    if (!EchoIfTrue(_mergeBlocks.Count == 0 && _rotors.Count == 0 && _connectors.Count == 0, "> WARN: No merge blocks, rotors, or connectors found for detaching"))
    {
        EchoBlockCount(_mergeBlocks.Count, "merge");
        EchoBlockCount(_rotors.Count, "rotor");
        EchoBlockCount(_connectors.Count, "connector");
    }

    // INFO
    EchoBlockCount(_artMasses.Count, "art. mass block");
    EchoBlockCount(_sensors.Count, "sensor");
    EchoBlockCount(_warheads.Count, "warhead");
    EchoBlockCount(_beacons.Count, "beacon");
    EchoBlockCount(_cameras.Count, "camera");
    EchoBlockCount(_timers.Count, "timer");
    EchoBlockCount(_unsortedThrusters.Count, "total thruster");
    EchoBlockCount(_mainThrusters.Count, "main thruster");
    EchoBlockCount(_sideThrusters.Count, "side thruster");
    EchoBlockCount(_detachThrusters.Count, "detach thruster");
    EchoBlockCount(_gasTanks.Count, "gas tank");

    return !setupFailed;
}

void EchoBlockCount(int count, string name)
{
    _setupBuilder.Append($"> Info: {count} {name}{(count == 1 ? "" : "s")}\n");
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
        GetCameraOrientation(camera);
    }
    else if (AddToListIfType(block, _artMasses)
            || AddToListIfType(block, _batteries)
            || AddToListIfType(block, _gyros)
            || AddToListIfType(block, _mergeBlocks)
            || AddToListIfType(block, _shipControllers)
            || AddToListIfType(block, _connectors)
            || AddToListIfType(block, _rotors)
            || AddToListIfType(block, _reactors)
            || AddToListIfType(block, _beacons)
            || AddToListIfType(block, _sensors)
            || AddToListIfType(block, _timers)
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

bool EchoIfTrue(bool state, string toEcho)
{
    if (state)
    {
        _setupBuilder.Append(toEcho).Append("\n");
    }
    return state;
}

// Assumes all thrust is on same grid as PB
void GetThrusterOrientation(IMyTerminalBlock refBlock)
{
    var forwardDirn = refBlock.Orientation.Forward;

    foreach (IMyThrust t in _unsortedThrusters)
    {
        var thrustDirn = Base6Directions.GetFlippedDirection(t.Orientation.Forward);
        if (thrustDirn == forwardDirn)
        {
            _mainThrusters.Add(t);
        }
        else
        {
            _sideThrusters.Add(t);
        }
    }
}

void GetCameraOrientation(IMyCameraBlock c)
{
    switch (c.Orientation.Forward)
    {
        case Base6Directions.Direction.Forward:
            _camerasFront.Add(c);
            return;
        case Base6Directions.Direction.Backward:
            _camerasBack.Add(c);
            return;
        case Base6Directions.Direction.Left:
            _camerasLeft.Add(c);
            return;
        case Base6Directions.Direction.Right:
            _camerasRight.Add(c);
            return;
        case Base6Directions.Direction.Up:
            _camerasUp.Add(c);
            return;
        case Base6Directions.Direction.Down:
            _camerasDown.Add(c);
            return;
    }
}
#endregion

#region Missile Launch Sequence
// Prepares missile for launch by activating power sources.
void MissileStage1()
{
    _missileStage = 1;

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

    foreach (var t in _timers)
    {
        t.Trigger();
    }
}

// Detaches missile from the firing ship.
void MissileStage2()
{
    _missileStage = 2;

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
    }

    foreach (var r in _rotors)
    {
        r.Detach();
    }

    foreach (var a in _antennas)
    {
        a.Radius = 1f;
        a.Enabled = false;
        a.EnableBroadcasting = false;
        a.Enabled = true; //this used to be a bug workaround, not sure if it is still needed tbh
        a.CustomName = "";
    }

    foreach (var b in _beacons)
    {
        b.Radius = 1f;
        b.Enabled = true;
        b.CustomName = "";
    }

    ApplyThrustOverride(_sideThrusters, MinThrust, false);
    ApplyThrustOverride(_detachThrusters, 100f);
}

// Disables missile thrust for drifting.
void MissileStage3()
{
    _missileStage = 3;

    ApplyThrustOverride(_detachThrusters, MinThrust);
}

// Ignites main thrust.
void MissileStage4()
{
    _missileStage = 4;

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
    ApplyThrustOverride(_mainThrusters, 100f);

    Me.CubeGrid.CustomName = _missileGroupNameTag;

    _killAllowed = true;
}
#endregion

#region Missile Guidance
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
    bool pastArmingRange, shouldSpiral;

    Navigation(_minimumArmingRange,
        _enableEvasion,
        _evadeWithSpiral,
        out missileMatrix,
        out missilePos,
        out missileVel,
        out _shooterPos,
        out gravityVec,
        out missileMass,
        out missileAccel,
        out _distanceFromShooter,
        out pastArmingRange,
        out shouldSpiral);

    Vector3D accelCmd = GuidanceMain(
        _guidanceMode,
        missileMatrix,
        missilePos,
        missileVel,
        gravityVec,
        missileAccel,
        pastArmingRange,
        shouldSpiral,
        out _shouldProximityScan);

    Control(missileMatrix, accelCmd, gravityVec, missileVel, missileMass);
    #endregion
}

void Navigation(
    double minArmingRange,
    bool enableEvasion,
    bool evadeWithSpiral,
    out MatrixD missileMatrix,
    out Vector3D missilePos,
    out Vector3D missileVel,
    out Vector3D shooterPos,
    out Vector3D gravity,
    out double missileMass,
    out double missileAcceleration,
    out double distanceFromShooter,
    out bool pastMinArmingRange,
    out bool shouldSpiral)
{
    missilePos = _missileReference.CenterOfMass;
    missileVel = _missileReference.GetShipVelocities().LinearVelocity;
    missileMatrix = _missileReference.WorldMatrix; // TODO: Determine from thrust allocation

    shooterPos = _shooterPosCached + _offsetLeft * _shooterLeftVec + _offsetUp * _shooterUpVec;

    gravity = _missileReference.GetNaturalGravity();

    distanceFromShooter = Vector3D.Distance(_shooterPos, missilePos);
    ScaleAntennaRange(distanceFromShooter + 100);

    // Computing mass, thrust, and acceleration
    double missileThrust = CalculateMissileThrust(_mainThrusters);
    missileMass = _missileReference.CalculateShipMass().PhysicalMass;
    missileAcceleration = missileThrust / missileMass;

    pastMinArmingRange = Vector3D.DistanceSquared(missilePos, shooterPos) >= minArmingRange * minArmingRange;
    shouldSpiral = enableEvasion && evadeWithSpiral;
}

Vector3D GuidanceMain(
    GuidanceMode guidanceMode,
    MatrixD missileMatrix,
    Vector3D missilePos,
    Vector3D missileVel,
    Vector3D gravity,
    double missileAcceleration,
    bool pastMinArmingRange,
    bool shouldSpiral,
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

        double distanceToTgtSq = Vector3D.DistanceSquared(missilePos, adjustedTargetPos);
        double closingSpeedSq = (missileVel - _targetVel).LengthSquared();
        shouldProximityScan = pastMinArmingRange && (closingSpeedSq > distanceToTgtSq); // Roughly 1 second till impact

        // Only spiral if we are close enough to the target to conserve fuel
        shouldSpiral &= ((_spiralActivationRange * _spiralActivationRange > distanceToTgtSq) && (closingSpeedSq * 4.0 < distanceToTgtSq)); // TODO: Don't hard code this lol
    }

    if (shouldSpiral)
    {
        accelCmd = missileAcceleration * SpiralTrajectory(accelCmd, _missileReference.WorldMatrix.Up);
    }

    if (_enableEvasion && _evadeWithRandomizedHeading)
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

    Vector3D missileToTargetVec = destinationVec - missilePos;

    Vector3D accelCmd;
    if (_missileStage == 4)
    {
        accelCmd = CalculateDriftCompensation(missileVel, missileToTargetVec, missileAcceleration, 0.5, gravity, 60);
    }
    else
    {
        accelCmd = missileToTargetVec;
    }

    if (!Vector3D.IsZero(gravity))
    {
        accelCmd = MissileGuidanceBase.GravityCompensation(missileAcceleration, accelCmd, gravity);
    }

    return VectorMath.SafeNormalize(accelCmd) * missileAcceleration;
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
    if (_missileStage == 4)
    {
        var headingDeviation = VectorMath.CosBetween(accelCmd, missileMatrix.Forward);
        ApplyThrustOverride(_mainThrusters, (float)MathHelper.Clamp(headingDeviation, 0.25f, 1f) * 100f);
        var sideVelocity = VectorMath.Rejection(velocityVec, accelCmd);
        ApplySideThrust(_sideThrusters, sideVelocity, gravityVec, mass);
    }

    // Get pitch and yaw angles
    double yaw, pitch, roll;
    GetRotationAnglesSimultaneous(accelCmd, -gravityVec, missileMatrix, out pitch, out yaw, out roll);

    // Angle controller
    double yawSpeed = _yawPID.Control(yaw);
    double pitchSpeed = _pitchPID.Control(pitch);

    // Handle roll more simply
    double rollSpeed = 0;
    if (Math.Abs(_missileSpinRPM) > 1e-3 && _missileStage == 4)
    {
        rollSpeed = _missileSpinRPM * RpmToRad;
    }
    else
    {
        rollSpeed = roll;
    }

    // Yaw and pitch slowdown to avoid overshoot
    if (Math.Abs(yaw) < GyroSlowdownAngle)
    {
        yawSpeed = UpdatesPerSecond * .5 * yaw;
    }

    if (Math.Abs(pitch) < GyroSlowdownAngle)
    {
        pitchSpeed = UpdatesPerSecond * .5 * pitch;
    }

    ApplyGyroOverride(pitchSpeed, yawSpeed, rollSpeed, _gyros, missileMatrix);
}

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
string GetAntennaName()
{
    switch (_antennaMode.Value)
    {
        case AntennaNameMode.Meme:
            return _antennaMemeMessages[_memeIndex];
        case AntennaNameMode.MissileName:
            return _missileNameTag;
        case AntennaNameMode.MissileStatus:
            return $"{_missileNameTag} / Mode: {_guidanceMode} / Age: {(_guidanceMode == GuidanceMode.BeamRiding ? 0 : _timeSinceLastLock):0.0}";
        default:
        case AntennaNameMode.Empty:
            return "";
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
        a.CustomName = GetAntennaName();
    }

    foreach (IMyBeacon thisBeacon in _beacons)
    {
        if (thisBeacon.Closed)
            continue;

        thisBeacon.Radius = (float)dist;
    }
}

void ApplyThrustOverride(List<IMyThrust> thrusters, float overrideValue, bool turnOn = true)
{
    float thrustProportion = overrideValue * 0.01f;
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

    Vector3D missileToTarget = adjustedTargetPos - _missileReference.GetPosition();
    double distanceToTarget = missileToTarget.Length();
    Vector3D missileToTargetNorm = missileToTarget / distanceToTarget;

    double closingSpeed;

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
            closingSpeed = Vector3D.Dot(closingVelocity, missileToTargetNorm);
            if (distanceToTarget < adjustedDetonationRange + closingSpeed * SecondsPerUpdate)
            {
                Detonate((distanceToTarget - adjustedDetonationRange) / closingSpeed);
                return;
            }
        }

        return;
    }

    // Try raycast detonation methods
    double raycastHitDistance = 0;

    // Do one scan in the direction of the target (if applicable)
    if ((_guidanceMode & GuidanceMode.Homing) != 0 && RaycastTripwireInDirection(missileToTargetNorm, closingVelocity, out raycastHitDistance, out closingSpeed))
    {
        Detonate((raycastHitDistance - _raycastRange) / closingSpeed);
        return;
    }

    /*
    Do one scan in the direction of the relative velocity vector
    If that fails, we will scan a cross pattern that traces the radius of the missile to try and catch
    complex geometry and avoid missed detonations
    */
    double apparentRadius = CalculateGridRadiusFromAxis(_missileReference.CubeGrid, closingVelocity) + _raycastRange;

    var baseDirection = VectorMath.SafeNormalize(closingVelocity) * _raycastRange;
    var perp1 = Vector3D.CalculatePerpendicularVector(closingVelocity) * apparentRadius;
    var perp2 = VectorMath.SafeNormalize(Vector3D.Cross(perp1, closingVelocity)) * apparentRadius;
    if (RaycastTripwireInDirection(closingVelocity, closingVelocity, out raycastHitDistance, out closingSpeed) ||
        RaycastTripwireInDirection(closingVelocity, closingVelocity, out raycastHitDistance, out closingSpeed, perp1) ||
        RaycastTripwireInDirection(closingVelocity, closingVelocity, out raycastHitDistance, out closingSpeed, -perp1) ||
        RaycastTripwireInDirection(closingVelocity, closingVelocity, out raycastHitDistance, out closingSpeed, perp2) ||
        RaycastTripwireInDirection(closingVelocity, closingVelocity, out raycastHitDistance, out closingSpeed, -perp2))
    {
        Detonate((raycastHitDistance - _raycastRange) / closingSpeed);
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

bool RaycastTripwireInDirection(Vector3D directionToTarget, Vector3D closingVelocity, out double raycastHitDistance, out double closingSpeed, Vector3D? offsetVector = null)
{
    raycastHitDistance = 0;
    closingSpeed = 0;

    var directionToTargetNorm = VectorMath.SafeNormalize(directionToTarget);
    foreach (var cameraBuffer in _cameraBufferList)
    {
        if (cameraBuffer.Count == 0)
        {
            continue;
        }

        Vector3D localDirToTgt = Vector3D.TransformNormal(directionToTargetNorm, MatrixD.Transpose(cameraBuffer.Peek().WorldMatrix));
        if (localDirToTgt.Z < 0 &&
            Math.Abs(localDirToTgt.X) < .7071 &&
            Math.Abs(localDirToTgt.Y) < .7071)
        {
            IMyCameraBlock cam = cameraBuffer.MoveNext();
            if (!cam.EnableRaycast)
            {
                cam.EnableRaycast = true;
            }

            if (!cam.Enabled)
            {
                cam.Enabled = true;
            }

            closingSpeed = Math.Max(0, Vector3D.Dot(closingVelocity, directionToTargetNorm));
            var closingDisplacement = closingSpeed * SecondsPerUpdate;
            var scanRange = _raycastRange + closingDisplacement;
            var scanPosition = cam.GetPosition() + directionToTargetNorm * scanRange;
            if (offsetVector.HasValue)
            {
                scanPosition += offsetVector.Value;
            }
            MyDetectedEntityInfo targetInfo = cam.Raycast(scanPosition);
            bool valid = IsValidTarget(targetInfo);
            if (valid)
            {
                raycastHitDistance = Vector3D.Distance(targetInfo.HitPosition.Value, cam.GetPosition());
            }
            return valid;
        }
    }

    return false;
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

double CalculateMissileThrust(List<IMyThrust> mainThrusters)
{
    double thrust = 0;
    foreach (var block in mainThrusters)
    {
        if (block.Closed)
            continue;
        thrust += block.IsFunctional ? block.MaxEffectiveThrust : 0;
    }
    return thrust;
}
#endregion

#region Vector Math Functions

// Computes optimal drift compensation vector to eliminate drift in a specified time
static Vector3D CalculateDriftCompensation(Vector3D velocity, Vector3D directHeading, double accel, double timeConstant, Vector3D gravityVec, double maxDriftAngle = 60)
{
    if (directHeading.LengthSquared() == 0)
        return velocity;

    if (Vector3D.Dot(velocity, directHeading) < 0)
        return directHeading;

    if (velocity.LengthSquared() < 100)
        return directHeading;

    var normalVelocity = VectorMath.Rejection(velocity, directHeading);
    var normal = VectorMath.SafeNormalize(normalVelocity);
    var parallel = VectorMath.SafeNormalize(directHeading);

    var normalAccel = Vector3D.Dot(normal, normalVelocity) / timeConstant;
    normalAccel = Math.Min(normalAccel, accel * Math.Sin(MathHelper.ToRadians(maxDriftAngle)));

    var normalAccelerationVector = normalAccel * normal;

    double parallelAccel = 0;
    var diff = accel * accel - normalAccelerationVector.LengthSquared();
    if (diff > 0)
        parallelAccel = Math.Sqrt(diff);

    return parallelAccel * parallel - normal * normalAccel;
}

void ComputeRandomHeadingVector()
{
    if (!_enableEvasion || !_evadeWithRandomizedHeading || !_enableGuidance || _missileReference == null)
    {
        return;
    }

    double angle = RNGesus.NextDouble() * Math.PI * 2.0;
    _randomizedHeadingVector = Math.Sin(angle) * _missileReference.WorldMatrix.Up + Math.Cos(angle) * _missileReference.WorldMatrix.Right;
    _randomizedHeadingVector *= _maxRandomAccelRatio;
}

Vector3D ComputeRandomDispersion()
{
    Vector3D direction = new Vector3D(2 * _bellCurveRandom.NextDouble() - 1,
                                      2 * _bellCurveRandom.NextDouble() - 1,
                                      2 * _bellCurveRandom.NextDouble() - 1);
    return _maxAimDispersion * direction;
}

//Whip's Spiral Trajectory Method v2
double _timeSpiral = 0;
Vector3D SpiralTrajectory(Vector3D desiredForwardVector, Vector3D desiredUpVector)
{
    if (_timeSpiral > _timeMaxSpiral)
        _timeSpiral = 0;

    double angle = 2 * Math.PI * _timeSpiral / _timeMaxSpiral;

    Vector3D forward = VectorMath.SafeNormalize(desiredForwardVector);
    Vector3D right = VectorMath.SafeNormalize(Vector3D.Cross(forward, desiredUpVector));
    Vector3D up = Vector3D.Cross(right, forward);

    double lateralProportion = Math.Sin(_spiralDegrees * DegToRad);
    double forwardProportion = Math.Sqrt(1 - lateralProportion * lateralProportion);

    return forward * forwardProportion + lateralProportion * (Math.Sin(angle) * up + Math.Cos(angle) * right);
}


Vector3D[] _corners = new Vector3D[8];
double CalculateGridRadiusFromAxis(IMyCubeGrid grid, Vector3D axis)
{
    var axisLocal = VectorMath.SafeNormalize(Vector3D.Rotate(axis, MatrixD.Transpose(grid.WorldMatrix)));
    var min = ((Vector3D)grid.Min - 0.5) * grid.GridSize;
    var max = ((Vector3D)grid.Max + 0.5) * grid.GridSize;
    var bb = new BoundingBoxD(min, max);
    bb.GetCorners(_corners);

    double maxLenSq = 0;
    var point = _corners[0];
    foreach (var corner in _corners)
    {
        var dirn = corner - bb.Center;
        var rej = dirn - dirn.Dot(axisLocal) * axisLocal;
        var lenSq = rej.LengthSquared();
        if (lenSq > maxLenSq)
        {
            point = corner;
            maxLenSq = lenSq;
        }
    }
    return Math.Sqrt(maxLenSq);
}
#endregion

#region Storage Parsing/Saving
void ParseStorage() //TODO: Add proper ini save/parse for Active guidance
{
    if (GridTerminalSystem.GetBlockGroupWithName(_fireControlGroupNameTag) != null)
    {
        // This means missile is still attached to firing ship
        Storage = "";
        return;
    }

    var storageSplit = Storage.Split('\n');
    foreach (var line in storageSplit)
    {
        if (line.StartsWith("@"))
        {
            var trimmedLine = line.Replace("@", "");
            bool parsed = int.TryParse(trimmedLine, out _missileStage);
            if (parsed && _missileStage == 0)
            {
                _savedKeycodes.Clear();
                return;
            }
            continue;
        }

        long code = 0;
        if (!string.IsNullOrWhiteSpace(line) && long.TryParse(line, out code))
            _savedKeycodes.Add(code);
    }

    if (_savedKeycodes.Count != 0 && _missileStage != 0)
    {
        _postSetupAction = PostSetupAction.Fire;
        InitiateSetup(true);
    }

    Echo("Storage parsed");
}

void Save()
{
    _saveSB.Clear();
    foreach (var id in _savedKeycodes)
        _saveSB.Append($"{id}\n");

    _saveSB.Append($"@{_missileStage}");

    Storage = _saveSB.ToString();
}
#endregion
#endregion

#region INCLUDES

enum TargetRelation : byte { Neutral = 0, Other = 0, Enemy = 1, Friendly = 2, Locked = 4, LargeGrid = 8, SmallGrid = 16, Missile = 32, Asteroid = 64, RelationMask = Neutral | Enemy | Friendly, TypeMask = LargeGrid | SmallGrid | Other | Missile | Asteroid }

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

/// Whip's GetRotationAnglesSimultaneous - Last modified: 2022/08/10
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
///     Set to <c>Vector3D.Zero</c> if roll control is not desired.
/// </param>
/// <param name="worldMatrix">
///     World matrix describing current orientation.
///     The translation part of the matrix is ignored; only the orientation matters.
/// </param>
/// <param name="pitch">Pitch angle to desired orientation (rads).</param>
/// <param name="yaw">Yaw angle to desired orientation (rads).</param>
/// <param name="roll">Roll angle to desired orientation (rads).</param>
public static void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double pitch, out double yaw, out double roll)
{
    desiredForwardVector = VectorMath.SafeNormalize(desiredForwardVector);

    MatrixD transposedWm;
    MatrixD.Transpose(ref worldMatrix, out transposedWm);
    Vector3D.Rotate(ref desiredForwardVector, ref transposedWm, out desiredForwardVector);
    Vector3D.Rotate(ref desiredUpVector, ref transposedWm, out desiredUpVector);

    Vector3D leftVector = Vector3D.Cross(desiredUpVector, desiredForwardVector);
    Vector3D axis;
    double angle;
    
    if (Vector3D.IsZero(desiredUpVector) || Vector3D.IsZero(leftVector))
    {
        /*
         * Simple case where we have no valid roll constraint:
         * We merely cross the current forward vector (Vector3D.Forward) on the 
         * desiredForwardVector.
         */
        axis = new Vector3D(-desiredForwardVector.Y, desiredForwardVector.X, 0);
        angle = Math.Acos(MathHelper.Clamp(-desiredForwardVector.Z, -1.0, 1.0));
    }
    else
    {
        /*
         * Here we need to construct the target orientation matrix so that we
         * can extract the error from it in axis-angle representation.
         */
        leftVector = VectorMath.SafeNormalize(leftVector);
        Vector3D upVector = Vector3D.Cross(desiredForwardVector, leftVector);
        MatrixD targetOrientation = new MatrixD()
        {
            Forward = desiredForwardVector,
            Left = leftVector,
            Up = upVector,
        };

        axis = new Vector3D(targetOrientation.M32 - targetOrientation.M23,
                            targetOrientation.M13 - targetOrientation.M31,
                            targetOrientation.M21 - targetOrientation.M12);

        double trace = targetOrientation.M11 + targetOrientation.M22 + targetOrientation.M33;
        angle = Math.Acos(MathHelper.Clamp((trace - 1) * 0.5, -1.0, 1.0));
    }

    if (Vector3D.IsZero(axis))
    {
        /*
         * Degenerate case where we get a zero axis. This means we are either
         * exactly aligned or exactly anti-aligned. In the latter case, we just
         * assume the yaw is PI to get us away from the singularity.
         */
        angle = desiredForwardVector.Z < 0 ? 0 : Math.PI;
        yaw = angle;
        pitch = 0;
        roll = 0;
        return;
    }

    Vector3D axisAngle = VectorMath.SafeNormalize(axis) * angle;
    yaw = axisAngle.Y;
    pitch = axisAngle.X;
    roll = axisAngle.Z;
}

/*
Whip's ApplyGyroOverride - Last modified: 2020/08/27

Takes pitch, yaw, and roll speeds relative to the gyro's backwards
ass rotation axes. 
*/
void ApplyGyroOverride(double pitchSpeed, double yawSpeed, double rollSpeed, List<IMyGyro> gyroList, MatrixD worldMatrix)
{
    var rotationVec = new Vector3D(pitchSpeed, yawSpeed, rollSpeed);
    var relativeRotationVec = Vector3D.TransformNormal(rotationVec, worldMatrix);

    foreach (var thisGyro in gyroList)
    {
        var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(thisGyro.WorldMatrix));

        thisGyro.Pitch = (float)transformedRotationVec.X;
        thisGyro.Yaw = (float)transformedRotationVec.Y;
        thisGyro.Roll = (float)transformedRotationVec.Z;
        thisGyro.GyroOverride = true;
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

#region Raycast Homing
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
    public double SearchScanSpread {get; set; } = 0;
    public Vector3D TargetCenter { get; private set; } = Vector3D.Zero;
    public Vector3D OffsetTargetPosition { get; private set; } = Vector3D.Zero;
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

    MyDetectedEntityInfo _info = default(MyDetectedEntityInfo);
    MatrixD _targetOrientation;
    Vector3D _targetPositionOverride;
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
        _targetPositionOverride = hitPosition;
        TargetCenter = hitPosition;
        HitPosition = hitPosition;
        OffsetTargetPosition = hitPosition;
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
        _info = default(MyDetectedEntityInfo);
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

    public void Update(double timeStep, List<IMyCameraBlock> cameraList, List<IMyShipController> shipControllers, IMyTerminalBlock referenceBlock = null)
    {
        _timeSinceLastScan += timeStep;

        if (!IsScanning)
            return;

        TimeSinceLastLock += timeStep;

        _info = default(MyDetectedEntityInfo);
        _availableCameras.Clear();

        //Check for lock lost
        if (TimeSinceLastLock > (MaxTimeForLockBreak + AutoScanInterval) && Status == TargetingStatus.Locked)
        {
            LockLost = true;
            ClearLockInternal();
            return;
        }

        // Determine where to scan next
        var scanPosition = Vector3D.Zero;
        switch (_currentAimMode)
        {
            case AimMode.Offset:
                scanPosition = HitPosition + TargetVelocity * TimeSinceLastLock;
                break;
            case AimMode.OffsetRelative:
                scanPosition = OffsetTargetPosition + TargetVelocity * TimeSinceLastLock;
                break;
            default:
                scanPosition = TargetCenter + TargetVelocity * TimeSinceLastLock;
                break;
        }

        if (MissedLastScan && cameraList.Count > 0)
        {
            scanPosition += CalculateFudgeVector(scanPosition - cameraList[0].GetPosition());
        }

        // Trim out cameras that cant see our next scan position
        Vector3D testDirection = Vector3D.Zero;
        IMyTerminalBlock reference = null;
        if (Status == TargetingStatus.Locked || _manualLockOverride)
        {
            GetAvailableCameras(cameraList, _availableCameras, scanPosition, true);
        }
        else
        {
            /*
             * The following prioritizes references in the following hierarchy:
             * 1. Currently used camera
             * 2. Reference block
             * 3. Currently used control seat
             */
            if (reference == null)
                reference = GetControlledCamera(cameraList);
            
            if (reference == null)
                reference = referenceBlock;

            if (reference == null)
                reference = GetControlledShipController(shipControllers);

            if (reference != null)
            {
                testDirection = reference.WorldMatrix.Forward;
                GetAvailableCameras(cameraList, _availableCameras, testDirection);
            }
            else
            {
                _availableCameras.AddRange(cameraList);
            }
        }

        // Check for transition between faces
        if (_availableCameras.Count == 0)
        {
            _timeSinceLastScan = 100000;
            MissedLastScan = true;
            return;
        }

        var camera = GetCameraWithMaxRange(_availableCameras);
        var cameraMatrix = camera.WorldMatrix;

        double scanRange;
        Vector3D adjustedTargetPos = Vector3D.Zero;
        if (Status == TargetingStatus.Locked || _manualLockOverride)
        {
            // We adjust the scan position to scan a bit past the target so we are more likely to hit if it is moving away
            adjustedTargetPos = scanPosition + Vector3D.Normalize(scanPosition - cameraMatrix.Translation) * 2 * TargetSize;
            scanRange = (adjustedTargetPos - cameraMatrix.Translation).Length();
        }
        else
        {
            scanRange = MaxRange;
        }

        AutoScanInterval = scanRange / (1000.0 * camera.RaycastTimeMultiplier) / _availableCameras.Count * AutoScanScaleFactor;

        //Attempt to scan adjusted target position
        if (camera.AvailableScanRange >= scanRange &&
            _timeSinceLastScan >= AutoScanInterval)
        {
            if (Status == TargetingStatus.Locked || _manualLockOverride)
                _info = camera.Raycast(adjustedTargetPos);
            else if (!Vector3D.IsZero(testDirection))
                _info = camera.Raycast(GetSearchPos(reference.GetPosition(), testDirection, camera));
            else
                _info = camera.Raycast(MaxRange);

            _timeSinceLastScan = 0;
        }
        else // Not enough charge stored up yet
        {
            return;
        }

        // Validate target and assign values
        if (!_info.IsEmpty() &&
            !_targetFilter.Contains(_info.Type) &&
            !_gridIDsToIgnore.Contains(_info.EntityId)) //target lock
        {
            if (Vector3D.DistanceSquared(_info.Position, camera.GetPosition()) < MinRange * MinRange && Status != TargetingStatus.Locked)
            {
                Status = TargetingStatus.TooClose;
                return;
            }
            else if (Status == TargetingStatus.Locked) // Target already locked
            {
                if (_info.EntityId == TargetId)
                {
                    TargetCenter = _info.Position;
                    HitPosition = _info.HitPosition.Value;

                    _targetOrientation = _info.Orientation;
                    OffsetTargetPosition = TargetCenter + Vector3D.TransformNormal(PreciseModeOffset, _targetOrientation);

                    TargetVelocity = _info.Velocity;
                    TargetSize = _info.BoundingBox.Size.Length();
                    TimeSinceLastLock = 0;

                    _manualLockOverride = false;
                    
                    MissedLastScan = false;
                    TargetRelation = _info.Relationship;
                    TargetType = _info.Type;
                }
                else
                {
                    MissedLastScan = true;
                }
            }
            else // Target not yet locked: initial lockon
            {
                if (_manualLockOverride && TargetId != _info.EntityId)
                    return;

                Status = TargetingStatus.Locked;
                TargetId = _info.EntityId;
                TargetCenter = _info.Position;
                HitPosition = _info.HitPosition.Value;
                TargetVelocity = _info.Velocity;
                TargetSize = _info.BoundingBox.Size.Length();
                TimeSinceLastLock = 0;

                var aimingCamera = GetControlledCamera(_availableCameras);
                Vector3D hitPosOffset = Vector3D.Zero;
                if (aimingCamera != null)
                {
                    hitPosOffset = aimingCamera.GetPosition() - camera.GetPosition();
                }
                else if (reference != null)
                {
                    hitPosOffset = reference.GetPosition() - camera.GetPosition();
                }
                if (!Vector3D.IsZero(hitPosOffset))
                {
                    hitPosOffset = VectorRejection(hitPosOffset, HitPosition - camera.GetPosition());
                }

                var hitPos = _info.HitPosition.Value + hitPosOffset;
                _targetOrientation = _info.Orientation;

                if (_manualLockOverride)
                {
                    _manualLockOverride = false;
                }
                else
                {
                    PreciseModeOffset = Vector3D.TransformNormal(hitPos - TargetCenter, MatrixD.Transpose(_targetOrientation));
                    OffsetTargetPosition = hitPos;
                }

                MissedLastScan = false;
                TargetRelation = _info.Relationship;
                TargetType = _info.Type;
            }
        }
        else
        {
            MissedLastScan = true;
        }

        if (MissedLastScan)
        {
            _currentAimMode = (AimMode)((int)(_currentAimMode + 1) % 3);
        }
    }

    void GetAvailableCameras(List<IMyCameraBlock> allCameras, List<IMyCameraBlock> availableCameras, Vector3D testVector, bool vectorIsPosition = false)
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

    IMyShipController GetControlledShipController(List<IMyShipController> controllers)
    {
        if (controllers.Count == 0)
            return null;

        IMyShipController mainController = null;
        IMyShipController controlled = null;

        foreach (var sc in controllers)
        {
            if (sc.IsUnderControl && sc.CanControlShip)
            {
                if (controlled == null)
                {
                    controlled = sc;
                }

                if (sc.IsMainCockpit)
                {
                    mainController = sc; // Only one per grid so no null check needed
                }
            }
        }

        if (mainController != null)
            return mainController;

        if (controlled != null)
            return controlled;

        return controllers[0];
    }

    public static Vector3D VectorRejection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }
}
#endregion

#region MissileGuidanceBase
abstract class MissileGuidanceBase
{
    protected double _deltaTime;
    protected double _updatesPerSecond;

    Vector3D? _lastVelocity;

    public MissileGuidanceBase(double updatesPerSecond)
    {
        _updatesPerSecond = updatesPerSecond;
        _deltaTime = 1.0 / _updatesPerSecond;
    }

    public void ClearAcceleration()
    {
        _lastVelocity = null;
    }

    public Vector3D Update(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D? gravity = null)
    {
        Vector3D targetAcceleration = Vector3D.Zero;
        if (_lastVelocity.HasValue)
            targetAcceleration = (targetVelocity - _lastVelocity.Value) * _updatesPerSecond;
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

    protected abstract Vector3D GetPointingVector(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration);
}

abstract class RelNavGuidance : MissileGuidanceBase
{
    public double NavConstant;
    public double NavAccelConstant;

    public RelNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond)
    {
        NavConstant = navConstant;
        NavAccelConstant = navAccelConstant;
    }

    abstract protected Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration);

    override protected Vector3D GetPointingVector(Vector3D missilePosition, Vector3D missileVelocity, double missileAcceleration, Vector3D targetPosition, Vector3D targetVelocity, Vector3D targetAcceleration)
    {
        Vector3D missileToTarget = targetPosition - missilePosition;
        Vector3D missileToTargetNorm = Vector3D.Normalize(missileToTarget);
        Vector3D relativeVelocity = targetVelocity - missileVelocity;
        Vector3D lateralTargetAcceleration = (targetAcceleration - Vector3D.Dot(targetAcceleration, missileToTargetNorm) * missileToTargetNorm);

        Vector3D lateralAcceleration = GetLatax(missileToTarget, missileToTargetNorm, relativeVelocity, lateralTargetAcceleration);

        if (Vector3D.IsZero(lateralAcceleration))
            return missileToTarget;

        double diff = missileAcceleration * missileAcceleration - lateralAcceleration.LengthSquared();
        if (diff < 0)
            return lateralAcceleration; //fly parallel to the target
        return lateralAcceleration + Math.Sqrt(diff) * missileToTargetNorm;
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

    override protected Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        Vector3D omega = Vector3D.Cross(missileToTarget, relativeVelocity) / Math.Max(missileToTarget.LengthSquared(), 1); //to combat instability at close range
        return NavConstant * relativeVelocity.Length() * Vector3D.Cross(omega, missileToTargetNorm)
             + NavAccelConstant * lateralTargetAcceleration;
    }
}

class WhipNavGuidance : RelNavGuidance
{
    public WhipNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond, navConstant, navAccelConstant) { }

    override protected Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        Vector3D parallelVelocity = relativeVelocity.Dot(missileToTargetNorm) * missileToTargetNorm; //bootleg vector projection
        Vector3D normalVelocity = (relativeVelocity - parallelVelocity);
        return NavConstant * 0.1 * normalVelocity
             + NavAccelConstant * lateralTargetAcceleration;
    }
}

class HybridNavGuidance : RelNavGuidance
{
    public HybridNavGuidance(double updatesPerSecond, double navConstant, double navAccelConstant = 0) : base(updatesPerSecond, navConstant, navAccelConstant) { }

    override protected Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        Vector3D omega = Vector3D.Cross(missileToTarget, relativeVelocity) / Math.Max(missileToTarget.LengthSquared(), 1); //to combat instability at close range
        Vector3D parallelVelocity = relativeVelocity.Dot(missileToTargetNorm) * missileToTargetNorm; //bootleg vector projection
        Vector3D normalVelocity = (relativeVelocity - parallelVelocity);
        return NavConstant * (relativeVelocity.Length() * Vector3D.Cross(omega, missileToTargetNorm) + 0.1 * normalVelocity)
             + NavAccelConstant * lateralTargetAcceleration;
    }
}

/// <summary>
/// Zero Effort Miss Intercept
/// Derived from: https://doi.org/10.2514/1.26948
/// </summary>
class ZeroEffortMissGuidance : RelNavGuidance
{
    public ZeroEffortMissGuidance(double updatesPerSecond, double navConstant) : base(updatesPerSecond, navConstant, 0) { }
    override protected Vector3D GetLatax(Vector3D missileToTarget, Vector3D missileToTargetNorm, Vector3D relativeVelocity, Vector3D lateralTargetAcceleration)
    {
        double distToTarget = Vector3D.Dot(missileToTarget, missileToTargetNorm);
        double closingSpeed = Vector3D.Dot(relativeVelocity, missileToTargetNorm);
        // Equation (8) with sign modification to keep time positive and not NaN
        double tau = distToTarget / Math.Max(1, Math.Abs(closingSpeed));
        // Equation (6)
        Vector3D z = missileToTarget + relativeVelocity * tau;
        // Equation (7)
        return NavConstant * z / (tau * tau)
             + NavAccelConstant * lateralTargetAcceleration;
    }
}
#endregion

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

    public ConfigValue(string section, string name, T defaultValue, string comment)
    {
        Section = section;
        Name = name;
        Value = defaultValue;
        DefaultValue = defaultValue;
        _comment = comment;
    }

    public override string ToString()
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
        ini.Set(Section, Name, this.ToString());
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

public class ConfigDouble : ConfigValue<double>
{
    public ConfigDouble(string section, string name, double value = 0, string comment = null) : base(section, name, value, comment) { }
    protected override void SetValue(ref MyIniValue val) { if (!val.TryGetDouble(out Value)) SetDefault(); }
}

public class ConfigBool : ConfigValue<bool>
{
    public ConfigBool(string section, string name, bool value = false, string comment = null) : base(section, name, value, comment) { }
    protected override void SetValue(ref MyIniValue val) { if (!val.TryGetBoolean(out Value)) SetDefault(); }
}

public class ConfigInt : ConfigValue<int>
{
    public ConfigInt(string section, string name, int value = 0, string comment = null) : base(section, name, value, comment) { }
    protected override void SetValue(ref MyIniValue val) { if (!val.TryGetInt32(out Value)) SetDefault(); }
}

public class ConfigEnum<TEnum> : ConfigValue<TEnum> where TEnum : struct
{
    public ConfigEnum(string section, string name, TEnum defaultValue = default(TEnum), string comment = null)
    : base (section, name, defaultValue, comment) 
    {}
    
    protected override void SetValue(ref MyIniValue val)
    {
        string antennaModeStr;
        if (!val.TryGetString(out antennaModeStr) || 
            !Enum.TryParse(antennaModeStr, true, out Value) ||
            !Enum.IsDefined(typeof(TEnum), Value))
        {
            SetDefault();
        }
    }
}
#endregion
