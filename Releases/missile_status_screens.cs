
/*
/ //// / Whip's Missile Status Screens / //// /

     SEE THE WORKSHOP PAGE FOR INSCRUCTIONS!

=================================================
    DO NOT MODIFY VARIABLES IN THE SCRIPT!

 USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!
=================================================
*/

const string VERSION = "1.5.7";
const string DATE = "2021/08/14";

List<IMyTextSurface> _textSurfaces = new List<IMyTextSurface>();
List<MySpriteContainer> _missileSprites = new List<MySpriteContainer>();
List<MySpriteContainer> _missileShadowSprites = new List<MySpriteContainer>();
List<MySpriteContainer> _titleSprites = new List<MySpriteContainer>();
Dictionary<IMyTextSurface, StatusScreen> _statusScreens = new Dictionary<IMyTextSurface, StatusScreen>();
List<string> _missileNames = new List<string>();
List<IMyProgrammableBlock> _missilePrograms = new List<IMyProgrammableBlock>();
Dictionary<string, bool> _missileStatuses = new Dictionary<string, bool>();

string _screenNameTag = "Missile Status";
string _missileGroupNameTag = "Missile";

const string IniSectionGeneral = "Missile Status Screens - General Config";
const string IniScreenGroupName = "Status screen group name";
const string IniMissileGroupName = "Missile group name tag";
const string IniReadyColor = "Missile ready color";
const string IniFiredColor = "Missile fired color";
const string IniTitleBarColor = "Title bar color";
const string IniTitleTextColor = "Title text color";
const string IniBackgroundColor = "Background color";
const string IniShowTitleBar = "Show title bar";
const string IniTitleScale = "Title scale";

// Missile sprite config
const string IniSpriteScale = "Sprite scale";
const string IniSpriteLocation = "Sprite location";
const string IniSpriteScreen = "Screen index to display on";

MyIni _ini = new MyIni();
List<string> _sectionNames = new List<string>();
List<IMyProgrammableBlock> _programs = new List<IMyProgrammableBlock>();

Color _backgroundColor = new Color(0, 0, 0);
Color _titleTextColor = new Color(150, 150, 150);
Color _titleBarColor = new Color(25, 25, 25);
Color _missileReadyColor = new Color(0, 75, 0);
Color _missileFiredColor = new Color(75, 0, 0);

Scheduler _scheduler;
CircularBuffer<Action> _actionBuffer;
bool _showTitleBar = false;
bool _clearSpriteCache = false;
float _titleScale = 1f;

IMyBlockGroup _screenGroup;

