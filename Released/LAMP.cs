#region Script

#region DONT YOU DARE TOUCH THESE
const string VERSION = "95.0.0";
const string DATE = "2022/06/06";
const string COMPAT_VERSION = "170.0.0";
#endregion

/*
/ //// / LAMP | Launch A Missile Program (WHAM-FCS) / //// /
_______________________________
    INSTRUCTIONS

See workshop page for instructions, there is no room left in this script!

_______________________________
    NOTE

This code has been minified so that it will fit in the programmable block.
I have NOT obfuscated any of the code, so that if you copy paste this into an 
IDE like Visual Studio or use a website like https://codebeautify.org/csharpviewer,
you can uncompress this and it will be human readable.

_______________________________
    ARGUMENTS

fire
remote_fire
alpha
kill
stealth
stealth_switch
stealth_off
evasion
evasion_switch
evasion_on
evasion_off
spiral
spiral_switch
spiral_on
spiral_off
topdown
topdown_switch
topdown_on
topdown_off
mode_switch
mode_optical | mode_beamride
mode_semiactive | mode_camera
mode_turret
lock_on
lock_off
lock_switch
retask
precise
precise_switch
precise_on
precise_off
autofire
autofire_toggle
autofire_on
autofire_off










=================================================
DO NOT MODIFY VARIABLES IN THE SCRIPT!

USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!

*/

/*
___________________________________________________________________

============= Don't touch anything below this :) ==================
___________________________________________________________________
*/

#region Fields
GuidanceMode _preferredGuidanceMode = GuidanceMode.Camera;
GuidanceMode _designationMode = GuidanceMode.None;
string _missileNameTag = "Missile";
string _fireControlGroupName = "Fire Control";
string _referenceNameTag = "Reference";

bool _autoFire = false;
bool _autoFireRemote = false;
double _autoFireInterval = 1; // seconds
double _timeSinceAutoFire = 141;

double _idleAntennaRange = 800;
double _activeAntennaRange = 5000;
double _minRaycastRange = 50; //meters
double _maxRaycastRange = 5000; //meters
double _maxTimeForLockBreak = 3;
double _timeSinceTurretLock = 0;

bool _stealthySemiActiveAntenna = false;
bool _usePreciseAiming = false;
bool _fireEnabled = true;

const string IGC_TAG_IFF = "IGC_IFF_MSG",
    IGC_TAG_PARAMS = "IGC_MSL_PAR_MSG",
    IGC_TAG_HOMING = "IGC_MSL_HOM_MSG",
    IGC_TAG_BEAM_RIDING = "IGC_MSL_OPT_MSG",
    IGC_TAG_FIRE = "IGC_MSL_FIRE_MSG",
    IGC_TAG_REMOTE_FIRE_REQUEST = "IGC_MSL_REM_REQ",
    IGC_TAG_REMOTE_FIRE_RESPONSE = "IGC_MSL_REM_RSP",
    IGC_TAG_REMOTE_FIRE_NOTIFICATION = "IGC_MSL_REM_NTF",
    IGC_TAG_REGISTER = "IGC_MSL_REG_MSG",
    UNICAST_TAG = "UNICAST",
// General config
    INI_SECTION_GENERAL = "LAMP - General Config",
    INI_FIRE_GROUP_NAME = "Fire control group name",
    INI_MSL_NAME = "Missile group name tag",
    INI_REFERENCE_NAME = "Optional reference block name tag",
    INI_PREFERRED_GUID = "Preferred guidance mode",
    INI_PREFERRED_GUID_COMMENT = "Accepted guidance modes are\n  CAMERA\n  TURRET\n  BEAMRIDE",
    INI_AUTO_FIRE = "Enable auto-fire",
    INI_AUTO_FIRE_INTERVAL = "Auto-fire interval (s)",
    INI_AUTO_FIRE_REMOTE = "Auto-fire remote missiles",
    INI_ANTENNA_RANGE_IDLE = "Antenna range - Idle (m)",
    INI_ANTENNA_RANGE_ACTIVE = "Antenna range - Active (m)",
    INI_ANTENNA_RANGE_DYNAMIC = "Use dynamic active antenna range",
    INI_MIN_RAYCAST_RANGE = "Minimum allowed lock on range (m)",
// Sound config
    INI_SECTION_SOUND_LOCK_SEARCH = "LAMP - Sound Config - Lock Search",
    INI_SECTION_SOUND_LOCK_GOOD = "LAMP - Sound Config - Lock Good",
    INI_SECTION_SOUND_LOCK_LOST = "LAMP - Sound Config - Lock Lost/Abort",
    INI_SECTION_SOUND_LOCK_BAD = "LAMP - Sound Config - Lock Bad",
// Status screen config
    INI_SECTION_COLORS = "LAMP - Status Screen Colors",
    INI_COLOR_TOP_BAR = "Title bar background color",
    INI_COLOR_TITLE_TEXT = "Title text color",
    INI_COLOR_BACKGROUND = "Background color",
    INI_COLOR_TEXT = "Primary text color",
    INI_COLOR_TEXT_SECONDARY = "Secondary text color",
    INI_COLOR_TEXT_STATUS = "Status text color",
    INI_COLOR_STATUS_BACKGROUND = "Status bar background color",
    INI_COLOR_GUID_SELECTED = "Selected guidance outline color",
    INI_COLOR_GUID_ALLOWED = "Allowed guidance text color",
    INI_COLOR_GUID_DISALLOWED = "Disallowed guidance text color",
// Text surface config
    INI_SECTION_TEXT_SURF = "LAMP - Text Surface Config",
    INI_TEXT_SURF_TEMPLATE = "Show on screen {0}",
// Silo door config
    INI_SECTION_SILO_DOOR = "LAMP - Silo Door Config",
    INI_SILO_NUMBER_COMMENT = "This door will be opened when this specified missile is fired",
    INI_SILO_NUMBER = "Missile number",
// Fire timer config
    INI_SECTION_FIRE_TIMER = "LAMP - Fire Timer Config",
    INI_FIRE_TIMER_NUMBER_COMMENT = "This timer will be triggered when this specified missile is fired",
    INI_FIRE_TIMER_NUMBER = "Missile number",
    INI_FIRE_TIMER_ANY_COMMENT = "If this timer should be triggered ANY time a missile is fired",
    INI_FIRE_TIMER_ANY = "Trigger on any fire",
    INI_FIRE_TIMER_TRIGGER_ON_STATE = "Trigger on targeting state",
    INI_FIRE_TIMER_TRIGGER_ON_STATE_COMMENT = " This timer will be triggered when the script enters one of the following\n targeting states:\n"+
                                              "   None, Idle, Searching, Targeting\n"+
                                              " The \"Targeting\" state is triggered when a homing lock is established\n OR when beam ride mode is activated.",
// Display screen constants
    TARGET_LOCKED_TEXT = "Target Locked",
    TARGET_NOT_LOCKED_TEXT = "No Target",
    TARGET_TOO_CLOSE_TEXT = "Target Too Close",
    TARGET_SEARCHING_TEXT = "Searching",
    BEAM_RIDE_ACTIVE = "Active";

const double RuntimeToRealTime = (1.0 / 60.0) / 0.0166666;
const double UpdatesPerSecond = 10;
const double UpdateTime = 1.0 / UpdatesPerSecond;

readonly Color DefaultTextColor = new Color(150, 150, 150);
readonly Color TargetLockedColor = new Color(150, 150, 150);
readonly Color TargetNotLockedColor = new Color(100, 0, 0);
readonly Color TargetSearchingColor = new Color(100, 100, 0);
readonly Color TargetTooCloseColor = new Color(100, 100, 0);

string _statusText = "";
Color _statusColor;
float _lockStrength = 0f;

ArgumentParser _args = new ArgumentParser();

public enum GuidanceMode : int { None = 0, BeamRiding = 1, Camera = 1 << 1, Turret = 1 << 2 };
enum TargetingStatus { Idle, Searching, Targeting };
TargetingStatus _targetingStatus = TargetingStatus.Idle;
TargetingStatus _lastTargetingStatus = TargetingStatus.Idle;

bool _retask = false;
bool _stealth = true;
bool _spiral = false;
bool _topdown = false;
bool _semiActiveAllowed = false;
bool _opticalAllowed = false;
bool _turretAllowed = false;
bool _isSetup = false;
bool _killGuidance = false;
bool _hasKilled = false;
bool _inGravity = false;
bool _showBSOD = false;
bool _broadcastRangeOverride = false;
const double BsodShowTime = 5 * 60;
double _currentBsodShowTime = 0;
GuidanceMode _allowedGuidanceEnum = GuidanceMode.None;
IMyTerminalBlock _lastControlledReference = null;
List<GuidanceMode> _allowedGuidanceModes = new List<GuidanceMode>();
List<IMyTerminalBlock> _shooterReferences = new List<IMyTerminalBlock>();
List<IMySoundBlock> _soundBlocks = new List<IMySoundBlock>();
List<IMyCameraBlock> _cameraList = new List<IMyCameraBlock>();
List<IMyRadioAntenna> _broadcastList = new List<IMyRadioAntenna>();
List<IMyTextSurface> _textSurfaces = new List<IMyTextSurface>();
List<IMyShipController> _shipControllers = new List<IMyShipController>();
List<IMyLargeTurretBase> _turrets = new List<IMyLargeTurretBase>();
List<IMyTurretControlBlock> _turretControlBlocks = new List<IMyTurretControlBlock>();
List<IMyTimerBlock> _timersTriggerOnAnyFire = new List<IMyTimerBlock>(),
                    _idleTimers = new List<IMyTimerBlock>(),
                    _searchTimers = new List<IMyTimerBlock>(),
                    _lockTimers = new List<IMyTimerBlock>();
Dictionary<string, List<IMyTimerBlock>> _statusTimers;
Dictionary<int, IMyDoor> _siloDoorDict = new Dictionary<int, IMyDoor>();
Dictionary<int, IMyTimerBlock> _fireTimerDict = new Dictionary<int, IMyTimerBlock>();
StringBuilder _setupStringbuilder = new StringBuilder();
RuntimeTracker _runtimeTracker;
RaycastHoming _raycastHoming;
MissileStatusScreenHandler _screenHandler;
SoundBlockManager _soundManager = new SoundBlockManager();
StringBuilder _messageBuilder = new StringBuilder();
MyIni _textSurfaceIni = new MyIni();
MyIni _setupIni = new MyIni();
Scheduler _scheduler;
ScheduledAction _scheduledSetup;
StringBuilder _echoBuilder = new StringBuilder();
CircularBuffer<Action> _screenUpdateBuffer;
IMyTerminalBlock _reference = null;

IMyUnicastListener _unicastListener;
IMyBroadcastListener _remoteFireNotificationListener;

bool _clearSpriteCache = false;

Dictionary<string, GuidanceMode> _guidanceModeDict = new Dictionary<string, GuidanceMode>()
{
{"CAMERA", GuidanceMode.Camera},
{"TURRET", GuidanceMode.Turret},
{"BEAMRIDE", GuidanceMode.BeamRiding},
};

SoundConfig _lockSearchSound = new SoundConfig("ArcSoundBlockAlert2", 0.5f, 1f, true);
SoundConfig _lockGoodSound = new SoundConfig("ArcSoundBlockAlert2", 0.2f, 1f, true);
SoundConfig _lockBadSound = new SoundConfig("ArcSoundBlockAlert1", 0.15f, 1f, true);
SoundConfig _lockLostSound = new SoundConfig("ArcSoundBlockAlert1", 0.5f, 0f, false);

public struct SoundConfig
{
    public string Name;
    public float Duration;
    public float Interval;
    public bool Loop;
    
    const string INI_SOUND_NAME = "Sound name";
    const string INI_SOUND_DURATION = "Duration (s)";
    const string INI_SOUND_INTERVAL = "Loop interval (s)";
    const string INI_SOUND_SHOULD_LOOP = "Should loop";
    
    public SoundConfig(string name, float duration, float interval, bool loop)
    {
        Name = name;
        Duration = duration;
        Interval = interval;
        Loop = loop;
    }
    
    public void UpdateFromIni(string section, MyIni ini)
    {
        // Read
        Name = ini.Get(section, INI_SOUND_NAME).ToString(Name);
        Duration = ini.Get(section, INI_SOUND_DURATION).ToSingle(Duration);
        Interval = ini.Get(section, INI_SOUND_INTERVAL).ToSingle(Interval);
        Loop = ini.Get(section, INI_SOUND_SHOULD_LOOP).ToBoolean(Loop);
        
        // Write
        ini.Set(section, INI_SOUND_NAME, Name);
        ini.Set(section, INI_SOUND_DURATION, Duration);
        ini.Set(section, INI_SOUND_INTERVAL, Interval);
        ini.Set(section, INI_SOUND_SHOULD_LOOP, Loop);
    }
}

public Color TopBarColor = new Color(25, 25, 25);
public Color TitleTextColor = new Color(150, 150, 150);
public Color BackgroundColor = new Color(0, 0, 0);
public Color TextColor = new Color(150, 150, 150);
public Color SecondaryTextColor = new Color(75, 75, 75);
public Color StatusTextColor = new Color(150, 150, 150);
public Color StatusBarBackgroundColor = new Color(25, 25, 25);
public Color GuidanceSelectedColor = new Color(0, 50, 0);
public Color GuidanceAllowedColor = new Color(150, 150, 150);
public Color GuidanceDisallowedColor = new Color(25, 25, 25);

#endregion

