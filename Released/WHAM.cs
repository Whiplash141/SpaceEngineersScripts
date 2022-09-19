
#region WHAM
const string VERSION = "170.4.1";
const string DATE = "2022/09/17";
const string COMPAT_VERSION = "95.0.0";

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

int _guidanceAlgoIndex = 0;
bool _autoConfigure = true;
bool _debugAntennas = false; // If true, the antenna name will be set to the current missile status

MissileGuidanceBase _selectedGuidance;
ProNavGuidance _proNavGuid;
WhipNavGuidance _whipNavGuid;
HybridNavGuidance _hybridNavGuid;
ZeroEffortMissGuidance _zeroEffortMissGuid;

List<MissileGuidanceBase> _guidanceAlgorithms = new List<MissileGuidanceBase>();

const string MISSILE_NAME_PATTERN = "({0} {1})";
const string MISSILE_GROUP_PATTERN = "{0} {1}";

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
    _missileNameTag = "",
    _missileTag = "Missile",
    _fireControlGroupNameTag = "Fire Control",
    _detachThrustTag = "Detach";

double
    _disconnectDelay = 1,
    _guidanceDelay = 2,
    _detachDuration = 0,
    _mainIgnitionDelay = 0,
    _raycastRange = 2.5,
    _raycastMinimumTargetSize = 10,
    _spiralDegrees = 15,
    _timeMaxSpiral = 3,
    _spiralActivationRange = 1000,
    _gyroProportionalGain = 10,
    _gyroIntegralGain = 0,
    _gyroDerivativeGain = 10,
    _navConstant = 3,
    _accelNavConstant = 1.5,
    _offsetUp = 0,
    _offsetLeft = 0,
    _missileSpinRPM = 0,
    _minimumArmingRange = 100,
    _randomVectorInterval = 0.5,
    _maxRandomAccelRatio = 0.25,
    _maxAimDispersion = 0,
    _topDownAttackHeight = 1500,
    _timeSinceLastLock = 0,
    _distanceFromShooter = 0,
    _timeTotal = 0,
    _timeSinceLastIngest = 0;

int
    _missileNumber = 1,
    _setupTicks = 0,
    _missileStage = 0;

bool
    _useCamerasForHoming = true,
    _raycastIgnoreFriends = false,
    _raycastIgnorePlanetSurface = true,
    _ignoreIdForDetonation = false,
    _allowRemoteFire = false,
    _evadeWithRandomizedHeading = true,
    _evadeWithSpiral = false,
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
    _remotelyFired = false,
    _topDownAttack = false,
    _enableEvasion = false,
    _precisionMode = false,
    _retask = false;

#region Meme Mode Stuff
bool _antennaMemeMode = true;
string _antennaName = "";
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
    UPDATES_PER_SECOND = 10.0,
    SECONDS_PER_UPDATE = 1.0 / UPDATES_PER_SECOND,
    DEG_TO_RAD = Math.PI / 180,
    RPM_TO_RAD = Math.PI / 30,
    TOPDOWN_DESCENT_ANGLE = Math.PI / 6,
    MAX_GUIDANCE_TIME = 180,
    RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666,
    GYRO_SLOWDOWN_ANGLE = Math.PI / 36;

const float MIN_THRUST = 1e-9f;

readonly MyIni _guidanceIni = new MyIni();
readonly StringBuilder _saveSB = new StringBuilder();
readonly RaycastHoming _raycastHoming;

enum GuidanceMode : int { BeamRiding = 1, SemiActive = 2, Active = 4, Homing = SemiActive | Active };

PID _yawPID = new PID(1, 0, 0, SECONDS_PER_UPDATE),
    _pitchPID = new PID(1, 0, 0, SECONDS_PER_UPDATE);
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
    IGC_TAG_PARAMS = "IGC_MSL_PAR_MSG",
    IGC_TAG_HOMING = "IGC_MSL_HOM_MSG",
    IGC_TAG_BEAM_RIDING = "IGC_MSL_OPT_MSG",
    IGC_TAG_IFF = "IGC_IFF_PKT",
    IGC_TAG_FIRE = "IGC_MSL_FIRE_MSG",
    IGC_TAG_REMOTE_FIRE_REQUEST = "IGC_MSL_REM_REQ",
    IGC_TAG_REMOTE_FIRE_RESPONSE = "IGC_MSL_REM_RSP",
    IGC_TAG_REMOTE_FIRE_NOTIFICATION = "IGC_MSL_REM_NTF",
    IGC_TAG_REGISTER = "IGC_MSL_REG_MSG",
    UNICAST_TAG = "UNICAST";

