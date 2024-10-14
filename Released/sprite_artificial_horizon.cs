
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

const string VERSION = "1.12.8";
const string DATE = "2024/10/14";

#region Fields
List<IMyShipController> Controllers
{
    get
    {
        return _taggedControllers.Count > 0 ? _taggedControllers : _allControllers;
    }
}

public enum AccelUnits { m_per_s2, G_force }

IMyShipController reference = null;

double _collisionSoundTimeSum = 141;
bool _lastCollisionWarningState = false;
bool _clearSpriteCache = false;

#region Ini Keys
ConfigSection
    _configGeneral = new ConfigSection("Artificial Horizon - General"),
    _configColor = new ConfigSection("Artificial Horizon - Colors");

ConfigString _textSurfaceNameTag = new ConfigString("Text surface name tag", "Horizon");
public ConfigDouble TimeToCollisionThreshold = new ConfigDouble("Collision warning time threshold (s)", 5, " Time before predicted collision that the AH will\n warn you to pull up (-1 disables warning)");
ConfigString _soundBlockNameTag = new ConfigString("Collision warning sound name tag", "Horizon");
ConfigDouble _collisionSoundInterval = new ConfigDouble("Collision warning sound loop interval (s)", 0.166);
ConfigString _referenceNameTag = new ConfigString("Optional reference name tag", "Reference");
public ConfigDouble AltitudeTransitionThreshold = new ConfigDouble("Surface to Sealevel transition alt. (m)", 1000);
public ConfigBool ShowXYZAxis = new ConfigBool("Show XYZ axes in space", true);
ConfigBool _drawTitleScreen = new ConfigBool("Draw title screen", true);
public ConfigVector3 SunRotationAxis = new ConfigVector3("Sun rotation axis", new Vector3(0, -1, 0));
public ConfigEnum<AccelUnits> AccelerationMode = new ConfigEnum<AccelUnits>("Acceleration display mode", AccelUnits.m_per_s2, " Accepted values are:\n m_per_s2 or G_force");
public ConfigDouble AltitudeOffset = new ConfigDouble("Altitude offset (m)", 0);

public ConfigColor
    SkyColor = new ConfigColor("Sky background", new Color(10, 30, 50)),
    GroundColor = new ConfigColor("Ground background", new Color(10, 10, 10)),
    SpaceBackgroundColor = new ConfigColor("Space background", new Color(0, 0, 0)),
    ProgradeColor = new ConfigColor("Prograde velocity", new Color(150, 150, 0)),
    RetrogradeColor = new ConfigColor("Retrograde velocity", new Color(150, 0, 0)),
    TextColor = new ConfigColor("Text", new Color(150, 150, 150, 100)),
    TextBoxColor = new ConfigColor("Text box outline", new Color(150, 150, 150, 100)),
    TextBoxBackground = new ConfigColor("Text box background", new Color(10, 10, 10, 150)),
    HorizonLineColor = new ConfigColor("Horizon line", new Color(0, 0, 0)),
    ElevationLineColor = new ConfigColor("Elevation lines", new Color(150, 150, 150)),
    OrientationLineColor = new ConfigColor("Orientation indicator", new Color(150, 150, 150)),
    XAxisColor = new ConfigColor("Space x-axis", new Color(100, 50, 0, 150)),
    YAxisColor = new ConfigColor("Space y-axis", new Color(0, 100, 0, 150)),
    ZAxisColor = new ConfigColor("Space z-axis", new Color(0, 50, 100, 150));

public Color AltitudeWarningColor { get; private set; } = Color.Red;

const string INI_SECTION_TEXT_SURF = "Artificial Horizon - Text Surface Config";
const string INI_TEXT_SURF_TEMPLATE = "Show on screen {0}";
#endregion

const double TICK = 1.0 / 60.0;

readonly List<IMyShipController> _allControllers = new List<IMyShipController>();
readonly List<IMyShipController> _taggedControllers = new List<IMyShipController>();
readonly List<IMyTextSurface> _textSurfaces = new List<IMyTextSurface>();
readonly List<IMySoundBlock> _soundBlocks = new List<IMySoundBlock>();
MyIni _ini = new MyIni();
readonly MyIni _textSurfaceIni = new MyIni();
readonly ArtificialHorizon _artificialHorizon;
readonly Scheduler _scheduler;
readonly ScheduledAction _scheduledSetup;
readonly RuntimeTracker _runtimeTracker;
readonly RunningSymbol _runningSymbol = new RunningSymbol(new string[] { "", ".", "..", "...", "....", "...", "..", "." });
readonly CircularBuffer<Action> _buffer;
readonly ArtificialHorizonTitleScreen _titleScreen;
#endregion