RuntimeTracker _runtimeTracker;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;

    float step = 1f / 10f;
    _actionBuffer = new CircularBuffer<Action>(10);
    _actionBuffer.Add(() => DrawScreens(0f * step, 1f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(1f * step, 2f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(2f * step, 3f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(3f * step, 4f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(4f * step, 5f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(5f * step, 6f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(6f * step, 7f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(7f * step, 8f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(8f * step, 9f * step, _clearSpriteCache));
    _actionBuffer.Add(() => DrawScreens(9f * step, 10f * step, _clearSpriteCache));

    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(GetStatuses, 4);
    _scheduler.AddScheduledAction(DrawNextScreens, 60);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(() => _clearSpriteCache = !_clearSpriteCache, 0.1);

    _runtimeTracker = new RuntimeTracker(this);

    GenerateMissileSprites();
    Setup();
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();
    ProcessArguments(arg);
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void ProcessArguments(string arg)
{
    /*
    // TODO
    switch (arg.ToUpperInvariant())
    {
        case "SETUP":
            Setup();
            break;
    }
    */
}

void DrawNextScreens()
{
    _actionBuffer.MoveNext().Invoke();
}

void PrintDetailedInfo()
{
    Echo($"WMI Missile Status Online...\n(Version {VERSION} - {DATE})\n\nRecompile to process custom data changes");
    if (_screenGroup == null)
        Echo($"\nERROR: No block group named\n  '{_screenNameTag}'!");
    Echo($"\nText Surfaces: {_textSurfaces.Count}");
    Echo(_runtimeTracker.Write());
}

public class MissileSpriteData
{
    public readonly Vector2 LocationRatio;
    public readonly float Scale;
    public bool Ready;

    public MissileSpriteData(Vector2 locationRatio, float scale)
    {
        LocationRatio = locationRatio;
        Scale = scale;
        Ready = true;
    }

    public Vector2 GetLocationPx(Vector2 screenSize)
    {
        return LocationRatio * 0.5f * screenSize;
    }
}

public class StatusScreen
{
    public int Index;
    public IMyTextSurface Surface;
    public Dictionary<string, MissileSpriteData> MissileSprites = new Dictionary<string, MissileSpriteData>();

    public StatusScreen(IMyTextSurface surf, int idx)
    {
        Surface = surf;
        Index = idx;
    }

    public void AddData(string name, MissileSpriteData data)
    {
        MissileSprites[name] = data;
    }

    public void Update(Dictionary<string, bool> missileStatuses)
    {
        foreach (var kvp in MissileSprites)
        {
            bool ready = false;
            missileStatuses.TryGetValue(kvp.Key, out ready);
            kvp.Value.Ready = ready;
        }
    }
} 

bool CollectBlocks(IMyTerminalBlock b)
{
    var tsp = b as IMyTextSurfaceProvider;
    if (tsp == null)
        return false;

    bool singleScreen = tsp.SurfaceCount == 1;

    _ini.Clear();
    bool parsed = _ini.TryParse(b.CustomData);
    if (!parsed && !string.IsNullOrWhiteSpace(b.CustomData))
    {
        _ini.EndContent = b.CustomData;
    }

    _sectionNames.Clear();
    _ini.GetSections(_sectionNames);

    foreach (string name in _sectionNames)
    {
        if (StringExtensions.Contains(name, IniSectionGeneral))
            continue;

        if (!StringExtensions.Contains(name, _missileGroupNameTag))
            continue;

        _missileStatuses[name] = false;

        // Read ini
        Vector2 locationRatio = MyIniHelper.GetVector2(name, IniSpriteLocation, _ini);
        float scale = _ini.Get(name, IniSpriteScale).ToSingle(1f);
        int index = 0;
        if (!singleScreen)
        {
            index = _ini.Get(name, IniSpriteScreen).ToInt32(index);
        }
        index = MathHelper.Clamp(index, 0, tsp.SurfaceCount - 1);

        // Save sprite data and screen
        var surf = tsp.GetSurface(index);
        if (!_textSurfaces.Contains(surf))
            _textSurfaces.Add(surf);
        StatusScreen statusScreen;
        if (!_statusScreens.TryGetValue(surf, out statusScreen))
        {
            statusScreen = new StatusScreen(surf, index);
            _statusScreens[surf] = statusScreen;
        }
        var data = new MissileSpriteData(locationRatio, scale);
        statusScreen.AddData(name, data);

        // Write ini
        MyIniHelper.SetVector2(name, IniSpriteLocation, ref locationRatio, _ini);
        _ini.Set(name, IniSpriteScale, scale);
        if (!singleScreen)
        {
            _ini.Set(name, IniSpriteScreen, index);
        }
    }

    // Write ini output
    string output = _ini.ToString();
    if (b.CustomData != output)
    {
        b.CustomData = output;
    }
    return false;
}

void Setup()
{
    ParseIni();

    // Fetch blocks
    _textSurfaces.Clear();
    _statusScreens.Clear();
    _missileStatuses.Clear();
    _missileNames.Clear();

    _screenGroup = GridTerminalSystem.GetBlockGroupWithName(_screenNameTag);
    if (_screenGroup != null)
    {
        _screenGroup.GetBlocks(null, CollectBlocks);
    }
    
    foreach (var kvp in _missileStatuses)
    {
        _missileNames.Add(kvp.Key);
    }

    GenerateTitleSprites();
}

void ParseIni()
{
    _ini.Clear();

    if (_ini.TryParse(Me.CustomData))
    {
        // General section parsing
        _screenNameTag = _ini.Get(IniSectionGeneral, IniScreenGroupName).ToString(_screenNameTag);
        _missileGroupNameTag = _ini.Get(IniSectionGeneral, IniMissileGroupName).ToString(_missileGroupNameTag);
        _showTitleBar = _ini.Get(IniSectionGeneral, IniShowTitleBar).ToBoolean(_showTitleBar);
        _titleScale = _ini.Get(IniSectionGeneral, IniTitleScale).ToSingle(_titleScale);
        _missileReadyColor = MyIniHelper.GetColor(IniSectionGeneral, IniReadyColor, _ini, _missileReadyColor);
        _missileFiredColor = MyIniHelper.GetColor(IniSectionGeneral, IniFiredColor, _ini, _missileFiredColor);
        _titleBarColor = MyIniHelper.GetColor(IniSectionGeneral, IniTitleBarColor, _ini, _titleBarColor);
        _titleTextColor = MyIniHelper.GetColor(IniSectionGeneral, IniTitleTextColor, _ini, _titleTextColor);
        _backgroundColor = MyIniHelper.GetColor(IniSectionGeneral, IniBackgroundColor, _ini, _backgroundColor);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(IniSectionGeneral, IniScreenGroupName, _screenNameTag);
    _ini.Set(IniSectionGeneral, IniMissileGroupName, _missileGroupNameTag);
    _ini.Set(IniSectionGeneral, IniShowTitleBar, _showTitleBar);
    _ini.Set(IniSectionGeneral, IniTitleScale, _titleScale);
    MyIniHelper.SetColor(IniSectionGeneral, IniReadyColor, _missileReadyColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniFiredColor, _missileFiredColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniTitleBarColor, _titleBarColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniTitleTextColor, _titleTextColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniBackgroundColor, _backgroundColor, _ini);

    string iniOutput = _ini.ToString();
    if (iniOutput.Equals(Me.CustomData))
        return;

    Me.CustomData = iniOutput;
}

void GetStatuses()
{
    foreach (var name in _missileNames)
    {
        _missilePrograms.Clear();
        bool present = false;
        var group = GridTerminalSystem.GetBlockGroupWithName(name);
        if (group != null)
        {
            group.GetBlocksOfType<IMyProgrammableBlock>(_missilePrograms);
            present = _missilePrograms.Count > 0;
        }
        _missileStatuses[name] = present;
    }
}

void DrawScreens(float startProportion, float endProportion, bool clearSpriteCache)
{
    int start = (int)(startProportion * _textSurfaces.Count);
    int end = (int)(endProportion * _textSurfaces.Count);
    for (int i = start; i < end; ++i)
    {
        var surface = _textSurfaces[i];
        StatusScreen screen;
        if (!_statusScreens.TryGetValue(surface, out screen))
            continue;

        screen.Update(_missileStatuses);

        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";
        surface.ScriptBackgroundColor = _backgroundColor;

        Vector2 textureSize = surface.TextureSize;
        Vector2 screenCenter = textureSize * 0.5f;
        Vector2 viewportSize = surface.SurfaceSize;
        Vector2 scale = viewportSize / 512f;
        Vector2 offset = (textureSize - viewportSize) * 0.5f;
        float minScale = Math.Max(scale.X, scale.Y);

        using (var frame = surface.DrawFrame())
        {
            if (clearSpriteCache)
            {
                frame.Add(new MySprite());
            }

            if (_showTitleBar)
            {
                // We handle title differently
                var titleBarSize = new Vector2(viewportSize.X, _titleScale * scale.Y * TitleBarHeight);
                var titleBarPosition = 0.5f * titleBarSize + offset;
                var titleBarTextSize = _titleScale * scale.Y * TitleTextSize;
                var titleBarTextPosition = titleBarPosition + new Vector2(0, -titleBarTextSize * BaseTextHeightPx * 0.5f);

                var titleBar = new MySprite(SpriteType.TEXTURE, "SquareSimple", titleBarPosition, titleBarSize, _titleBarColor, rotation: 0);
                var titleText = new MySprite(SpriteType.TEXT, TitleBarText, titleBarTextPosition, null, _titleTextColor, "DEBUG", rotation: titleBarTextSize, alignment: TextAlignment.CENTER);
                frame.Add(titleBar);
                frame.Add(titleText);
            }

            foreach (var missileSpriteData in screen.MissileSprites)
            {
                Color colorOverride = missileSpriteData.Value.Ready ? _missileReadyColor : _missileFiredColor;
                float spriteScale = missileSpriteData.Value.Scale;
                Vector2 spriteOffset = missileSpriteData.Value.GetLocationPx(viewportSize);
                foreach (var spriteContainer in _missileSprites)
                {
                    frame.Add(spriteContainer.CreateSprite(minScale * spriteScale, screenCenter + spriteOffset, colorOverride));
                }
                foreach (var spriteContainer in _missileShadowSprites)
                {
                    frame.Add(spriteContainer.CreateSprite(minScale * spriteScale, screenCenter + spriteOffset, _backgroundColor));
                }
            }
        }
    }
}

// Default sizes
const float TitleTextSize = 1.5f;
const float BaseTextHeightPx = 37f;
const float TitleTextOffset = 0.5f * BaseTextHeightPx * TitleTextSize;
const float DefaultScreenSize = 512;
const float DefaultScreenHalfSize = DefaultScreenSize * 0.5f;
const float TitleBarHeight = 64;
readonly Vector2 TitleBarSize = new Vector2(512, 64);

// Default Positions
readonly Vector2 TitleBarPos = new Vector2(0, -DefaultScreenHalfSize + 32); //TODO: compute in ctor
readonly Vector2 TitleBarTextPos = new Vector2(0, -DefaultScreenHalfSize + 32 + TitleTextOffset);

const string TitleBarText = "WMI Missile Status";

void GenerateTitleSprites()
{
    _titleSprites.Clear();
    MySpriteContainer spriteContainer;
    spriteContainer = new MySpriteContainer("SquareSimple", TitleBarSize, TitleBarPos, 0f, _titleBarColor);
    _titleSprites.Add(spriteContainer);

    spriteContainer = new MySpriteContainer(TitleBarText, "DEBUG", TitleTextSize, TitleBarTextPos, _titleTextColor);
    _titleSprites.Add(spriteContainer);
}

// Default Positions
const float MissileBaseWidth = 12f;
void GenerateMissileSprites()
{
    _missileSprites.Clear();

    MySpriteContainer container;

    container = new MySpriteContainer("SquareSimple", new Vector2(MissileBaseWidth, 4f * MissileBaseWidth), new Vector2(0, 0), 0f, _missileReadyColor);
    _missileSprites.Add(container);

    container = new MySpriteContainer("SquareSimple", new Vector2(2f * MissileBaseWidth, MissileBaseWidth), new Vector2(0, 2.5f * MissileBaseWidth), 0f, _missileReadyColor);
    _missileSprites.Add(container);

    container = new MySpriteContainer("Circle", new Vector2(MissileBaseWidth, 2f * MissileBaseWidth), new Vector2(0, -2f * MissileBaseWidth), 0f, _missileReadyColor);
    _missileSprites.Add(container);

    container = new MySpriteContainer("Triangle", new Vector2(2f * MissileBaseWidth, 2f * MissileBaseWidth), new Vector2(0, -1.5f * MissileBaseWidth), 0f, _missileReadyColor);
    _missileSprites.Add(container);

    container = new MySpriteContainer("Triangle", new Vector2(2f * MissileBaseWidth, 2f * MissileBaseWidth), new Vector2(0, 1f * MissileBaseWidth), 0f, _missileReadyColor);
    _missileSprites.Add(container);

    container = new MySpriteContainer("SquareSimple", new Vector2(1f / 6f * MissileBaseWidth, 2f * MissileBaseWidth), new Vector2(0, 2f * MissileBaseWidth), 0f, _backgroundColor);
    _missileShadowSprites.Add(container);

    container = new MySpriteContainer("SquareSimple", new Vector2(1f / 6f * MissileBaseWidth, 1f * MissileBaseWidth), new Vector2(0, -1f * MissileBaseWidth), 0f, _backgroundColor);
    _missileShadowSprites.Add(container);

    container = new MySpriteContainer("SquareSimple", new Vector2(1f / 6f * MissileBaseWidth, 6f * MissileBaseWidth), new Vector2((1f + 1f / 6f) * 0.5f * MissileBaseWidth, 0), 0f, _backgroundColor);
    _missileShadowSprites.Add(container);

    container = new MySpriteContainer("SquareSimple", new Vector2(1f / 6f * MissileBaseWidth, 6f * MissileBaseWidth), new Vector2((1f + 1f / 6f) * -0.5f * MissileBaseWidth, 0), 0f, _backgroundColor);
    _missileShadowSprites.Add(container);

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

    const float DEFAULT_SURFACE_WIDTH = 512f; //px

    public MySpriteContainer(string spriteName, Vector2 size, Vector2 positionFromCenter, float rotation, Color color)
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
    }

    public MySprite CreateSprite(float scale, Vector2 center, Color? colorOverride = null)
    {
        return CreateSprite(scale, scale, center, colorOverride);
    }

    public MySprite CreateSprite(float widthScale, float heightScale, Vector2 center, Color? colorOverride = null)
    {
        Color color = _color;
        if (colorOverride.HasValue)
            color = colorOverride.Value;

        Vector2 scaleVec = new Vector2(widthScale, heightScale);
        Vector2 scaledPosition = scaleVec * _positionFromCenter;
        Vector2 scaledSize = scaleVec * _size;

        if (!_isText)
            return new MySprite(SpriteType.TEXTURE, _spriteName, center + scaledPosition, scaledSize, color, rotation: _rotationOrScale);
        else
            return new MySprite(SpriteType.TEXT, _text, center + scaledPosition, null, color, _font, rotation: _rotationOrScale * heightScale, alignment: _textAlign);
    }
}

public static class MyIniHelper
{
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void SetVector2(string sectionName, string vectorName, ref Vector2 vector, MyIni ini)
    {
        string vectorString = string.Format("{0}, {1}", vector.X, vector.Y);
        ini.Set(sectionName, vectorName, vectorString);
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector2 GetVector2(string sectionName, string vectorName, MyIni ini, Vector2? defaultVector = null)
    {
        string vectorString = ini.Get(sectionName, vectorName).ToString("null");
        string[] stringSplit = vectorString.Split(',');

        float x, y;
        if (stringSplit.Length != 2)
        {
            if (defaultVector.HasValue)
                return defaultVector.Value;
            else
                return default(Vector2);
        }

        float.TryParse(stringSplit[0].Trim(), out x);
        float.TryParse(stringSplit[1].Trim(), out y);

        return new Vector2(x, y);
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
    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultColor = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length != 4)
        {
            if (defaultColor.HasValue)
                return defaultColor.Value;
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

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

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

#region Runtime tracker
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