#region Storage Ini
const string
    INI_PARAMETER_SECTION_NAME = "params",
    INI_KEYCODE_SECTION_NAME = "key",
    INI_OPTICAL_SECTION_NAME = "op",
    INI_SEMIACTIVE_SECTION_NAME = "sa",
    INI_KEYCODE_CODE_NAME = "kc_c",
    INI_OPTICAL_SHOOTER_POS_NAME = "op_sp",
    INI_OPTICAL_FRONT_VEC_NAME = "op_fv",
    INI_OPTICAL_LEFT_VEC_NAME = "op_lv",
    INI_OPTICAL_UP_VEC_NAME = "op_uv",
    INI_SEMIACTIVE_TARGET_POS_NAME = "sa_tp",
    INI_SEMIACTIVE_HIT_POS_NAME = "sa_hp",
    INI_SEMIACTIVE_TARGET_VEL_NAME = "sa_tv",
    INI_SEMIACTIVE_SHOOTER_POS_NAME = "sa_sp",
    INI_SEMIACTIVE_TIME_SINCE_LOCK_NAME = "sa_t",
    INI_PARAMETER_KILL_NAME = "kill",
    INI_PARAMETER_STEALTH_NAME = "stealth",
    INI_PARAMETER_SPIRAL_NAME = "spiral",
    INI_PARAMETER_TOPDOWN_NAME = "topdown";
#endregion

#region Custom Data Ini
readonly MyIni _myIni = new MyIni();

const string
    INI_SECTION_NAME = "Names",
    INI_NAME_AUTO_SETUP = "Auto-configure missile name",
    INI_NAME_TAG = "Missile name tag",
    INI_NAME_NUM = "Missile number",
    INI_NAME_FIRE_CTRL = "Fire control group name",
    INI_NAME_DETACH = "Detach thruster name tag",

    INI_SECTION_DELAY = "Delays",
    INI_DELAY_GUIDANCE = "Guidance delay (s)",
    INI_DELAY_DISCONNECT = "Stage 1: Disconnect delay (s)",
    INI_DELAY_DETACH = "Stage 2: Detach duration (s)",
    INI_DELAY_MAIN_IGITION = "Stage 3: Main ignition delay (s)",

    INI_SECTION_GYRO = "Gyros",
    INI_GYRO_KP = "Proportional gain",
    INI_GYRO_KI = "Integral gain",
    INI_GYRO_KD = "Derivative gain",

    INI_SECTION_HOMING = "Homing Parameters",
    INI_HOMING_RELNAV = "Navigation constant",
    INI_HOMING_RELNAV_ACCEL = "Acceleration constant",
    INI_HOMING_AIM_DISPERSION = "Max aim dispersion (m)",
    INI_TOPDOWN_ATTACK_HEIGHT = "Topdown attack height (m)",

    INI_SECTION_BEAMRIDE = "Beam Riding Parameters",
    INI_BEAMRIDE_OFFSET_UP = "Hit offset up (m)",
    INI_BEAMRIDE_OFFSET_LEFT = "Hit offset left (m)",

    INI_SECTION_EVASION = "Evasion Parameters",
    INI_EVASION_SPIN_RPM = "Spin rate (RPM)",
    INI_EVASION_USE_SPIRAL = "Use spiral",
    INI_EVASION_USE_RANDOM = "Use random flight path",
    INI_COMMENT_EVASION_USE_RANDOM = " AKA \"Drunken Missile Mode\"",

    INI_SECTION_SPIRAL = "Spiral Parameters",
    INI_SPIRAL_DEG = "Spiral angle (deg)",
    INI_SPIRAL_TIME = "Spiral time (sec)",
    INI_SPIRAL_RANGE = "Spiral activation range (m)",

    INI_SECTION_RANDOM = "Random Fligh Path Parameters",
    INI_RANDOM_INTERVAL = "Direction change interval (sec)",
    INI_RANDOM_MAX_ACCEL = "Max acceleration ratio",

    INI_SECTION_RAYCAST = "Raycast/Sensors",
    INI_RAYCAST_CAMS_FOR_HOMING = "Use cameras for homing",
    INI_RAYCAST_RANGE = "Tripwire range (m)",
    INI_RAYCAST_MIN_TGT_SIZE = "Minimum target size (m)",
    INI_RAYCAST_MIN_RANGE = "Minimum warhead arming range (m)",
    INI_RAYCAST_FRIENDS = "Ignore friendlies",
    INI_RAYCAST_IGNORE_PLANETS = "Ignore planets",
    INI_RAYCAST_IGNORE_ID_DETONATION = "Ignore target ID for detonation",

    INI_SECTION_MISC = "Misc.",
    INI_REMOTE_FIRE = "Allow remote firing",
    INI_MEME_MODE = "Antenna meme mode";