#region Main methods
Program()
{
    _configGeneral.AddValues(
        _textSurfaceNameTag,
        TimeToCollisionThreshold,
        _soundBlockNameTag,
        _collisionSoundInterval,
        _referenceNameTag,
        AltitudeTransitionThreshold,
        ShowXYZAxis,
        _drawTitleScreen,
        SunRotationAxis,
        AccelerationMode,
        AltitudeOffset
    );

    _configColor.AddValues(
        SkyColor,
        GroundColor,
        SpaceBackgroundColor,
        ProgradeColor,
        RetrogradeColor,
        TextColor,
        TextBoxColor,
        TextBoxBackground,
        HorizonLineColor,
        ElevationLineColor,
        OrientationLineColor,
        XAxisColor,
        YAxisColor,
        ZAxisColor
    );

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    _titleScreen = new ArtificialHorizonTitleScreen(VERSION, this);
    _runtimeTracker = new RuntimeTracker(this);
    _artificialHorizon = new ArtificialHorizon(this);

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

    _scheduler.AddScheduledAction(_scheduledSetup);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(PlaySounds, 6);
    _scheduler.AddScheduledAction(_titleScreen.RestartDraw, 0.2);
    _scheduler.AddScheduledAction(DrawTitleScreen, 6);
    _scheduler.AddScheduledAction(MoveNextScreens, 60);

    Setup();
}

void Main(string arg)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void DrawTitleScreen()
{
    if (_drawTitleScreen)
    {
        _titleScreen.Draw();
    }
}

void CalculateAHParams()
{
    reference = GetControlledShipController(Controllers, reference); // Primary, get active controller
    if (reference == null)
    {
        if (Controllers.Count == 0)
        {
            return;
        }
        reference = Controllers[0];
    }

    _artificialHorizon.CalculateParameters(reference, 6);
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
    Echo($"{Log.Default.Output}");
    Echo($"Text surfaces: {_textSurfaces.Count}\n");
    Echo($"Reference seat:\n\"{(reference?.CustomName)}\"");
    Echo(_runtimeTracker.Write());
}
#endregion

#region Ini
void ParseIni()
{
    _ini.Clear();
    if (!_ini.TryParse(Me.CustomData) && !string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _configGeneral.Update(_ini);
    _configColor.Update(_ini);


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
    Log.Default.Clear();

    _textSurfaces.Clear();
    _taggedControllers.Clear();
    _allControllers.Clear();
    _soundBlocks.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, PopulateLists);

    if (_textSurfaces.Count == 0)
        Log.Default.Error($"No text panels or text surface providers with name tag '{_textSurfaceNameTag}' were found.");

    if (_allControllers.Count == 0)
        Log.Default.Error($"No ship controllers were found.");
    else
    {
        if (_taggedControllers.Count == 0)
            Log.Default.Info($"No ship controllers with name tag \"{_referenceNameTag}\" were found. Using all available ship controllers. (This is NOT an error!)");
        else
            Log.Default.Info($"One or more ship controllers with name tag \"{_referenceNameTag}\" were found. Using these to orient the artificial horizon.");
    }

    if (_soundBlocks.Count == 0)
        Log.Default.Info($"No optional sound blocks with name tag \"{_soundBlockNameTag}\" were found. Sounds will not be played when ground collision is imminent.");

    Log.Default.Write();
}