#region Main Methods
Program()
{   
    _statusTimers = new Dictionary<string, List<IMyTimerBlock>>()
    {
        {"IDLE", _idleTimers},
        {"SEARCHING", _searchTimers},
        {"TARGETING", _lockTimers},
    };
    
    _unicastListener = IGC.UnicastListener;
    _unicastListener.SetMessageCallback(UNICAST_TAG);
    
    _remoteFireNotificationListener = IGC.RegisterBroadcastListener(IGC_TAG_REMOTE_FIRE_NOTIFICATION);
    _remoteFireNotificationListener.SetMessageCallback(IGC_TAG_REMOTE_FIRE_NOTIFICATION);

    _raycastHoming = new RaycastHoming(_maxRaycastRange, _maxTimeForLockBreak, _minRaycastRange, Me.CubeGrid.EntityId);
    _raycastHoming.AddEntityTypeToFilter(MyDetectedEntityType.FloatingObject, MyDetectedEntityType.Planet, MyDetectedEntityType.Asteroid);

    _screenHandler = new MissileStatusScreenHandler(this);

    _isSetup = GrabBlocks();
    GetLargestGridRadius();
    ParseStorage();
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    _runtimeTracker = new RuntimeTracker(this, 5 * 60, 0.005); // 5 second buffer

    float step = 1f / 9f;
    _screenUpdateBuffer = new CircularBuffer<Action>(10);
    _screenUpdateBuffer.Add(() => _screenHandler.ComputeScreenParams(_designationMode, _allowedGuidanceEnum, _lockStrength, _statusText, _statusColor, _maxRaycastRange, _inGravity, _stealth, _spiral, _topdown, _usePreciseAiming, _autoFire, _fireEnabled));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 0 * step, 1 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 1 * step, 2 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 2 * step, 3 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 3 * step, 4 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 4 * step, 5 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 5 * step, 6 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 6 * step, 7 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 7 * step, 8 * step, _clearSpriteCache));
    _screenUpdateBuffer.Add(() => _screenHandler.DrawScreens(_textSurfaces, 8 * step, 9 * step, _clearSpriteCache));

    _scheduledSetup = new ScheduledAction(Setup, 0.1);

    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(_scheduledSetup);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(HandleDisplays, 60);
    _scheduler.AddScheduledAction(GuidanceProcess, UpdatesPerSecond);
    _scheduler.AddScheduledAction(NetworkTargets, 6);
    _scheduler.AddScheduledAction(GetLargestGridRadius, 1.0 / 30.0);
    _scheduler.AddScheduledAction(() => AgeFiredPrograms(1), 1);
    
    OnNewTargetingStatus();
}

void Main(string arg, UpdateType updateType)
{
    if (_showBSOD)
    {
        ++_currentBsodShowTime;
        if (_currentBsodShowTime > BsodShowTime)
        {
            _showBSOD = false;
            _isSetup = GrabBlocks();
        }

        return;
    }

    _runtimeTracker.AddRuntime();

    if ((updateType & UpdateType.IGC) != 0)
    {
        if (arg.Equals(UNICAST_TAG))
        {
            ParseUnicastMessages();
        }
        if (arg.Equals(IGC_TAG_REMOTE_FIRE_NOTIFICATION))
        {
            ParseRemoteFireNotification();
        }
    }
    else if (!string.IsNullOrWhiteSpace(arg))
    {
        ParseArguments(arg);
    }
    
    var lastRuntime = RuntimeToRealTime * Math.Max(Runtime.TimeSinceLastRun.TotalSeconds, 0);
    _timeSinceTurretLock += lastRuntime;
    if (_autoFire)
        _timeSinceAutoFire += lastRuntime;

    try
    {
        _scheduler.Update();
        _soundManager.Update((float)lastRuntime);
    }
    catch (Exception e)
    {
        string scriptName = "WMI Missile Fire Control";
        PrintBsod(Me.GetSurface(0), scriptName, VERSION, 1f, e);
        foreach (var surface in _textSurfaces)
        {
            PrintBsod(surface, scriptName, VERSION, 1f, e);
        }
        _showBSOD = true;
    }
    _runtimeTracker.AddInstructions();
}

void PrintDetailedInfo()
{
    _echoBuilder.AppendLine($"LAMP | Launch A Missile Program\n(Version {VERSION} - {DATE})");
    _echoBuilder.AppendLine($"\nFor use with WHAM v{COMPAT_VERSION} or later.\n");
    _echoBuilder.AppendLine($"Next refresh in {Math.Max(_scheduledSetup.RunInterval - _scheduledSetup.TimeSinceLastRun, 0):N0} seconds\n");
    _echoBuilder.AppendLine($"Text Surfaces: {_textSurfaces.Count}\n");
    _echoBuilder.AppendLine($"Last setup results:\n{_setupStringbuilder}");
    _echoBuilder.AppendLine(_runtimeTracker.Write());
    Echo(_echoBuilder.ToString());
    _echoBuilder.Clear();
}

void Setup()
{
    _clearSpriteCache = !_clearSpriteCache;
    _isSetup = GrabBlocks();
}

void HandleDisplays()
{
    _screenUpdateBuffer.MoveNext().Invoke();
}

void OnNewTargetingStatus()
{
    List<IMyTimerBlock> timers;
    switch (_targetingStatus)
    {
        case TargetingStatus.Idle:
            timers = _idleTimers;
            break;
        case TargetingStatus.Searching:
            timers = _searchTimers;
            break;
        case TargetingStatus.Targeting:
            timers = _lockTimers;
            break;
        default:
            return;
    }
    foreach (var t in timers)
    {
        t.Trigger();
    }
}

void GuidanceProcess()
{
    if (!_isSetup)
        return;

    bool shouldBroadcast = false;
    _statusColor = DefaultTextColor;
    _lockStrength = 0f;
    _targetingStatus = TargetingStatus.Idle;

    if (_shipControllers.Count > 0)
    {
        _inGravity = !Vector3D.IsZero(_shipControllers[0].GetNaturalGravity());
    }

    switch (_designationMode)
    {
        case GuidanceMode.BeamRiding:
            HandleOptical(ref shouldBroadcast);
            break;
        case GuidanceMode.Camera:
            HandleCameraHoming(ref shouldBroadcast);
            break;
        case GuidanceMode.Turret:
            HandleTurretHoming(ref shouldBroadcast);
            break;
    }
    
    if (_targetingStatus != _lastTargetingStatus)
    {
        OnNewTargetingStatus();
    }
    _lastTargetingStatus = _targetingStatus;

    if (shouldBroadcast) //or if kill command
    {
        BroadcastTargetingData();
        BroadcastParameterMessage();
    }
    else if (_broadcastRangeOverride)
    {
        _scheduler.AddQueuedAction(() => ScaleAntennaRange(_activeAntennaRange), 0);
        _scheduler.AddQueuedAction(BroadcastParameterMessage, 1.0 / 6.0);
    }
}

void BroadcastTargetingData()
{
    long broadcastKey = GetBroadcastKey();
    switch (_designationMode)
    {
        case GuidanceMode.BeamRiding:
            SendMissileBeamRideMessage(
                frontVec,
                leftVec,
                upVec,
                originPos,
                broadcastKey);
            break;
        case GuidanceMode.Camera:
            SendMissileHomingMessage(
                _raycastHoming.HitPosition,
                _raycastHoming.TargetPosition,
                _raycastHoming.TargetVelocity,
                _raycastHoming.PreciseModeOffset,
                Me.CubeGrid.WorldAABB.Center,
                _raycastHoming.TimeSinceLastLock,
                _raycastHoming.TargetId,
                broadcastKey);
            break;
        case GuidanceMode.Turret:
            SendMissileHomingMessage(
                _targetInfo.Position,
                _targetInfo.Position,
                _targetInfo.Velocity,
                Vector3D.Zero,
                Me.CubeGrid.WorldAABB.Center,
                _timeSinceTurretLock,
                0, // TODO: Test if ID works now
                broadcastKey);
            break;
    }
}

void BroadcastParameterMessage()
{
    long broadcastKey = GetBroadcastKey();
    bool killNow = (_killGuidance && !_hasKilled);

    SendMissileParameterMessage(
        killNow,
        _stealth,
        _spiral,
        _topdown,
        _usePreciseAiming,
        _retask,
        broadcastKey);

    if (_retask)
        _retask = false;

    if (killNow)
        _hasKilled = true;

    if (_broadcastRangeOverride)
        _broadcastRangeOverride = false;
}

#region Guidance Moding
void HandleOptical(ref bool shouldBroadcast)
{
    shouldBroadcast = true;
    OpticalGuidance();
    ScaleAntennaRange(_activeAntennaRange);
    StopAllSounds();

    // Status
    _statusText = BEAM_RIDE_ACTIVE;
    _targetingStatus = TargetingStatus.Targeting;
}

void HandleCameraHoming(ref bool shouldBroadcast)
{
    _raycastHoming.Update(UpdateTime, _cameraList, _shipControllers, _reference);

    double antennaRange = _idleAntennaRange;
    if (_raycastHoming.Status == RaycastHoming.TargetingStatus.Locked)
    {
        shouldBroadcast = true;

        // Antenna range
        if (_stealthySemiActiveAntenna)
            antennaRange = Vector3D.Distance(base.Me.CubeGrid.WorldAABB.Center, _raycastHoming.TargetPosition) - _raycastHoming.TargetSize;
        else
            antennaRange = _activeAntennaRange;

        // Play sound
        if (!_raycastHoming.MissedLastScan)
            PlayLockOnSound(_soundBlocks);
        else if (_raycastHoming.MissedLastScan)
            PlayScanMissedSound(_soundBlocks);

        // Status
        _lockStrength = 1f - (float)((_raycastHoming.TimeSinceLastLock - _raycastHoming.AutoScanInterval) / _raycastHoming.MaxTimeForLockBreak);
        _lockStrength = MathHelper.Clamp(_lockStrength, 0f, 1f);

        _statusText = TARGET_LOCKED_TEXT;
        _statusColor = TargetLockedColor;
        _targetingStatus = TargetingStatus.Targeting;

        // Autofire
        if (_autoFire && FiringAllowed && _timeSinceAutoFire >= _autoFireInterval)
        {
            if (_autoFireRemote)
                RequestRemoteMissileFire();
            else
                FireNextMissile(1);
            _timeSinceAutoFire = 0;
        }
    }
    else
    {
        // Sound
        if (_raycastHoming.IsScanning)
        {
            PlayLockSearchSound(_soundBlocks);
        }
        else if (_raycastHoming.LockLost)
        {
            _raycastHoming.AcknowledgeLockLost();
            PlayFireAbortSound(_soundBlocks);
        }

        // Status
        if (_raycastHoming.Status == RaycastHoming.TargetingStatus.NotLocked)
        {
            if (!_raycastHoming.IsScanning)
            {
                _statusText = TARGET_NOT_LOCKED_TEXT;
                _statusColor = TargetNotLockedColor;
            }
            else
            {
                _statusText = TARGET_SEARCHING_TEXT;
                _statusColor = TargetSearchingColor;
                _targetingStatus = TargetingStatus.Searching;
            }
        }
        else if (_raycastHoming.Status == RaycastHoming.TargetingStatus.TooClose)
        {
            _statusText = TARGET_TOO_CLOSE_TEXT;
            _statusColor = TargetTooCloseColor;
        }
    }

    // Set antenna range
    if (!_broadcastRangeOverride)
        ScaleAntennaRange(antennaRange);
}

void HandleTurretHoming(ref bool shouldBroadcast)
{
    // TODO: Make turret guidance populate fields
    TurretGuidance(_turrets, _turretControlBlocks);

    double antennaRange = _stealthySemiActiveAntenna ? 1 : _idleAntennaRange;
    if (_turretLocked)
    {
        shouldBroadcast = true;
        
        // Sound
        PlayLockOnSound(_soundBlocks);

        // Antenna range
        if (_stealthySemiActiveAntenna)
            antennaRange = Vector3D.Distance(Me.CubeGrid.WorldAABB.Center, _targetInfo.Position) - 10.0;
        else
            antennaRange = _activeAntennaRange;

        // Status
        _lockStrength = 1f;
        _statusText = TARGET_LOCKED_TEXT;
        _statusColor = TargetLockedColor;
        _targetingStatus = TargetingStatus.Targeting;

        // Handle autofire
        if (_autoFire && FiringAllowed && _timeSinceAutoFire >= _autoFireInterval)
        {
            if (_autoFireRemote)
                RequestRemoteMissileFire();
            else
                FireNextMissile(1);
            _timeSinceAutoFire = 0;
        }
    }
    else
    {
        // Status
        _statusText = TARGET_NOT_LOCKED_TEXT;
        _statusColor = TargetNotLockedColor;
        StopAllSounds();
    }

    // Set antenna range
    if (!_broadcastRangeOverride)
        ScaleAntennaRange(antennaRange);
}
#endregion

#endregion

#region IGC Unicast Handling
void ParseUnicastMessages()
{
    while (_unicastListener.HasPendingMessage)
    {
        MyIGCMessage message = _unicastListener.AcceptMessage();
        object data = message.Data;
        if (message.Tag == IGC_TAG_REMOTE_FIRE_RESPONSE)
        {
            if (data is MyTuple<Vector3D, long>)
            {
                var payload = (MyTuple<Vector3D, long>)data;
                var response = new RemoteFireResponse((Vector3)payload.Item1, payload.Item2);
                if (!_remoteFireResponses.Contains(response))
                    _remoteFireResponses.Add(response);
            }
        }
    }
}
#endregion

#region Remote Fire
int _remoteFireRequests = 0;
bool _awaitingResponse = false;

struct RemoteFireResponse
{
    public Vector3D Position;
    public long EntityId;

    public RemoteFireResponse(Vector3 pos, long id)
    {
        Position = pos;
        EntityId = id;
    }

    public override bool Equals(object obj)
    {
        if (!(obj is RemoteFireResponse))
            return false;
        return this.Equals((RemoteFireResponse)obj);
    }

    public bool Equals(RemoteFireResponse other)
    {
        return other.EntityId == this.EntityId;
    }

    public override int GetHashCode()
    {
        return this.EntityId.GetHashCode();
    }
}

void RequestRemoteMissileFire()
{
    _remoteFireRequests++;

    if (!_awaitingResponse)
    {
        var payload = new MyTuple<Vector3D, long>();
        payload.Item1 = Me.GetPosition();
        payload.Item2 = Me.EntityId;
        IGC.SendBroadcastMessage(IGC_TAG_REMOTE_FIRE_REQUEST, payload);

        // Delay processing (Gives missiles 20 ticks to respond)
        _scheduler.AddScheduledAction(ParseRemoteFireResponses, 3, true);
        _awaitingResponse = true;
    }
}