#endregion

QueuedAction
    _stage1Action,
    _stage2Action,
    _stage3Action,
    _stage4Action;

ScheduledAction
    _guidanceActivateAction,
    _randomHeadingVectorAction;
#endregion

#region Main Methods
Program()
{
    _unicastListener = IGC.UnicastListener;
    _unicastListener.SetMessageCallback(UNICAST_TAG);

    _broadcastListenerRemoteFire = IGC.RegisterBroadcastListener(IGC_TAG_REMOTE_FIRE_REQUEST);
    _broadcastListenerRemoteFire.SetMessageCallback(IGC_TAG_REMOTE_FIRE_REQUEST);

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
    _scheduler.AddScheduledAction(GuidanceNavAndControl, UPDATES_PER_SECOND);
    _scheduler.AddScheduledAction(CheckProximity, UPDATES_PER_SECOND);
    _scheduler.AddScheduledAction(PrintEcho, 1);
    _scheduler.AddScheduledAction(NetworkTargets, 6);
    _scheduler.AddScheduledAction(_randomHeadingVectorAction);

    // Setting up sequential tasks
    _scheduler.AddQueuedAction(_stage1Action);
    _scheduler.AddQueuedAction(_stage2Action);
    _scheduler.AddQueuedAction(_stage3Action);
    _scheduler.AddQueuedAction(_stage4Action);
    _scheduler.AddQueuedAction(KillPower, MAX_GUIDANCE_TIME);

    _runtimeTracker = new RuntimeTracker(this, 120, 0.005);

    _raycastHoming = new RaycastHoming(5000, 3, 0, Me.CubeGrid.EntityId);
    _raycastHoming.AddEntityTypeToFilter(MyDetectedEntityType.FloatingObject, MyDetectedEntityType.Planet, MyDetectedEntityType.Asteroid);

    // Init guidance
    _proNavGuid = new ProNavGuidance(UPDATES_PER_SECOND, _navConstant);
    _whipNavGuid = new WhipNavGuidance(UPDATES_PER_SECOND, _navConstant);
    _hybridNavGuid = new HybridNavGuidance(UPDATES_PER_SECOND, _navConstant);
    _zeroEffortMissGuid = new ZeroEffortMissGuidance(UPDATES_PER_SECOND, _navConstant);

    // Populate guidance algo list
    _guidanceAlgorithms.Add(_proNavGuid);
    _guidanceAlgorithms.Add(_whipNavGuid);
    _guidanceAlgorithms.Add(_hybridNavGuid);
    _guidanceAlgorithms.Add(_zeroEffortMissGuid);

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
        if (arg.Equals(UNICAST_TAG))
        {
            ParseUnicastMessages();
        }
        else if (arg.Equals(IGC_TAG_REMOTE_FIRE_REQUEST) && _allowRemoteFire && !_shouldFire)
        {
            ParseRemoteFireRequest();
        }
    }

    if (!_shouldFire)
        return;

    if (igcMsg && (arg.Equals(IGC_TAG_PARAMS) || arg.Equals(IGC_TAG_HOMING) || arg.Equals(IGC_TAG_BEAM_RIDING)))
    {
        HandleBroadcastListeners();
    }

    _scheduler.Update();

    var lastRuntime = Math.Min(RUNTIME_TO_REALTIME * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0), SECONDS_PER_UPDATE);
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

    _raycastHoming.Update(SECONDS_PER_UPDATE, _homingCameras, _shipControllers);

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

    _autoConfigure = _myIni.Get(INI_SECTION_NAME, INI_NAME_AUTO_SETUP).ToBoolean(_autoConfigure);
    _missileTag = _myIni.Get(INI_SECTION_NAME, INI_NAME_TAG).ToString(_missileTag);
    _missileNumber = _myIni.Get(INI_SECTION_NAME, INI_NAME_NUM).ToInt32(_missileNumber);

    _fireControlGroupNameTag = _myIni.Get(INI_SECTION_NAME, INI_NAME_FIRE_CTRL).ToString(_fireControlGroupNameTag);
    _detachThrustTag = _myIni.Get(INI_SECTION_NAME, INI_NAME_DETACH).ToString(_detachThrustTag);

    _disconnectDelay = _myIni.Get(INI_SECTION_DELAY, INI_DELAY_DISCONNECT).ToDouble(_disconnectDelay);
    _guidanceDelay = _myIni.Get(INI_SECTION_DELAY, INI_DELAY_GUIDANCE).ToDouble(_guidanceDelay);
    _detachDuration = _myIni.Get(INI_SECTION_DELAY, INI_DELAY_DETACH).ToDouble(_detachDuration);
    _mainIgnitionDelay = _myIni.Get(INI_SECTION_DELAY, INI_DELAY_MAIN_IGITION).ToDouble(_mainIgnitionDelay);

    _gyroProportionalGain = _myIni.Get(INI_SECTION_GYRO, INI_GYRO_KP).ToDouble(_gyroProportionalGain);
    _gyroIntegralGain = _myIni.Get(INI_SECTION_GYRO, INI_GYRO_KI).ToDouble(_gyroIntegralGain);
    _gyroDerivativeGain = _myIni.Get(INI_SECTION_GYRO, INI_GYRO_KD).ToDouble(_gyroDerivativeGain);

    _navConstant = _myIni.Get(INI_SECTION_HOMING, INI_HOMING_RELNAV).ToDouble(_navConstant);
    _accelNavConstant = _myIni.Get(INI_SECTION_HOMING, INI_HOMING_RELNAV_ACCEL).ToDouble(_accelNavConstant);
    _maxAimDispersion = _myIni.Get(INI_SECTION_HOMING, INI_HOMING_AIM_DISPERSION).ToDouble(_maxAimDispersion);
    _topDownAttackHeight = _myIni.Get(INI_SECTION_HOMING, INI_TOPDOWN_ATTACK_HEIGHT).ToDouble(_topDownAttackHeight);
    _topDownAttackHeight = Math.Max(0, _topDownAttackHeight);

    _offsetUp = _myIni.Get(INI_SECTION_BEAMRIDE, INI_BEAMRIDE_OFFSET_UP).ToDouble(_offsetUp);
    _offsetLeft = _myIni.Get(INI_SECTION_BEAMRIDE, INI_BEAMRIDE_OFFSET_LEFT).ToDouble(_offsetLeft);

    _missileSpinRPM = _myIni.Get(INI_SECTION_EVASION, INI_EVASION_SPIN_RPM).ToDouble(_missileSpinRPM);
    _evadeWithSpiral = _myIni.Get(INI_SECTION_EVASION, INI_EVASION_USE_SPIRAL).ToBoolean(_evadeWithSpiral);
    _evadeWithRandomizedHeading = _myIni.Get(INI_SECTION_EVASION, INI_EVASION_USE_RANDOM).ToBoolean(_evadeWithRandomizedHeading);

    _spiralDegrees = _myIni.Get(INI_SECTION_SPIRAL, INI_SPIRAL_DEG).ToDouble(_spiralDegrees);
    _timeMaxSpiral = _myIni.Get(INI_SECTION_SPIRAL, INI_SPIRAL_TIME).ToDouble(_timeMaxSpiral);
    _spiralActivationRange = _myIni.Get(INI_SECTION_SPIRAL, INI_SPIRAL_RANGE).ToDouble(_spiralActivationRange);

    _randomVectorInterval = _myIni.Get(INI_SECTION_RANDOM, INI_RANDOM_INTERVAL).ToDouble(_randomVectorInterval);
    _maxRandomAccelRatio = _myIni.Get(INI_SECTION_RANDOM, INI_RANDOM_MAX_ACCEL).ToDouble(_maxRandomAccelRatio);

    _useCamerasForHoming = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_CAMS_FOR_HOMING).ToBoolean(_useCamerasForHoming);
    _raycastRange = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_RANGE).ToDouble(_raycastRange);
    _raycastMinimumTargetSize = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_MIN_TGT_SIZE).ToDouble(_raycastMinimumTargetSize);
    _minimumArmingRange = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_MIN_RANGE).ToDouble(_minimumArmingRange);
    _raycastIgnoreFriends = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_FRIENDS).ToBoolean(_raycastIgnoreFriends);
    _raycastIgnorePlanetSurface = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_IGNORE_PLANETS).ToBoolean(_raycastIgnorePlanetSurface);
    _ignoreIdForDetonation = _myIni.Get(INI_SECTION_RAYCAST, INI_RAYCAST_IGNORE_ID_DETONATION).ToBoolean(_ignoreIdForDetonation);

    _allowRemoteFire = _myIni.Get(INI_SECTION_MISC, INI_REMOTE_FIRE).ToBoolean(_allowRemoteFire);
    _antennaMemeMode = _myIni.Get(INI_SECTION_MISC, INI_MEME_MODE).ToBoolean(_antennaMemeMode);

    _setupBuilder.Append("Loaded missile config!\n");
}

