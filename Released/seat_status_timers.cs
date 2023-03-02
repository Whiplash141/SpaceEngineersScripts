/*
/ //// / Whip's Seat Entry/Exit Activated Timers / //// /

INSTRUCTIONS
1. Make a group named "SEAT" (all caps) that contains all the seats 
   that you want to add entry/exit timers to.
   - You can also include turrets and custom turret controllers in this group.
2. Place this script in a programmable block
3. Open the custom data of each seat in the "SEAT" group 
   and set the names of the timers that should be triggered. Leave
   them blank if you don't want a timer triggered.
4. Recompile the code to process *any* Custom Data or group changes.

- Whiplash141
*/

const string VERSION = "1.1.2";
const string DATE = "2023/03/02";

const string INI_SECTION_GENERAL = "Seat Entry/Exit Activated Timers - General Config";
const string INI_KEY_GROUP_NAME = "Group name";
const string INI_KEY_DRAW_SCREENS = "Draw title screen";

const string INI_SECTION_TIMER = "Seat Entry/Exit Activated Timers - {0} Config";
const string INI_KEY_TIMER_ENTRY = "Timer to trigger on entry";
const string INI_KEY_TIMER_EXIT = "Timer to trigger on exit";

bool _drawTitleScreen = true;
bool _isSetup = false;
string _groupName = "SEAT";
MyIni _ini = new MyIni();
StringBuilder _bobTheBuilder = new StringBuilder(128);
String _errorString = "";
List<StatusTimer> _statusTimers = new List<StatusTimer>();
SeatStatusTimersScreenManager _screenManager;
int _seatCount = 0;
int _turretCount = 0;
int _ctcCount = 0;

public interface IControllable
{
    bool IsUnderControl { get; }
    string CustomData { get; set; }
    string Type { get; }
}

public abstract class CustomDataBlock : IControllable
{
    private IMyTerminalBlock _block;
    
    public CustomDataBlock(IMyTerminalBlock block) { _block = block; }
    
    public abstract bool IsUnderControl { get; }
    
    public abstract string Type { get; }
    
    public string CustomData
    {
        get { return _block.CustomData; }
        set { _block.CustomData = value; }
    }
} 

public class ShipControlBlock : CustomDataBlock
{
    private IMyShipController _ctrl;
    
    public ShipControlBlock(IMyShipController controller) : base(controller) { _ctrl = controller; }
    
    public override bool IsUnderControl => _ctrl.IsUnderControl;
    
    public override string Type => "Seat";
}

public class TurretBlock : CustomDataBlock
{
    private IMyLargeTurretBase _turret;
    
    public TurretBlock(IMyLargeTurretBase turret) : base(turret) { _turret = turret; }
    
    public override bool IsUnderControl => _turret.IsUnderControl;
    
    public override string Type => "Turret";
}

public class CustomTurretControlBlock : CustomDataBlock
{
    private IMyTurretControlBlock _tcb;
    
    public CustomTurretControlBlock(IMyTurretControlBlock tcb) : base(tcb) { _tcb = tcb; }
    
    public override bool IsUnderControl => _tcb.IsUnderControl;
    
    public override string Type => "CTC";
}

class StatusTimer
{
    IControllable _controller;
    IMyTimerBlock _entryTimer;
    IMyTimerBlock _exitTimer;
    MyIni _ini = new MyIni();
    Program _p;

    bool _firstRun = true;
    bool _wasControlled = false;

    public StatusTimer(IControllable controller, Program program)
    {
        _p = program;
        _controller = controller;
        string entryTimerName = "", exitTimerName = "";
        _ini.Clear();
        string sectionName = string.Format(INI_SECTION_TIMER, _controller.Type);
        if (_ini.TryParse(_controller.CustomData))
        {
            entryTimerName = _ini.Get(sectionName, INI_KEY_TIMER_ENTRY).ToString();
            exitTimerName = _ini.Get(sectionName, INI_KEY_TIMER_EXIT).ToString();
            _entryTimer = program.GridTerminalSystem.GetBlockWithName(entryTimerName) as IMyTimerBlock;
            _exitTimer = program.GridTerminalSystem.GetBlockWithName(exitTimerName) as IMyTimerBlock;
        }
        _ini.Set(sectionName, INI_KEY_TIMER_ENTRY, entryTimerName);
        _ini.Set(sectionName, INI_KEY_TIMER_EXIT, exitTimerName);

        string output = _ini.ToString();
        if (output != _controller.CustomData)
        {
            _controller.CustomData = output;
        }
    }

