
#region In-game Script
/*
/ //// / Whip's Artificial Horizon Redux / //// /

HOW DO I USE THIS?

1. Place this script in a programmable block.
2. Place a ship controller.
3. Add "Horizon" to the name of text panels and text surfaces
    - Configure which surface the script is shown on in the
      Custom Data of the text surface (not needed for regular LCDs)
4. Enjoy!




=================================================
    DO NOT MODIFY VARIABLES IN THE SCRIPT!

 USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!

*/

const string VERSION = "1.9.1";
const string DATE = "2021/04/22";

#region Fields
List<IMyShipController> Controllers
{
    get
    {
        return _taggedControllers.Count > 0 ? _taggedControllers : _allControllers;
    }
}

double _altitudeTransitionThreshold = 1000;
IMyShipController reference = null;
IMyShipController lastActiveShipController = null;
string _textSurfaceNameTag = "Horizon";
string _soundBlockNameTag = "Horizon";
string _referenceNameTag = "Reference";
string _lastSetupResult = "";
Vector3D _sunRotationAxis = new Vector3D(0, -1, 0);
Color _skyColor = new Color(10, 30, 50);
Color _groundColor = new Color(10, 10, 10);
Color _spaceBackgroundColor = new Color(0, 0, 0);
Color _progradeColor = new Color(150, 150, 0);
Color _retrogradeColor = new Color(150, 0, 0);
Color _textColor = new Color(150, 150, 150, 100);
Color _textBoxColor = new Color(150, 150, 150, 100);
Color _textBoxBackground = new Color(10, 10, 10, 150);
Color _horizonLineColor = new Color(0, 0, 0);
Color _elevationLineColor = new Color(150, 150, 150);
Color _orientationLineColor = new Color(150, 150, 150);
Color _altitudeWarningColor = Color.Red;
Color _xAxisColor = new Color(100, 50, 0, 150);
Color _yAxisColor = new Color(0, 100, 0, 150);
Color _zAxisColor = new Color(0, 50, 100, 150);
bool _showXYZAxis = true;
double _collisionSoundInterval = 0.166;
double _timeToCollisionThreshold = 5;

double _collisionSoundTimeSum = 141;
bool _lastCollisionWarningState = false;
bool _clearSpriteCache = false;

#region Ini Keys
const string INI_SECTION_GENERAL = "Artificial Horizon - General";
const string INI_GENERAL_TEXT_NAME = "Text surface name tag";
const string INI_GENERAL_SOUND_NAME = "Collision warning sound name tag";
const string INI_GENERAL_SOUND_INTERVAL = "Collision warning sound loop interval (s)";
const string INI_GENERAL_COLLISION_THRESH = "Collision warning time threshold (s)";
const string INI_GENERAL_REF_NAME = "Optional reference name tag";
const string INI_GENERAL_ALT_TRANS = "Surface to Sealevel transition alt. (m)"; //TODO: Add comment
const string INI_GENERAL_SHOW_XYZ = "Show XYZ axes in space";
const string INI_GENERAL_SUN_ROTATION = "Sun rotation axis";

const string INI_SECTION_COLORS = "Artificial Horizon - Colors";
const string INI_COLORS_SKY = "Sky background";
const string INI_COLORS_GROUND = "Ground background";
const string INI_COLORS_SPACE = "Space background";
const string INI_COLORS_PROGRADE = "Prograde velocity";
const string INI_COLORS_RETROGRADE = "Retrograde velocity";
const string INI_COLORS_TEXT = "Text";
const string INI_COLORS_TEXT_BOX_OUTLINE = "Text box outline";
const string INI_COLORS_TEXT_BOX_BACKGROUND = "Text box background";
const string INI_COLORS_HORIZON_LINE = "Horizon line";
const string INI_COLORS_ELEVATION_LINE = "Elevation lines";
const string INI_COLORS_ORENTATION = "Orientation indicator";
const string INI_COLORS_X_AXIS = "Space x-axis";
const string INI_COLORS_Y_AXIS = "Space y-axis";
const string INI_COLORS_Z_AXIS = "Space z-axis";

const string INI_SECTION_TEXT_SURF = "Artificial Horizon - Text Surface Config";
const string INI_TEXT_SURF_TEMPLATE = "Show on screen {0}";
#endregion

const double TICK = 1.0 / 60.0;

readonly List<IMyShipController> _allControllers = new List<IMyShipController>();
readonly List<IMyShipController> _taggedControllers = new List<IMyShipController>();
readonly List<IMyTextSurface> _textSurfaces = new List<IMyTextSurface>();
readonly List<IMySoundBlock> _soundBlocks = new List<IMySoundBlock>();
readonly MyIni _ini = new MyIni();
readonly MyIni _textSurfaceIni = new MyIni();
readonly ArtificialHorizon _artificialHorizon;
readonly Scheduler _scheduler;
readonly ScheduledAction _scheduledSetup;
readonly RuntimeTracker _runtimeTracker;
readonly RunningSymbol _runningSymbol = new RunningSymbol(new string[] { "", ".", "..", "...", "....", "...", "..", "." });
readonly CircularBuffer<Action> _buffer;
#endregion

#region Main methods
Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;

    _runtimeTracker = new RuntimeTracker(this);

    _artificialHorizon = new ArtificialHorizon(
            _sunRotationAxis,
            _skyColor,
            _groundColor,
            _progradeColor,
            _retrogradeColor,
            _textColor,
            _textBoxColor,
            _textBoxBackground,
            _horizonLineColor,
            _elevationLineColor,
            _spaceBackgroundColor,
            _orientationLineColor,
            _altitudeWarningColor,
            _xAxisColor,
            _yAxisColor,
            _zAxisColor,
            _altitudeTransitionThreshold,
            _timeToCollisionThreshold,
            _showXYZAxis,
            this);

    Setup();

    _scheduledSetup = new ScheduledAction(Setup, 0.1);

    _scheduler = new Scheduler(this);

    float step = 1f / 9f;
    _buffer = new CircularBuffer<Action>(10);
    _buffer.Add(CalculateAHParams);
    _buffer.Add(() => UpdateScreens(0 * step, 1 * step));
    _buffer.Add(() => UpdateScreens(1 * step, 2 * step));
    _buffer.Add(() => UpdateScreens(2 * step, 3 * step));
    _buffer.Add(() => UpdateScreens(3 * step, 4 * step));
    _buffer.Add(() => UpdateScreens(4 * step, 5 * step));
    _buffer.Add(() => UpdateScreens(5 * step, 6 * step));
    _buffer.Add(() => UpdateScreens(6 * step, 7 * step));
    _buffer.Add(() => UpdateScreens(7 * step, 8 * step));
    _buffer.Add(() => UpdateScreens(8 * step, 9 * step));

    // Scheduled actions
    _scheduler.AddScheduledAction(_scheduledSetup);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(PlaySounds, 6);
    _scheduler.AddScheduledAction(MoveNextScreens, 60);
}

void Main(string arg)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void CalculateAHParams()
{
    reference = GetControlledShipController(Controllers); // Primary, get active controller
    if (reference == null)
    {
        if (lastActiveShipController != null)
        {
            // Backup, use last active controller
            reference = lastActiveShipController;
        }
        else if (reference == null && Controllers.Count != 0)
        {
            // Last case, resort to the first controller in the list
            reference = Controllers[0];
        }
        else
        {
            return;
        }
    }

    _artificialHorizon.CalculateParameters(reference, 6);
    lastActiveShipController = reference;
}