void SaveIniConfig()
{
    _myIni.Clear();

    _missileGroupNameTag = string.Format(MISSILE_GROUP_PATTERN, _missileTag, _missileNumber);
    _missileNameTag = string.Format(MISSILE_NAME_PATTERN, _missileTag, _missileNumber);

    _myIni.Set(INI_SECTION_NAME, INI_NAME_AUTO_SETUP, _autoConfigure);
    _myIni.Set(INI_SECTION_NAME, INI_NAME_TAG, _missileTag);
    _myIni.Set(INI_SECTION_NAME, INI_NAME_NUM, _missileNumber);
    _myIni.Set(INI_SECTION_NAME, INI_NAME_FIRE_CTRL, _fireControlGroupNameTag);
    _myIni.Set(INI_SECTION_NAME, INI_NAME_DETACH, _detachThrustTag);

    _myIni.Set(INI_SECTION_DELAY, INI_DELAY_GUIDANCE, _guidanceDelay);
    _myIni.Set(INI_SECTION_DELAY, INI_DELAY_DISCONNECT, _disconnectDelay);
    _myIni.Set(INI_SECTION_DELAY, INI_DELAY_DETACH, _detachDuration);
    _myIni.Set(INI_SECTION_DELAY, INI_DELAY_MAIN_IGITION, _mainIgnitionDelay);

    _myIni.Set(INI_SECTION_GYRO, INI_GYRO_KP, _gyroProportionalGain);
    _myIni.Set(INI_SECTION_GYRO, INI_GYRO_KI, _gyroIntegralGain);
    _myIni.Set(INI_SECTION_GYRO, INI_GYRO_KD, _gyroDerivativeGain);

    _myIni.Set(INI_SECTION_HOMING, INI_HOMING_RELNAV, _navConstant);
    _myIni.Set(INI_SECTION_HOMING, INI_HOMING_RELNAV_ACCEL, _accelNavConstant);
    _myIni.Set(INI_SECTION_HOMING, INI_HOMING_AIM_DISPERSION, _maxAimDispersion);
    _myIni.Set(INI_SECTION_HOMING, INI_TOPDOWN_ATTACK_HEIGHT, _topDownAttackHeight);

    _myIni.Set(INI_SECTION_BEAMRIDE, INI_BEAMRIDE_OFFSET_UP, _offsetUp);
    _myIni.Set(INI_SECTION_BEAMRIDE, INI_BEAMRIDE_OFFSET_LEFT, _offsetLeft);

    _myIni.Set(INI_SECTION_EVASION, INI_EVASION_SPIN_RPM, _missileSpinRPM);
    _myIni.Set(INI_SECTION_EVASION, INI_EVASION_USE_SPIRAL, _evadeWithSpiral);
    _myIni.Set(INI_SECTION_EVASION, INI_EVASION_USE_RANDOM, _evadeWithRandomizedHeading);
    _myIni.SetComment(INI_SECTION_EVASION, INI_EVASION_USE_RANDOM, INI_COMMENT_EVASION_USE_RANDOM);

    _myIni.Set(INI_SECTION_SPIRAL, INI_SPIRAL_DEG, _spiralDegrees);
    _timeMaxSpiral = Math.Max(_timeMaxSpiral, 0.1);
    _myIni.Set(INI_SECTION_SPIRAL, INI_SPIRAL_TIME, _timeMaxSpiral);
    _myIni.Set(INI_SECTION_SPIRAL, INI_SPIRAL_RANGE, _spiralActivationRange);

    _myIni.Set(INI_SECTION_RANDOM, INI_RANDOM_INTERVAL, _randomVectorInterval);
    _maxRandomAccelRatio = MathHelper.Clamp(_maxRandomAccelRatio, 0, 1);
    _myIni.Set(INI_SECTION_RANDOM, INI_RANDOM_MAX_ACCEL, _maxRandomAccelRatio);

    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_CAMS_FOR_HOMING, _useCamerasForHoming);
    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_RANGE, _raycastRange);
    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_MIN_TGT_SIZE, _raycastMinimumTargetSize);
    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_MIN_RANGE, _minimumArmingRange);
    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_FRIENDS, _raycastIgnoreFriends);
    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_IGNORE_PLANETS, _raycastIgnorePlanetSurface);
    _myIni.Set(INI_SECTION_RAYCAST, INI_RAYCAST_IGNORE_ID_DETONATION, _ignoreIdForDetonation);

    _myIni.Set(INI_SECTION_MISC, INI_REMOTE_FIRE, _allowRemoteFire);
    _myIni.Set(INI_SECTION_MISC, INI_MEME_MODE, _antennaMemeMode);

    _guidanceActivateAction.RunInterval = _guidanceDelay;
    _stage2Action.RunInterval = _disconnectDelay;
    _stage3Action.RunInterval = _detachDuration;
    _stage4Action.RunInterval = _mainIgnitionDelay;
    _randomHeadingVectorAction.RunInterval = _randomVectorInterval;

    Me.CustomData = _myIni.ToString();
}
#endregion

