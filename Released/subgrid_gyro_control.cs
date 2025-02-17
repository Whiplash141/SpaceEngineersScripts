/*
/ //// / Whip's Subgrid Gyro Control Script / //// /

BASIC INSTRUCTIONS:
1. Place this script in a programmable block
2. Place some ship controllers on your ship
3. Place some subgrid gyros on your ship
4. Thats it!














=================================================
    DO NOT MODIFY VARIABLES IN THE SCRIPT!

 USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!

*/

const string NAME = "Whip's Subgrid Gyro Manager (SuGMa)";
const string VERSION = "1.4.0";
const string DATE = "2025/02/17";

const string INI_SECTION_SGCS = "Subgrid Gyro Config";
const string INI_KEY_IGNORE_TAG = "Gyro ignore name tag";
const string INI_KEY_SCAN_CONNECTORS = "Detect blocks over connectors";
const string INI_KEY_PITCH_GAIN = "Pitch gain";
const string INI_KEY_YAW_GAIN = "Yaw gain";
const string INI_KEY_ROLL_GAIN = "Roll gain";
const string INI_KEY_TITLE_SCREEN = "Draw title screen";

const int MaxInstructionsPerRun = 5000;
public float GyroPitchGain = 60.0f;
public float GyroYawGain = 60.0f;
public float GyroRollGain = 60.0f;
bool _detectOverConnectors = false;
string _gyroIgnoreTag = "Ignore";
bool _drawTitleScreen = true;

const float RPMToRadsPerSecond = (float)Math.PI / 30.0f;
bool _setup = false;
List<IMyShipController> _controllers = new List<IMyShipController>(); 
List<IMyGyro> _gyros = new List<IMyGyro>(); 
List<IMyGyro> _subgridGyros = new List<IMyGyro>(); 
Scheduler _scheduler;
ScheduledAction _scheduledSetup;
IMyShipController _lastControlled;
StringBuilder _echoBuilder = new StringBuilder();
CircularBuffer<InputState> _inputBuffer = new CircularBuffer<InputState>(6);
long _lastCubeGridId = -1;
MyIni _ini = new MyIni();
SubgridGyroScreenManager _screenManager;
IEnumerator<bool> _stateMachine;
int _enumeratorSteps = 0;
int _lastEnumeratorSteps = 0;
RuntimeTracker _runtimeTracker;
bool _setupReady = false;

void Draw()
{
    if (_drawTitleScreen)
    {
        _screenManager.Draw();
    }
}