void PlaySounds()
{
    if (_soundBlocks.Count == 0)
        return;

    _collisionSoundTimeSum += 10 * TICK;

    if (_collisionSoundTimeSum < _collisionSoundInterval && _artificialHorizon.CollisionWarning == _lastCollisionWarningState)
        return;

    _collisionSoundTimeSum = 0;

    foreach (var block in _soundBlocks)
    {
        if (_artificialHorizon.CollisionWarning)
            block.Play();
        else
            block.Stop();
    }

    _lastCollisionWarningState = _artificialHorizon.CollisionWarning;
}

void MoveNextScreens()
{
    _buffer.MoveNext().Invoke();
}

void UpdateScreens(float startProportion, float endProportion)
{
    int start = (int)(startProportion * _textSurfaces.Count);
    int end = (int)(endProportion * _textSurfaces.Count);
    for (int i = start; i < end; ++i)
    {
        var textSurface = _textSurfaces[i];
        _artificialHorizon.Draw(textSurface, _clearSpriteCache);
    }
}

void PrintDetailedInfo()
{
    Echo($"WMI Artificial Horizon Redux{_runningSymbol.Iterate()}\n(Version {VERSION} - {DATE})\n\nCustomize variables in Custom Data!");
    Echo($"\nNext refresh in {Math.Max(_scheduledSetup.RunInterval - _scheduledSetup.TimeSinceLastRun, 0):N0} seconds");
    Echo($"{_lastSetupResult}");
    Echo($"Text surfaces: {_textSurfaces.Count}\n");
    Echo($"Reference seat:\n\"{(reference?.CustomName)}\"");
    Echo(_runtimeTracker.Write());
}
#endregion

#region Ini
void ParseIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        // General
        _textSurfaceNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_TEXT_NAME).ToString(_textSurfaceNameTag);
        _timeToCollisionThreshold = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_COLLISION_THRESH).ToDouble(_timeToCollisionThreshold);
        _soundBlockNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_SOUND_NAME).ToString(_soundBlockNameTag);
        _collisionSoundInterval = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_SOUND_INTERVAL).ToDouble(_collisionSoundInterval);
        _referenceNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_REF_NAME).ToString(_referenceNameTag);
        _altitudeTransitionThreshold = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_ALT_TRANS).ToDouble(_altitudeTransitionThreshold);
        _showXYZAxis = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_SHOW_XYZ).ToBoolean(_showXYZAxis);
        _sunRotationAxis = MyIniHelper.GetVector3D(INI_SECTION_GENERAL, INI_GENERAL_SUN_ROTATION, _ini, _sunRotationAxis);

        // Colors
        _skyColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_SKY, _ini, _skyColor);
        _groundColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_GROUND, _ini, _groundColor);
        _spaceBackgroundColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_SPACE, _ini, _spaceBackgroundColor);
        _progradeColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_PROGRADE, _ini, _progradeColor);
        _retrogradeColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_RETROGRADE, _ini, _retrogradeColor);
        _textColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_TEXT, _ini, _textColor);
        _textBoxColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_TEXT_BOX_OUTLINE, _ini, _textBoxColor);
        _textBoxBackground = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_TEXT_BOX_BACKGROUND, _ini, _textBoxBackground);
        _horizonLineColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_HORIZON_LINE, _ini, _horizonLineColor);
        _elevationLineColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_ELEVATION_LINE, _ini, _elevationLineColor);
        _orientationLineColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_ORENTATION, _ini, _orientationLineColor);
        _xAxisColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_X_AXIS, _ini, _xAxisColor);
        _yAxisColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_Y_AXIS, _ini, _yAxisColor);
        _zAxisColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLORS_Z_AXIS, _ini, _zAxisColor);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    WriteIni();
}

void WriteIni()
{
    // General
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_TEXT_NAME, _textSurfaceNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_COLLISION_THRESH, _timeToCollisionThreshold);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_SOUND_NAME, _soundBlockNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_SOUND_INTERVAL, _collisionSoundInterval);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_REF_NAME, _referenceNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_ALT_TRANS, _altitudeTransitionThreshold);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_SHOW_XYZ, _showXYZAxis);
    MyIniHelper.SetVector3D(INI_SECTION_GENERAL, INI_GENERAL_SUN_ROTATION, ref _sunRotationAxis, _ini);

    // Colors
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_SKY, _skyColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_GROUND, _groundColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_SPACE, _spaceBackgroundColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_PROGRADE, _progradeColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_RETROGRADE, _retrogradeColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_TEXT, _textColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_TEXT_BOX_OUTLINE, _textBoxColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_TEXT_BOX_BACKGROUND, _textBoxBackground, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_HORIZON_LINE, _horizonLineColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_ELEVATION_LINE, _elevationLineColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_ORENTATION, _orientationLineColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_X_AXIS, _xAxisColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_Y_AXIS, _yAxisColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLORS_Z_AXIS, _zAxisColor, _ini);

    _ini.SetComment(INI_SECTION_GENERAL, INI_GENERAL_COLLISION_THRESH, "Time before predicted collision that the AH will\nwarn you to pull up (-1 disables warning)");

    string output = _ini.ToString();
    if (!string.Equals(output, Me.CustomData))
        Me.CustomData = output;
}
#endregion

#region Setup
void Setup()
{
    _clearSpriteCache = !_clearSpriteCache;
    
    ParseIni();
    Log.Clear();

    if (_artificialHorizon != null)
    {
        _artificialHorizon.UpdateFields(
                _sunRotationAxis,
                _skyColor,
                _groundColor,
                _progradeColor,
                _retrogradeColor,
                _textColor,
                _textBoxColor,
                _textBoxBackground,
                _horizonLineColor,
                _elevationLineColor,
                _spaceBackgroundColor,
                _orientationLineColor,
                _altitudeWarningColor,
                _xAxisColor,
                _yAxisColor,
                _zAxisColor,
                _altitudeTransitionThreshold,
                _timeToCollisionThreshold,
                _showXYZAxis);
    }

    _textSurfaces.Clear();
    _taggedControllers.Clear();
    _allControllers.Clear();
    _soundBlocks.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, PopulateLists);

    if (_textSurfaces.Count == 0)
        Log.Error($"No text panels or text surface providers with name tag '{_textSurfaceNameTag}' were found.");

    if (_allControllers.Count == 0)
        Log.Error($"No ship controllers were found.");
    else
    {
        if (_taggedControllers.Count == 0)
            Log.Info($"No ship controllers with name tag \"{_referenceNameTag}\" were found. Using all available ship controllers. (This is NOT an error!)");
        else
            Log.Info($"One or more ship controllers with name tag \"{_referenceNameTag}\" were found. Using these to orient the artificial horizon.");
    }

    if (_soundBlocks.Count == 0)
        Log.Info($"No optional sound blocks with name tag \"{_soundBlockNameTag}\" were found. Sounds will not be played when ground collision is imminent.");

    _lastSetupResult = Log.Write();
}

