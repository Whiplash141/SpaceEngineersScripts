
#region Missile Status Screens
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

const string VERSION = "1.8.2";
const string DATE = "2024/04/08";

public enum MissileStatus { Ready, Inactive, Fired };

List<IMyTextSurface> _textSurfaces = new List<IMyTextSurface>();
Dictionary<IMyTextSurface, StatusScreen> _statusScreens = new Dictionary<IMyTextSurface, StatusScreen>();
List<string> _missileNames = new List<string>();
List<IMyProgrammableBlock> _missilePrograms = new List<IMyProgrammableBlock>();
Dictionary<string, MissileStatus> _missileStatuses = new Dictionary<string, MissileStatus>();

string _screenNameTag = "Missile Status";
string _missileGroupNameTag = "Missile";

const string IniSectionGeneral = "Missile Status Screens - General Config";
const string IniScreenGroupName = "Status screen group name";
const string IniMissileGroupName = "Missile group name tag";
const string IniReadyColor = "Missile ready color";
const string IniInactiveColor = "Missile inactive color";
const string IniFiredColor = "Missile fired color";
const string IniTitleBarColor = "Title bar color";
const string IniTitleTextColor = "Title text color";
const string IniBackgroundColor = "Background color";
const string IniShowTitleBar = "Show title bar";
const string IniTitleScale = "Title scale";
const string IniGlobalSpriteScale = "Global sprite scale";
const string IniSpriteList = "Sprite list";

// Missile sprite config
const string IniSpriteScale = "Sprite scale";
const string IniSpriteLocation = "Sprite location";
const string IniSpriteRotation = "Sprite rotation (deg)";
const string IniSpriteScreen = "Screen index to display on";

// Texture sprite config
const string IniSectionTextureTemplate = "Texture:";
const string IniKeyTextureName = "Type";
const string IniKeyTexturePos = "Position";
const string IniKeyTextureSize = "Size";
const string InkKeyTextureColor = "Color";
const string IniKeyTextureRotation = "Rotation";

// Text sprite config
const string IniSectionTextTemplate = "Text:";
const string IniKeyTextContent = "Text";
const string IniKeyTextPos = "Position";
const string IniKeyTextColor = "Color";
const string IniKeyTextFont = "Font";
const string IniKeyTextScale = "Scale";

const string IniCommentSpriteList = " You can create your own sprites using the SE Sprite Builder\n https://gitlab.com/whiplash141/spritebuilder/-/wikis/home";

MyIni _ini = new MyIni();
List<string> _sectionNames = new List<string>();
List<MySprite> _spriteListing = new List<MySprite>();
List<IMyProgrammableBlock> _programs = new List<IMyProgrammableBlock>();

Color _backgroundColor = new Color(0, 0, 0);
Color _titleTextColor = new Color(150, 150, 150);
Color _titleBarColor = new Color(25, 25, 25);
Color _missileReadyColor = new Color(0, 75, 0);
Color _missileInactiveColor = new Color(75, 75, 0);
Color _missileFiredColor = new Color(25, 25, 25);

Scheduler _scheduler;
bool _showTitleBar = false;
bool _clearSpriteCache = false;
float _titleScale = 1f;
float _spriteScale = 0.25f;

IMyBlockGroup _screenGroup;

RuntimeTracker _runtimeTracker;

Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;

    _scheduler = new Scheduler(this);
    _scheduler.AddScheduledAction(GetStatuses, 3);
    _scheduler.AddScheduledAction(DrawScreens, 6);
    _scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    _scheduler.AddScheduledAction(() => _clearSpriteCache = !_clearSpriteCache, 0.1);

    _runtimeTracker = new RuntimeTracker(this);

    Setup();
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}

void PrintDetailedInfo()
{
    Echo($"WMI Missile Status Online...\n(Version {VERSION} - {DATE})\n\nRecompile to process custom data changes");
    if (_screenGroup == null)
        Echo($"\nERROR: No block group named\n  '{_screenNameTag}'!");
    Echo($"\nText Surfaces: {_textSurfaces.Count}\n");
    Echo(_runtimeTracker.Write());
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

        _missileStatuses[name] = MissileStatus.Fired;

        // Read ini
        Vector2 locationRatio = MyIniHelper.DeprecatedGetVector2(name, IniSpriteLocation, _ini);
        float scale = _ini.Get(name, IniSpriteScale).ToSingle(1f);
        float rotation = MathHelper.ToRadians(_ini.Get(name, IniSpriteRotation).ToSingle(0f));
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
        var data = new MissileSpriteData(locationRatio, scale, rotation);
        statusScreen.AddData(name, data);

        // Write ini
        MyIniHelper.DeprecatedSetVector2(name, IniSpriteLocation, ref locationRatio, _ini);
        _ini.Set(name, IniSpriteScale, scale);
        _ini.Set(name, IniSpriteRotation, MathHelper.ToDegrees(rotation));
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
}