    public void Update()
    {
        bool controlled = _controller.IsUnderControl;
        if (!_firstRun && controlled != _wasControlled) // New state
        {
            if (controlled)
            {
                if (_entryTimer != null)
                    _entryTimer.Trigger();
            }
            else
            {
                if (_exitTimer != null)
                    _exitTimer.Trigger();
            }
        }

        _wasControlled = controlled;
        _firstRun = false;
    }
}

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;
    _screenManager = new SeatStatusTimersScreenManager(VERSION, this);
    GetBlocks();
}

void Main(string arg, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update10) != 0)
    {
        foreach (var x in _statusTimers)
        {
            x.Update();
        }

        if (_drawTitleScreen)
        {
            _screenManager.Draw();
        }
    }
    
    if ((updateSource & UpdateType.Update100) != 0)
    {        
        Echo($"Seat Entry Activated\nTimers Running...\n(Version {VERSION} - {DATE})\n");
        if (_isSetup)
        {
            Echo($"Found:\n - {_seatCount} status seats\n - {_turretCount} turrets\n - {_ctcCount} custom turret controllers\n");
        }
        else
        {
            Echo(_errorString);
        }
        Echo($"Recompile to process block group\nand Custom Data changes.");
        _screenManager.RestartDraw();
    }
}

void GetBlocks()
{
    _ini.Clear();
    _bobTheBuilder.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _groupName = _ini.Get(INI_SECTION_GENERAL, INI_KEY_GROUP_NAME).ToString(_groupName);
        _drawTitleScreen = _ini.Get(INI_SECTION_GENERAL, INI_KEY_DRAW_SCREENS).ToBoolean(_drawTitleScreen);
    }
    _ini.Set(INI_SECTION_GENERAL, INI_KEY_GROUP_NAME, _groupName);
    _ini.Set(INI_SECTION_GENERAL, INI_KEY_DRAW_SCREENS, _drawTitleScreen);

    string output = _ini.ToString();
    if (output != Me.CustomData)
    {
        Me.CustomData = output;
    }

    _seatCount = 0;
    _turretCount = 0;
    _ctcCount = 0;
    _statusTimers.Clear();
    var group = GridTerminalSystem.GetBlockGroupWithName(_groupName);
    if (group == null)
    {
        _isSetup = false;
        _bobTheBuilder.Append($"ERROR: No group named\n'{_groupName}' found!\n");
    }
    else
    {
        group.GetBlocksOfType<IMyTerminalBlock>(null, CollectBlocks);
        _isSetup = true;
    }
    _errorString = _bobTheBuilder.ToString();
}

bool CollectBlocks(IMyTerminalBlock b)
{
    var sc = b as IMyShipController;
    if (sc != null)
    {
        _statusTimers.Add(new StatusTimer(new ShipControlBlock(sc), this));
        _seatCount++;
    }
    
    var turret = b as IMyLargeTurretBase;
    if (turret != null)
    {
        _statusTimers.Add(new StatusTimer(new TurretBlock(turret), this));
        _turretCount++;
    }
    
    var tcb = b as IMyTurretControlBlock;
    if (tcb != null)
    {
        _statusTimers.Add(new StatusTimer(new CustomTurretControlBlock(tcb), this));
        _ctcCount++;
    }
    return false;
}

class SeatStatusTimersScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.5f;
    const float BaseTextHeightPx = 37f;
    
    const float ChairSpriteScale = 1f;
    const float TimerSpriteScale = 1f;
    
    const string Font = "Debug";
    const string TitleFormat = "SEAT - v{0}";
    readonly string _titleText;

    Program _program;

    int _idx = 0;
    readonly Vector2 _chairPos = new Vector2(-100, 40);
    readonly Vector2 _enterTimerPos = new Vector2(100, -20);
    readonly Vector2 _exitTimerPos = new Vector2(100, 100);
    
    static readonly Color _timerBlinkColor = new Color(0, 100, 255, 255);
    static readonly Color _timerIdleColor = new Color(0, 255, 0);

    struct AnimationParams
    {
        public readonly Color TimerEnterColor;
        public readonly Color TimerExitColor;
        public readonly bool DrawPerson;

        public AnimationParams(bool drawPerson, Color enterColor, Color exitEolor)
        {
            DrawPerson = drawPerson;
            TimerEnterColor = enterColor;
            TimerExitColor = exitEolor;
        }
    }

    AnimationParams[] _animSequence = new AnimationParams[] {
        // Enter
        new AnimationParams(true, _timerIdleColor, _timerIdleColor),
        new AnimationParams(true, _timerBlinkColor, _timerIdleColor),
        new AnimationParams(true, _timerBlinkColor, _timerIdleColor),
        new AnimationParams(true, _timerIdleColor, _timerIdleColor),
        new AnimationParams(true, _timerIdleColor, _timerIdleColor),
        new AnimationParams(true, _timerIdleColor, _timerIdleColor),
        new AnimationParams(true, _timerIdleColor, _timerIdleColor),
        new AnimationParams(true, _timerIdleColor, _timerIdleColor),
        // Exit
        new AnimationParams(false, _timerIdleColor, _timerIdleColor),
        new AnimationParams(false, _timerIdleColor, _timerBlinkColor),
        new AnimationParams(false, _timerIdleColor, _timerBlinkColor),
        new AnimationParams(false, _timerIdleColor, _timerIdleColor),
        new AnimationParams(false, _timerIdleColor, _timerIdleColor),
        new AnimationParams(false, _timerIdleColor, _timerIdleColor),
        new AnimationParams(false, _timerIdleColor, _timerIdleColor),
        new AnimationParams(false, _timerIdleColor, _timerIdleColor),
    };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public SeatStatusTimersScreenManager(string version, Program program)
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

        using (var frame = _surface.DrawFrame())
        {
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }

            DrawChair(frame, screenCenter + _chairPos * minScale, anim.DrawPerson, ChairSpriteScale * minScale);
            DrawTimer(frame, screenCenter + _enterTimerPos * minScale, anim.TimerEnterColor, TimerSpriteScale * minScale);
            DrawTimer(frame, screenCenter + _exitTimerPos * minScale, anim.TimerExitColor, TimerSpriteScale * minScale);

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

    public void DrawChair(MySpriteDrawFrame frame, Vector2 centerPos, bool drawPerson, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,50f)*scale+centerPos, new Vector2(100f,30f)*scale, _white, null, TextAlignment.CENTER, 0f)); // seat chair
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(50f,50f)*scale+centerPos, new Vector2(30f,30f)*scale, _white, null, TextAlignment.CENTER, 0f)); // seat front
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-63f,-24f)*scale+centerPos, new Vector2(30f,150f)*scale, _white, null, TextAlignment.CENTER, -0.1745f)); // seat back
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-50f,50f)*scale+centerPos, new Vector2(30f,30f)*scale, _white, null, TextAlignment.CENTER, 0f)); // seat corner
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-76f,-98f)*scale+centerPos, new Vector2(30f,30f)*scale, _white, null, TextAlignment.CENTER, 0f)); // head rest
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,85f)*scale+centerPos, new Vector2(10f,30f)*scale, _white, null, TextAlignment.CENTER, 0f)); // stilt
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,105f)*scale+centerPos, new Vector2(100f,10f)*scale, _white, null, TextAlignment.CENTER, 0f)); // base
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(50f,105f)*scale+centerPos, new Vector2(10f,10f)*scale, _white, null, TextAlignment.CENTER, 0f)); // base corner right
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-50f,105f)*scale+centerPos, new Vector2(10f,10f)*scale, _white, null, TextAlignment.CENTER, 0f)); // base corner left
        if (!drawPerson)
        {
            return;
        }
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(26f,13f)*scale+centerPos, new Vector2(90f,10f)*scale, _white, null, TextAlignment.CENTER, 0f)); // person thigh
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-25f,-43f)*scale+centerPos, new Vector2(10f,120f)*scale, _white, null, TextAlignment.CENTER, -0.1745f)); // person spine
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(87f,45f)*scale+centerPos, new Vector2(10f,80f)*scale, _white, null, TextAlignment.CENTER, -0.5236f)); // person leg
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-41f,-135f)*scale+centerPos, new Vector2(50f,50f)*scale, _white, null, TextAlignment.CENTER, 0f)); // person head
    }
    
    public void DrawTimer(MySpriteDrawFrame frame, Vector2 centerPos, Color blinkColor, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,0f)*scale+centerPos, new Vector2(100f,100f)*scale, _white, null, TextAlignment.CENTER, 0f)); // block
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(20f,0f)*scale+centerPos, new Vector2(10f,100f)*scale, _black, null, TextAlignment.CENTER, 0f)); // right stripe
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,20f)*scale+centerPos, new Vector2(50f,10f)*scale, _black, null, TextAlignment.CENTER, 0f)); // bottom stripe
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,-20f)*scale+centerPos, new Vector2(50f,10f)*scale, _black, null, TextAlignment.CENTER, 0f)); // top stripe
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-20f,0f)*scale+centerPos, new Vector2(10f,100f)*scale, _black, null, TextAlignment.CENTER, 0f)); // left stripe
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,0f)*scale+centerPos, new Vector2(30f,30f)*scale, blinkColor, null, TextAlignment.CENTER, 0f)); // blinky bit
    }

    #endregion
}