List<RemoteFireResponse> _remoteFireResponses = new List<RemoteFireResponse>();
void ParseRemoteFireResponses()
{
    Vector3D referencePosition = Me.GetPosition();
    switch (_designationMode)
    {
        case GuidanceMode.Turret:
            referencePosition = _targetInfo.Position;
            break;

        case GuidanceMode.Camera:
            referencePosition = _raycastHoming.TargetPosition;
            break;
    }

    _remoteFireResponses.Sort((x, y) =>
    {
        var num1 = Vector3D.DistanceSquared(x.Position, referencePosition);
        var num2 = Vector3D.DistanceSquared(y.Position, referencePosition);
        return num1.CompareTo(num2);
    });

    long broadcastKey = GetBroadcastKey();
    if (broadcastKey <= 0)
    {
        PlayFireAbortSound(_soundBlocks);
    }
    else
    {
        for (int i = 0; i < _remoteFireResponses.Count; ++i)
        {
            if (i + 1 > _remoteFireRequests)
                break;

            var response = _remoteFireResponses[i];

            IGC.SendUnicastMessage(response.EntityId, IGC_TAG_REGISTER, broadcastKey);
            IGC.SendUnicastMessage(response.EntityId, IGC_TAG_FIRE, "");
        }
    }

    _remoteFireResponses.Clear();
    _awaitingResponse = false;
    _remoteFireRequests = 0;
}

void ParseRemoteFireNotification()
{
    while (_remoteFireNotificationListener.HasPendingMessage)
    {
        var msg = _remoteFireNotificationListener.AcceptMessage();
        if (msg.Data is int)
        {
            var missileNumber = (int)(msg.Data);
            OpenSiloDoor(missileNumber);
            TriggerFireTimer(missileNumber);
        }
    }
}
#endregion

#region Broadcast IFF
IMyCubeGrid _biggestGrid;
double _biggestGridRadius;

void GetLargestGridRadius()
{
    _biggestGridRadius = Me.CubeGrid.WorldVolume.Radius;
    _biggestGrid = Me.CubeGrid;
    GridTerminalSystem.GetBlocksOfType<IMyMechanicalConnectionBlock>(null, b => {
        var m = (IMyMechanicalConnectionBlock)b;
        double rad = m.CubeGrid.WorldVolume.Radius;
        IMyCubeGrid grid = m.CubeGrid;

        if (m.IsAttached)
        {
            double radT = m.TopGrid.WorldVolume.Radius;
            if (radT > rad)
            {
                rad = radT;
                grid = m.TopGrid;
            }
        }

        if (rad > _biggestGridRadius)
        {
            _biggestGridRadius = rad;
            _biggestGrid = grid;
        }

        return false;
    });
}


enum TargetRelation : byte { Neutral = 0, Other = 0, Enemy = 1, Friendly = 2, Locked = 4, LargeGrid = 8, SmallGrid = 16, RelationMask = Neutral | Enemy | Friendly, TypeMask = LargeGrid | SmallGrid | Other }
void NetworkTargets()
{
    // Broadcast own position
    TargetRelation myType = _biggestGrid.GridSizeEnum == MyCubeSize.Large ? TargetRelation.LargeGrid : TargetRelation.SmallGrid;
    var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)(TargetRelation.Friendly | myType), _biggestGrid.EntityId, _biggestGrid.WorldVolume.Center, _biggestGridRadius * _biggestGridRadius);
    IGC.SendBroadcastMessage(IGC_TAG_IFF, myTuple);

    // Broadcast target pos
    if ((_designationMode == GuidanceMode.Camera && _raycastHoming.Status == RaycastHoming.TargetingStatus.Locked)
        || (_designationMode == GuidanceMode.Turret && _turretLocked))
    {
        MyRelationsBetweenPlayerAndBlock relationBetweenPlayerAndBlock;
        MyDetectedEntityType type;
        long targetId = 0;
        Vector3D targetPos = Vector3.Zero;
        switch (_designationMode)
        {
            case GuidanceMode.Camera:
                relationBetweenPlayerAndBlock = _raycastHoming.TargetRelation;
                targetId = _raycastHoming.TargetId;
                targetPos = _raycastHoming.TargetCenter;
                type = _raycastHoming.TargetType;
                break;

            default: // Turret
                relationBetweenPlayerAndBlock = _targetInfo.Relationship;
                targetId = _targetInfo.EntityId;
                targetPos = _targetInfo.Position;
                type = _targetInfo.Type;
                break;
        }

        TargetRelation relation = TargetRelation.Locked;
        switch (relationBetweenPlayerAndBlock)
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
            default:
                relation |= TargetRelation.Neutral;
                break;
        }

        switch (type)
        {
            case MyDetectedEntityType.LargeGrid:
                relation |= TargetRelation.LargeGrid;
                break;
            case MyDetectedEntityType.SmallGrid:
                relation |= TargetRelation.SmallGrid;
                break;
        }

        myTuple = new MyTuple<byte, long, Vector3D, double>((byte)relation, targetId, targetPos, 0);
        IGC.SendBroadcastMessage(IGC_TAG_IFF, myTuple);
    }
}
#endregion

#region Save and Argument Parsing
void Save()
{
    // TODO: Reimplement this properly
}

void ParseStorage()
{
    // TODO: Reimplement this properly
}

bool FiringAllowed
{
    get
    {
        bool allowed = (_designationMode == GuidanceMode.Camera && _raycastHoming.Status == RaycastHoming.TargetingStatus.Locked);
        allowed |= (_designationMode == GuidanceMode.Turret && _turretLocked);
        allowed |= (_designationMode == GuidanceMode.BeamRiding);
        allowed &= _fireEnabled;
        return allowed;
    }
}

void ParseArguments(string arg)
{
    if (!_args.TryParse(arg))
    {
        // TODO: Print error msg
        return;
    }

    switch (_args.Argument(0).ToLowerInvariant())
    {
        #region fire and kill commands
        case "enable_fire":
            _fireEnabled = true;
            break;
            
        case "disable_fire":
            _fireEnabled = false;
            break;
        
        case "fire":
            if (FiringAllowed)
            {
                int count = 1, start = 0, end = -1;
                bool useRange = false;

                if (_args.HasSwitch("range"))
                {
                    int rangeIdx = _args.GetSwitchIndex("range");
                    string startStr = _args.Argument(1 + rangeIdx);
                    string endStr = _args.Argument(2 + rangeIdx);
                    if (int.TryParse(startStr, out start) && int.TryParse(endStr, out end))
                    {
                        useRange = true;
                    }
                }

                if (_args.HasSwitch("count"))
                {
                    if (!int.TryParse(_args.Argument(1 + _args.GetSwitchIndex("count")), out count))
                    {
                        count = 1;
                    }
                }

                if (useRange)
                {
                    FireMissileInRange(count, start, end);
                }
                else
                {
                    FireNextMissile(count);
                }
            }
            else
            {
                PlayFireAbortSound(_soundBlocks);
            }
            break;

        case "remote_fire":
            if (FiringAllowed)
            {
                // No broadcast override needed since we have to be active to fire
                RequestRemoteMissileFire();
            }
            else
            {
                PlayFireAbortSound(_soundBlocks);
            }
            break;

        case "kill":
            _killGuidance = true;
            _hasKilled = false;
            _broadcastRangeOverride = true;
            break;

        case "alpha":
            if (FiringAllowed)
            {
                AlphaStrike();
            }
            else
            {
                PlayFireAbortSound(_soundBlocks);
            }
            break;
        #endregion

        #region stealth toggle
        case "stealth":
        case "stealth_switch":
            _stealth = !_stealth;
            _broadcastRangeOverride = true;
            break;

        case "stealth_on":
            _stealth = true;
            _broadcastRangeOverride = true;
            break;

        case "stealth_off":
            _stealth = false;
            _broadcastRangeOverride = true;
            break;
        #endregion

        #region spiral trajectory toggle
        case "evasion":
        case "evasion_switch":
        case "spiral":
        case "spiral_switch":
            _spiral = !_spiral;
            _broadcastRangeOverride = true;
            break;

        case "evasion_on":
        case "spiral_on":
            _spiral = true;
            _broadcastRangeOverride = true;
            break;

        case "evasion_off":
        case "spiral_off":
            _spiral = false;
            _broadcastRangeOverride = true;
            break;
        #endregion

        #region top down attack mode
        case "topdown":
        case "topdown_switch":
            _topdown = !_topdown;
            _broadcastRangeOverride = true;
            break;

        case "topdown_on":
            _topdown = true;
            _broadcastRangeOverride = true;
            break;

        case "topdown_off":
            _topdown = false;
            _broadcastRangeOverride = true;
            break;
        #endregion

        #region guidance switching
        case "mode_switch":
            CycleGuidanceModes();
            break;

        case "mode_beamride":
        case "mode_optical":
            if (_opticalAllowed)
                _designationMode = GuidanceMode.BeamRiding;
            break;

        case "mode_camera":
        case "mode_semiactive":
            if (_semiActiveAllowed)
            {
                _raycastHoming.ClearLock();
                _designationMode = GuidanceMode.Camera;
            }
            break;

        case "mode_turret":
            if (_turretAllowed)
                _designationMode = GuidanceMode.Turret;
            break;
        #endregion

        #region lock on
        case "lock_on":
            if (_semiActiveAllowed)
            {
                _designationMode = GuidanceMode.Camera;
                _raycastHoming.LockOn();
            }
            break;

        case "lock_off":
            if (_semiActiveAllowed)
            {
                _designationMode = GuidanceMode.Camera;
                _raycastHoming.ClearLock();
                PlayFireAbortSound(_soundBlocks);
            }
            break;

        case "lock_switch":
            if (_semiActiveAllowed)
            {
                _designationMode = GuidanceMode.Camera;
                if (_raycastHoming.IsScanning) //if scanning, switch
                {
                    _raycastHoming.ClearLock();
                    PlayFireAbortSound(_soundBlocks);
                }
                else
                {
                    _raycastHoming.LockOn();
                }
            }
            break;

        case "retask":
            _retask = true;
            break;
        #endregion

        #region Precision mode
        case "precise":
        case "precise_switch":
            _usePreciseAiming = !_usePreciseAiming;
            _raycastHoming.OffsetTargeting = _usePreciseAiming;
            _broadcastRangeOverride = true;
            break;

        case "precise_on":
            _usePreciseAiming = true;
            _raycastHoming.OffsetTargeting = _usePreciseAiming;
            _broadcastRangeOverride = true;
            break;

        case "precise_off":
            _usePreciseAiming = false;
            _raycastHoming.OffsetTargeting = _usePreciseAiming;
            _broadcastRangeOverride = true;
            break;
        #endregion

        #region Auto fire toggle
        case "autofire":
        case "autofire_toggle":
            _autoFire = !_autoFire;
            WriteAutoFire();
            break;

        case "autofire_on":
            _autoFire = true;
            WriteAutoFire();
            break;

        case "autofire_off":
            _autoFire = false;
            WriteAutoFire();
            break;
            #endregion
    }
}

void WriteAutoFire()
{
    _setupIni.Clear();
    _setupIni.TryParse(Me.CustomData);
    _setupIni.Set(INI_SECTION_GENERAL, INI_AUTO_FIRE, _autoFire);

    string output = _setupIni.ToString();
    if (!string.Equals(output, Me.CustomData))
    {
        Me.CustomData = output;
    }
}

void CycleGuidanceModes()
{
    if (_allowedGuidanceModes.Count == 0)
        return;

    int index = _allowedGuidanceModes.FindIndex(x => x == _designationMode);
    index = ++index % _allowedGuidanceModes.Count;
    _designationMode = _allowedGuidanceModes[index];
    if (_designationMode == GuidanceMode.Camera)
        _raycastHoming.ClearLock();
}
#endregion

#region Block Fetching
bool GrabBlocks()
{
    _setupStringbuilder.Clear();
    HandleIni();

    var group = GridTerminalSystem.GetBlockGroupWithName(_fireControlGroupName);
    if (group == null)
    {
        _setupStringbuilder.AppendLine($"ERROR: No block group named '{_fireControlGroupName}' was found");
        return false;
    }

    _soundBlocks.Clear();
    _cameraList.Clear();
    _broadcastList.Clear();
    _textSurfaces.Clear();
    _shipControllers.Clear();
    _turrets.Clear();
    _turretControlBlocks.Clear();
    _timersTriggerOnAnyFire.Clear();
    _idleTimers.Clear();
    _searchTimers.Clear();
    _lockTimers.Clear();
    _siloDoorDict.Clear();
    _fireTimerDict.Clear();
    _reference = null;

    group.GetBlocksOfType<IMyTerminalBlock>(null, CollectionFunction);
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(_shipControllers);

    if (_textSurfaces.Count == 0)
    {
        _setupStringbuilder.AppendLine($"Info: No text surfaces in '{_fireControlGroupName}' group");
    }

    if (_soundBlocks.Count == 0)
    {
        _setupStringbuilder.AppendLine($"Info: No sound blocks in '{_fireControlGroupName}' group");
    }

    // Getting allowed guidance modes
    _allowedGuidanceModes.Clear();

    // Camera guidance checks
    _semiActiveAllowed = true;
    if (_cameraList.Count == 0)
    {
        _setupStringbuilder.AppendLine("WARNING: Camera homing unavailable.");
        _setupStringbuilder.AppendLine($"Info: No cameras in '{_fireControlGroupName}' group");
        _semiActiveAllowed = false;
    }
    else
        _allowedGuidanceModes.Add(GuidanceMode.Camera);

    // Turret guidance checks
    _turretAllowed = true;
    if (_turrets.Count == 0 && _turretControlBlocks.Count == 0)
    {
        _setupStringbuilder.AppendLine("WARNING: Turret homing unavailable.");
        _setupStringbuilder.AppendLine($"Info: No turrets in '{_fireControlGroupName}' group");
        _turretAllowed = false;
    }
    else
        _allowedGuidanceModes.Add(GuidanceMode.Turret);

    // Optical guidance checks
    _opticalAllowed = true;

    if (_shipControllers.Count == 0 && _cameraList.Count == 0 && _reference == null)
    {
        _setupStringbuilder.AppendLine("WARNING: Optical guidance unavailable.");
        _opticalAllowed = false;
    }
    else
    {
        _allowedGuidanceModes.Add(GuidanceMode.BeamRiding);
    }

    if (_shipControllers.Count == 0)
    {
        _setupStringbuilder.AppendLine("Info: No ship controllers on this ship");
    }

    //Antenna Blocks
    if (_broadcastList.Count == 0)
    {
        _setupStringbuilder.AppendLine($"ERROR: No antenna named found in '{_fireControlGroupName}' group");
        _setupStringbuilder.AppendLine("\n<<< SETUP FAILED >>>");
        return false;
    }

    if (_allowedGuidanceModes.Count == 0)
    {
        _setupStringbuilder.AppendLine("ERROR: No allowed guidance modes");
        _setupStringbuilder.AppendLine("\n<<< SETUP FAILED >>>");
        return false;
    }

    if (_designationMode == GuidanceMode.None)
    {
        if (_allowedGuidanceModes.Contains(_preferredGuidanceMode))
        {
            _designationMode = _preferredGuidanceMode;
        }
    }

    if (_designationMode == GuidanceMode.None)
        _designationMode = _allowedGuidanceModes[0];

    _setupStringbuilder.AppendLine("\n>>> SETUP SUCCESSFUL <<<");
    _setupStringbuilder.AppendLine($"\nAllowed guidance modes:");
    _allowedGuidanceEnum = GuidanceMode.None;
    foreach (var mode in _allowedGuidanceModes)
    {
        _allowedGuidanceEnum |= mode;
        _setupStringbuilder.AppendLine($" - {mode}");
    }
    return true;
}