bool PopulateLists(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    if (StringContains(block.CustomName, _textSurfaceNameTag))
    {
        AddTextSurfaces(block, _textSurfaces);
    }

    var controller = block as IMyShipController;
    if (controller != null)
    {
        _allControllers.Add(controller);
        if (StringContains(block.CustomName, _referenceNameTag))
            _taggedControllers.Add(controller);
        return false;
    }

    var sound = block as IMySoundBlock;
    if (sound != null && StringContains(block.CustomName, _soundBlockNameTag))
    {
        _soundBlocks.Add(sound);
        if (!sound.IsSoundSelected)
        {
            Log.Warning($"Sound block named \"{sound.CustomName}\" does not have a sound selected.");
        }
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

#region General methods
IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return null;
}

public static bool StringContains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
{
    return source?.IndexOf(toCheck, comp) >= 0;
}
#endregion

#region Artificial horizon
class ArtificialHorizon
{
    #region Fields
    public bool CollisionWarning { get; private set; } = false;

    Color _skyColor;
    Color _groundColor;
    Color _progradeColor;
    Color _retrogradeColor;
    Color _textColor;
    Color _horizonLineColor;
    Color _elevationLineColor;
    Color _spaceBackgroundColor;
    Color _textBoxBorderColor;
    Color _textBoxBackgroundColor;
    Color _orientationColor;
    Color _altitudeWarningColor;
    Color _xAxisColor;
    Color _yAxisColor;
    Color _zAxisColor;
    double _bearing;
    double _surfaceAltitude;
    double _sealevelAltitude;
    double _altitudeTransitionThreshold;
    double _lastSurfaceAltitude = 0;
    double _collisionTimeThreshold;
    double _verticalSpeed;
    float _speed;
    float _pitch;
    float _roll;
    float _rollCos;
    float _rollSin;
    float _acceleration;
    float _collisionTimeProportion;
    bool _inGravity;
    bool _movingBackwards;
    bool _showXYZAxis = true;
    bool _showPullUp = false;
    bool _lastCollisionWarning = false;
    Vector3D _sunRotationAxis;
    Vector3D _gravity;
    Vector3D _velocity;
    Vector3D _lastVelocity;
    Vector3D _axisZCosVector;
    Vector2 _flattenedVelocity;
    Vector2 _rollOffset;
    Vector2 _pitchOffset;
    Vector2 _xAxisFlattened;
    Vector2 _yAxisFlattened;
    Vector2 _zAxisFlattened;
    Vector2 _xAxisDirn;
    Vector2 _yAxisDirn;
    Vector2 _zAxisDirn;
    Vector2 _xAxisSign;
    Vector2 _yAxisSign;
    Vector2 _zAxisSign;

    Program _program;

    double Altitude
    {
        get
        {
            return _surfaceAltitude >= _altitudeTransitionThreshold ? _sealevelAltitude : _surfaceAltitude;
        }
    }

    string AltitudeLabel
    {
        get
        {
            return _surfaceAltitude >= _altitudeTransitionThreshold ? "Sea level" : "Surface";
        }
    }

    const string VERTICAL_SPEED = "Vertical";
    const string PULL_UP_TEXT = "PULL UP";
    const float PULL_UP_TEXT_SIZE = 1.5f;
    const float STATUS_TEXT_SIZE = 1.3f;
    const float ELEVATION_TEXT_SIZE = 0.8f;
    const float ELEVATION_CONSTANT = 1f;
    const float HORIZON_THICKNESS = 5f;
    const float VELOCITY_INDICATOR_LINE_WIDTH = 5f;
    const float ONE_OVER_HALF_PI = 1f / MathHelper.PiOver2;
    const float AXIS_LINE_WIDTH = 8f;
    const float AXIS_TEXT_OFFSET = 24f;
    const float AXIS_LENGTH_SCALE = 0.6f;
    readonly string[] _axisIcon = new string[3];
    readonly byte[] _axisDrawOrder = new byte[3];
    readonly Vector2 RETROGRADE_CROSS_SIZE = new Vector2(32, 4);
    readonly Vector2 VELOCITY_INDICATOR_SIZE = new Vector2(64, 64);
    readonly Vector2 ELEVATION_LADDER_SIZE = new Vector2(175, 32);
    readonly Vector2 TEXT_BOX_SIZE = new Vector2(120, 45);
    readonly Vector2 AXIS_MARKER_SIZE = new Vector2(24, 48);
    readonly Vector2 PULL_UP_CROSS_SIZE = new Vector2(128, 128);
    readonly StringBuilder _pullUpStringBuilder = new StringBuilder(8);
    readonly StringBuilder _heightStringBuilder = new StringBuilder();
    readonly Color _axisArrowBackColor = new Color(10, 10, 10);
    readonly CircularBuffer<double> _velocityBuffer = new CircularBuffer<double>(5);

    enum AxisEnum : byte { None = 0, X = 1, Y = 2, Z = 4 }
    const AxisEnum ALL_AXIS_ENUMS = AxisEnum.X | AxisEnum.Y | AxisEnum.Z;
    #endregion

    #region Ctor and updating fields
    public ArtificialHorizon(
            Vector3D sunRotationAxis,
            Color skyColor,
            Color groundColor,
            Color progradeColor,
            Color retrogradeColor,
            Color textColor,
            Color textBoxColor,
            Color textBackgroundColor,
            Color horizonLineColor,
            Color elevationLineColor,
            Color spaceBackgroundColor,
            Color orientationColor,
            Color altitudeWarningColor,
            Color xAxisColor,
            Color yAxisColor,
            Color zAxisColor,
            double altitudeTransitionThreshold,
            double collisionTimeThreshold,
            bool showXYZAxis,
            Program program)
    {
        UpdateFields(
                sunRotationAxis,
                skyColor,
                groundColor,
                progradeColor,
                retrogradeColor,
                textColor,
                textBoxColor,
                textBackgroundColor,
                horizonLineColor,
                elevationLineColor,
                spaceBackgroundColor,
                orientationColor,
                altitudeWarningColor,
                xAxisColor,
                yAxisColor,
                zAxisColor,
                altitudeTransitionThreshold,
                collisionTimeThreshold,
                showXYZAxis);
        _pullUpStringBuilder.Append(PULL_UP_TEXT);
        _heightStringBuilder.Append("X");
        _program = program;
    }

    public void UpdateFields(
            Vector3D sunRotationAxis,
            Color skyColor,
            Color groundColor,
            Color progradeColor,
            Color retrogradeColor,
            Color textColor,
            Color textBoxColor,
            Color textBackgroundColor,
            Color horizonLineColor,
            Color elevationLineColor,
            Color spaceBackgroundColor,
            Color orientationColor,
            Color altitudeWarningColor,
            Color xAxisColor,
            Color yAxisColor,
            Color zAxisColor,
            double altitudeTransitionThreshold,
            double collisionTimeThreshold,
            bool showXYZAxis)
    {
        _sunRotationAxis = sunRotationAxis;
        _skyColor = skyColor;
        _groundColor = groundColor;
        _progradeColor = progradeColor;
        _retrogradeColor = retrogradeColor;
        _textColor = textColor;
        _textBoxBorderColor = textBoxColor;
        _textBoxBackgroundColor = textBackgroundColor;
        _horizonLineColor = horizonLineColor;
        _elevationLineColor = elevationLineColor;
        _spaceBackgroundColor = spaceBackgroundColor;
        _orientationColor = orientationColor;
        _altitudeWarningColor = altitudeWarningColor;
        _xAxisColor = xAxisColor;
        _yAxisColor = yAxisColor;
        _zAxisColor = zAxisColor;
        _altitudeTransitionThreshold = altitudeTransitionThreshold;
        _collisionTimeThreshold = collisionTimeThreshold;
        _showXYZAxis = showXYZAxis;
    }
    #endregion

    #region Cached calculations
    public void CalculateParameters(IMyShipController controller, double updatesPerSecond)
    {
        _velocity = controller.GetShipVelocities().LinearVelocity;
        _acceleration = (float)((_velocity - _lastVelocity) * updatesPerSecond).Length();
        _lastVelocity = _velocity;

        if (!Vector3D.IsZero(_velocity, 1e-2))
        {
            Vector3D velocityNorm = _velocity;
            _speed = (float)velocityNorm.Normalize();
            Vector3D localVelocity = Vector3D.Rotate(velocityNorm, MatrixD.Transpose(controller.WorldMatrix));
            _flattenedVelocity.X = (float)Math.Asin(MathHelper.Clamp(localVelocity.X, -1, 1)) * ONE_OVER_HALF_PI;
            _flattenedVelocity.Y = (float)Math.Asin(MathHelper.Clamp(-localVelocity.Y, -1, 1)) * ONE_OVER_HALF_PI;
            _movingBackwards = localVelocity.Z > 1e-3;
        }
        else
        {
            _speed = 0;
            _flattenedVelocity = Vector2.Zero;
            _movingBackwards = false;
        }

        _gravity = controller.GetNaturalGravity();
        _inGravity = !Vector3D.IsZero(_gravity);
        if (_inGravity)
        {
            CalculateArtificialHorizonParameters(controller, updatesPerSecond);
        }
        else
        {
            CalculateSpaceParameters(controller);
        }
    }

    void CalculateArtificialHorizonParameters(IMyShipController controller, double updatesPerSecond)
    {
        Vector3D up = -_gravity;
        Vector3D left = Vector3D.Cross(up, controller.WorldMatrix.Forward);
        Vector3D forward = Vector3D.Cross(left, up);

        var localUpVector = Vector3D.Rotate(up, MatrixD.Transpose(controller.WorldMatrix));
        var flattenedUpVector = new Vector3D(localUpVector.X, localUpVector.Y, 0);
        _roll = (float)VectorMath.AngleBetween(flattenedUpVector, Vector3D.Up) * Math.Sign(Vector3D.Dot(Vector3D.Right, flattenedUpVector));
        _pitch = (float)VectorMath.AngleBetween(forward, controller.WorldMatrix.Forward) * Math.Sign(Vector3D.Dot(up, controller.WorldMatrix.Forward));

        _rollCos = MyMath.FastCos(_roll);
        _rollSin = MyMath.FastSin(_roll);

        double alt;
        controller.TryGetPlanetElevation(MyPlanetElevation.Surface, out alt);
        _surfaceAltitude = alt;

        controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out alt);
        _sealevelAltitude = alt;

        _velocityBuffer.Add((_lastSurfaceAltitude - _surfaceAltitude) * updatesPerSecond);
        double velocitySum = 0;
        for (int i = 0; i < _velocityBuffer.Capacity; ++i)
        {
            velocitySum += _velocityBuffer.MoveNext();
        }
        
        double terrainHeightDerivative = velocitySum / _velocityBuffer.Capacity;
        double timeTillGroundCollision = _surfaceAltitude / (terrainHeightDerivative);
        _collisionTimeProportion = (float)(timeTillGroundCollision / _collisionTimeThreshold);
        CollisionWarning = terrainHeightDerivative > 0 && _speed > 10 && timeTillGroundCollision <= _collisionTimeThreshold;
        if (_lastCollisionWarning != CollisionWarning)
            _showPullUp = true;
        else
            _showPullUp = !_showPullUp;

        _lastCollisionWarning = CollisionWarning;
        _lastSurfaceAltitude = _surfaceAltitude;

        Vector3D eastVec = Vector3D.Cross(_gravity, _sunRotationAxis);
        Vector3D northVec = Vector3D.Cross(eastVec, _gravity);
        Vector3D heading = VectorMath.Rejection(controller.WorldMatrix.Forward, _gravity);

        _bearing = MathHelper.ToDegrees(VectorMath.AngleBetween(heading, northVec));
        if (Vector3D.Dot(controller.WorldMatrix.Forward, eastVec) < 0)
            _bearing = 360 - _bearing;
            
        _verticalSpeed = VectorMath.ScalarProjection(_velocity, -_gravity);
    }

    void CalculateSpaceParameters(IMyShipController controller)
    {
        // Flattening axes onto the screen surface
        MatrixD transposedMatrix = MatrixD.Transpose(controller.WorldMatrix);
        Vector3D xTrans = Vector3D.Rotate(Vector3D.UnitX, transposedMatrix);
        Vector3D yTrans = Vector3D.Rotate(Vector3D.UnitY, transposedMatrix);
        Vector3D zTrans = Vector3D.Rotate(Vector3D.UnitZ, transposedMatrix);

        _xAxisFlattened.X = (float)(xTrans.X) * AXIS_LENGTH_SCALE;
        _xAxisFlattened.Y = (float)(-xTrans.Y) * AXIS_LENGTH_SCALE;
        _yAxisFlattened.X = (float)(yTrans.X) * AXIS_LENGTH_SCALE;
        _yAxisFlattened.Y = (float)(-yTrans.Y) * AXIS_LENGTH_SCALE;
        _zAxisFlattened.X = (float)(zTrans.X) * AXIS_LENGTH_SCALE;
        _zAxisFlattened.Y = (float)(-zTrans.Y) * AXIS_LENGTH_SCALE;

        // Getting non-zero sign of vectors for positioning the axis label text
        _xAxisSign = Vector2.SignNonZero(_xAxisFlattened);
        _yAxisSign = Vector2.SignNonZero(_yAxisFlattened);
        _zAxisSign = Vector2.SignNonZero(_zAxisFlattened);

        // Get normalized axis directions for drawing arrow heads
        if (!Vector2.IsZero(ref _xAxisFlattened, MathHelper.EPSILON))
            _xAxisDirn = Vector2.Normalize(_xAxisFlattened);

        if (!Vector2.IsZero(ref _yAxisFlattened, MathHelper.EPSILON))
            _yAxisDirn = Vector2.Normalize(_yAxisFlattened);

        if (!Vector2.IsZero(ref _zAxisFlattened, MathHelper.EPSILON))
            _zAxisDirn = Vector2.Normalize(_zAxisFlattened);

        // Getting the icons for the axes based on if they are pointing at or away from the user
        _axisIcon[0] = GetAxisIcon(xTrans.Z);
        _axisIcon[1] = GetAxisIcon(yTrans.Z);
        _axisIcon[2] = GetAxisIcon(zTrans.Z);

        _axisZCosVector = new Vector3D(xTrans.Z, yTrans.Z, zTrans.Z);
        double max = _axisZCosVector.Max();
        double min = _axisZCosVector.Min();

        // Determining the order to draw the axes so that perspective looks correct.
        AxisEnum usedAxes = AxisEnum.None;
        if (max == _axisZCosVector.X)
        {
            _axisDrawOrder[2] = (byte)AxisEnum.X;
            usedAxes |= AxisEnum.X;
        }
        else if (max == _axisZCosVector.Y)
        {
            _axisDrawOrder[2] = (byte)AxisEnum.Y;
            usedAxes |= AxisEnum.Y;
        }
        else
        {
            _axisDrawOrder[2] = (byte)AxisEnum.Z;
            usedAxes |= AxisEnum.Z;
        }

        if (min == _axisZCosVector.X)
        {
            _axisDrawOrder[0] = (byte)AxisEnum.X;
            usedAxes |= AxisEnum.X;

        }
        else if (min == _axisZCosVector.Y)
        {
            _axisDrawOrder[0] = (byte)AxisEnum.Y;
            usedAxes |= AxisEnum.Y;
        }
        else
        {
            _axisDrawOrder[0] = (byte)AxisEnum.Z;
            usedAxes |= AxisEnum.Z;
        }

        _axisDrawOrder[1] = (byte)MathHelper.Clamp((byte)(ALL_AXIS_ENUMS & ~usedAxes), 0, (byte)ALL_AXIS_ENUMS);
    }

    string GetAxisIcon(double z)
    {
        return z < 0 ? "CircleHollow" : "Circle";
    }
    #endregion

    #region Draw functions
    public void Draw(IMyTextSurface surface, bool clearSpriteCache)
    {
        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";
        surface.BackgroundAlpha = 0;
        surface.ScriptBackgroundColor = _inGravity ? _groundColor : _spaceBackgroundColor;

        Vector2 surfaceSize = surface.TextureSize;
        Vector2 screenCenter = surfaceSize * 0.5f;
        Vector2 avgViewportSize = surface.SurfaceSize - 12f;

        Vector2 scaleVec = (surfaceSize + avgViewportSize) * 0.5f / 512f;
        float scale = Math.Min(scaleVec.X, scaleVec.Y);
        float minSideLength = Math.Min(avgViewportSize.X, avgViewportSize.Y);
        Vector2 squareViewportSize = new Vector2(minSideLength, minSideLength);
        avgViewportSize = (avgViewportSize + squareViewportSize) * 0.5f;

        using (var frame = surface.DrawFrame())
        {
            if (clearSpriteCache)
            {
                frame.Add(new MySprite());
            }
            
            if (_inGravity)
            {
                DrawArtificialHorizon(frame, screenCenter, scale, minSideLength);
                DrawTextBoxes(frame, surface, screenCenter, avgViewportSize, scale, $"{_speed:n1}", $"{Altitude:0}", $"{_bearing:0}°");
                DrawAltitudeWarning(frame, screenCenter, avgViewportSize, scale, surface);
            }
            else
            {
                DrawSpace(frame, screenCenter, minSideLength * 0.5f, scale);
                DrawTextBoxes(frame, surface, screenCenter, avgViewportSize, scale, $"{_speed:n1}", $"{_acceleration:n1}");
            }

            // Draw orientation indicator
            DrawLine(frame, new Vector2(0, screenCenter.Y), new Vector2(screenCenter.X - 64 * scale, screenCenter.Y), HORIZON_THICKNESS * scale, _orientationColor);
            DrawLine(frame, new Vector2(screenCenter.X + 64 * scale, screenCenter.Y), new Vector2(screenCenter.X * 2f, screenCenter.Y), HORIZON_THICKNESS * scale, _orientationColor);

            Vector2 scaledIconSize = VELOCITY_INDICATOR_SIZE * scale;
            MySprite centerSprite = new MySprite(SpriteType.TEXTURE, "AH_BoreSight", size: scaledIconSize * 1.2f, position: screenCenter + Vector2.UnitY * scaledIconSize * 0.5f, color: _orientationColor);
            centerSprite.RotationOrScale = -MathHelper.PiOver2;
            frame.Add(centerSprite);

            // Draw velocity indicator
            MySprite velocitySprite = new MySprite(SpriteType.TEXTURE, "AH_VelocityVector", size: scaledIconSize, color: !_movingBackwards ? _progradeColor : _retrogradeColor);
            float sign = _movingBackwards ? -1 : 1;
            velocitySprite.Position = screenCenter + (squareViewportSize * 0.5f * _flattenedVelocity * sign);
            frame.Add(velocitySprite);

            if (_movingBackwards)
            {
                Vector2 retrogradeCrossSize = RETROGRADE_CROSS_SIZE * scale;
                MySprite retrograteSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: retrogradeCrossSize, color: _retrogradeColor);
                retrograteSprite.Position = velocitySprite.Position;
                retrograteSprite.RotationOrScale = MathHelper.PiOver4;
                frame.Add(retrograteSprite);
                retrograteSprite.RotationOrScale += MathHelper.PiOver2;
                frame.Add(retrograteSprite);
            }
        }
    }

    void DrawTextBoxes(MySpriteDrawFrame frame, IMyTextSurface surface, Vector2 screenCenter, Vector2 screenSize, float scale, string leftText, string rightText, string topText = "")
    {
        Vector2 boxSize = TEXT_BOX_SIZE * scale;
        float textSize = STATUS_TEXT_SIZE * scale;
        Vector2 leftBoxPos = screenCenter + new Vector2(-0.5f * (screenSize.X - boxSize.X), boxSize.Y * 0.5f);//+ new Vector2(screenSize.X * -0.40f, boxSize.Y * 0.5f);
        Vector2 rightBoxPos = screenCenter + new Vector2(0.5f * (screenSize.X - boxSize.X), boxSize.Y * 0.5f); //+ new Vector2(screenSize.X * 0.40f, boxSize.Y * 0.5f);
        string leftTitle = "SPEED";
        string rightTitle = _inGravity ? "ALT" : "ACCEL";

        DrawTextBox(frame, surface, boxSize, leftBoxPos, _textColor, _textBoxBorderColor, _textBoxBackgroundColor, textSize, leftText, leftTitle);
        DrawTextBox(frame, surface, boxSize, rightBoxPos, _textColor, _textBoxBorderColor, _textBoxBackgroundColor, textSize, rightText, rightTitle);

        if (_inGravity)
        {
            MySprite altMode = MySprite.CreateText(AltitudeLabel, "Debug", _textColor, textSize * 0.75f, TextAlignment.CENTER);
            altMode.Position = screenCenter + new Vector2(0.5f * (screenSize.X - boxSize.X), boxSize.Y * 1.0f);
            frame.Add(altMode);
            
            MySprite verticalSpeedLabel = MySprite.CreateText(VERTICAL_SPEED, "Debug", _textColor, textSize * 0.75f, TextAlignment.CENTER);
            verticalSpeedLabel.Position = screenCenter + new Vector2(-0.5f * (screenSize.X - boxSize.X), boxSize.Y * 1.0f);
            frame.Add(verticalSpeedLabel);
            
            MySprite verticalSpeed = MySprite.CreateText($"{_verticalSpeed:n1}", "Debug", _textColor, textSize * 0.75f, TextAlignment.CENTER);
            verticalSpeed.Position = screenCenter + new Vector2(-0.5f * (screenSize.X - boxSize.X), boxSize.Y * 1.5f);
            frame.Add(verticalSpeed);
        }

        if (!string.IsNullOrWhiteSpace(topText))
        {
            Vector2 topBoxPos = screenCenter + new Vector2(0, screenSize.Y * -0.40f);
            DrawTextBox(frame, surface, boxSize, topBoxPos, _textColor, _textBoxBorderColor, _textBoxBackgroundColor, textSize, topText); //, drawBackground: false);
        }
    }

    void DrawTextBox(MySpriteDrawFrame frame, IMyTextSurface surface, Vector2 size, Vector2 position, Color textColor, Color borderColor, Color backgroundColor, float textSize, string text, string title = "")
    {
        Vector2 measuredTextSize = surface.MeasureStringInPixels(_heightStringBuilder, "Debug", textSize);
        
        Vector2 textPos = position;
        textPos.Y -= measuredTextSize.Y * 0.5f;

        Vector2 titlePos = position;
        titlePos.Y -= (size.Y + measuredTextSize.Y * 0.5f);

        MySprite background = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: backgroundColor, size: size);
        background.Position = position;
        frame.Add(background);

        MySprite perimeter = new MySprite(SpriteType.TEXTURE, "AH_TextBox", color: borderColor, size: size);
        perimeter.Position = position;

        MySprite textSprite = MySprite.CreateText(text, "Debug", textColor, scale: textSize);
        textSprite.Position = textPos;

        frame.Add(perimeter);
        frame.Add(textSprite);

        if (!string.IsNullOrWhiteSpace(title))
        {
            MySprite titleSprite = MySprite.CreateText(title, "Debug", textColor, scale: textSize);
            titleSprite.Position = titlePos;
            frame.Add(titleSprite);
        }
    }

    void DrawArtificialHorizon(MySpriteDrawFrame frame, Vector2 screenCenter, float scale, float minSideLength)
    {
        Vector2 skySpriteSize = screenCenter * 6f;
        _rollOffset.Y = skySpriteSize.Y * 0.5f * (1 - _rollCos);
        _rollOffset.X = skySpriteSize.Y * 0.5f * (_rollSin);
        _pitchOffset.Y = _rollCos * minSideLength * 0.5f;
        _pitchOffset.X = -_rollSin * minSideLength * 0.5f;
        float pitchProportion = _pitch / MathHelper.PiOver2;

        MySprite skySprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _skyColor, size: skySpriteSize);
        skySprite.RotationOrScale = _roll;

        Vector2 skyMidPt = screenCenter + new Vector2(0, -skySpriteSize.Y * 0.5f); //surfaceSize.Y * new Vector2(0.5f, -1f);
        skySprite.Position = skyMidPt + _rollOffset + _pitchOffset * pitchProportion;
        frame.Add(skySprite);

        // Draw horizon line
        MySprite horizonLineSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _horizonLineColor, size: new Vector2(skySpriteSize.X, HORIZON_THICKNESS * scale));
        horizonLineSprite.RotationOrScale = _roll;
        horizonLineSprite.Position = screenCenter + _pitchOffset * pitchProportion;
        frame.Add(horizonLineSprite);

        for (int i = -90; i <= 90; i += 30)
        {
            if (i == 0)
                continue;
            DrawElevationLadder(frame, screenCenter, ELEVATION_LADDER_SIZE, pitchProportion, i, scale, _elevationLineColor, true);
        }
    }

    void DrawAltitudeWarning(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 screenSize, float scale, IMyTextSurface surface)
    {
        if (CollisionWarning)
        {
            if (_showPullUp)
            {
                float textSize = PULL_UP_TEXT_SIZE * scale;
                Vector2 textBoxSize = surface.MeasureStringInPixels(_pullUpStringBuilder, "Debug", textSize) + scale * 24f;
                Vector2 textPosition = screenCenter + new Vector2(0, screenSize.Y * 0.25f);
                DrawTextBox(frame, surface, textBoxSize, textPosition, _altitudeWarningColor, _altitudeWarningColor, _textBoxBackgroundColor, textSize, PULL_UP_TEXT);
            }

            Vector2 warningCrossSize = PULL_UP_CROSS_SIZE * scale;
            Vector2 warningCrossPosition = new Vector2(-screenSize.X * 0.5f * _collisionTimeProportion, 0);
            MySprite warningCrossHalf = MySprite.CreateSprite("AH_BoreSight", screenCenter + warningCrossPosition, warningCrossSize);
            warningCrossHalf.Color = _altitudeWarningColor;
            warningCrossHalf.RotationOrScale = 0;

            frame.Add(warningCrossHalf);

            warningCrossHalf.RotationOrScale = MathHelper.Pi;
            warningCrossHalf.Position = screenCenter - warningCrossPosition;

            frame.Add(warningCrossHalf);
        }
    }

    void DrawElevationLadder(MySpriteDrawFrame frame, Vector2 midPoint, Vector2 size, float basePitchProportion, float elevationAngleDeg, float scale, Color color, bool drawText)
    {
        float pitchProportion = MathHelper.ToRadians(-elevationAngleDeg) / MathHelper.PiOver2;
        string textureName = pitchProportion <= 0 ? "AH_GravityHudPositiveDegrees" : "AH_GravityHudNegativeDegrees";
        Vector2 scaledSize = size * scale;

        MySprite ladderSprite = new MySprite(SpriteType.TEXTURE, textureName, color: _elevationLineColor, size: scaledSize);
        ladderSprite.RotationOrScale = _roll + (pitchProportion <= 0 ? MathHelper.Pi : 0);
        ladderSprite.Position = midPoint + (pitchProportion + basePitchProportion) * _pitchOffset;
        frame.Add(ladderSprite);

        if (!drawText)
            return;

        Vector2 textHorizontalOffset = new Vector2(_rollCos, _rollSin) * (scaledSize.X + 48f * scale) * 0.5f;
        Vector2 textVerticalOffset = Vector2.UnitY * -24f * scale * (pitchProportion <= 0 ? 0 : 1);

        MySprite text = MySprite.CreateText($"{elevationAngleDeg}", "Debug", _elevationLineColor);
        text.RotationOrScale = ELEVATION_TEXT_SIZE * scale;
        text.Position = ladderSprite.Position + textHorizontalOffset + textVerticalOffset;
        frame.Add(text);

        text.Position = ladderSprite.Position - textHorizontalOffset + textVerticalOffset;
        frame.Add(text);
    }

    void DrawSpace(MySpriteDrawFrame frame, Vector2 screenCenter, float halfExtent, float scale)
    {
        if (!_showXYZAxis)
            return;

        float textSize = scale * STATUS_TEXT_SIZE;
        float lineSize = scale * AXIS_LINE_WIDTH;
        float offset = scale * AXIS_TEXT_OFFSET;
        Vector2 markerSize = scale * AXIS_MARKER_SIZE;
        Vector2 xPos = screenCenter + _xAxisFlattened * halfExtent;
        Vector2 yPos = screenCenter + _yAxisFlattened * halfExtent;
        Vector2 zPos = screenCenter + _zAxisFlattened * halfExtent;

        MySprite xLine = DrawLine(screenCenter, xPos, lineSize, _xAxisColor);
        MySprite yLine = DrawLine(screenCenter, yPos, lineSize, _yAxisColor);
        MySprite zLine = DrawLine(screenCenter, zPos, lineSize, _zAxisColor);

        MySprite xLabel = MySprite.CreateText("X", "Debug", _xAxisColor, textSize, TextAlignment.CENTER);
        xLabel.Position = xPos + offset * _xAxisSign - Vector2.UnitY * markerSize.Y;

        MySprite yLabel = MySprite.CreateText("Y", "Debug", _yAxisColor, textSize, TextAlignment.CENTER);
        yLabel.Position = yPos + offset * _yAxisSign - Vector2.UnitY * markerSize.Y; ;

        MySprite zLabel = MySprite.CreateText("Z", "Debug", _zAxisColor, textSize, TextAlignment.CENTER);
        zLabel.Position = zPos + offset * _zAxisSign - Vector2.UnitY * markerSize.Y; ;

        foreach (var axis in _axisDrawOrder)
        {
            if (axis == (byte)AxisEnum.X)
            {
                DrawArrowHead(frame, xPos, AXIS_MARKER_SIZE * scale, _xAxisDirn, _axisZCosVector.X, _xAxisColor, _axisArrowBackColor);
                frame.Add(xLine);
                frame.Add(xLabel);
            }
            else if (axis == (byte)AxisEnum.Y)
            {
                DrawArrowHead(frame, yPos, AXIS_MARKER_SIZE * scale, _yAxisDirn, _axisZCosVector.Y, _yAxisColor, _axisArrowBackColor);
                frame.Add(yLine);
                frame.Add(yLabel);
            }
            else
            {
                DrawArrowHead(frame, zPos, AXIS_MARKER_SIZE * scale, _zAxisDirn, _axisZCosVector.Z, _zAxisColor, _axisArrowBackColor);
                frame.Add(zLine);
                frame.Add(zLabel);
            }
        }
    }

    MySprite DrawLine(Vector2 point1, Vector2 point2, float width, Color color)
    {
        Vector2 position = 0.5f * (point1 + point2);
        Vector2 diff = point1 - point2;
        float length = diff.Length();
        if (length > 0)
            diff /= length;

        Vector2 size = new Vector2(length, width);
        float angle = (float)Math.Acos(Vector2.Dot(diff, Vector2.UnitX));
        angle *= Math.Sign(Vector2.Dot(diff, Vector2.UnitY));

        MySprite sprite = MySprite.CreateSprite("SquareSimple", position, size);
        sprite.RotationOrScale = angle;
        sprite.Color = color;
        return sprite;
    }

    void DrawLine(MySpriteDrawFrame frame, Vector2 point1, Vector2 point2, float width, Color color)
    {
        Vector2 position = 0.5f * (point1 + point2);
        Vector2 diff = point1 - point2;
        float length = diff.Length();
        if (length > 0)
            diff /= length;

        Vector2 size = new Vector2(length, width);
        float angle = (float)Math.Acos(Vector2.Dot(diff, Vector2.UnitX));
        angle *= Math.Sign(Vector2.Dot(diff, Vector2.UnitY));

        MySprite sprite = MySprite.CreateSprite("SquareSimple", position, size);
        sprite.RotationOrScale = angle;
        sprite.Color = color;
        frame.Add(sprite);
    }

    void DrawArrowHead(MySpriteDrawFrame frame, Vector2 position, Vector2 arrowSize, Vector2 flattenedDirection, double depthSin, Color color, Color backColor)
    {
        if (Math.Abs(flattenedDirection.LengthSquared() - 1) < MathHelper.EPSILON)
            flattenedDirection.Normalize();

        arrowSize.Y *= (float)Math.Sqrt(1 - depthSin * depthSin);
        Vector2 baseSize = Vector2.One * arrowSize.X;
        baseSize.Y *= (float)Math.Abs(depthSin);

        float angle = (float)Math.Acos(Vector2.Dot(flattenedDirection, -Vector2.UnitY));
        angle *= Math.Sign(Vector2.Dot(flattenedDirection, Vector2.UnitX));

        Vector2 trianglePosition = position + flattenedDirection * arrowSize.Y * 0.5f;

        MySprite circle = MySprite.CreateSprite("Circle", position, baseSize);
        circle.Color = depthSin >= 0 ? color : backColor;
        circle.RotationOrScale = angle;

        MySprite triangle = MySprite.CreateSprite("Triangle", trianglePosition, arrowSize);
        triangle.Color = color;
        triangle.RotationOrScale = angle;

        frame.Add(triangle);
        frame.Add(circle);
    }
    #endregion
}
#endregion