void ParseIni()
{
    _ini.Clear();

    string spriteList = "";
    _spriteListing.Clear();

    if (_ini.TryParse(Me.CustomData))
    {
        // General section parsing
        _screenNameTag = _ini.Get(IniSectionGeneral, IniScreenGroupName).ToString(_screenNameTag);
        _missileGroupNameTag = _ini.Get(IniSectionGeneral, IniMissileGroupName).ToString(_missileGroupNameTag);
        _showTitleBar = _ini.Get(IniSectionGeneral, IniShowTitleBar).ToBoolean(_showTitleBar);
        _titleScale = _ini.Get(IniSectionGeneral, IniTitleScale).ToSingle(_titleScale);
        _missileReadyColor = MyIniHelper.GetColor(IniSectionGeneral, IniReadyColor, _ini, _missileReadyColor);
        _missileInactiveColor = MyIniHelper.GetColor(IniSectionGeneral, IniInactiveColor, _ini, _missileInactiveColor);
        _missileFiredColor = MyIniHelper.GetColor(IniSectionGeneral, IniFiredColor, _ini, _missileFiredColor);
        _titleBarColor = MyIniHelper.GetColor(IniSectionGeneral, IniTitleBarColor, _ini, _titleBarColor);
        _titleTextColor = MyIniHelper.GetColor(IniSectionGeneral, IniTitleTextColor, _ini, _titleTextColor);
        _backgroundColor = MyIniHelper.GetColor(IniSectionGeneral, IniBackgroundColor, _ini, _backgroundColor);
        _spriteScale = _ini.Get(IniSectionGeneral, IniGlobalSpriteScale).ToSingle(_spriteScale);
        spriteList = _ini.Get(IniSectionGeneral, IniSpriteList).ToString(spriteList);

        if (!string.IsNullOrWhiteSpace(spriteList))
        {
            string[] spriteLines = spriteList.Split('\n');
            ParseSpriteList(ref _ini, spriteLines, _spriteListing, _spriteScale);
        }
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
    MyIniHelper.SetColor(IniSectionGeneral, IniInactiveColor, _missileInactiveColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniFiredColor, _missileFiredColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniTitleBarColor, _titleBarColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniTitleTextColor, _titleTextColor, _ini);
    MyIniHelper.SetColor(IniSectionGeneral, IniBackgroundColor, _backgroundColor, _ini);
    _ini.Set(IniSectionGeneral, IniGlobalSpriteScale, _spriteScale);
    _ini.Set(IniSectionGeneral, IniSpriteList, spriteList);

    _ini.SetComment(IniSectionGeneral, IniSpriteList, IniCommentSpriteList);

    string iniOutput = _ini.ToString();
    if (iniOutput.Equals(Me.CustomData))
        return;

    Me.CustomData = iniOutput;
}

void ParseSpriteList(ref MyIni ini, string[] spriteSections, List<MySprite> spriteList, float spriteScale)
{
    spriteList.Clear();
    foreach (var spriteSectionName in spriteSections)
    {
        if (!ini.ContainsSection(spriteSectionName))
        {
            Echo($"WARNING: Could not find sprite section '{spriteSectionName}'");
            continue;
        }

        if (spriteSectionName.StartsWith(IniSectionTextureTemplate))
        {
            string name = ini.Get(spriteSectionName, IniKeyTextureName).ToString();
            Vector2 position = MyIniHelper.GetVector2(spriteSectionName, IniKeyTexturePos, ini);
            Vector2 size = MyIniHelper.GetVector2(spriteSectionName, IniKeyTextureSize, ini);
            Color color = MyIniHelper.GetColor(spriteSectionName, InkKeyTextureColor, ini, Color.White);
            float rotation = ini.Get(spriteSectionName, IniKeyTextureRotation).ToSingle();
            spriteList.Add(new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Alignment = TextAlignment.CENTER,
                Data = name,
                Position = position * spriteScale,
                Size = size * spriteScale,
                Color = color,
                RotationOrScale = rotation,
            });
        }
        else if (spriteSectionName.StartsWith(IniSectionTextTemplate))
        {
            string content = ini.Get(spriteSectionName, IniKeyTextContent).ToString();
            Vector2 position = MyIniHelper.GetVector2(spriteSectionName, IniKeyTextPos, ini);
            Color color = MyIniHelper.GetColor(spriteSectionName, IniKeyTextColor, ini, Color.White);
            string font = ini.Get(spriteSectionName, IniKeyTextFont).ToString("Debug");
            float scale = ini.Get(spriteSectionName, IniKeyTextScale).ToSingle();
            spriteList.Add(new MySprite()
            {
                Type = SpriteType.TEXT,
                Alignment = TextAlignment.LEFT,
                Data = content,
                Position = position * spriteScale,
                FontId = font,
                Color = color,
                RotationOrScale = scale * spriteScale,
            });
        }
        else
        {
            Echo($"WARNING: Unknown prefix for section '{spriteSectionName}'");
        }    
    }
}