bool PopulateLists(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    if (StringExtensions.Contains(block.CustomName, _textSurfaceNameTag))
    {
        AddTextSurfaces(block, _textSurfaces);
    }

    var controller = block as IMyShipController;
    if (controller != null)
    {
        _allControllers.Add(controller);
        if (StringExtensions.Contains(block.CustomName, _referenceNameTag))
            _taggedControllers.Add(controller);
        return false;
    }

    var sound = block as IMySoundBlock;
    if (sound != null && StringExtensions.Contains(block.CustomName, _soundBlockNameTag))
    {
        _soundBlocks.Add(sound);
        if (!sound.IsSoundSelected)
        {
            Log.Default.Warning($"Sound block named \"{sound.CustomName}\" does not have a sound selected.");
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

#region Artificial horizon
class ArtificialHorizon
{
    #region Fields
    public bool CollisionWarning { get; private set; } = false;

    const float G = 9.80665f;

    double _bearing;
    double _surfaceAltitude;
    double _sealevelAltitude;
    double _lastSurfaceAltitude = 0;
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
    bool _showPullUp = false;
    bool _lastCollisionWarning = false;
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
            double altitude = _surfaceAltitude >= _program.AltitudeTransitionThreshold ? _sealevelAltitude : _surfaceAltitude;
            return altitude + _program.AltitudeOffset;
        }
    }

    string AltitudeLabel
    {
        get
        {
            return _surfaceAltitude >= _program.AltitudeTransitionThreshold ? "Sea level" : "Surface";
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
    readonly AxisEnum[] _axisDrawOrder = new AxisEnum[3];
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
    public ArtificialHorizon(Program program)
    {
        _pullUpStringBuilder.Append(PULL_UP_TEXT);
        _heightStringBuilder.Append("X");
        _program = program;
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
        _collisionTimeProportion = (float)(timeTillGroundCollision / _program.TimeToCollisionThreshold);
        CollisionWarning = terrainHeightDerivative > 0 && _speed > 10 && timeTillGroundCollision <= _program.TimeToCollisionThreshold;
        if (_lastCollisionWarning != CollisionWarning)
            _showPullUp = true;
        else
            _showPullUp = !_showPullUp;

        _lastCollisionWarning = CollisionWarning;
        _lastSurfaceAltitude = _surfaceAltitude;

        Vector3D eastVec = Vector3D.Cross(_gravity, _program.SunRotationAxis.Value);
        Vector3D northVec = Vector3D.Cross(eastVec, _gravity);
        Vector3D heading = VectorMath.Rejection(controller.WorldMatrix.Forward, _gravity);

        _bearing = MathHelper.ToDegrees(VectorMath.AngleBetween(heading, northVec));
        if (Vector3D.Dot(controller.WorldMatrix.Forward, eastVec) < 0)
            _bearing = 360 - _bearing;

        if (_bearing >= 359.5)
            _bearing = 0;

        _verticalSpeed = VectorMath.ScalarProjection(_velocity, -_gravity);
    }

    void CalculateSpaceParameters(IMyShipController controller)
    {
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

        _xAxisSign = Vector2.SignNonZero(_xAxisFlattened);
        _yAxisSign = Vector2.SignNonZero(_yAxisFlattened);
        _zAxisSign = Vector2.SignNonZero(_zAxisFlattened);

        if (!Vector2.IsZero(ref _xAxisFlattened, MathHelper.EPSILON))
            _xAxisDirn = Vector2.Normalize(_xAxisFlattened);

        if (!Vector2.IsZero(ref _yAxisFlattened, MathHelper.EPSILON))
            _yAxisDirn = Vector2.Normalize(_yAxisFlattened);

        if (!Vector2.IsZero(ref _zAxisFlattened, MathHelper.EPSILON))
            _zAxisDirn = Vector2.Normalize(_zAxisFlattened);

        _axisZCosVector = new Vector3D(xTrans.Z, yTrans.Z, zTrans.Z);
        double max = _axisZCosVector.Max();
        double min = _axisZCosVector.Min();

        AxisEnum usedAxes = AxisEnum.None;
        if (max == _axisZCosVector.X)
        {
            _axisDrawOrder[2] = AxisEnum.X;
            usedAxes |= AxisEnum.X;
        }
        else if (max == _axisZCosVector.Y)
        {
            _axisDrawOrder[2] = AxisEnum.Y;
            usedAxes |= AxisEnum.Y;
        }
        else
        {
            _axisDrawOrder[2] = AxisEnum.Z;
            usedAxes |= AxisEnum.Z;
        }

        if (min == _axisZCosVector.X)
        {
            _axisDrawOrder[0] = AxisEnum.X;
            usedAxes |= AxisEnum.X;

        }
        else if (min == _axisZCosVector.Y)
        {
            _axisDrawOrder[0] = AxisEnum.Y;
            usedAxes |= AxisEnum.Y;
        }
        else
        {
            _axisDrawOrder[0] = AxisEnum.Z;
            usedAxes |= AxisEnum.Z;
        }

        _axisDrawOrder[1] = (AxisEnum)MathHelper.Clamp((byte)(ALL_AXIS_ENUMS & ~usedAxes), (byte)0, (byte)ALL_AXIS_ENUMS);
    }

    #endregion

    #region Draw functions
    public void Draw(IMyTextSurface surface, bool clearSpriteCache)
    {
        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";
        surface.BackgroundAlpha = 0;
        surface.ScriptBackgroundColor = _inGravity ? _program.GroundColor : _program.SpaceBackgroundColor;

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
                DrawTextBoxes(frame, surface, screenCenter, avgViewportSize, scale, $"{_speed:n1}", $"{Altitude:0}", "m/s", "m", $"{_bearing:0}°");
                DrawAltitudeWarning(frame, screenCenter, avgViewportSize, scale, surface);
            }
            else
            {
                DrawSpace(frame, screenCenter, minSideLength * 0.5f, scale);
                float acc;
                string units, accFormat;
                if (_program.AccelerationMode == AccelUnits.G_force)
                {
                    acc = _acceleration / G;
                    units = "g";
                    accFormat = "n2";
                }
                else
                {
                    acc = _acceleration;
                    units = "m/s²";
                    accFormat = "n1";
                }
                DrawTextBoxes(frame, surface, screenCenter, avgViewportSize, scale, $"{_speed:n1}", $"{acc.ToString(accFormat)}", "m/s", units);
            }

            DrawLine(frame, new Vector2(0, screenCenter.Y), new Vector2(screenCenter.X - 64 * scale, screenCenter.Y), HORIZON_THICKNESS * scale, _program.OrientationLineColor);
            DrawLine(frame, new Vector2(screenCenter.X + 64 * scale, screenCenter.Y), new Vector2(screenCenter.X * 2f, screenCenter.Y), HORIZON_THICKNESS * scale, _program.OrientationLineColor);

            Vector2 scaledIconSize = VELOCITY_INDICATOR_SIZE * scale;
            MySprite centerSprite = new MySprite(SpriteType.TEXTURE, "AH_BoreSight", size: scaledIconSize * 1.2f, position: screenCenter + Vector2.UnitY * scaledIconSize * 0.5f, color: _program.OrientationLineColor);
            centerSprite.RotationOrScale = -MathHelper.PiOver2;
            frame.Add(centerSprite);

            MySprite velocitySprite = new MySprite(SpriteType.TEXTURE, "AH_VelocityVector", size: scaledIconSize, color: !_movingBackwards ? _program.ProgradeColor : _program.RetrogradeColor);
            float sign = _movingBackwards ? -1 : 1;
            velocitySprite.Position = screenCenter + (squareViewportSize * 0.5f * _flattenedVelocity * sign);
            frame.Add(velocitySprite);

            if (_movingBackwards)
            {
                Vector2 retrogradeCrossSize = RETROGRADE_CROSS_SIZE * scale;
                MySprite retrograteSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: retrogradeCrossSize, color: _program.RetrogradeColor);
                retrograteSprite.Position = velocitySprite.Position;
                retrograteSprite.RotationOrScale = MathHelper.PiOver4;
                frame.Add(retrograteSprite);
                retrograteSprite.RotationOrScale += MathHelper.PiOver2;
                frame.Add(retrograteSprite);
            }
        }
    }

    void DrawTextBoxes(MySpriteDrawFrame frame, IMyTextSurface surface, Vector2 screenCenter, Vector2 screenSize, float scale, string leftText, string rightText, string leftUnits, string rightUnits, string topText = "")
    {
        Vector2 boxSize = TEXT_BOX_SIZE * scale;
        float textSize = STATUS_TEXT_SIZE * scale;
        Vector2 leftBoxPos = screenCenter + new Vector2(-0.5f * (screenSize.X - boxSize.X), boxSize.Y * 0.5f);
        Vector2 rightBoxPos = screenCenter + new Vector2(0.5f * (screenSize.X - boxSize.X), boxSize.Y * 0.5f);


        float textHeight = textSize * 28.8f;

        string leftTitle = "SPD";
        string rightTitle = _inGravity ? "ALT" : "ACC";

        DrawTextBox(frame, surface, boxSize, leftBoxPos, _program.TextColor, _program.TextBoxColor, _program.TextBoxBackground, textSize, leftText, leftTitle);
        DrawTextBox(frame, surface, boxSize, rightBoxPos, _program.TextColor, _program.TextBoxColor, _program.TextBoxBackground, textSize, rightText, rightTitle);

        MySprite leftUnitSprite = MySprite.CreateText(leftUnits, "Debug", _program.TextColor, textSize * 1.0f, TextAlignment.CENTER);
        leftUnitSprite.Position = leftBoxPos + 0.5f * Vector2.UnitY * textHeight;
        frame.Add(leftUnitSprite);
    
        MySprite rightUnitSprite = MySprite.CreateText(rightUnits, "Debug", _program.TextColor, textSize * 1.0f, TextAlignment.CENTER);
        rightUnitSprite.Position = rightBoxPos + 0.5f * Vector2.UnitY * textHeight;
        frame.Add(rightUnitSprite);

        if (_inGravity)
        {
            MySprite altMode = MySprite.CreateText(AltitudeLabel, "Debug", _program.TextColor, textSize * 0.75f, TextAlignment.CENTER);
            altMode.Position = rightUnitSprite.Position.Value + 1f * Vector2.UnitY * textHeight;
            frame.Add(altMode);

            MySprite verticalSpeedLabel = MySprite.CreateText(VERTICAL_SPEED, "Debug", _program.TextColor, textSize * 0.75f, TextAlignment.CENTER);
            verticalSpeedLabel.Position = leftUnitSprite.Position.Value + 1f * Vector2.UnitY * textHeight;
            frame.Add(verticalSpeedLabel);

            MySprite verticalSpeed = MySprite.CreateText($"{_verticalSpeed:n1}", "Debug", _program.TextColor, textSize * 0.75f, TextAlignment.CENTER);
            verticalSpeed.Position = leftUnitSprite.Position.Value + 1.75f * Vector2.UnitY * textHeight;
            frame.Add(verticalSpeed);
        }

        if (!string.IsNullOrWhiteSpace(topText))
        {
            Vector2 topBoxPos = screenCenter + new Vector2(0, screenSize.Y * -0.40f);
            DrawTextBox(frame, surface, boxSize, topBoxPos, _program.TextColor, _program.TextBoxColor, _program.TextBoxBackground, textSize, topText); //, drawBackground: false);
        }
    }

    void DrawTextBox(MySpriteDrawFrame frame, IMyTextSurface surface, Vector2 size, Vector2 position, Color textColor, Color borderColor, Color backgroundColor, float textSize, string text, string title = "", TextAlignment titleAlignment = TextAlignment.CENTER)
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
            titleSprite.Alignment = titleAlignment;
            switch(titleAlignment)
            {
                case TextAlignment.CENTER:
                default:
                    break;
                case TextAlignment.LEFT:
                    titlePos.X -= 0.5f * size.X;
                    break;
                case TextAlignment.RIGHT:
                    titlePos.X += 0.5f * size.X;
                    break;

            }
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

        MySprite skySprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _program.SkyColor, size: skySpriteSize);
        skySprite.RotationOrScale = _roll;

        Vector2 skyMidPt = screenCenter + new Vector2(0, -skySpriteSize.Y * 0.5f); //surfaceSize.Y * new Vector2(0.5f, -1f);
        skySprite.Position = skyMidPt + _rollOffset + _pitchOffset * pitchProportion;
        frame.Add(skySprite);

        MySprite horizonLineSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _program.HorizonLineColor, size: new Vector2(skySpriteSize.X, HORIZON_THICKNESS * scale));
        horizonLineSprite.RotationOrScale = _roll;
        horizonLineSprite.Position = screenCenter + _pitchOffset * pitchProportion;
        frame.Add(horizonLineSprite);

        for (int i = -90; i <= 90; i += 30)
        {
            if (i == 0)
                continue;
            DrawElevationLadder(frame, screenCenter, ELEVATION_LADDER_SIZE, pitchProportion, i, scale, _program.ElevationLineColor, true);
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
                DrawTextBox(frame, surface, textBoxSize, textPosition, _program.AltitudeWarningColor, _program.AltitudeWarningColor, _program.TextBoxBackground, textSize, PULL_UP_TEXT);
            }

            Vector2 warningCrossSize = PULL_UP_CROSS_SIZE * scale;
            Vector2 warningCrossPosition = new Vector2(-screenSize.X * 0.5f * _collisionTimeProportion, 0);
            MySprite warningCrossHalf = MySprite.CreateSprite("AH_BoreSight", screenCenter + warningCrossPosition, warningCrossSize);
            warningCrossHalf.Color = _program.AltitudeWarningColor;
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

        MySprite ladderSprite = new MySprite(SpriteType.TEXTURE, textureName, color: _program.ElevationLineColor, size: scaledSize);
        ladderSprite.RotationOrScale = _roll + (pitchProportion <= 0 ? MathHelper.Pi : 0);
        ladderSprite.Position = midPoint + (pitchProportion + basePitchProportion) * _pitchOffset;
        frame.Add(ladderSprite);

        if (!drawText)
            return;

        Vector2 textHorizontalOffset = new Vector2(_rollCos, _rollSin) * (scaledSize.X + 48f * scale) * 0.5f;
        Vector2 textVerticalOffset = Vector2.UnitY * -24f * scale * (pitchProportion <= 0 ? 0 : 1);

        MySprite text = MySprite.CreateText($"{elevationAngleDeg}", "Debug", _program.ElevationLineColor);
        text.RotationOrScale = ELEVATION_TEXT_SIZE * scale;
        text.Position = ladderSprite.Position + textHorizontalOffset + textVerticalOffset;
        frame.Add(text);

        text.Position = ladderSprite.Position - textHorizontalOffset + textVerticalOffset;
        frame.Add(text);
    }

    void DrawSpace(MySpriteDrawFrame frame, Vector2 screenCenter, float halfExtent, float scale)
    {
        if (!_program.ShowXYZAxis)
            return;

        float textSize = scale * STATUS_TEXT_SIZE;
        float lineSize = scale * AXIS_LINE_WIDTH;
        float offset = scale * AXIS_TEXT_OFFSET;
        Vector2 markerSize = scale * AXIS_MARKER_SIZE;
        Vector2 xPos = screenCenter + _xAxisFlattened * halfExtent;
        Vector2 yPos = screenCenter + _yAxisFlattened * halfExtent;
        Vector2 zPos = screenCenter + _zAxisFlattened * halfExtent;

        MySprite xLabel = MySprite.CreateText("X", "Debug", _program.XAxisColor, textSize, TextAlignment.CENTER);
        xLabel.Position = xPos + offset * _xAxisSign - Vector2.UnitY * markerSize.Y;

        MySprite yLabel = MySprite.CreateText("Y", "Debug", _program.YAxisColor, textSize, TextAlignment.CENTER);
        yLabel.Position = yPos + offset * _yAxisSign - Vector2.UnitY * markerSize.Y; ;

        MySprite zLabel = MySprite.CreateText("Z", "Debug", _program.ZAxisColor, textSize, TextAlignment.CENTER);
        zLabel.Position = zPos + offset * _zAxisSign - Vector2.UnitY * markerSize.Y; ;

        foreach (var axis in _axisDrawOrder)
        {
            if (axis == AxisEnum.X)
            {
                DrawArrowHead(frame, xPos, AXIS_MARKER_SIZE * scale, _xAxisDirn, _axisZCosVector.X, _program.XAxisColor, _axisArrowBackColor);
                DrawLine(frame, screenCenter, xPos, lineSize, _program.XAxisColor, true);
                frame.Add(xLabel);
            }
            else if (axis == AxisEnum.Y)
            {
                DrawArrowHead(frame, yPos, AXIS_MARKER_SIZE * scale, _yAxisDirn, _axisZCosVector.Y, _program.YAxisColor, _axisArrowBackColor);
                DrawLine(frame, screenCenter, yPos, lineSize, _program.YAxisColor, true);
                frame.Add(yLabel);
            }
            else
            {
                DrawArrowHead(frame, zPos, AXIS_MARKER_SIZE * scale, _zAxisDirn, _axisZCosVector.Z, _program.ZAxisColor, _axisArrowBackColor);
                DrawLine(frame, screenCenter, zPos, lineSize, _program.ZAxisColor, true);
                frame.Add(zLabel);
            }
        }
    }
    #endregion
}
#endregion