#region Argument Handling and IGC Processing
void ParseUnicastMessages()
{
    bool fireCommanded = false;

    // Process message queue
    while (_unicastListener.HasPendingMessage)
    {
        MyIGCMessage message = _unicastListener.AcceptMessage();
        object data = message.Data;
        if (message.Tag == IGC_TAG_FIRE)
        {
            fireCommanded = true;
        }
        else if (message.Tag == IGC_TAG_REGISTER)
        {
            if (data is long)
            {
                long keycode = (long)data;
                _savedKeycodes.Add(keycode);
                _remotelyFired = true;
            }
        }
    }

    if (fireCommanded)
    {
        _postSetupAction = PostSetupAction.Fire;
        InitiateSetup(_remotelyFired);
        if (_remotelyFired)
        {
            IGC.SendBroadcastMessage(IGC_TAG_REMOTE_FIRE_NOTIFICATION, _missileNumber, TransmissionDistance.CurrentConstruct);
        }
    }
}

void ParseRemoteFireRequest()
{
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
    antennaRange = (float)Math.Sqrt(antennaRange);

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

        IGC.SendUnicastMessage(programId, IGC_TAG_REMOTE_FIRE_RESPONSE, response);
    }

    _remoteFireRequests.Clear();

    foreach (var a in _antennas)
    {
        if (a.Closed)
            continue;
        a.EnableBroadcasting = false;
        break;
    }
}