Program()
{
    _screenManager = new SubgridGyroScreenManager(VERSION, this);
    
    _scheduledSetup = new ScheduledAction(InitiateSetup, 0.1);

    _runtimeTracker = new RuntimeTracker(this);
    
    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(ProcessInputs, 60);
    _scheduler.AddScheduledAction(() => TrySetup(false), 60);
    _scheduler.AddScheduledAction(StartStateMachine, 10, timeOffset: 0.05);
    _scheduler.AddScheduledAction(RunStateMachine, 60);
    _scheduler.AddScheduledAction(WriteDetailedInfo, 1);
    _scheduler.AddScheduledAction(Draw, 6);
    _scheduler.AddScheduledAction(_screenManager.RestartDraw, 0.1, timeOffset: 0.5);
    _scheduler.AddScheduledAction(_scheduledSetup);
    
    TrySetup(true);
    
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void WriteDetailedInfo()
{
    Echo($"{NAME}\n(v{VERSION} - {DATE})\n");
    
    Echo($"Next block refresh in {Math.Max(_scheduledSetup.RunInterval - _scheduledSetup.TimeSinceLastRun, 0):n0} second(s)\n");
    
    if (_setup)
    {
        if (_lastControlled != null && _lastControlled.IsUnderControl)
        {
            Echo("> Controlling gyro(s)...");
        }
        else
        {
            Echo("> No pilot detected");
        }
        Echo($"> {_subgridGyros.Count} subgrid gyro(s) found");
        Echo($"> Controller: {(_lastControlled == null ? "(none)" : _lastControlled.CustomName)}");
        Echo($"> {_lastEnumeratorSteps} tick(s) required to run core logic");
    }
    else
    {
        if (_gyros.Count == 0)
        {
            Echo(">> ERROR: No gyros found on\n    this ship or attached subgrids");
        }
        
        if (_controllers.Count == 0)
        {
            Echo(">> ERROR: No ship controllers\n    found on this ship or attached\n    subgrids");
        }
    }

    Echo("");
    Echo(_runtimeTracker.Write());

    string output = _echoBuilder.ToString();
    base.Echo(output);
    _echoBuilder.Clear();
}

new void Echo(string msg)
{
    _echoBuilder.Append(msg).Append("\n");
}

void ProcessInputs()
{
    InputState state;
    if (_lastControlled == null)
    {
        state = InputState.Zero;
    }
    else
    {
        state = InputState.FromShipController(_lastControlled);
    }
    _inputBuffer.Add(state);
}

Vector3D GetAverageRotationInput()
{
    InputState avgState = InputState.Zero;
    for (int i = 0; i < _inputBuffer.Capacity; ++i)
    {
        InputState currentState = _inputBuffer.MoveNext();
        avgState += currentState;
    }
    avgState *= 1.0f / _inputBuffer.Capacity;

    return new Vector3D(avgState.RotationIndicator, avgState.RollIndicator);
}

void StartStateMachine()
{
    if (_stateMachine == null && _setup)
    {
        _lastEnumeratorSteps = _enumeratorSteps;
        _enumeratorSteps = 0;
        _stateMachine = DoWork();
    }
}

void RunStateMachine()
{
    if (_stateMachine == null)
    {
        return;
    }
    
    ++_enumeratorSteps;

    bool moreSteps = _stateMachine.MoveNext();

    if (!moreSteps)
    {
        _stateMachine.Dispose();
        _stateMachine = null;
    }
}

public bool AtInstructionLimit()
{
    return Runtime.CurrentInstructionCount >= MaxInstructionsPerRun;
}

IEnumerator<bool> DoWork()
{   
    // Update selected controller
    var controller = GetControlledShipController(_controllers, _lastControlled);
    if (controller == null)
    {
        if (_lastControlled != null)
            controller = _lastControlled;
        else
            controller = _controllers[0];
    }

    _lastControlled = controller;
    
    // Get gyros (inlined)
    if (_lastCubeGridId != controller.CubeGrid.EntityId || _subgridGyros.Count == 0)
    {
        _subgridGyros.Clear();
        foreach (IMyGyro gyro in _gyros)
        { 
            if (AtInstructionLimit())
            {
                yield return true;
            }
            
            if (!GridTerminalSystem.CanAccess(gyro))
            {
                continue;
            }
            
            if (controller.CubeGrid != gyro.CubeGrid)
            {
                _subgridGyros.Add(gyro);
            }
            else
            {
                gyro.GyroOverride = false;
                gyro.Yaw = 0;
                gyro.Pitch = 0;
                gyro.Roll = 0;
            }
        }

    }
    _lastCubeGridId = controller.CubeGrid.EntityId;


    // Get average rotation state
    Vector3 rotationState = (Vector3)GetAverageRotationInput();

    // Process rotation state
    Vector2 rotationClamped = new Vector2(rotationState.X, rotationState.Y) / 20f;
    rotationClamped = Vector2.ClampToSphere(rotationClamped, 1f);
    float roll = rotationState.Z * 0.2f;
    Vector3 rotationVec = new Vector3(rotationClamped.X, rotationClamped.Y, roll);
    
    // Apply controller
    Vector3 inputScale = new Vector3(GyroPitchGain, GyroYawGain, GyroRollGain) * RPMToRadsPerSecond;
    rotationVec *= inputScale;

    // Apply rotation command (inlined)
    var relativeRotationVec = Vector3D.Rotate(rotationVec, controller.WorldMatrix);

    foreach (var gyro in _gyros)
    {
        if (AtInstructionLimit())
        {
            yield return true;
        }

        if (!GridTerminalSystem.CanAccess(gyro)) 
        { 
            continue; 
        }
        var transformedRotationVec = Vector3D.Rotate(relativeRotationVec, Matrix.Transpose(gyro.WorldMatrix));
        gyro.Pitch = (float)transformedRotationVec.X;
        gyro.Yaw = (float)transformedRotationVec.Y;
        gyro.Roll = (float)transformedRotationVec.Z;
        gyro.GyroOverride = true;
    }
}

void InitiateSetup()
{
    _setupReady = true;
}

void TrySetup(bool force = false)
{
    if (force || (_setupReady && _stateMachine == null))
    {
        _setupReady = false;

        _gyros.Clear();
        _subgridGyros.Clear();
        _controllers.Clear();
        
        ParseIni();
        
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectBlocks);
        
        if (!_controllers.Contains(_lastControlled))
        {
            _lastControlled = null;
        }
        
        _setup = true;
        if (_gyros.Count == 0 || _controllers.Count == 0)
        {
            _setup = false;
        }
    }
}

void ParseIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _gyroIgnoreTag = _ini.Get(INI_SECTION_SGCS, INI_KEY_IGNORE_TAG).ToString(_gyroIgnoreTag);
        _detectOverConnectors = _ini.Get(INI_SECTION_SGCS, INI_KEY_SCAN_CONNECTORS).ToBoolean(_detectOverConnectors);
        GyroPitchGain = _ini.Get(INI_SECTION_SGCS, INI_KEY_PITCH_GAIN).ToSingle(GyroPitchGain);
        GyroYawGain = _ini.Get(INI_SECTION_SGCS, INI_KEY_YAW_GAIN).ToSingle(GyroYawGain);
        GyroRollGain = _ini.Get(INI_SECTION_SGCS, INI_KEY_ROLL_GAIN).ToSingle(GyroRollGain);
        _drawTitleScreen = _ini.Get(INI_SECTION_SGCS, INI_KEY_TITLE_SCREEN).ToBoolean(_drawTitleScreen);

    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }
    
    _ini.Set(INI_SECTION_SGCS, INI_KEY_IGNORE_TAG, _gyroIgnoreTag);
    _ini.Set(INI_SECTION_SGCS, INI_KEY_SCAN_CONNECTORS, _detectOverConnectors);
    _ini.Set(INI_SECTION_SGCS, INI_KEY_PITCH_GAIN, GyroPitchGain);
    _ini.Set(INI_SECTION_SGCS, INI_KEY_YAW_GAIN, GyroYawGain);
    _ini.Set(INI_SECTION_SGCS, INI_KEY_ROLL_GAIN, GyroRollGain);
    _ini.Set(INI_SECTION_SGCS, INI_KEY_TITLE_SCREEN, _drawTitleScreen);

    
    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }
}

bool CollectBlocks(IMyTerminalBlock x)
{
    if (!_detectOverConnectors && !GridTerminalSystem.CanAccess(x, MyTerminalAccessScope.Construct))
    {
        return false;
    }
    
    if (x is IMyShipController)
    {
        _controllers.Add((IMyShipController)x);
    }
    else if (x is IMyGyro)
    {
        if (x.CustomName.Contains(_gyroIgnoreTag))
        {
            return false;
        }
        
        _gyros.Add((IMyGyro)x);
    }
    
    return false;
}

class SubgridGyroScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;
    const float MouseSpriteScale = 0.75f;
    const float ArrowSpriteScale = 1f;
    const float RotorSpriteScale = 1f;
    const float GyroSpriteScale = 0.6f;
    const string Font = "Debug";
    const string TitleFormat = "Whip's Subgrid Gyros - v{0}";
    readonly string _titleText;

    Program _program;

    int _idx = 0;
    const float MouseY = 100f;
    const float MaxMouseDelta = -150f;
    const float MouseX = -100;
    const float MaxTurretAngle = -45f;
    readonly Vector2 _arrowPos = new Vector2(100, -20);
    readonly Vector2 _rotorPos = new Vector2(100, 100);
    readonly Vector2 _gyroPos =  new Vector2(100, -20);

    struct AnimationParams
    {
        public readonly float MouseOffset;
        public readonly float ArrowRotation;
        public readonly bool DrawArrow;
        public readonly bool CounterClockwiseArrow;

        public AnimationParams(float offset, float rotation, bool drawArrow, bool ccw)
        {
            MouseOffset = offset;
            ArrowRotation = rotation;
            DrawArrow = drawArrow;
            CounterClockwiseArrow = ccw;
        }
    }

    AnimationParams[] _animSequence = new AnimationParams[] {
        // Mouse up
        new AnimationParams(0.2f, -45f, true, false),
        new AnimationParams(0.4f, -15f, true, false),
        new AnimationParams(0.6f, 15f, true, false),
        new AnimationParams(0.8f, 45f, true, false),
        new AnimationParams(1.0f, 0f, false, false),
        new AnimationParams(1.0f, 0f, false, false),
        new AnimationParams(1.0f, 0f, false, false),
        new AnimationParams(1.0f, 0f, false, false),
        // Mouse down
        new AnimationParams(0.8f, 45f, true,  true),
        new AnimationParams(0.6f, 15f, true,  true),
        new AnimationParams(0.4f, -15f, true, true),
        new AnimationParams(0.2f, -45f, true, true),
        new AnimationParams(0f, 0f, false, true),
        new AnimationParams(0f, 0f, false, true),
        new AnimationParams(0f, 0f, false, true),
        new AnimationParams(0f, 0f, false, true),
        new AnimationParams(0f, 0f, false, true),
        new AnimationParams(0f, 0f, false, true),
    };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public SubgridGyroScreenManager(string version, Program program)
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
        float angle = MathHelper.ToRadians(anim.ArrowRotation);
        Vector2 mousePos = new Vector2(MouseX, MouseY + MaxMouseDelta * anim.MouseOffset);

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

            if (anim.DrawArrow)
            {
                if (anim.CounterClockwiseArrow)
                {
                    DrawArrowCCW(frame, screenCenter + _arrowPos * minScale, minScale * ArrowSpriteScale, angle);
                }
                else
                {
                    DrawArrowCW(frame, screenCenter + _arrowPos * minScale, minScale * ArrowSpriteScale, angle);
                }
            }

            DrawMouse(frame, screenCenter + mousePos * minScale, minScale * MouseSpriteScale);
            DrawGyro(frame, screenCenter + _gyroPos * minScale, minScale * GyroSpriteScale);
            DrawRotor(frame, screenCenter + _rotorPos * minScale, minScale * RotorSpriteScale);
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

    void DrawMouse(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 0f) * scale + centerPos, new Vector2(50f, 250f) * scale, _white, null, Center, 0f)); // vertical
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 0f) * scale + centerPos, new Vector2(150f, 100f) * scale, _white, null, Center, 0f)); // horizontal
        frame.Add(new MySprite(Texture, "Circle", new Vector2(-25f, 50f) * scale + centerPos, new Vector2(100f, 150f) * scale, _white, null, Center, 0f)); // corner bottom left
        frame.Add(new MySprite(Texture, "Circle", new Vector2(25f, 50f) * scale + centerPos, new Vector2(100f, 150f) * scale, _white, null, Center, 0f)); // corner bottom right
        frame.Add(new MySprite(Texture, "Circle", new Vector2(25f, -50f) * scale + centerPos, new Vector2(100f, 150f) * scale, _white, null, Center, 0f)); // corner top right
        frame.Add(new MySprite(Texture, "Circle", new Vector2(-25f, -50f) * scale + centerPos, new Vector2(100f, 150f) * scale, _white, null, Center, 0f)); // corner top left
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -80f) * scale + centerPos, new Vector2(15f, 100f) * scale, _black, null, Center, 0f)); // detail vertical
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -23f) * scale + centerPos, new Vector2(150f, 15f) * scale, _black, null, Center, 0f)); // detail horizontal
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -75f) * scale + centerPos, new Vector2(31f, 45f) * scale, _black, null, Center, 0f)); // scroll outline
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -75f) * scale + centerPos, new Vector2(15f, 30f) * scale, _white, null, Center, 0f)); // scroll
    }

    void DrawArrowCW(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(200f, 200f) * scale, _white, null, Center, 0f + rotation)); // body
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, -2.3562f + rotation)); // body mask2
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(150f, 150f) * scale, _black, null, Center, 0f + rotation)); // body mask center
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, 2.3562f + rotation)); // body mask1
        frame.Add(new MySprite(Texture, "Triangle", new Vector2(cos * 75f - sin * -48f, sin * 75f + cos * -48f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, Center, 2.3562f + rotation)); // head
    }

    void DrawArrowCCW(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(200f, 200f) * scale, _white, null, Center, 0f + rotation)); // body
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, -2.3562f + rotation)); // body mask2
        frame.Add(new MySprite(Texture, "Circle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(150f, 150f) * scale, _black, null, Center, 0f + rotation)); // body mask center
        frame.Add(new MySprite(Texture, "SemiCircle", new Vector2(cos * 0f - sin * 0f, sin * 0f + cos * 0f) * scale + centerPos, new Vector2(250f, 250f) * scale, _black, null, Center, 2.3562f + rotation)); // body mask1
        frame.Add(new MySprite(Texture, "Triangle", new Vector2(cos * -75f - sin * -48f, sin * -75f + cos * -48f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, Center, -2.3562f + rotation)); // head
    }

    void DrawRotor(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -15f) * scale + centerPos, new Vector2(80f, 10f) * scale, _white, null, Center, 0f)); // stator top
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 20f) * scale + centerPos, new Vector2(100f, 60f) * scale, _white, null, Center, 0f)); // stator
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -35f) * scale + centerPos, new Vector2(40f, 20f) * scale, _white, null, Center, 0f)); // rotor shaft
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -50f) * scale + centerPos, new Vector2(100f, 10f) * scale, _white, null, Center, 0f)); // rotor top
    }

    void DrawGyro(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(150f, 150f) * scale, _white, null, Center, 0f)); // circle bit
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(60f, 60f) * scale, _black, null, Center, 0f)); // center circle shadow
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-37f, 22f) * scale + centerPos, new Vector2(30f, 100f) * scale, _black, null, Center, 1.0472f)); // line3 shadow
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(-37f, 22f) * scale + centerPos, new Vector2(20f, 100f) * scale, _white, null, Center, 1.0472f)); // line3
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(37f, 22f) * scale + centerPos, new Vector2(30f, 100f) * scale, _black, null, Center, -1.0472f)); // line2 shadow
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(37f, 22f) * scale + centerPos, new Vector2(20f, 100f) * scale, _white, null, Center, -1.0472f)); // line2
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -44f) * scale + centerPos, new Vector2(30f, 100f) * scale, _black, null, Center, 0f)); // line1 shadow
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, -44f) * scale + centerPos, new Vector2(20f, 100f) * scale, _white, null, Center, 0f)); // line1
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 53f) * scale + centerPos, new Vector2(50f, 53f) * scale, _black, null, Center, 0f)); // mount shadow
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 50f) * scale + centerPos, new Vector2(40f, 60f) * scale, _white, null, Center, 0f)); // mount
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(50f, 50f) * scale, _white, null, Center, 0f)); // center circle
        frame.Add(new MySprite(Texture, "Circle", new Vector2(0f, 0f) * scale + centerPos, new Vector2(30f, 30f) * scale, _black, null, Center, 0f)); // center circle top
        frame.Add(new MySprite(Texture, "SquareSimple", new Vector2(0f, 86f) * scale + centerPos, new Vector2(160f, 15f) * scale, _white, null, Center, 0f)); // base
    }

    #endregion
}