#region Other classes
#region MyIni helper
public static class MyIniHelper
{
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void SetVector3D(string sectionName, string vectorName, ref Vector3D vector, MyIni ini)
    {
        ini.Set(sectionName, vectorName, vector.ToString());
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector3D GetVector3D(string sectionName, string vectorName, MyIni ini, Vector3D? defaultVector = null)
    {
        var vector = Vector3D.Zero;
        if (Vector3D.TryParse(ini.Get(sectionName, vectorName).ToString(), out vector))
            return vector;
        else if (defaultVector.HasValue)
            return defaultVector.Value;
        return default(Vector3D);
    }

    /// <summary>
    /// Adds a Color to a MyIni object
    /// </summary>
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini)
    {
        string colorString = string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);
        ini.Set(sectionName, itemName, colorString);
    }

    /// <summary>
    /// Parses a MyIni for a Color
    /// </summary>
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

#region Vector math
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
        Vector3D project_a = Projection(a, b);
        Vector3D reject_a = a - project_a;
        return project_a - reject_a * rejectionFactor;
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
    /// Computes angle between 2 vectors
    /// </summary>
    public static double AngleBetween(Vector3D a, Vector3D b) //returns radians
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;
        else
            return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
    }

    /// <summary>
    /// Computes cosine of the angle between 2 vectors
    /// </summary>
    public static double CosBetween(Vector3D a, Vector3D b, bool useSmallestAngle = false) //returns radians
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
    public static bool IsDotProductWithinTolerance(Vector3D a, Vector3D b, double tolerance)
    {
        double dot = Vector3D.Dot(a, b);
        double num = a.LengthSquared() * b.LengthSquared() * tolerance * tolerance;
        return dot * dot > num;
    }
}
#endregion