void HandleIni()
{
    _setupIni.Clear();
    string preferredStr = "CAMERA";
    if (_setupIni.TryParse(Me.CustomData))
    {
        _fireControlGroupName = _setupIni.Get(INI_SECTION_GENERAL, INI_FIRE_GROUP_NAME).ToString(_fireControlGroupName);
        _missileNameTag = _setupIni.Get(INI_SECTION_GENERAL, INI_MSL_NAME).ToString(_missileNameTag);
        _referenceNameTag = _setupIni.Get(INI_SECTION_GENERAL, INI_REFERENCE_NAME).ToString(_referenceNameTag);
        _autoFire = _setupIni.Get(INI_SECTION_GENERAL, INI_AUTO_FIRE).ToBoolean(_autoFire);
        _autoFireRemote = _setupIni.Get(INI_SECTION_GENERAL, INI_AUTO_FIRE_REMOTE).ToBoolean(_autoFireRemote);
        _autoFireInterval = _setupIni.Get(INI_SECTION_GENERAL, INI_AUTO_FIRE_INTERVAL).ToDouble(_autoFireInterval);
        _idleAntennaRange = _setupIni.Get(INI_SECTION_GENERAL, INI_ANTENNA_RANGE_IDLE).ToDouble(_idleAntennaRange);
        _activeAntennaRange = _setupIni.Get(INI_SECTION_GENERAL, INI_ANTENNA_RANGE_ACTIVE).ToDouble(_activeAntennaRange);
        _stealthySemiActiveAntenna = _setupIni.Get(INI_SECTION_GENERAL, INI_ANTENNA_RANGE_DYNAMIC).ToBoolean(_stealthySemiActiveAntenna);
        _minRaycastRange = _setupIni.Get(INI_SECTION_GENERAL, INI_MIN_RAYCAST_RANGE).ToDouble(_minRaycastRange);

        TopBarColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TOP_BAR, _setupIni, TopBarColor);
        TitleTextColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TITLE_TEXT, _setupIni, TitleTextColor);
        BackgroundColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_BACKGROUND, _setupIni, BackgroundColor);
        TextColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TEXT, _setupIni, TextColor);
        SecondaryTextColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TEXT_SECONDARY, _setupIni, SecondaryTextColor);
        StatusTextColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TEXT_STATUS, _setupIni, StatusTextColor);
        StatusBarBackgroundColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_STATUS_BACKGROUND, _setupIni, StatusBarBackgroundColor);
        GuidanceSelectedColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_GUID_SELECTED, _setupIni, GuidanceSelectedColor);
        GuidanceAllowedColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_GUID_ALLOWED, _setupIni, GuidanceAllowedColor);
        GuidanceDisallowedColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_GUID_DISALLOWED, _setupIni, GuidanceDisallowedColor);

        // Get preferred guidance mode
        preferredStr = _setupIni.Get(INI_SECTION_GENERAL, INI_PREFERRED_GUID).ToString(preferredStr);
        GuidanceMode preferredMode = GuidanceMode.None;
        if (_guidanceModeDict.TryGetValue(preferredStr, out preferredMode))
        {
            _preferredGuidanceMode = preferredMode;
        }
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _setupIni.Clear();
        _setupIni.EndContent = Me.CustomData;
    }

    _setupIni.Set(INI_SECTION_GENERAL, INI_FIRE_GROUP_NAME, _fireControlGroupName);
    _setupIni.Set(INI_SECTION_GENERAL, INI_MSL_NAME, _missileNameTag);
    _setupIni.Set(INI_SECTION_GENERAL, INI_REFERENCE_NAME, _referenceNameTag);
    _setupIni.Set(INI_SECTION_GENERAL, INI_PREFERRED_GUID, preferredStr);
    _setupIni.SetComment(INI_SECTION_GENERAL, INI_PREFERRED_GUID, INI_PREFERRED_GUID_COMMENT);
    _setupIni.Set(INI_SECTION_GENERAL, INI_AUTO_FIRE, _autoFire);
    _setupIni.Set(INI_SECTION_GENERAL, INI_AUTO_FIRE_REMOTE, _autoFireRemote);

    _setupIni.Set(INI_SECTION_GENERAL, INI_AUTO_FIRE_INTERVAL, _autoFireInterval);
    _setupIni.Set(INI_SECTION_GENERAL, INI_ANTENNA_RANGE_IDLE, _idleAntennaRange);
    _setupIni.Set(INI_SECTION_GENERAL, INI_ANTENNA_RANGE_ACTIVE, _activeAntennaRange);
    _setupIni.Set(INI_SECTION_GENERAL, INI_ANTENNA_RANGE_DYNAMIC, _stealthySemiActiveAntenna);
    _setupIni.Set(INI_SECTION_GENERAL, INI_MIN_RAYCAST_RANGE, _minRaycastRange);

    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TOP_BAR, _setupIni, TopBarColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TITLE_TEXT, _setupIni, TitleTextColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_BACKGROUND, _setupIni, BackgroundColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TEXT, _setupIni, TextColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TEXT_SECONDARY, _setupIni, SecondaryTextColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TEXT_STATUS, _setupIni, StatusTextColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_STATUS_BACKGROUND, _setupIni, StatusBarBackgroundColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_GUID_SELECTED, _setupIni, GuidanceSelectedColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_GUID_ALLOWED, _setupIni, GuidanceAllowedColor);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_GUID_DISALLOWED, _setupIni, GuidanceDisallowedColor);

    _lockSearchSound.UpdateFromIni(INI_SECTION_SOUND_LOCK_SEARCH, _setupIni);
    _lockGoodSound.UpdateFromIni(INI_SECTION_SOUND_LOCK_GOOD, _setupIni);
    _lockBadSound.UpdateFromIni(INI_SECTION_SOUND_LOCK_BAD, _setupIni);
    _lockLostSound.UpdateFromIni(INI_SECTION_SOUND_LOCK_LOST, _setupIni);

    string output = _setupIni.ToString();
    if (!string.Equals(output, Me.CustomData))
    {
        Me.CustomData = output;
    }
}

bool CollectionFunction(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    AddTextSurfaces(block, _textSurfaces);

    if (block.CustomName.IndexOf(_referenceNameTag, StringComparison.OrdinalIgnoreCase) >= 0)
        _reference = block;

    // TODO: Only look for ship controllers in group? Maybe prioritize those in the group?

    var door = block as IMyDoor;
    if (door != null)
    {
        _setupIni.Clear();
        bool parsed = _setupIni.TryParse(door.CustomData);
        int siloNumber = 0; // Default value
        if (parsed)
        {
            siloNumber = _setupIni.Get(INI_SECTION_SILO_DOOR, INI_SILO_NUMBER).ToInt32(siloNumber);
        }
        _setupIni.Set(INI_SECTION_SILO_DOOR, INI_SILO_NUMBER, siloNumber);
        _setupIni.SetComment(INI_SECTION_SILO_DOOR, INI_SILO_NUMBER, INI_SILO_NUMBER_COMMENT);
        _siloDoorDict[siloNumber] = door;

        string output = _setupIni.ToString();
        if (!string.Equals(output, door.CustomData))
        {
            door.CustomData = output;
        }
        // TODO: Print warn if multiple doors bound to same missile
        return false;
    }

    var timer = block as IMyTimerBlock;
    if (timer != null)
    {
        _setupIni.Clear();
        bool parsed = _setupIni.TryParse(timer.CustomData);
        int siloNumber = 0; // Default value
        bool fireAny = false;
        string triggerState = "None";
        
        if (parsed)
        {
            siloNumber = _setupIni.Get(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_NUMBER).ToInt32(siloNumber);
            fireAny = _setupIni.Get(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_ANY).ToBoolean(fireAny);
            triggerState = _setupIni.Get(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_TRIGGER_ON_STATE).ToString(triggerState);

            List<IMyTimerBlock> timers;
            if (_statusTimers.TryGetValue(triggerState.ToUpperInvariant(), out timers))
            {
                timers.Add(timer);
            }
            else
            {
                triggerState = "None";
            }
        }
        _setupIni.Set(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_NUMBER, siloNumber);
        _setupIni.SetComment(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_NUMBER, INI_FIRE_TIMER_NUMBER_COMMENT);
        _setupIni.Set(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_ANY, fireAny);
        _setupIni.SetComment(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_ANY, INI_FIRE_TIMER_ANY_COMMENT);
        _setupIni.Set(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_TRIGGER_ON_STATE, triggerState);
        _setupIni.SetComment(INI_SECTION_FIRE_TIMER, INI_FIRE_TIMER_TRIGGER_ON_STATE, INI_FIRE_TIMER_TRIGGER_ON_STATE_COMMENT);

        if (fireAny)
        {
            _timersTriggerOnAnyFire.Add(timer);
        }
        else
        {
            _fireTimerDict[siloNumber] = timer;
        }

        string output = _setupIni.ToString();
        if (!string.Equals(output, timer.CustomData))
        {
            timer.CustomData = output;
        }
        // TODO: Print warn if multiple timers bound to same missile
        return false;
    }

    var soundBlock = block as IMySoundBlock;
    if (soundBlock != null)
    {
        _soundBlocks.Add(soundBlock);
        return false;
    }

    var camera = block as IMyCameraBlock;
    if (camera != null)
    {
        _cameraList.Add(camera);
        camera.EnableRaycast = true;
        return false;
    }

    var antenna = block as IMyRadioAntenna;
    if (antenna != null)
    {
        _broadcastList.Add(antenna);
        return false;
    }

    var turret = block as IMyLargeTurretBase;
    if (turret != null)
    {
        _turrets.Add(turret);
        return false;
    }
    
    var tcb = block as IMyTurretControlBlock;
    if (tcb != null)
    {
        _turretControlBlocks.Add(tcb);
        return false;
    }

    return false;
}

void AddTextSurfaces(IMyTerminalBlock block, List<IMyTextSurface> textSurfaces)
{
    var textSurface = block as IMyTextSurface;
    if (textSurface != null)
    {
        textSurfaces.Add(textSurface);
        return;
    }

    var surfaceProvider = block as IMyTextSurfaceProvider;
    if (surfaceProvider == null)
        return;

    _textSurfaceIni.Clear();
    bool parsed = _textSurfaceIni.TryParse(block.CustomData);
    if (!parsed && !string.IsNullOrWhiteSpace(block.CustomData))
    {
        _textSurfaceIni.Clear();
        _textSurfaceIni.EndContent = block.CustomData;
    }

    int surfaceCount = surfaceProvider.SurfaceCount;
    for (int i = 0; i < surfaceCount; ++i)
    {
        string iniKey = string.Format(INI_TEXT_SURF_TEMPLATE, i);
        bool display = _textSurfaceIni.Get(INI_SECTION_TEXT_SURF, iniKey).ToBoolean(i == 0 && !(block is IMyProgrammableBlock));
        if (display)
            textSurfaces.Add(surfaceProvider.GetSurface(i));

        _textSurfaceIni.Set(INI_SECTION_TEXT_SURF, iniKey, display);
    }

    string output = _textSurfaceIni.ToString();
    if (!string.Equals(output, block.CustomData))
        block.CustomData = output;
}
#endregion

#region Firing Methods
List<IMyBlockGroup> missileGroups = new List<IMyBlockGroup>();
List<int> currentMissileNumbers = new List<int>();
List<IMyProgrammableBlock> missilePrograms = new List<IMyProgrammableBlock>();
Dictionary<int, IMyBlockGroup> missileNumberDict = new Dictionary<int, IMyBlockGroup>();
Dictionary<IMyProgrammableBlock, double> firedMissileProgramAge = new Dictionary<IMyProgrammableBlock, double>();
List<IMyProgrammableBlock> firedMissilesKeyList = new List<IMyProgrammableBlock>();
List<IMyProgrammableBlock> firedMissileProgramKeysToRemove = new List<IMyProgrammableBlock>();
const double MIN_PROGRAM_AGE_TO_REMOVE = 10.0;

void AgeFiredPrograms(double deltaTime)
{
    firedMissilesKeyList.Clear();
    foreach (var key in firedMissileProgramAge.Keys)
    {
        firedMissilesKeyList.Add(key);
    }

    foreach (var key in firedMissilesKeyList)
    {
        double elapsed = firedMissileProgramAge[key];
        if (elapsed > MIN_PROGRAM_AGE_TO_REMOVE)
        {
            firedMissileProgramKeysToRemove.Add(key);
        }
        else
        {
            firedMissileProgramAge[key] = elapsed + deltaTime;
        }
    }

    foreach (var key in firedMissileProgramKeysToRemove)
    {
        firedMissileProgramAge.Remove(key);
    }
}