void GetStatuses()
{
    foreach (var name in _missileNames)
    {
        _missilePrograms.Clear();
        var group = GridTerminalSystem.GetBlockGroupWithName(name);
        MissileStatus status = MissileStatus.Fired;
        if (group != null)
        {
            group.GetBlocksOfType<IMyProgrammableBlock>(_missilePrograms);
            bool present = _missilePrograms.Count > 0;
            if (present)
            {
                status = _missilePrograms[0].IsWorking ? MissileStatus.Ready : MissileStatus.Inactive;
            }
        }
        _missileStatuses[name] = status;
    }
}

// Default sizes
const float TitleTextSize = 1.5f;
const float BaseTextHeightPx = 28.8f;
const float TitleBarHeight = 64;
readonly Vector2 TitleBarSize = new Vector2(512, 64);
const string TitleBarText = "WMI Missile Status";

void DrawScreens()
{
    for (int i = 0; i < _textSurfaces.Count; ++i)
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
            if (_clearSpriteCache)
            {
                frame.Add(new MySprite());
            }

            if (_showTitleBar)
            {
                // We handle title differently
                var titleBarSize = new Vector2(viewportSize.X, _titleScale * scale.Y * TitleBarHeight);
                var titleBarPosition = 0.5f * titleBarSize + offset;
                var titleBarTextSize = _titleScale * scale.Y * TitleTextSize;
                var titleBarTextPosition = titleBarPosition - new Vector2(0, titleBarTextSize * BaseTextHeightPx * 0.5f);

                var titleBar = new MySprite(SpriteType.TEXTURE, "SquareSimple", titleBarPosition, titleBarSize, _titleBarColor, rotation: 0);
                var titleText = new MySprite(SpriteType.TEXT, TitleBarText, titleBarTextPosition, null, _titleTextColor, "Debug", rotation: titleBarTextSize, alignment: TextAlignment.CENTER);
                frame.Add(titleBar);
                frame.Add(titleText);
            }

            foreach (var missileSpriteData in screen.MissileSprites)
            {
                Color colorOverride;
                switch (missileSpriteData.Value.Status)
                {
                    case MissileStatus.Ready:
                        colorOverride = _missileReadyColor;
                        break;
                    case MissileStatus.Inactive:
                        colorOverride = _missileInactiveColor;
                        break;
                    default:
                    case MissileStatus.Fired:
                        colorOverride = _missileFiredColor;
                        break;
                }
                Vector2 spriteOffset = missileSpriteData.Value.GetLocationPx(viewportSize);
                float spriteScale = minScale * missileSpriteData.Value.Scale;
                float rotation = missileSpriteData.Value.Rotation;

                if (_spriteListing.Count > 0)
                {
                    Vector4 colorScale = colorOverride.ToVector4();

                    foreach (var sprite in _spriteListing)
                    {
                        var copy = sprite;
                        copy.Position = sprite.Position + screenCenter + spriteOffset;
                        copy.Color = new Color(sprite.Color.Value.ToVector4() * colorScale);
                        frame.Add(copy);
                    }
                }
                else
                {
                    DrawMissileSprites(frame, screenCenter + spriteOffset, colorOverride, _backgroundColor, spriteScale, rotation);
                }
            }
        }
    }
}