#region Circular Buffer
/// <summary>
/// A simple, generic circular buffer class with a fixed capacity.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CircularBuffer<T>
{
    public readonly int Capacity;

    T[] _array = null;
    int _setIndex = 0;
    int _getIndex = 0;

    /// <summary>
    /// CircularBuffer ctor.
    /// </summary>
    /// <param name="capacity">Capacity of the CircularBuffer.</param>
    public CircularBuffer(int capacity)
    {
        if (capacity < 1)
            throw new Exception($"Capacity of CircularBuffer ({capacity}) can not be less than 1");
        Capacity = capacity;
        _array = new T[Capacity];
    }

    /// <summary>
    /// Adds an item to the buffer. If the buffer is full, it will overwrite the oldest value.
    /// </summary>
    /// <param name="item"></param>
    public void Add(T item)
    {
        _array[_setIndex] = item;
        _setIndex = ++_setIndex % Capacity;
    }

    /// <summary>
    /// Retrieves the current item in the buffer and increments the buffer index.
    /// </summary>
    /// <returns></returns>
    public T MoveNext()
    {
        T val = _array[_getIndex];
        _getIndex = ++_getIndex % Capacity;
        return val;
    }

    /// <summary>
    /// Retrieves the current item in the buffer without incrementing the buffer index.
    /// </summary>
    /// <returns></returns>
    public T Peek()
    {
        return _array[_getIndex];
    }
}
#endregion

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    
    readonly bool _ignoreFirstRun;
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
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);
        
        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;
        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        // Remove all actions that we should dispose
        _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

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
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false, double timeOffset = 0)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun, timeOffset);
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        _scheduledActions.Add(scheduledAction);
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
    public readonly double RunInterval;

    readonly double _runFrequency;
    readonly Action _action;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        _runFrequency = runFrequency;
        RunInterval = 1.0 / _runFrequency;
        DisposeAfterRun = removeAfterRun;
        TimeSinceLastRun = timeOffset;
    }

    public void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun >= RunInterval)
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