void GetCurrentMissiles()
{
    currentMissileNumbers.Clear();
    missileNumberDict.Clear();
    GridTerminalSystem.GetBlockGroups(null, CollectMissileNumbers);
    currentMissileNumbers.Sort();
}

bool CollectMissileNumbers(IMyBlockGroup g)
{
    var number = GetMissileNumber(g, _missileNameTag);
    if (number < 0)
    {
        return false;
    }

    currentMissileNumbers.Add(number);
    missileNumberDict[number] = g;
    return false;
}

int GetMissileNumber(IMyBlockGroup group, string missileTag)
{
    string groupName = group.Name;
    // Check for tag
    if (!groupName.StartsWith(missileTag))
    {
        return -1;
    }

    // Check that string is long enough to have both a space and a number
    if (missileTag.Length + 2 > groupName.Length)
    {
        return -1;
    }

    // Check for space
    if (groupName[missileTag.Length] != ' ')
    {
        return -1;
    }

    // Check for number after space
    int startIdx = missileTag.Length + 1;
    int missileNumber;
    bool parsed = int.TryParse(groupName.Substring(startIdx), out missileNumber);
    if (!parsed)
    {
        return -1;
    }

    return missileNumber;
}

void AlphaStrike()
{
    GetCurrentMissiles();

    foreach (var missileNumber in currentMissileNumbers)
    {
        FireMissilePrograms(missileNumber);
    }
}

bool FireMissilePrograms(int missileNumber)
{
    IMyBlockGroup group = null;
    if (!missileNumberDict.TryGetValue(missileNumber, out group))
        return false;

    missilePrograms.Clear();
    group.GetBlocksOfType(missilePrograms, x => !firedMissileProgramAge.ContainsKey(x));
    if (missilePrograms.Count == 0)
    {
        return false; // Could not fire
    }

    foreach (var pb in missilePrograms)
    {
        IGC.SendUnicastMessage(pb.EntityId, IGC_TAG_FIRE, "");
        firedMissileProgramAge[pb] = 0;
    }

    OpenSiloDoor(missileNumber);
    TriggerFireTimer(missileNumber);
    
    BroadcastTargetingData();
    BroadcastParameterMessage();

    return true;
}

void FireMissileInRange(int numberToFire, int start, int end)
{
    GetCurrentMissiles();

    int numberFired = 0;
    foreach (var missileNumber in currentMissileNumbers)
    {
        if (missileNumber < start || missileNumber > end)
        {
            continue;
        }

        bool fired = FireMissilePrograms(missileNumber);
        if (fired)
        {
            numberFired++;
        }

        if (numberFired >= numberToFire)
        {
            break;
        }
    }
}

void FireNextMissile(int numberToFire)
{
    GetCurrentMissiles();

    int numberFired = 0;
    foreach (var missileNumber in currentMissileNumbers)
    {
        bool fired = FireMissilePrograms(missileNumber);
        if (fired)
        {
            numberFired++;
        }

        if (numberFired >= numberToFire)
        {
            break;
        }
    }
}

void OpenSiloDoor(int missileNumber)
{
    IMyDoor siloDoor;
    if (_siloDoorDict.TryGetValue(missileNumber, out siloDoor))
    {
        siloDoor.Enabled = true;
        siloDoor.OpenDoor();
    }
}

void TriggerFireTimer(int missileNumber)
{
    IMyTimerBlock timer;
    if (_fireTimerDict.TryGetValue(missileNumber, out timer))
    {
        timer.Trigger();
    }

    foreach (var t in _timersTriggerOnAnyFire)
    {
        t.Trigger();
    }
}
#endregion

#region Optical Guidance
Vector3D originPos = new Vector3D(0, 0, 0);
Vector3D frontVec = new Vector3D(0, 0, 0);
Vector3D leftVec = new Vector3D(0, 0, 0);
Vector3D upVec = new Vector3D(0, 0, 0);

void OpticalGuidance()
{
    /*
     * The following prioritizes references in the following hierchy:
     * 1. Currently used camera
     * 2. Reference block (if any is specified)
     * 3. Currently used control seat
     * 4. Last active control seat
     * 5. First control seat that is found
     * 6. First camera that is found
     */

    IMyTerminalBlock reference = GetControlledCamera(_cameraList);

    if (reference == null)
        reference = _reference;

    if (reference == null)
        reference = GetControlledShipController(_shipControllers);

    if (reference == null)
    {
        if (_lastControlledReference != null)
        {
            reference = _lastControlledReference;
        }
        else if (_shipControllers.Count > 0)
        {
            reference = _shipControllers[0];
        }
        else if (_cameraList.Count > 0)
        {
            reference = _cameraList[0];
        }
        else
        {
            return;
        }
    }

    _lastControlledReference = reference;

    originPos = reference.GetPosition();
    frontVec = reference.WorldMatrix.Forward;
    leftVec = reference.WorldMatrix.Left;
    upVec = reference.WorldMatrix.Up;
}
#endregion

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
    enum AimMode { None = 0, Center = 1, Offset = 2, OffsetRelative = 4 };

    AimMode _currentAimMode = AimMode.Center;

    readonly HashSet<MyDetectedEntityType> _targetFilter = new HashSet<MyDetectedEntityType>();
    readonly List<IMyCameraBlock> _availableCameras = new List<IMyCameraBlock>();
    readonly Random _rngeesus = new Random();

    MyDetectedEntityInfo _targetInfo = default(MyDetectedEntityInfo);
    MatrixD _targetOrientation;
    Vector3D _targetPosCached;
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

    public void RemoveEntityTypeToFilter(params MyDetectedEntityType[] types)
    {
        foreach (var type in types)
        {
            _targetFilter.Remove(type);
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
        _targetInfo = default(MyDetectedEntityInfo);
        IsScanning = false;
        Status = TargetingStatus.NotLocked;
        MissedLastScan = false;
        TimeSinceLastLock = 0;
        TargetSize = 0;
        HitPosition = Vector3D.Zero;
        TargetId = 0;
        _timeSinceLastScan = 141;
        _targetPosCached = Vector3D.Zero;
        _currentAimMode = AimMode.Center;
        TargetRelation = MyRelationsBetweenPlayerAndBlock.NoOwnership;
        TargetType = MyDetectedEntityType.None;
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

        var randomVector = (2.0 * _rngeesus.NextDouble() - 1.0) * perpVector1 + (2.0 * _rngeesus.NextDouble() - 1.0) * perpVector2;
        return randomVector * fudgeFactor * TimeSinceLastLock;
    }

    public void Update(double timeStep, List<IMyCameraBlock> cameraList, List<IMyShipController> shipControllers, IMyTerminalBlock referenceBlock = null)
    {
        _timeSinceLastScan += timeStep;

        if (!IsScanning)
            return;

        TimeSinceLastLock += timeStep;

        _targetInfo = default(MyDetectedEntityInfo);
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
        if (_currentAimMode == AimMode.Offset)
        {
            scanPosition = HitPosition + TargetVelocity * TimeSinceLastLock;
        }
        else if (_currentAimMode == AimMode.OffsetRelative)
        {
            scanPosition = OffsetTargetPosition + TargetVelocity * TimeSinceLastLock;
        }
        else
        {
            scanPosition = TargetCenter + TargetVelocity * TimeSinceLastLock;
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
        if (_availableCameras.Count == 0) //transition between faces
        {
            _timeSinceLastScan = 100000; // Set to scan immediately on the next cycle
            MissedLastScan = true;
            return;
        }

        //Get camera with maximum available range
        var thisCamera = GetCameraWithMaxRange(_availableCameras);
        var cameraMatrix = thisCamera.WorldMatrix;

        // Determine scan range
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

        // This operates under the assumption that raycast charges at 2km/s
        AutoScanInterval = scanRange / 2000 / _availableCameras.Count * AutoScanScaleFactor; // Give a fudge factor for safety
        var availableScanRange = thisCamera.AvailableScanRange;

        //Attempt to scan adjusted target position
        if (availableScanRange > scanRange &&
            _timeSinceLastScan >= AutoScanInterval)
        {
            if (Status == TargetingStatus.Locked || _manualLockOverride)
                _targetInfo = thisCamera.Raycast(adjustedTargetPos);
            else if (!Vector3D.IsZero(testDirection))
                _targetInfo = thisCamera.Raycast(cameraMatrix.Translation + testDirection * MaxRange);
            else
                _targetInfo = thisCamera.Raycast(MaxRange);

            _timeSinceLastScan = 0;
        }
        else // Not enough charge stored up yet
        {
            return;
        }

        // Validate target and assign values
        if (!_targetInfo.IsEmpty() &&
            !_targetFilter.Contains(_targetInfo.Type) &&
            !_gridIDsToIgnore.Contains(_targetInfo.EntityId)) //target lock
        {
            if (Vector3D.DistanceSquared(_targetInfo.Position, thisCamera.GetPosition()) < MinRange * MinRange && Status != TargetingStatus.Locked)
            {
                Status = TargetingStatus.TooClose;
                return;
            }
            else if (Status == TargetingStatus.Locked) // Target already locked
            {
                if (_targetInfo.EntityId == TargetId)
                {
                    TargetCenter = _targetInfo.Position;
                    HitPosition = _targetInfo.HitPosition.Value;

                    _targetOrientation = _targetInfo.Orientation;
                    OffsetTargetPosition = TargetCenter + Vector3D.TransformNormal(PreciseModeOffset, _targetOrientation);

                    TargetVelocity = _targetInfo.Velocity;
                    TargetSize = _targetInfo.BoundingBox.Size.Length();
                    TimeSinceLastLock = 0;

                    if (_manualLockOverride)
                    {
                        TargetId = _targetInfo.EntityId;
                        _manualLockOverride = false;
                    }

                    MissedLastScan = false;
                    TargetRelation = _targetInfo.Relationship;
                    TargetType = _targetInfo.Type;
                }
                else
                {
                    MissedLastScan = true;
                }
            }
            else // Target not yet locked: initial lockon
            {
                if (_manualLockOverride && TargetId != _targetInfo.EntityId && TargetId != 0)
                    return;

                if (_manualLockOverride && TargetId == 0)
                {
                    double maxDispPerTickSq = 100 + _targetInfo.Velocity.LengthSquared() * timeStep * timeStep;
                    if (Vector3D.DistanceSquared(_targetPositionOverride, _targetInfo.HitPosition.Value) > maxDispPerTickSq)
                    {
                        return; // Abort lock since it is not locked onto what we want
                    }
                }

                Status = TargetingStatus.Locked;
                TargetId = _targetInfo.EntityId;
                TargetCenter = _targetInfo.Position;
                HitPosition = _targetInfo.HitPosition.Value;
                TargetVelocity = _targetInfo.Velocity;
                TargetSize = _targetInfo.BoundingBox.Size.Length();
                TimeSinceLastLock = 0;

                var aimingCamera = GetControlledCamera(_availableCameras);
                Vector3D hitPosOffset = Vector3D.Zero;
                if (aimingCamera != null)
                {
                    hitPosOffset = aimingCamera.GetPosition() - thisCamera.GetPosition();
                }
                else if (reference != null)
                {
                    hitPosOffset = reference.GetPosition() - thisCamera.GetPosition();
                }
                if (!Vector3D.IsZero(hitPosOffset))
                {
                    hitPosOffset = VectorRejection(hitPosOffset, HitPosition - thisCamera.GetPosition());
                }

                var hitPos = _targetInfo.HitPosition.Value + hitPosOffset;
                _targetOrientation = _targetInfo.Orientation;

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
                TargetRelation = _targetInfo.Relationship;
                TargetType = _targetInfo.Type;
            }

            _targetPosCached = _targetInfo.Position;
        }
        else
        {
            //TODO: Investigate if we should only count a miss when we are LOCKED
            MissedLastScan = true;
        }

        if (MissedLastScan)
        {
            // If scan missed, switch aim mode
            if (_currentAimMode == AimMode.Center)
            {
                _currentAimMode = AimMode.Offset;
            }
            else if (_currentAimMode == AimMode.Offset)
            {
                _currentAimMode = AimMode.OffsetRelative;
            }
            else
            {
                _currentAimMode = AimMode.Center;
            }
            //_timeSinceLastScan = 1000000; // Make sure we scan as fast as we can to try and re-establish lock.
        }
    }

    void GetAvailableCameras(List<IMyCameraBlock> allCameras, List<IMyCameraBlock> availableCameras, Vector3D testVector, bool vectorIsPosition = false)
    {
        availableCameras.Clear();

        foreach (var block in allCameras)
        {
            if (block.Closed)
                continue;

            if (TestCameraAngles(block, vectorIsPosition ? testVector - block.GetPosition() : testVector))
                availableCameras.Add(block);
        }
    }

    bool TestCameraAngles(IMyCameraBlock camera, Vector3D direction)
    {
        Vector3D localDirection = Vector3D.Rotate(direction, MatrixD.Transpose(camera.WorldMatrix));

        if (localDirection.Z > 0) //pointing backwards
            return false;

        var yawTan = Math.Abs(localDirection.X / localDirection.Z);
        var pitchTanSq = localDirection.Y * localDirection.Y / (localDirection.X * localDirection.X + localDirection.Z * localDirection.Z);

        return yawTan <= 1 && pitchTanSq <= 1;
    }

    IMyCameraBlock GetCameraWithMaxRange(List<IMyCameraBlock> cameraList)
    {
        double maxRange = double.MinValue;
        IMyCameraBlock maxRangeCamera = default(IMyCameraBlock);

        foreach (IMyCameraBlock thisCamera in cameraList)
        {
            if (thisCamera.AvailableScanRange > maxRange)
            {
                maxRangeCamera = thisCamera;
                maxRange = maxRangeCamera.AvailableScanRange;
            }
        }

        return maxRangeCamera;
    }

    IMyCameraBlock GetControlledCamera(List<IMyCameraBlock> cameras)
    {
        foreach (var block in cameras)
        {
            if (block.Closed)
                continue;

            if (block.IsActive)
                return block;
        }
        return null;
    }

    IMyShipController GetControlledShipController(List<IMyShipController> shipControllers)
    {
        if (shipControllers.Count == 0)
            return null;

        IMyShipController mainController = null;
        IMyShipController controlled = null;

        foreach (IMyShipController thisController in shipControllers)
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
            return mainController;

        if (controlled != null)
            return controlled;

        return shipControllers[0];
    }

    public static Vector3D VectorRejection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }
}
#endregion