class ArtificialHorizonTitleScreen
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;
    const float TextSize = 1.3f;
    const string TitleFormat = "Artificial Horizon Redux - v{0}";
    readonly string _titleText;
    Program _program;
    int _idx = 0;

    const float SpriteScale = 1f;
    readonly Vector2 _spritePos = new Vector2(0, 30);
    enum FrameId { Neg60, Neg40, Neg20, Level, Pos20, Pos40, Pos60 }

    struct AnimationParams
    {
        public readonly float AngleRad;

        public AnimationParams(float angleDeg)
        {
            AngleRad = MathHelper.ToRadians(angleDeg);
        }
    }

    readonly AnimationParams[] _animSequence = new AnimationParams[] {
new AnimationParams(0),
new AnimationParams(20),
new AnimationParams(40),
new AnimationParams(60),
new AnimationParams(60),
new AnimationParams(60),
new AnimationParams(60),
new AnimationParams(60),
new AnimationParams(40),
new AnimationParams(20),
new AnimationParams(0),
new AnimationParams(-20),
new AnimationParams(-40),
new AnimationParams(-60),
new AnimationParams(-60),
new AnimationParams(-60),
new AnimationParams(-60),
new AnimationParams(-60),
new AnimationParams(-40),
new AnimationParams(-20),
};

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public ArtificialHorizonTitleScreen(string version, Program program)
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

        DrawHorizon(frame, screenCenter + _spritePos * minScale, SpriteScale * minScale, anim.AngleRad);
        DrawTitleBar(ref frame, _surface, _topBarColor, _white, _titleText, minScale, textSize: TextSize);

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

    void DrawHorizon(MySpriteDrawFrame frame, Vector2 centerPos, float scale, float rotation)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 0f) * scale + centerPos, new Vector2(280f, 280f) * scale, _program.SkyColor, null, TextAlignment.CENTER, 0f)); // sky
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos * 0f - sin * 140f, sin * 0f + cos * 140f) * scale + centerPos, new Vector2(700f, 280f) * scale, _program.GroundColor, null, TextAlignment.CENTER, rotation)); // ground
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(90f, 0f) * scale + centerPos, new Vector2(100f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f)); // horizon line right
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-90f, 0f) * scale + centerPos, new Vector2(100f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f)); // horizon line left
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(8f, 15f) * scale + centerPos, new Vector2(10f, 30f) * scale, _white, null, TextAlignment.CENTER, -0.7854f)); // pip right
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-8f, 15f) * scale + centerPos, new Vector2(10f, 30f) * scale, _white, null, TextAlignment.CENTER, 0.7854f)); // pip left
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 300f) * scale + centerPos, new Vector2(900f, 300f) * scale, _black, null, TextAlignment.CENTER, 0f)); // mask bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, -300f) * scale + centerPos, new Vector2(900f, 300f) * scale, _black, null, TextAlignment.CENTER, 0f)); // mask top
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-300f, 0f) * scale + centerPos, new Vector2(300f, 300f) * scale, _black, null, TextAlignment.CENTER, 0f)); // mask left
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(300f, 0f) * scale + centerPos, new Vector2(300f, 300f) * scale, _black, null, TextAlignment.CENTER, 0f)); // mask right
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, 145f) * scale + centerPos, new Vector2(280f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f)); // border bottom
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f, -145f) * scale + centerPos, new Vector2(280f, 10f) * scale, _white, null, TextAlignment.CENTER, 0f)); // border top
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-145f, 0f) * scale + centerPos, new Vector2(10f, 300f) * scale, _white, null, TextAlignment.CENTER, 0f)); // border left
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(145f, 0f) * scale + centerPos, new Vector2(10f, 300f) * scale, _white, null, TextAlignment.CENTER, 0f)); // border right
    }
    #endregion
}