void HandleBroadcastListeners()
{
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

void RegisterBroadcastListeners()
{
    if (_broadcastListenersRegistered)
        return;

    _broadcastListenerHoming = IGC.RegisterBroadcastListener(IGC_TAG_HOMING);
    _broadcastListenerHoming.SetMessageCallback(IGC_TAG_HOMING);

    _broadcastListenerBeamRiding = IGC.RegisterBroadcastListener(IGC_TAG_BEAM_RIDING);
    _broadcastListenerBeamRiding.SetMessageCallback(IGC_TAG_BEAM_RIDING);

    _broadcastListenerParameters = IGC.RegisterBroadcastListener(IGC_TAG_PARAMS);
    _broadcastListenerParameters.SetMessageCallback(IGC_TAG_PARAMS);

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
    return $"Whip's Homing Adv. Missile Script\n(Version {VERSION} - {DATE})\n\nFor use with LAMP v{COMPAT_VERSION} or later.\n";
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
                int spaceIndex = groupName.IndexOf(' ', _missileTag.Length);
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
            _missileNumber = _autoConfigureMissileNumber;
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
        var relNav = guid as RelNavGuidance;
        if (relNav != null)
        {
            relNav.NavConstant = _navConstant;
            relNav.NavAccelConstant = _accelNavConstant;
        }
    }
    _selectedGuidance = _guidanceAlgorithms[_guidanceAlgoIndex];

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

    #region Set Meme Mode name
    if (_antennaMemeMode)
    {
        int index = RNGesus.Next(_antennaMemeMessages.Length);
        _antennaName = _antennaMemeMessages[index];
    }
    else
    {
        _antennaName = "";
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
                _setupBuilder.Append($"\n>>> Setup Failed! <<<\n");
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
        _setupBuilder.Append($"\n>>> Setup Failed! <<<\n");
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
    if (!setupFailed)
    {
        GetThrusterOrientation(_shipControllers[0]);
    }
    setupFailed |= EchoIfTrue(_mainThrusters.Count == 0, ">> ERR: No main thrusters found");
    setupFailed |= EchoIfTrue(_batteries.Count == 0 && _reactors.Count == 0, ">> ERR: No batteries or reactors found");

    // WARNINGS
    if(!EchoIfTrue(_mergeBlocks.Count == 0 && _rotors.Count == 0 && _connectors.Count == 0, "> WARN: No merge blocks, rotors, or connectors found for detaching"))
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

    ApplyThrustOverride(_sideThrusters, MIN_THRUST, false);
    ApplyThrustOverride(_detachThrusters, 100f);
}

// Disables missile thrust for drifting.
void MissileStage3()
{
    _missileStage = 3;

    ApplyThrustOverride(_detachThrusters, MIN_THRUST);
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

    ApplyThrustOverride(_detachThrusters, MIN_THRUST);
    ApplyThrustOverride(_sideThrusters, MIN_THRUST);
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

    Vector3D headingVec = GuidanceMain(
        _guidanceMode,
        missileMatrix,
        missilePos,
        missileVel,
        gravityVec,
        missileAccel,
        pastArmingRange,
        shouldSpiral,
        out _shouldProximityScan);

    Control(missileMatrix, headingVec, gravityVec, missileVel, missileMass);
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
    Vector3D headingVec;
    if (guidanceMode == GuidanceMode.BeamRiding)
    {
        headingVec = BeamRideGuidance(
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

        headingVec = HomingGuidance(
            missilePos,
            missileVel,
            gravity,
            missileAcceleration,
            out adjustedTargetPos);

        double distanceToTgtSq = Vector3D.DistanceSquared(missilePos, adjustedTargetPos);
        double closingSpeedSq = (missileVel - _targetVel).LengthSquared();
        shouldProximityScan = pastMinArmingRange && (closingSpeedSq > distanceToTgtSq); // Roughly 1 second till impact

        // Only spiral if we are close enough to the target to conserve fuel
        if (shouldSpiral)
        {
            shouldSpiral = ((_spiralActivationRange * _spiralActivationRange > distanceToTgtSq) && (closingSpeedSq * 4.0 < distanceToTgtSq)); // TODO: Don't hard code this lol
        }
    }

    if (shouldSpiral)
    {
        headingVec = missileAcceleration * SpiralTrajectory(headingVec, _missileReference.WorldMatrix.Up);
    }

    if (_enableEvasion && _evadeWithRandomizedHeading)
    {
        headingVec += missileAcceleration * _randomizedHeadingVector;
    }

    return headingVec;
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

    Vector3D headingVec;
    if (_missileStage == 4)
    {
        headingVec = CalculateDriftCompensation(missileVel, missileToTargetVec, missileAcceleration, 0.5, gravity, 60);
    }
    else
    {
        headingVec = missileToTargetVec;
    }

    if (!Vector3D.IsZero(gravity))
    {
        headingVec = MissileGuidanceBase.GravityCompensation(missileAcceleration, headingVec, gravity);
    }

    return headingVec;
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
        if (VectorMath.AngleBetween(adjustedTargetPos - missilePos, gravityVec) < TOPDOWN_DESCENT_ANGLE)
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

    Vector3D headingVec = _selectedGuidance.Update(missilePos, missileVel, missileAcceleration, adjustedTargetPos, _targetVel, gravityVec);
    return headingVec;
}

void Control(MatrixD missileMatrix, Vector3D headingVec, Vector3D gravityVec, Vector3D velocityVec, double mass)
{
    if (_missileStage == 4)
    {
        var headingDeviation = VectorMath.CosBetween(headingVec, missileMatrix.Forward);
        ApplyThrustOverride(_mainThrusters, (float)MathHelper.Clamp(headingDeviation, 0.25f, 1f) * 100f);
        var sideVelocity = VectorMath.Rejection(velocityVec, headingVec);
        ApplySideThrust(_sideThrusters, sideVelocity, gravityVec, mass);
    }

    // Get pitch and yaw angles
    double yaw, pitch, roll;
    GetRotationAnglesSimultaneous(headingVec, -gravityVec, missileMatrix, out pitch, out yaw, out roll);

    // Angle controller
    double yawSpeed = _yawPID.Control(yaw);
    double pitchSpeed = _pitchPID.Control(pitch);

    // Handle roll more simply
    double rollSpeed = 0;
    if (Math.Abs(_missileSpinRPM) > 1e-3 && _missileStage == 4)
    {
        rollSpeed = _missileSpinRPM * RPM_TO_RAD;
    }
    else
    {
        rollSpeed = roll;
    }

    // Yaw and pitch slowdown to avoid overshoot
    if (Math.Abs(yaw) < GYRO_SLOWDOWN_ANGLE)
    {
        yawSpeed = UPDATES_PER_SECOND * .5 * yaw;
    }

    if (Math.Abs(pitch) < GYRO_SLOWDOWN_ANGLE)
    {
        pitchSpeed = UPDATES_PER_SECOND * .5 * pitch;
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

    IGC.SendBroadcastMessage(IGC_TAG_IFF, _messageBuilder.MoveToImmutable());
}
#endregion

#region Block Property Functions
void ScaleAntennaRange(double dist)
{
    foreach (IMyRadioAntenna a in _antennas)
    {
        if (a.Closed)
            continue;

        a.Radius = (float)dist;

        if (_shouldStealth)
            a.EnableBroadcasting = false;
        else
            a.EnableBroadcasting = true;

        if (_debugAntennas)
            a.CustomName = $"{_guidanceMode}\n{_guidanceAlgoIndex}\n{_raycastHoming.Status}\n";
        else
            a.CustomName = _antennaName;
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
            t.ThrustOverridePercentage = MIN_THRUST;
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
            if (distanceToTarget < adjustedDetonationRange + closingSpeed * SECONDS_PER_UPDATE)
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
        Vector3D localDirToTgt = Vector3D.TransformNormal(directionToTargetNorm, MatrixD.Transpose(cameraBuffer.Peek().WorldMatrix));
        if (cameraBuffer.Count != 0 &&
            localDirToTgt.Z < 0 &&
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
            var closingDisplacement = closingSpeed * SECONDS_PER_UPDATE;
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
        thisWarhead.DetonationTime = Math.Max(0f, (float)fuzeTime - 1f/60f);
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

    double lateralProportion = Math.Sin(_spiralDegrees * DEG_TO_RAD);
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

enum TargetRelation : byte { Neutral = 0, Other = 0, Enemy = 1, Friendly = 2, Locked = 4, LargeGrid = 8, SmallGrid = 16, Missile = 32, RelationMask = Neutral | Enemy | Friendly, TypeMask = LargeGrid | SmallGrid | Other | Missile }

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
void GetRotationAnglesSimultaneous(Vector3D desiredForwardVector, Vector3D desiredUpVector, MatrixD worldMatrix, out double pitch, out double yaw, out double roll)
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
        return pointingVector;
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
#endregion