#region Turret Guidance
List<MyDetectedEntityInfo> _targetInfoList = new List<MyDetectedEntityInfo>();
MyDetectedEntityInfo _targetInfo = new MyDetectedEntityInfo();
double _maxTurretRange = 0;
bool _turretLocked = false;
void TurretGuidance(List<IMyLargeTurretBase> turrets, List<IMyTurretControlBlock> turretControlBlocks)
{
    //get targets
    _targetInfoList.Clear();
    _maxTurretRange = 0;
    foreach (var block in turrets)
    {
        if (block.HasTarget && !block.GetTargetedEntity().IsEmpty())
        {
            _targetInfoList.Add(block.GetTargetedEntity());
        }

        if (block.IsWorking && block.AIEnabled)
        {
            var thisRange = block.Range;
            if (thisRange > _maxTurretRange)
                _maxTurretRange = thisRange;
        }
    }

    foreach (var block in turretControlBlocks)
    {
        if (block.HasTarget && !block.GetTargetedEntity().IsEmpty())
        {
            _targetInfoList.Add(block.GetTargetedEntity());
        }

        if (block.IsWorking && block.AIEnabled)
        {
            var thisRange = block.Range;
            if (thisRange > _maxTurretRange)
                _maxTurretRange = thisRange;
        }
    }

    if (_targetInfoList.Count == 0)
    {
        if (_turretLocked)
        {
            PlayScanMissedSound(_soundBlocks);
        }
        _turretLocked = false;
        return;
    }

    _turretLocked = true;
    _timeSinceTurretLock = 0;

    //prioritize targets
    _targetInfoList.Sort((x, y) =>
    {
        var num1 = (x.Position - Me.GetPosition()).LengthSquared();
        var num2 = (y.Position - Me.GetPosition()).LengthSquared();
        return num1.CompareTo(num2);
    });

    //pick closest target
    _targetInfo = _targetInfoList[0];
    Vector3D targetVelocityVec = _targetInfo.Velocity;
}
#endregion

#region Sound Block Control

void StopAllSounds()
{
    _soundManager.ShouldPlay = false;
    _soundManager.ShouldLoop = false;
}

void PlayLockSearchSound(List<IMySoundBlock> soundBlocks)
{
    _soundManager.ShouldPlay = true;
    _soundManager.ShouldLoop = _lockSearchSound.Loop;
    _soundManager.SoundName = _lockSearchSound.Name;
    _soundManager.LoopDuration = _lockSearchSound.Interval;
    _soundManager.SoundDuration = _lockSearchSound.Duration;
    _soundManager.SoundBlocks = soundBlocks;
}

void PlayLockOnSound(List<IMySoundBlock> soundBlocks)
{
    _soundManager.ShouldPlay = true;
    _soundManager.ShouldLoop = _lockGoodSound.Loop;
    _soundManager.SoundName = _lockGoodSound.Name;
    _soundManager.LoopDuration = _lockGoodSound.Interval;
    _soundManager.SoundDuration = _lockGoodSound.Duration;
    _soundManager.SoundBlocks = soundBlocks;
}

void PlayFireAbortSound(List<IMySoundBlock> soundBlocks)
{
    _soundManager.ShouldPlay = false; // Force state change to cause the sound to be played immideately
    _soundManager.ShouldPlay = true;
    _soundManager.ShouldLoop = _lockLostSound.Loop;
    _soundManager.SoundName = _lockLostSound.Name;
    _soundManager.LoopDuration = _lockLostSound.Interval;
    _soundManager.SoundDuration = _lockLostSound.Duration;
    _soundManager.SoundBlocks = soundBlocks;
}

void PlayScanMissedSound(List<IMySoundBlock> soundBlocks)
{
    _soundManager.ShouldPlay = true;
    _soundManager.ShouldLoop = _lockBadSound.Loop;
    _soundManager.SoundName = _lockBadSound.Name;
    _soundManager.LoopDuration = _lockBadSound.Interval;
    _soundManager.SoundDuration = _lockBadSound.Duration;
    _soundManager.SoundBlocks = soundBlocks;
}

class SoundBlockManager
{
    public bool ShouldLoop = true;

    public float SoundDuration
    {
        get
        {
            return _soundDuration;
        }
        set
        {
            if (Math.Abs(value - _soundDuration) < 1e-3)
            {
                return;
            }
            _soundDuration = value;
            _settingsDirty = true;
        }
    }

    public float LoopDuration;

    public bool ShouldPlay
    {
        get
        {
            return _shouldPlay;
        }
        set
        {
            if (value == _shouldPlay)
                return;
            _shouldPlay = value;
            _hasPlayed = false;
        }
    }

    public string SoundName
    {
        get
        {
            return _soundName;
        }
        set
        {
            if (value == _soundName)
            {
                return;
            }
            _soundName = value;
            _settingsDirty = true;
        }
    }

    public List<IMySoundBlock> SoundBlocks;

    bool _settingsDirty = false;
    bool _shouldPlay = false;
    float _soundDuration;
    string _soundName;
    bool _hasPlayed = false;
    float _loopTime;
    float _soundPlayTime;

    enum SoundBlockAction { None = 0, UpdateSettings = 1, Play = 2, Stop = 4 }

    public void Update(float dt)
    {
        SoundBlockAction action = SoundBlockAction.None;

        if (_settingsDirty)
        {
            action |= SoundBlockAction.UpdateSettings;
            _settingsDirty = false;
        }

        if (ShouldPlay)
        {
            if (!_hasPlayed)
            {
                action |= SoundBlockAction.Play;
                _hasPlayed = true;
                _soundPlayTime = 0;
                _loopTime = 0;
            }
            else
            {
                _loopTime += dt;
                _soundPlayTime += dt;
                if (_soundPlayTime >= SoundDuration)
                {
                    action |= SoundBlockAction.Stop;
                    if (!ShouldLoop)
                    {
                        ShouldPlay = false;
                    }
                }

                if (ShouldLoop && _loopTime >= LoopDuration && _hasPlayed)
                {
                    _hasPlayed = false;
                }
            }
        }
        else
        {
            action |= SoundBlockAction.Stop;
        }

        // Apply sound block action
        if (action != SoundBlockAction.None && SoundBlocks != null)
        {
            foreach (var sb in SoundBlocks)
            {
                if ((action & SoundBlockAction.UpdateSettings) != 0)
                {
                    sb.LoopPeriod = 100f;
                    sb.SelectedSound = SoundName;
                }
                if ((action & SoundBlockAction.Play) != 0)
                {
                    sb.Play();
                }
                if ((action & SoundBlockAction.Stop) != 0)
                {
                    sb.Stop();
                }
            }
        }
    }
}

#endregion

#region Screen Display
/*
** Description:
**   Class for handling WHAM status screen displays.
**
** Dependencies:
**   MySpriteContainer
*/
public class MissileStatusScreenHandler
{
    #region Fields
    List<MySpriteContainer> _spriteContainers = new List<MySpriteContainer>();

    // Default sizes
    const float DEFAULT_SCREEN_SIZE = 512;
    const float DEFAULT_SCREEN_HALF_SIZE = 512 * 0.5f;

    // UI positions
    Vector2 TOP_BAR_SIZE;
    Vector2 STATUS_BAR_SIZE;
    Vector2 TOP_BAR_POS;
    Vector2 TOP_BAR_TEXT_POS;
    Vector2 STEALTH_TEXT_POS;
    Vector2 AIM_POINT_POS;
    Vector2 RANGE_TEXT_POS;
    Vector2 SPIRAL_TEXT_POS;
    Vector2 TOPDOWN_TEXT_POS;
    Vector2 STATUS_BAR_POS;
    Vector2 STATUS_BAR_TEXT_POS;
    Vector2 STATUS_BAR_WEAK_TEXT_POS;
    Vector2 STATUS_BAR_STRONG_TEXT_POS;
    Vector2 SECONDARY_TEXT_POS_OFFSET;
    Vector2 DROP_SHADOW_OFFSET;
    Vector2 MODE_CAMERA_POS;
    Vector2 MODE_TURRET_POS;
    Vector2 MODE_BEAMRIDE_POS;
    Vector2 MODE_CAMERA_SELECT_POS;
    Vector2 MODE_TURRET_SELECT_POS;
    Vector2 MODE_BEAMRIDE_SELECT_POS;
    Vector2 MODE_CAMERA_SELECT_SIZE;
    Vector2 MODE_TURRET_SELECT_SIZE;
    Vector2 MODE_BEAMRIDE_SELECT_SIZE;
    Vector2 AUTOFIRE_TEXT_POS;
    Vector2 FIRE_DISABLED_POS;
    Vector2 FIRE_DISABLED_TEXT_BOX_SIZE;

    // Constants
    const float PRIMARY_TEXT_SIZE = 1.5f;
    const float SECONDARY_TEXT_SIZE = 1.2f;
    const float TERTIARY_TEXT_SIZE = 1f;
    const float BASE_TEXT_HEIGHT_PX = 37f;
    const float PRIMARY_TEXT_OFFSET = -0.5f * BASE_TEXT_HEIGHT_PX * PRIMARY_TEXT_SIZE;
    const float MODE_SELECT_LINE_LENGTH = 20f;
    const float MODE_SELECT_LINE_WIDTH = 6f;
    const string FONT = "DEBUG";
    const string TOP_TEXT = "WMI Missile Fire Control";
    const string MODE_TEXT = "Mode";
    const string MODE_CAMERA_TEXT = "Camera";
    const string MODE_TURRET_TEXT = "Turret";
    const string MODE_BEAMRIDE_TEXT = "Beam Ride";
    const string RANGE_TEXT = "Range";
    const string STEALTH_TEXT = "Stealth";
    const string SPIRAL_TEXT = "Evasion";
    const string TOPDOWN_TEXT = "Topdown";
    const string ENABLED_TEXT = "Enabled";
    const string DISABLED_TEXT = "Disabled";
    const string NOT_APPLICABLE_TEXT = "N/A";
    const string STATUS_TEXT = "Status";
    const string WEAK_TEXT = "Weak";
    const string STRONG_TEXT = "Strong";
    const string AIM_POINT_TEXT = "Aim Point";
    const string AIM_CENTER_TEXT = "Center";
    const string AIM_OFFSET_TEXT = "Offset";
    const string AUTOFIRE_TEXT = "Autofire";
    const string FIRE_DISABLED_TEXT = "FIRING DISABLED";

    // Non-configurable colors
    readonly Color STATUS_GOOD_COLOR = new Color(0, 50, 0);
    readonly Color STATUS_BAD_COLOR = new Color(50, 0, 0);
    readonly Color _warningColor = Color.Red;
    readonly Color _warningBackgroundColor = new Color(10, 10, 10, 200);

    Program _p;
    #endregion

    public MissileStatusScreenHandler(Program program)
    {
        _p = program;

        SECONDARY_TEXT_POS_OFFSET = new Vector2(0, -1.5f * PRIMARY_TEXT_OFFSET);
        DROP_SHADOW_OFFSET = new Vector2(2, 2);

        // Top bar
        TOP_BAR_SIZE = new Vector2(512, 64);
        TOP_BAR_POS = new Vector2(0, -DEFAULT_SCREEN_HALF_SIZE + 32); //TODO: compute in ctor
        TOP_BAR_TEXT_POS = new Vector2(0, -DEFAULT_SCREEN_HALF_SIZE + 32 + PRIMARY_TEXT_OFFSET);

        // Modes
        MODE_CAMERA_SELECT_SIZE = new Vector2(130, 56);
        MODE_TURRET_SELECT_SIZE = new Vector2(110, 56);
        MODE_BEAMRIDE_SELECT_SIZE = new Vector2(170, 56);

        MODE_CAMERA_SELECT_POS = new Vector2(-160, -140);
        MODE_TURRET_SELECT_POS = new Vector2(-20, -140);
        MODE_BEAMRIDE_SELECT_POS = new Vector2(140, -140);

        float secondaryTextVeticalOffset = -0.5f * BASE_TEXT_HEIGHT_PX * SECONDARY_TEXT_SIZE;
        MODE_CAMERA_POS = MODE_CAMERA_SELECT_POS + new Vector2(0, secondaryTextVeticalOffset);
        MODE_TURRET_POS = MODE_TURRET_SELECT_POS + new Vector2(0, secondaryTextVeticalOffset);
        MODE_BEAMRIDE_POS = MODE_BEAMRIDE_SELECT_POS + new Vector2(0, secondaryTextVeticalOffset);

        // Status bar
        STATUS_BAR_POS = new Vector2(0, -70);
        STATUS_BAR_SIZE = new Vector2(450, 56);
        STATUS_BAR_TEXT_POS = STATUS_BAR_POS + new Vector2(0, PRIMARY_TEXT_OFFSET);
        STATUS_BAR_WEAK_TEXT_POS = STATUS_BAR_POS + new Vector2(-200, 40);
        STATUS_BAR_STRONG_TEXT_POS = STATUS_BAR_POS + new Vector2(200, 40);

        // Left column
        RANGE_TEXT_POS = new Vector2(-220, 0 + PRIMARY_TEXT_OFFSET);
        SPIRAL_TEXT_POS = new Vector2(-220, 90 + PRIMARY_TEXT_OFFSET);
        STEALTH_TEXT_POS = new Vector2(-220, 180 + PRIMARY_TEXT_OFFSET);

        // Right column
        AIM_POINT_POS = new Vector2(50, 0 + PRIMARY_TEXT_OFFSET);
        AUTOFIRE_TEXT_POS = new Vector2(50, 90 + PRIMARY_TEXT_OFFSET);
        TOPDOWN_TEXT_POS = new Vector2(50, 180 + PRIMARY_TEXT_OFFSET);
        
        // Fire disabled
        FIRE_DISABLED_POS = new Vector2(0, 50);
        FIRE_DISABLED_TEXT_BOX_SIZE = new Vector2(360, -PRIMARY_TEXT_OFFSET * PRIMARY_TEXT_SIZE + 24);
    }