#region Runtime tracking
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

#region Running symbol
public class RunningSymbol
{
    int _runningSymbolVariant = 0;
    int _runningSymbolCount = 0;
    int _increment = 1;
    string[] _runningSymbols = new string[] { "−", "\\", "|", "/" };

    public RunningSymbol() { }

    public RunningSymbol(int increment)
    {
        _increment = increment;
    }

    public RunningSymbol(string[] runningSymbols)
    {
        if (runningSymbols.Length != 0)
            _runningSymbols = runningSymbols;
    }

    public RunningSymbol(int increment, string[] runningSymbols)
    {
        _increment = increment;
        if (runningSymbols.Length != 0)
            _runningSymbols = runningSymbols;
    }

    public string Iterate(int ticks = 1)
    {
        if (_runningSymbolCount >= _increment)
        {
            _runningSymbolCount = 0;
            _runningSymbolVariant++;
            _runningSymbolVariant = _runningSymbolVariant++ % _runningSymbols.Length;
        }
        _runningSymbolCount += ticks;

        return this.ToString();
    }

    public override string ToString()
    {
        return _runningSymbols[_runningSymbolVariant];
    }
}
#endregion

#region Script Logging
public static class Log
{
    static StringBuilder _builder = new StringBuilder();
    static List<string> _errorList = new List<string>();
    static List<string> _warningList = new List<string>();
    static List<string> _infoList = new List<string>();
    const int _logWidth = 530; //chars, conservative estimate