// 4x larger than base image
public void DrawMissileSprites(MySpriteDrawFrame frame, Vector2 centerPos, Color missileColor, Color backgroundColor, float scale, float rotation)
{
    float sin = (float)Math.Sin(rotation);
    float cos = (float)Math.Cos(rotation);
    scale *= _spriteScale;
    frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", new Vector2(-sin*-72f,cos*-72f)*scale+centerPos, new Vector2(96f,96f)*scale, missileColor, null, TextAlignment.CENTER, rotation)); // topFin
    frame.Add(new MySprite(SpriteType.TEXTURE, "Triangle", new Vector2(-sin*48f,cos*48f)*scale+centerPos, new Vector2(96f,96f)*scale, missileColor, null, TextAlignment.CENTER, rotation)); // bottomFin
    frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-sin*-96f,cos*-96f)*scale+centerPos, new Vector2(48f,96f)*scale, missileColor, null, TextAlignment.CENTER, rotation)); // noseCone
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-sin*120f,cos*120f)*scale+centerPos, new Vector2(96f,48f)*scale, missileColor, null, TextAlignment.CENTER, rotation)); // bottomFinBase
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(0f,0f)*scale+centerPos, new Vector2(48f,192f)*scale, missileColor, null, TextAlignment.CENTER, rotation)); // tube
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos*-28f,sin*-28f)*scale+centerPos, new Vector2(8f,288f)*scale, backgroundColor, null, TextAlignment.CENTER, rotation)); // shadowLeft
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos*28f,sin*28f)*scale+centerPos, new Vector2(8f,288f)*scale, backgroundColor, null, TextAlignment.CENTER, rotation)); // shadowRight
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-sin*-48f,cos*-48f)*scale+centerPos, new Vector2(8f,48f)*scale, backgroundColor, null, TextAlignment.CENTER, rotation)); // shadowCenterFinTop
    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(-sin*96f,cos*96f)*scale+centerPos, new Vector2(8f,96f)*scale, backgroundColor, null, TextAlignment.CENTER, rotation)); // shadowCenterFinBottom
}

#region Classes

public class MissileSpriteData
{
    public readonly Vector2 LocationRatio;
    public readonly float Scale;
    public readonly float Rotation;
    public MissileStatus Status;

    public MissileSpriteData(Vector2 locationRatio, float scale, float rotation)
    {
        LocationRatio = locationRatio;
        Scale = scale;
        Rotation = rotation;
        Status = MissileStatus.Ready;
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

    public void Update(Dictionary<string, MissileStatus> missileStatuses)
    {
        foreach (var kvp in MissileSprites)
        {
            MissileStatus status;
            if (!missileStatuses.TryGetValue(kvp.Key, out status))
            {
                status = MissileStatus.Fired;
            }
            kvp.Value.Status = status;
        }
    }
} 
#endregion
#endregion

#region INCLUDES
public static class MyIniHelper
{
    #region List<string>
    /// <summary>
    /// Deserializes a List<string> from MyIni
    /// </summary>
    public static void GetStringList(string section, string name, MyIni ini, List<string> list)
    {
        string raw = ini.Get(section, name).ToString(null);
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Preserve contents
            return;
        }

        list.Clear();
        string[] split = raw.Split('\n');
        foreach (var s in split)
        {
            list.Add(s);
        }
    }

    /// <summary>
    /// Serializes a List<string> to MyIni
    /// </summary>
    public static void SetStringList(string section, string name, MyIni ini, List<string> list)
    {
        string output = string.Join($"\n", list);
        ini.Set(section, name, output);
    }
    #endregion
    
    #region List<int>
    const char LIST_DELIMITER = ',';

    /// <summary>
    /// Deserializes a List<int> from MyIni
    /// </summary>
    public static void GetListInt(string section, string name, MyIni ini, List<int> list)
    {
        list.Clear();
        string raw = ini.Get(section, name).ToString();
        string[] split = raw.Split(LIST_DELIMITER);
        foreach (var s in split)
        {
            int i;
            if (int.TryParse(s, out i))
            {
                list.Add(i);
            }
        }
    }
    