#region INCLUDES

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

public struct InputState
{
    public Vector3 MoveIndicator;
    public Vector2 RotationIndicator;
    public float RollIndicator;

    public static readonly InputState Zero = new InputState
    {
        MoveIndicator = Vector3.Zero,
        RotationIndicator = Vector2.Zero,
        RollIndicator = 0,
    };

    public static InputState FromShipController(IMyShipController controller)
    {
        if (controller == null || !controller.IsUnderControl)
        {
            return InputState.Zero;
        }

        return new InputState()
        {
            MoveIndicator = controller.MoveIndicator,
            RotationIndicator = controller.RotationIndicator,
            RollIndicator = controller.RollIndicator,
        };
    }
    
    public static InputState FromCTC(IMyTurretControlBlock controller)
    {
        if (controller == null || !controller.IsUnderControl)
        {
            return InputState.Zero;
        }

        return new InputState()
        {
            MoveIndicator = controller.MoveIndicator,
            RotationIndicator = controller.RotationIndicator,
            RollIndicator = controller.RollIndicator,
        };
    }
    
    public static InputState operator+(InputState a, InputState b)
    {
        return new InputState
        {
            MoveIndicator = a.MoveIndicator + b.MoveIndicator,
            RotationIndicator = a.RotationIndicator + b.RotationIndicator,
            RollIndicator = a.RollIndicator + b.RollIndicator,
        };
    }
    
    public static InputState operator*(InputState a, float b)
    {
        return new InputState
        {
            MoveIndicator = a.MoveIndicator * b,
            RotationIndicator = a.RotationIndicator * b,
            RollIndicator = a.RollIndicator * b,
        };
    }
    
    public static InputState operator*(float b, InputState a)
    {
        return a * b;
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
#endregion