    //For debugging
    public void Echo(string content)
    {
        _p.Echo(content);
    }

    public Color CustomInterpolation(Color color1, Color color2, float ratio)
    {
        float scale = 2f * (1f - ratio);
        if (ratio >= 0.5)
        {
            return new Color(color1.R + (byte)(scale * (float)color2.R),
                              color1.G + (byte)(scale * (float)color2.G),
                              color1.B + (byte)(scale * (float)color2.B));
        }

        return new Color(color2.R + (byte)(2 * ratio * (float)color1.R),
                          color2.G + (byte)(2 * ratio * (float)color1.G),
                          color2.B + (byte)(2 * ratio * (float)color1.B));
    }

    public void ComputeScreenParams(GuidanceMode mode,
                                    GuidanceMode allowedModes,
                                    float lockStrength,
                                    string statusText,
                                    Color statusColor,
                                    double range,
                                    bool inGravity,
                                    bool stealth,
                                    bool spiral,
                                    bool topdown,
                                    bool precise,
                                    bool autofire,
                                    bool fireEnabled)
    {
        _spriteContainers.Clear();

        var guidanceMode = mode;
        var allowedModesEnum = allowedModes;
        bool showTopdownAndAimMode = true;
        bool anyGuidanceAllowed = allowedModesEnum != GuidanceMode.None;
        if (!anyGuidanceAllowed)
        {
            statusText = "ERROR";
            statusColor = STATUS_BAD_COLOR;
        }

        Vector2 modeSelectSize = Vector2.Zero;
        Vector2 modeSelectPos = Vector2.Zero;

        if (guidanceMode == GuidanceMode.BeamRiding)
        {
            showTopdownAndAimMode = false;
            lockStrength = 1;

            modeSelectSize = MODE_BEAMRIDE_SELECT_SIZE;
            modeSelectPos = MODE_BEAMRIDE_SELECT_POS;
        }
        else
        {
            if (guidanceMode == GuidanceMode.Camera)
            {
                modeSelectSize = MODE_CAMERA_SELECT_SIZE;
                modeSelectPos = MODE_CAMERA_SELECT_POS;
            }
            else if (guidanceMode == GuidanceMode.Turret)
            {
                modeSelectSize = MODE_TURRET_SELECT_SIZE;
                modeSelectPos = MODE_TURRET_SELECT_POS;
            }
        }

        MySpriteContainer container;

        // Title bar
        container = new MySpriteContainer("SquareSimple", TOP_BAR_SIZE, TOP_BAR_POS, 0, _p.TopBarColor, true);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(TOP_TEXT, FONT, PRIMARY_TEXT_SIZE, TOP_BAR_TEXT_POS, _p.TitleTextColor);
        _spriteContainers.Add(container);

        // Status bar
        container = new MySpriteContainer("SquareSimple", STATUS_BAR_SIZE, STATUS_BAR_POS, 0, _p.StatusBarBackgroundColor);
        _spriteContainers.Add(container);

        Color lerpedStatusColor = CustomInterpolation(STATUS_GOOD_COLOR, STATUS_BAD_COLOR, lockStrength);
        Vector2 statusBarSize = STATUS_BAR_SIZE * new Vector2(lockStrength, 1f);
        //Vector2 statusBarPos = new Vector2(-0.5f * (STATUS_BAR_SIZE.X - statusBarSize.X), STATUS_BAR_POS.Y);
        container = new MySpriteContainer("SquareSimple", statusBarSize, STATUS_BAR_POS, 0, lerpedStatusColor);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(statusText, FONT, PRIMARY_TEXT_SIZE, STATUS_BAR_TEXT_POS + DROP_SHADOW_OFFSET, Color.Black);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(statusText, FONT, PRIMARY_TEXT_SIZE, STATUS_BAR_TEXT_POS, statusColor);
        _spriteContainers.Add(container);

        // Modes
        DrawBoxCorners(modeSelectSize, modeSelectPos, MODE_SELECT_LINE_LENGTH, MODE_SELECT_LINE_WIDTH, _p.GuidanceSelectedColor, _spriteContainers);

        container = new MySpriteContainer(MODE_CAMERA_TEXT, FONT, SECONDARY_TEXT_SIZE, MODE_CAMERA_POS, (allowedModesEnum & GuidanceMode.Camera) != 0 ? _p.GuidanceAllowedColor : _p.GuidanceDisallowedColor, TextAlignment.CENTER);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(MODE_TURRET_TEXT, FONT, SECONDARY_TEXT_SIZE, MODE_TURRET_POS, (allowedModesEnum & GuidanceMode.Turret) != 0 ? _p.GuidanceAllowedColor : _p.GuidanceDisallowedColor, TextAlignment.CENTER);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(MODE_BEAMRIDE_TEXT, FONT, SECONDARY_TEXT_SIZE, MODE_BEAMRIDE_POS, (allowedModesEnum & GuidanceMode.BeamRiding) != 0 ? _p.GuidanceAllowedColor : _p.GuidanceDisallowedColor, TextAlignment.CENTER);
        _spriteContainers.Add(container);

        // Range
        container = new MySpriteContainer(RANGE_TEXT, FONT, PRIMARY_TEXT_SIZE, RANGE_TEXT_POS, _p.TextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        container = new MySpriteContainer($"{range * 0.001:n1} km", FONT, SECONDARY_TEXT_SIZE, RANGE_TEXT_POS + SECONDARY_TEXT_POS_OFFSET, _p.SecondaryTextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        // Stealth
        container = new MySpriteContainer(STEALTH_TEXT, FONT, PRIMARY_TEXT_SIZE, STEALTH_TEXT_POS, _p.TextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(stealth ? ENABLED_TEXT : DISABLED_TEXT, FONT, SECONDARY_TEXT_SIZE, STEALTH_TEXT_POS + SECONDARY_TEXT_POS_OFFSET, _p.SecondaryTextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        // Spiral
        container = new MySpriteContainer(SPIRAL_TEXT, FONT, PRIMARY_TEXT_SIZE, SPIRAL_TEXT_POS, _p.TextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(spiral ? ENABLED_TEXT : DISABLED_TEXT, FONT, SECONDARY_TEXT_SIZE, SPIRAL_TEXT_POS + SECONDARY_TEXT_POS_OFFSET, _p.SecondaryTextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        // Topdown
        container = new MySpriteContainer(TOPDOWN_TEXT, FONT, PRIMARY_TEXT_SIZE, TOPDOWN_TEXT_POS, _p.TextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        container = new MySpriteContainer((!inGravity || !showTopdownAndAimMode) ? NOT_APPLICABLE_TEXT : (topdown ? ENABLED_TEXT : DISABLED_TEXT), FONT, SECONDARY_TEXT_SIZE, TOPDOWN_TEXT_POS + SECONDARY_TEXT_POS_OFFSET, _p.SecondaryTextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        // Aimpoint
        container = new MySpriteContainer(AIM_POINT_TEXT, FONT, PRIMARY_TEXT_SIZE, AIM_POINT_POS, _p.TextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(!showTopdownAndAimMode ? NOT_APPLICABLE_TEXT : (precise ? AIM_OFFSET_TEXT : AIM_CENTER_TEXT), FONT, SECONDARY_TEXT_SIZE, AIM_POINT_POS + SECONDARY_TEXT_POS_OFFSET, _p.SecondaryTextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        // Autofire
        container = new MySpriteContainer(AUTOFIRE_TEXT, FONT, PRIMARY_TEXT_SIZE, AUTOFIRE_TEXT_POS, _p.TextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);

        container = new MySpriteContainer(!showTopdownAndAimMode ? NOT_APPLICABLE_TEXT : (autofire ? ENABLED_TEXT : DISABLED_TEXT), FONT, SECONDARY_TEXT_SIZE, AUTOFIRE_TEXT_POS + SECONDARY_TEXT_POS_OFFSET, _p.SecondaryTextColor, TextAlignment.LEFT);
        _spriteContainers.Add(container);
        
        // Fire Disabled Warning
        if (!fireEnabled)
        {
            container = new MySpriteContainer("SquareSimple", FIRE_DISABLED_TEXT_BOX_SIZE, FIRE_DISABLED_POS, 0, _warningBackgroundColor);
            _spriteContainers.Add(container);

            container = new MySpriteContainer("AH_TextBox", FIRE_DISABLED_TEXT_BOX_SIZE, FIRE_DISABLED_POS, 0, _warningColor);
            _spriteContainers.Add(container);

            container = new MySpriteContainer(FIRE_DISABLED_TEXT, FONT, PRIMARY_TEXT_SIZE, FIRE_DISABLED_POS + new Vector2(0, -22), _warningColor, TextAlignment.CENTER);
            _spriteContainers.Add(container);
        }
    }

    public void DrawScreens(List<IMyTextSurface> surfaces, float startProportion, float endProportion, bool clearSpriteCache)
    {
        int startInt = (int)Math.Round(startProportion * surfaces.Count);
        int endInt = (int)Math.Round(endProportion * surfaces.Count);

        for (int i = startInt; i < endInt; ++i)
        {
            var surface = surfaces[i];

            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";
            surface.ScriptBackgroundColor = _p.BackgroundColor;


            Vector2 textureSize = surface.TextureSize;
            Vector2 screenCenter = textureSize * 0.5f;
            Vector2 viewportSize = surface.SurfaceSize;
            Vector2 scale = viewportSize / 512f;
            float minScale = Math.Min(scale.X, scale.Y);

            using (var frame = surface.DrawFrame())
            {
                if (clearSpriteCache)
                {
                    frame.Add(new MySprite());
                }

                foreach (var spriteContainer in _spriteContainers)
                {
                    frame.Add(spriteContainer.CreateSprite(minScale, ref screenCenter, ref viewportSize));
                }
            }
        }
    }

    /*
    Draws a box that looks like this:
     __        __
    |            |

    |__        __|
    */
    static void DrawBoxCorners(Vector2 boxSize, Vector2 centerPos, float lineLength, float lineWidth, Color color, List<MySpriteContainer> spriteContainers)
    {
        Vector2 horizontalSize = new Vector2(lineLength, lineWidth);
        Vector2 verticalSize = new Vector2(lineWidth, lineLength);

        Vector2 horizontalOffset = 0.5f * horizontalSize;
        Vector2 verticalOffset = 0.5f * verticalSize;

        Vector2 boxHalfSize = 0.5f * boxSize;
        Vector2 boxTopLeft = centerPos - boxHalfSize;
        Vector2 boxBottomRight = centerPos + boxHalfSize;
        Vector2 boxTopRight = centerPos + new Vector2(boxHalfSize.X, -boxHalfSize.Y);
        Vector2 boxBottomLeft = centerPos + new Vector2(-boxHalfSize.X, boxHalfSize.Y);

        MySpriteContainer container;

        // Top left
        container = new MySpriteContainer("SquareSimple", horizontalSize, boxTopLeft + horizontalOffset, 0, color);
        spriteContainers.Add(container);

        container = new MySpriteContainer("SquareSimple", verticalSize, boxTopLeft + verticalOffset, 0, color);
        spriteContainers.Add(container);

        // Top right
        container = new MySpriteContainer("SquareSimple", horizontalSize, boxTopRight + new Vector2(-horizontalOffset.X, horizontalOffset.Y), 0, color);
        spriteContainers.Add(container);

        container = new MySpriteContainer("SquareSimple", verticalSize, boxTopRight + new Vector2(-verticalOffset.X, verticalOffset.Y), 0, color);
        spriteContainers.Add(container);

        // Bottom left
        container = new MySpriteContainer("SquareSimple", horizontalSize, boxBottomLeft + new Vector2(horizontalOffset.X, -horizontalOffset.Y), 0, color);
        spriteContainers.Add(container);

        container = new MySpriteContainer("SquareSimple", verticalSize, boxBottomLeft + new Vector2(verticalOffset.X, -verticalOffset.Y), 0, color);
        spriteContainers.Add(container);

        // Bottom right
        container = new MySpriteContainer("SquareSimple", horizontalSize, boxBottomRight - horizontalOffset, 0, color);
        spriteContainers.Add(container);

        container = new MySpriteContainer("SquareSimple", verticalSize, boxBottomRight - verticalOffset, 0, color);
        spriteContainers.Add(container);
    }
}

public struct MySpriteContainer
{
    readonly string _spriteName;
    readonly Vector2 _size;
    readonly Vector2 _positionFromCenter;
    readonly float _rotationOrScale;
    readonly Color _color;
    readonly string _font;
    readonly string _text;
    readonly float _scale;
    readonly bool _isText;
    readonly TextAlignment _textAlign;
    readonly bool _fillWidth;

    public MySpriteContainer(string spriteName, Vector2 size, Vector2 positionFromCenter, float rotation, Color color, bool fillWidth = false)
    {
        _spriteName = spriteName;
        _size = size;
        _positionFromCenter = positionFromCenter;
        _rotationOrScale = rotation;
        _color = color;
        _isText = false;

        _font = "";
        _text = "";
        _scale = 0f;

        _textAlign = TextAlignment.CENTER;
        _fillWidth = fillWidth;
    }

    public MySpriteContainer(string text, string font, float scale, Vector2 positionFromCenter, Color color, TextAlignment textAlign = TextAlignment.CENTER)
    {
        _text = text;
        _font = font;
        _scale = scale;
        _positionFromCenter = positionFromCenter;
        _rotationOrScale = scale;
        _color = color;
        _isText = true;
        _textAlign = textAlign;

        _spriteName = "";
        _size = Vector2.Zero;
        _fillWidth = false;
    }

    public MySprite CreateSprite(float scale, ref Vector2 center, ref Vector2 viewportSize)
    {
        if (!_isText)
        {
            if (_fillWidth)
            {
                Vector2 sizeAdjusted = new Vector2(viewportSize.X, _size.Y * scale);
                return new MySprite(SpriteType.TEXTURE, _spriteName, center + _positionFromCenter * scale, sizeAdjusted, _color, rotation: _rotationOrScale);
            }
            return new MySprite(SpriteType.TEXTURE, _spriteName, center + _positionFromCenter * scale, _size * scale, _color, rotation: _rotationOrScale);
        }
        else
            return new MySprite(SpriteType.TEXT, _text, center + _positionFromCenter * scale, null, _color, _font, rotation: _rotationOrScale * scale, alignment: _textAlign);
    }
}
#endregion

#region Antenna Broadcasting
void PopulateMatrix3x3Columns(ref Matrix3x3 mat, ref Vector3D col0, ref Vector3D col1, ref Vector3D col2)
{
    mat.M11 = (float)col0.X;
    mat.M21 = (float)col0.Y;
    mat.M31 = (float)col0.Z;

    mat.M12 = (float)col1.X;
    mat.M22 = (float)col1.Y;
    mat.M32 = (float)col1.Z;

    mat.M13 = (float)col2.X;
    mat.M23 = (float)col2.Y;
    mat.M33 = (float)col2.Z;
}

void SendMissileHomingMessage(Vector3D lastHitPosition, Vector3D targetPosition, Vector3D targetVelocity, Vector3D preciseOffset, Vector3D shooterPosition, double timeSinceLastLock, long targetId, long keycode)
{
    Matrix3x3 matrix1 = new Matrix3x3();
    PopulateMatrix3x3Columns(ref matrix1, ref lastHitPosition, ref targetPosition, ref targetVelocity);

    Matrix3x3 matrix2 = new Matrix3x3();
    PopulateMatrix3x3Columns(ref matrix2, ref preciseOffset, ref shooterPosition, ref Vector3D.Zero);

    var payload = new MyTuple<Matrix3x3, Matrix3x3, float, long, long>
    {
        Item1 = matrix1,
        Item2 = matrix2,
        Item3 = (float)timeSinceLastLock,
        Item4 = targetId,
        Item5 = keycode,
    };

    IGC.SendBroadcastMessage(IGC_TAG_HOMING, payload);
}

void SendMissileBeamRideMessage(Vector3D forward, Vector3D left, Vector3D up, Vector3D shooterPosition, long keycode)
{
    var payload = new MyTuple<Vector3, Vector3, Vector3, Vector3, long>
    {
        Item1 = (Vector3)forward,
        Item2 = (Vector3)left,
        Item3 = (Vector3)up,
        Item4 = (Vector3)shooterPosition,
        Item5 = keycode
    };

    IGC.SendBroadcastMessage(IGC_TAG_BEAM_RIDING, payload);
}

void SendMissileParameterMessage(bool kill, bool stealth, bool spiral, bool topdown, bool precise, bool retask, long keycode)
{
    byte packedBools = 0;
    packedBools |= BoolToByte(kill);
    packedBools |= (byte)(BoolToByte(stealth) << 1);
    packedBools |= (byte)(BoolToByte(spiral) << 2);
    packedBools |= (byte)(BoolToByte(topdown) << 3);
    packedBools |= (byte)(BoolToByte(precise) << 4);
    packedBools |= (byte)(BoolToByte(retask) << 5);

    var payload = new MyTuple<byte, long>
    {
        Item1 = packedBools,
        Item2 = keycode
    };

    IGC.SendBroadcastMessage(IGC_TAG_PARAMS, payload);
}

byte BoolToByte(bool value)
{
    return value ? (byte)1 : (byte)0;
}

long GetBroadcastKey()
{
    long broadcastKey = -1;
    if (_broadcastList.Count > 0)
        broadcastKey = _broadcastList[0].EntityId;

    return broadcastKey;
}
#endregion

#region General Functions
public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

Vector3D GetAverageBlockPosition<T>(List<T> blocks) where T : class, IMyTerminalBlock
{
    Vector3D sum = Vector3D.Zero;
    foreach (var block in blocks)
    {
        sum += block.GetPosition();
    }
    return sum / blocks.Count();
}

IMyCameraBlock GetControlledCamera(List<IMyCameraBlock> cameras)
{
    foreach (var block in cameras)
    {
        if (block.IsActive)
            return block;
    }
    return null;
}

IMyLargeTurretBase GetControlledTurret(List<IMyLargeTurretBase> turrets)
{
    foreach (var block in turrets)
    {
        if (block.IsUnderControl)
            return block;
    }
    return null;
}

IMyShipController GetControlledShipController(List<IMyShipController> shipControllers)
{
    if (shipControllers.Count == 0)
        return null;

    IMyShipController mainControlled = null;
    IMyShipController controlled = null;

    foreach (IMyShipController thisController in shipControllers)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
        {
            if (controlled == null)
            {
                controlled = thisController;
            }

            if (thisController.IsMainCockpit)
            {
                mainControlled = thisController; // Only one per grid so no null check needed
            }
        }
    }

    if (mainControlled != null)
        return mainControlled;

    if (controlled != null)
        return controlled;

    return null; //shipControllers[0];
}

void ScaleAntennaRange(double dist)
{
    foreach (IMyRadioAntenna thisAntenna in _broadcastList)
    {
        thisAntenna.EnableBroadcasting = true;

        thisAntenna.Radius = (float)dist;
    }
}
#endregion

#region Runtime Tracking
// Class that tracks runtime history.
public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime { get; private set; }
    public double MaxInstructions { get; private set; }
    public double AverageRuntime { get; private set; }
    public double AverageInstructions { get; private set; }

    private readonly Queue<double> _runtimes = new Queue<double>();
    private readonly Queue<double> _instructions = new Queue<double>();
    private readonly StringBuilder _sb = new StringBuilder();
    private readonly int _instructionLimit;
    private readonly Program _program;

    public RuntimeTracker(Program program, int capacity = 100, double sensitivity = 0.01)
    {
        _program = program;
        Capacity = capacity;
        Sensitivity = sensitivity;
        _instructionLimit = _program.Runtime.MaxInstructionCount;
    }

    public void AddRuntime()
    {
        double runtime = _program.Runtime.LastRunTimeMs;
        AverageRuntime = Sensitivity * (runtime - AverageRuntime) + AverageRuntime;

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
        _sb.AppendLine("\n_____________________________\nGeneral Runtime Info\n");
        _sb.AppendLine($"Avg instructions: {AverageInstructions:n2}");
        _sb.AppendLine($"Max instructions: {MaxInstructions:n0}");
        _sb.AppendLine($"Avg complexity: {MaxInstructions / _instructionLimit:0.000}%");
        _sb.AppendLine($"Avg runtime: {AverageRuntime:n4} ms");
        _sb.AppendLine($"Max runtime: {MaxRuntime:n4} ms");
        return _sb.ToString();
    }
}
#endregion

#region Ini Helper
public static class MyIniHelper
{
    public static void SetVector3D(string sectionName, string vectorName, ref Vector3D vector, MyIni ini)
    {
        ini.Set(sectionName, vectorName, vector.ToString());
    }

    public static Vector3D GetVector3D(string sectionName, string vectorName, MyIni ini)
    {
        var vector = Vector3D.Zero;
        Vector3D.TryParse(ini.Get(sectionName, vectorName).ToString(), out vector);
        return vector;
    }

    public static void SetColor(string sectionName, string itemName, MyIni ini, Color color)
    {
        string colorString = string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);
        ini.Set(sectionName, itemName, colorString);
    }

    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length != 4)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);
        bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
            a = 255;

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);

        return new Color(r, g, b, a);
    }
}
#endregion

#region Scheduler
public class Scheduler
{
    public double CurrentTimeSinceLastRun { get; private set; } = 0;
    public long CurrentTicksSinceLastRun { get; private set; } = 0;

    ScheduledAction _currentlyQueuedAction = null;
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

    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

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
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTicks);
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
        QueuedAction scheduledAction = new QueuedAction(action, updateInterval);
        _queuedActions.Enqueue(scheduledAction);
    }

    public void AddQueuedAction(QueuedAction scheduledAction)
    {
        _queuedActions.Enqueue(scheduledAction);
    }
}

public class QueuedAction : ScheduledAction
{
    public QueuedAction(Action action, double runInterval)
        : base(action, 1.0 / runInterval, removeAfterRun: true, timeOffset: 0)
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
            _runFrequency = value == 0 ? double.MaxValue : 1.0 / _runIntervalTicks;
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

#region Circular Buffer
public class CircularBuffer<T>
{
    public readonly int Capacity;