    /// <summary>
    /// Serializes a List<int> to MyIni
    /// </summary>
    public static void SetListInt(string section, string name, MyIni ini, List<int> list)
    {
        string output = string.Join($"{LIST_DELIMITER}", list);
        ini.Set(section, name, output);
    }
    #endregion

    #region Vector2
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void SetVector2(string sectionName, string vectorName, ref Vector2 vector, MyIni ini)
    {
        ini.Set(sectionName, vectorName, vector.ToString());
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector2 GetVector2(string sectionName, string vectorName, MyIni ini, Vector2? defaultVector = null)
    {
        var vector = Vector2.Zero;
        if (TryParseVector2(ini.Get(sectionName, vectorName).ToString(), out vector))
            return vector;
        else if (defaultVector.HasValue)
            return defaultVector.Value;
        return default(Vector2);
    }

    static bool TryParseVector2(string source, out Vector2 vec)
    {
        // Source formatting {X:{0} Y:{1}}
        vec = default(Vector2);
        var fragments = source.Split(':', ' ', '{', '}');
        if (fragments.Length < 5)
            return false;
        if (!float.TryParse(fragments[2], out vec.X))
        {
            return false;
        }
        if (!float.TryParse(fragments[4], out vec.Y))
        {
            return false;
        }
        return true;
    }
    #endregion

    #region Vector2 Compat
    /// <summary>
    /// Adds a Vector3D to a MyIni object
    /// </summary>
    public static void DeprecatedSetVector2(string sectionName, string vectorName, ref Vector2 vector, MyIni ini)
    {
        string vectorString = string.Format("{0}, {1}", vector.X, vector.Y);
        ini.Set(sectionName, vectorName, vectorString);
    }

    /// <summary>
    /// Parses a MyIni object for a Vector3D
    /// </summary>
    public static Vector2 DeprecatedGetVector2(string sectionName, string vectorName, MyIni ini, Vector2? defaultVector = null)
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
    #endregion

    #region Vector3D
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
    #endregion

    #region ColorChar
    /// <summary>
    /// Adds a color character to a MyIni object
    /// </summary>
    public static void SetColorChar(string sectionName, string charName, char colorChar, MyIni ini)
    {
        int rgb = (int)colorChar - 0xe100;
        int b = rgb & 7;
        int g = rgb >> 3 & 7;
        int r = rgb >> 6 & 7;
        string colorString = $"{r}, {g}, {b}";

        ini.Set(sectionName, charName, colorString);
    }

    /// <summary>
    /// Parses a MyIni for a color character 
    /// </summary>
    public static char GetColorChar(string sectionName, string charName, MyIni ini, char defaultChar = (char)(0xe100))
    {
        string rgbString = ini.Get(sectionName, charName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0;
        if (rgbSplit.Length != 3)
            return defaultChar;

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);

        r = MathHelper.Clamp(r, 0, 7);
        g = MathHelper.Clamp(g, 0, 7);
        b = MathHelper.Clamp(b, 0, 7);

        return (char)(0xe100 + (r << 6) + (g << 3) + b);
    }
    #endregion

    #region Color
    /// <summary>
    /// Adds a Color to a MyIni object
    /// </summary>
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini, bool writeAlpha = true)
    {
        if (writeAlpha)
        {
            ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A));
        }
        else
        {
            ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}", color.R, color.G, color.B));
        }
    }

    /// <summary>
    /// Parses a MyIni for a Color
    /// </summary>
    public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
    {
        string rgbString = ini.Get(sectionName, itemName).ToString("null");
        string[] rgbSplit = rgbString.Split(',');

        int r = 0, g = 0, b = 0, a = 0;
        if (rgbSplit.Length < 3)
        {
            if (defaultChar.HasValue)
                return defaultChar.Value;
            else
                return Color.Transparent;
        }

        int.TryParse(rgbSplit[0].Trim(), out r);
        int.TryParse(rgbSplit[1].Trim(), out g);
        int.TryParse(rgbSplit[2].Trim(), out b);
        bool hasAlpha = rgbSplit.Length >= 4 && int.TryParse(rgbSplit[3].Trim(), out a);
        if (!hasAlpha)
            a = 255;

        r = MathHelper.Clamp(r, 0, 255);
        g = MathHelper.Clamp(g, 0, 255);
        b = MathHelper.Clamp(b, 0, 255);
        a = MathHelper.Clamp(a, 0, 255);

        return new Color(r, g, b, a);
    }
    #endregion
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
#endregion