    public static void Clear()
    {
        _builder.Clear();
        _errorList.Clear();
        _warningList.Clear();
        _infoList.Clear();
    }

    public static void Error(string text)
    {
        _errorList.Add(text);
    }

    public static void Warning(string text)
    {
        _warningList.Add(text);
    }

    public static void Info(string text)
    {
        _infoList.Add(text);
    }

    public static string Write(bool preserveLog = false)
    {
        if (_errorList.Count != 0 && _warningList.Count != 0 && _infoList.Count != 0)
            WriteLine("");

        if (_errorList.Count != 0)
        {
            for (int i = 0; i < _errorList.Count; i++)
            {
                WriteLine("");
                WriteElememt(i + 1, "ERROR", _errorList[i]);
                //if (i < _errorList.Count - 1)
            }
        }

        if (_warningList.Count != 0)
        {
            for (int i = 0; i < _warningList.Count; i++)
            {
                WriteLine("");
                WriteElememt(i + 1, "WARNING", _warningList[i]);
                //if (i < _warningList.Count - 1)
            }
        }

        if (_infoList.Count != 0)
        {
            for (int i = 0; i < _infoList.Count; i++)
            {
                WriteLine("");
                WriteElememt(i + 1, "Info", _infoList[i]);
                //if (i < _infoList.Count - 1)
            }
        }

        string output = _builder.ToString();

        if (!preserveLog)
            Clear();

        return output;
    }