    T[] _array = null;
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

#region Argument Parser
class ArgumentParser
{
    public int ArgumentCount
    {
        get;
        private set;
    } = 0;

    public string ErrorMessage
    {
        get;
        private set;
    }

    const char Quote = '"';
    List<string> _arguments = new List<string>();
    HashSet<string> _argHash = new HashSet<string>();
    HashSet<string> _switchHash = new HashSet<string>();
    Dictionary<string, int> _switchIndexDict = new Dictionary<string, int>();

    enum ReturnCode { EndOfStream = -1, Nominal = 0, NoArgs = 1, NonAlphaSwitch = 2, NoEndQuote = 3, NoSwitchName = 4 }

    string _raw;

    public bool InRange(int index)
    {
        if (index < 0 || index >= _arguments.Count)
        {
            return false;
        }
        return true;
    }

    public string Argument(int index)
    {
        if (!InRange(index))
        {
            return "";
        }

        return _arguments[index];
    }

    public bool IsSwitch(int index)
    {
        if (!InRange(index))
        {
            return false;
        }

        return _switchHash.Contains(_arguments[index]);
    }

    public int GetSwitchIndex(string switchName)
    {
        int idx;
        if (_switchIndexDict.TryGetValue(switchName, out idx))
        {
            return idx;
        }
        return -1;
    }

    ReturnCode GetArgStartIdx(int startIdx, out int idx, out bool isQuoted, out bool isSwitch)
    {
        idx = -1;
        isQuoted = false;
        isSwitch = false;
        for (int i = startIdx; i < _raw.Length; ++i)
        {
            char c = _raw[i];
            if (c != ' ')
            {
                if (c == Quote)
                {
                    isQuoted = true;
                    idx = i + 1;
                    return ReturnCode.Nominal;
                }
                if (c == '-' && i + 1 < _raw.Length && _raw[i + 1] == '-')
                {
                    isSwitch = true;
                    idx = i + 2;
                    return ReturnCode.Nominal;
                }
                idx = i;
                return ReturnCode.Nominal;
            }
        }
        return ReturnCode.NoArgs;
    }

    ReturnCode GetArgLength(int startIdx, bool isQuoted, bool isSwitch, out int length)
    {
        length = 0;
        for (int i = startIdx; i < _raw.Length; ++i)
        {
            char c = _raw[i];
            if (isQuoted)
            {
                if (c == Quote)
                {
                    return ReturnCode.Nominal;
                }
            }
            else
            {
                if (c == ' ')
                {
                    if (isSwitch && length == 0)
                    {
                        return ReturnCode.NoSwitchName;
                    }
                    return ReturnCode.Nominal;
                }

                if (isSwitch)
                {
                    if (!char.IsLetter(c))
                    {
                        return ReturnCode.NonAlphaSwitch;
                    }
                }
            }
            length++;
        }
        if (isQuoted)
        {
            return ReturnCode.NoEndQuote;
        }
        if (length == 0 && isSwitch)
        {
            return ReturnCode.NoSwitchName;
        }
        return ReturnCode.EndOfStream; // Reached end of stream
    }

    void ClearArguments()
    {
        ArgumentCount = 0;
        _arguments.Clear();
        _switchHash.Clear();
        _argHash.Clear();
        _switchIndexDict.Clear();
    }

    public bool HasArgument(string argName)
    {
        return _argHash.Contains(argName);
    }

    public bool HasSwitch(string switchName)
    {
        return _switchHash.Contains(switchName);
    }

    public bool TryParse(string arg)
    {
        ReturnCode status;

        _raw = arg;
        ClearArguments();

        int idx = 0;
        while (idx < _raw.Length)
        {
            bool isQuoted, isSwitch;
            int startIdx, length;
            string argString;
            status = GetArgStartIdx(idx, out startIdx, out isQuoted, out isSwitch);
            if (status == ReturnCode.NoArgs)
            {
                ErrorMessage = "";
                return true;
            }

            status = GetArgLength(startIdx, isQuoted, isSwitch, out length);
            if (status == ReturnCode.NoEndQuote)
            {
                ErrorMessage = $"No closing quote found! (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.NonAlphaSwitch)
            {
                ErrorMessage = $"Switch can not contain non-alphabet characters! (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.NoSwitchName)
            {
                ErrorMessage = $"Switch does not have a name (idx: {startIdx})";
                ClearArguments();
                return false;
            }
            else if (status == ReturnCode.EndOfStream) // End of stream
            {
                argString = _raw.Substring(startIdx);
                _arguments.Add(argString);
                _argHash.Add(argString);
                if (isSwitch)
                {
                    _switchHash.Add(argString);
                    _switchIndexDict[argString] = ArgumentCount;
                }
                ArgumentCount++;
                ErrorMessage = "";
                return true;
            }

            argString = _raw.Substring(startIdx, length);
            _arguments.Add(argString);
            _argHash.Add(argString);
            if (isSwitch)
            {
                _switchHash.Add(argString);
                _switchIndexDict[argString] = ArgumentCount;
            }
            ArgumentCount++;
            idx = startIdx + length;
            if (isQuoted)
            {
                idx++; // Move past the quote
            }
        }
        ErrorMessage = "";
        return true;
    }
}
#endregion

#region Print BSOD
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
void PrintBsod(IMyTextSurface surface, string scriptName, string version, float fontSize, Exception e)
{
    surface.ContentType = ContentType.TEXT_AND_IMAGE;
    surface.Alignment = TextAlignment.LEFT;
    surface.FontSize = fontSize;
    surface.FontColor = Color.White;
    surface.BackgroundColor = Color.Blue;
    string exceptionStr = e.ToString();
    string[] exceptionLines = exceptionStr.Split('\n');
    bsodBuilder.Clear();
    Echo(exceptionLines.Length.ToString());
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
#endregion

#endregion