#endregion

#region INCLUDES

public static class VectorMath
{
    public static double AngleBetween(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
        {
            return 0;
        }
        return Math.Atan2(Vector3D.Cross(a, b).Length(), Vector3D.Dot(a, b));
    }

    public static Vector3D Projection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;
        
        if (Vector3D.IsUnit(ref b))
            return a.Dot(b) * b;

        return a.Dot(b) / b.LengthSquared() * b;
    }

    public static Vector3D Rejection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return Vector3D.Zero;

        return a - a.Dot(b) / b.LengthSquared() * b;
    }

    public static double ScalarProjection(Vector3D a, Vector3D b)
    {
        if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
            return 0;

        if (Vector3D.IsUnit(ref b))
            return a.Dot(b);

        return a.Dot(b) / b.Length();
    }
}

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

#region Scheduler
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
                if (!_currentlyQueuedAction.DisposeAfterRun)
                {
                    _queuedActions.Enqueue(_currentlyQueuedAction);
                }
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

    public void AddQueuedAction(Action action, double updateInterval, bool removeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        QueuedAction scheduledAction = new QueuedAction(action, updateInterval, removeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

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

public class RunningSymbol
{
    int _index = 0;
    string[] _runningSymbols = new string[] { "", ".", "..", "...", "..", "." };

    public RunningSymbol() {}

    public RunningSymbol(string[] runningSymbols)
    {
        if (runningSymbols.Length != 0)
        {
            _runningSymbols = runningSymbols;
        }
    }

    public string Iterate()
    {
        _index = ++_index % _runningSymbols.Length;

        return this.ToString();
    }

    public override string ToString()
    {
        return _runningSymbols[_index];
    }
}
public class Log
{
    public static Log Default = new Log();

    public StringBuilder Output = new StringBuilder();
    public StringBuilder ErrorOutput = new StringBuilder();
    public StringBuilder WarningOutput = new StringBuilder();
    public StringBuilder InfoOutput = new StringBuilder();

    int _errorCount = 0;
    int _warningCount = 0;
    int _infoCount = 0;

    const string ErrorTag = "[color=#FFFF0000]ERROR {0}: [/color]{1}";
    const string WarningTag = "[color=#FFFFFF00]WARNING {0}: [/color]{1}";
    const string InfoTag = "[color=#FF00AAFF]INFO {0}: [/color]{1}";

    public void Clear()
    {
        Output.Clear();
        ErrorOutput.Clear();
        WarningOutput.Clear();
        InfoOutput.Clear();
        _errorCount = 0;
        _warningCount = 0;
        _infoCount = 0;
    }

    public void Error(string text)
    {
        ErrorOutput.AppendLine(string.Format(ErrorTag, ++_errorCount, text));
    }

    public void Warning(string text)
    {
        WarningOutput.AppendLine(string.Format(WarningTag, ++_warningCount, text));
    }

    public void Info(string text)
    {
        InfoOutput.AppendLine(string.Format(InfoTag, ++_infoCount, text));
    }

    public void Write()
    {
        if (_errorCount > 0)
        {
            Output.Append(ErrorOutput);
        }

        if (_warningCount > 0)
        {
            Output.Append(WarningOutput);
        }

        if (_infoCount > 0)
        {
            Output.Append(InfoOutput);
        }
    }
}

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

public static void DrawLine(MySpriteDrawFrame frame, Vector2 point1, Vector2 point2, float width, Color color, bool roundedEnds = false)
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

    if (roundedEnds)
    {
        MySprite end = MySprite.CreateSprite("Circle", point1, new Vector2(width,width));
        end.Color = color;
        frame.Add(end);
        end.Position = point2;
        frame.Add(end);
    }
}

public static void DrawArrowHead(MySpriteDrawFrame frame, Vector2 position, Vector2 arrowSize, Vector2 flattenedDirection, double depthSin, Color color, Color backColor)
{
    if (Math.Abs(flattenedDirection.LengthSquared() - 1) < MathHelper.EPSILON)
        flattenedDirection.Normalize();

    arrowSize.Y *= (float)Math.Sqrt(1 - depthSin * depthSin);
    Vector2 baseSize = Vector2.One * arrowSize.X;
    baseSize.Y *= (float)Math.Abs(depthSin);

    float angle = (float)Math.Acos(Vector2.Dot(flattenedDirection, -Vector2.UnitY));
    if (Vector2.Dot(flattenedDirection, Vector2.UnitX) < 0)
    {
        angle *= -1f;
    }

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

public static IMyShipController GetControlledShipController(List<IMyShipController> controllers, IMyShipController lastController = null)
{
    IMyShipController currentlyControlled = null;
    foreach (IMyShipController ctrl in controllers)
    {
        if (ctrl.IsMainCockpit)
        {
            return ctrl;
        }

        if (currentlyControlled == null && ctrl != lastController && ctrl.IsUnderControl && ctrl.CanControlShip)
        {
            currentlyControlled = ctrl;
        }
    }

    if (lastController != null && lastController.IsUnderControl)
    {
        return lastController;
    }

    if (currentlyControlled != null)
    {
        return currentlyControlled;
    }

    return lastController;
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

public class ConfigVector3 : ConfigValue<Vector3>
{
    public ConfigVector3(string name, Vector3 value = default(Vector3), string comment = null) : base(name, value, comment) { }
    protected override bool SetValue(ref MyIniValue val)
    {
        string source = val.ToString("");
        int xIndex = source.IndexOf("X:");
        int yIndex = source.IndexOf("Y:");
        int zIndex = source.IndexOf("Z:");
        int closingBraceIndex = source.IndexOf("}");
        if (xIndex == -1 || yIndex == -1 || zIndex == -1 || closingBraceIndex == -1)
        {
            SetDefault();
            return false;
        }

        Vector3 vec = default(Vector3);
        string str = source.Substring(xIndex + 2, yIndex - (xIndex + 2));
        if (!float.TryParse(str, out vec.X))
        {
            SetDefault();
            return false;
        }

        str = source.Substring(yIndex + 2, zIndex - (yIndex + 2));
        if (!float.TryParse(str, out vec.Y))
        {
            SetDefault();
            return false;
        }

        str = source.Substring(zIndex + 2, closingBraceIndex - (zIndex + 2));
        if (!float.TryParse(str, out vec.Z))
        {
            SetDefault();
            return false;
        }

        _value = vec;
        return true;
    }
}

public class ConfigColor : ConfigValue<Color>
{
    public ConfigColor(string name, Color value = default(Color), string comment = null) : base(name, value, comment) { }
    public override string ToString()
    {
        return string.Format("{0}, {1}, {2}, {3}", Value.R, Value.G, Value.B, Value.A);
    }
    protected override bool SetValue(ref MyIniValue val)
    {
        string rgbString = val.ToString("");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length != 4 ||
            !int.TryParse(rgbSplit[0].Trim(), out r) ||
            !int.TryParse(rgbSplit[1].Trim(), out g) ||
            !int.TryParse(rgbSplit[2].Trim(), out b))
        {
            SetDefault();
            return false;
        }

        bool hasAlpha = int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
        {
            a = 255;
        }

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);
        _value = new Color(r, g, b, a);
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
#endregion