    private static void WriteElememt(int index, string header, string content)
    {
        WriteLine($"{header} {index}:");

        string wrappedContent = TextHelper.WrapText(content, 1, _logWidth);
        string[] wrappedSplit = wrappedContent.Split('\n');

        foreach (var line in wrappedSplit)
        {
            _builder.Append("  ").Append(line).Append('\n');
        }
    }

    private static void WriteLine(string text)
    {
        _builder.Append(text).Append('\n');
    }
}

// Whip's TextHelper Class v2
public class TextHelper
{
    static StringBuilder textSB = new StringBuilder();
    const float adjustedPixelWidth = (512f / 0.778378367f);
    const int monospaceCharWidth = 24 + 1; //accounting for spacer
    const int spaceWidth = 8;

    #region bigass dictionary
    static Dictionary<char, int> _charWidths = new Dictionary<char, int>()
{
{'.', 9},
{'!', 8},
{'?', 18},
{',', 9},
{':', 9},
{';', 9},
{'"', 10},
{'\'', 6},
{'+', 18},
{'-', 10},

{'(', 9},
{')', 9},
{'[', 9},
{']', 9},
{'{', 9},
{'}', 9},

{'\\', 12},
{'/', 14},
{'_', 15},
{'|', 6},

{'~', 18},
{'<', 18},
{'>', 18},
{'=', 18},

{'0', 19},
{'1', 9},
{'2', 19},
{'3', 17},
{'4', 19},
{'5', 19},
{'6', 19},
{'7', 16},
{'8', 19},
{'9', 19},

{'A', 21},
{'B', 21},
{'C', 19},
{'D', 21},
{'E', 18},
{'F', 17},
{'G', 20},
{'H', 20},
{'I', 8},
{'J', 16},
{'K', 17},
{'L', 15},
{'M', 26},
{'N', 21},
{'O', 21},
{'P', 20},
{'Q', 21},
{'R', 21},
{'S', 21},
{'T', 17},
{'U', 20},
{'V', 20},
{'W', 31},
{'X', 19},
{'Y', 20},
{'Z', 19},

{'a', 17},
{'b', 17},
{'c', 16},
{'d', 17},
{'e', 17},
{'f', 9},
{'g', 17},
{'h', 17},
{'i', 8},
{'j', 8},
{'k', 17},
{'l', 8},
{'m', 27},
{'n', 17},
{'o', 17},
{'p', 17},
{'q', 17},
{'r', 10},
{'s', 17},
{'t', 9},
{'u', 17},
{'v', 15},
{'w', 27},
{'x', 15},
{'y', 17},
{'z', 16}
};
    #endregion

    public static int GetWordWidth(string word)
    {
        int wordWidth = 0;
        foreach (char c in word)
        {
            int thisWidth = 0;
            bool contains = _charWidths.TryGetValue(c, out thisWidth);
            if (!contains)
                thisWidth = monospaceCharWidth; //conservative estimate

            wordWidth += (thisWidth + 1);
        }
        return wordWidth;
    }

    public static string WrapText(string text, float fontSize, float pixelWidth = adjustedPixelWidth)
    {
        textSB.Clear();
        var words = text.Split(' ');
        var screenWidth = (pixelWidth / fontSize);
        int currentLineWidth = 0;
        foreach (var word in words)
        {
            if (currentLineWidth == 0)
            {
                textSB.Append($"{word}");
                currentLineWidth += GetWordWidth(word);
                continue;
            }

            currentLineWidth += spaceWidth + GetWordWidth(word);
            if (currentLineWidth > screenWidth) //new line
            {
                currentLineWidth = GetWordWidth(word);
                textSB.Append($"\n{word}");
            }
            else
            {
                textSB.Append($" {word}");
            }
        }

        return textSB.ToString();
    }
}
#endregion
#endregion

#endregion
