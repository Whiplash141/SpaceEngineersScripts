
/* 
/ //// / Whip's Planetary Compass Script / //// /

How do I use this? 

    1) Make a program block with this script loaded into it
    
    2) Make a ship controller (remote control, cockpit, or flight seat) pointing forward
        - (OPTIONAL) Add the phrase "Reference" somewhere in its name.
        - If no ship controller tagged "Reference" is detected, the code will use all ship controllers that it finds on the grid/subgrids
        
    3) Add "Compass" to the name of text panels or blocks with text surfaces that you want the compass displayed.
        - Blocks with text surfaces include: Cockpits, flight seats, programmable blocks
        - Configure which text surface the compass is displayed on within the block's custom data (You don't need to do this for text panels).

    4) You are good to go! :)

Be sure to drop by my workshop page and leave a comment :D
http://steamcommunity.com/sharedfiles/filedetails/?id=616627882

Code by Whiplash141 
*/

#region Fields
const string VERSION = "22.0.3";
const string DATE = "2024/10/03";

string _referenceNameTag = "Reference";
string _screenNameTag = "Compass";
const string SCREEN_NAME_TAG_COMPAT = "Bearing"; // For backwards compatibility

bool _clearSpriteCache = false;

List<TextSurfaceConfig> _textSurfaces = new List<TextSurfaceConfig>();
List<IMyShipController> _taggedControllers = new List<IMyShipController>();
List<IMyShipController> _allControllers = new List<IMyShipController>();
CircularBuffer<Action> _screenUpdateBuffer;

List<IMyShipController> ReferenceList
{
    get
    {
        return _taggedControllers.Count == 0 ? _allControllers : _taggedControllers;
    }
}

RuntimeTracker _runtimeTracker;
Compass _compass;
Scheduler _scheduler;
MyIni _ini = new MyIni();
MyIni _textSurfaceIni = new MyIni();
StringBuilder _customDataSB = new StringBuilder();
StringBuilder _detailedInfo = new StringBuilder();
ScheduledAction _setupScheduled;

const string INI_SECTION_GENERAL = "Compass - General Config";
const string INI_GENERAL_SCREEN_NAME = "Text surface name tag";
const string INI_GENERAL_REFERENCE_NAME = "Optional reference name tag";
const string INI_GENERAL_DRAW_BEARING = "Draw bearing text box";
const string INI_GENERAL_NORTH_VEC = "Absolute north vector";
const string INI_COMMENT_NORTH_VEC = "The rotation axis of the sun.\nDefault value is configured to work with the easy start planet worlds.";

const string INI_SECTION_COLORS = "Compass - Colors";
const string INI_COMMENT_COLORS = "Colors are defined with R,G,B,Alpha color codes where\nvalues can range from 0,0,0,0 [transparent] to 255,255,255,255 [white].";
const string INI_COLOR_BACKGROUND = "Background";
const string INI_COLOR_LINE = "Line";
const string INI_COLOR_TEXT = "Text";
const string INI_COLOR_PIP = "Needle";
const string INI_COLOR_TEXT_BOX = "Text Box";

const string INI_SECTION_APPEARANCE = "Compass - Appearance";
const string INI_APPEARANCE_RADIAL = "Draw radial compass";
const string INI_APPEARANCE_TEMPLATE_RADIAL = "Draw radial compass - screen {0}";

const string INI_SECTION_TEXT_SURF = "Compass - Text Surface Config";
const string INI_TEXT_SURF_TEMPLATE = "Show on screen {0}";

public struct CompassConfig
{
    public bool DrawBearing;
    public Vector3D AbsNorthVec;
    public Color BackgroundColor;
    public Color LineColor;
    public Color TextColor;
    public Color PipColor;
    public Color TextBoxColor;
}

CompassConfig _compassConfig = new CompassConfig()
{
    DrawBearing = true,
    AbsNorthVec = new Vector3D(0, -1, 0),
    BackgroundColor = new Color(0, 0, 0),
    LineColor = new Color(150, 150, 150),
    TextColor = new Color(150, 150, 150),
    PipColor = new Color(150, 150, 150),
    TextBoxColor = new Color(150, 150, 150),
};

public struct TextSurfaceConfig
{
    public readonly IMyTextSurface Surface;
    public bool DrawRadialCompass;
    
    public TextSurfaceConfig(IMyTextSurface surf)
    {
        Surface = surf;
        
        float ratio = surf.SurfaceSize.X / surf.SurfaceSize.Y;
        if (ratio > 1)
            DrawRadialCompass = ratio <= 2f;
        else
            DrawRadialCompass = ratio >= 0.5f;
    }
}
#endregion

#region Entrypoints
Program()
{
    _runtimeTracker = new RuntimeTracker(this, 120, 0.005);
    _compass = new Compass(this, ref _compassConfig);
    _scheduler = new Scheduler(this);

    _screenUpdateBuffer = new CircularBuffer<Action>(10);
    _screenUpdateBuffer.Add(ComputeCompassParams);
    _screenUpdateBuffer.Add(() => UpdateScreenRange(0f / 9f, 1f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(1f / 9f, 2f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(2f / 9f, 3f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(3f / 9f, 4f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(4f / 9f, 5f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(5f / 9f, 6f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(6f / 9f, 7f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(7f / 9f, 8f / 9f));
    _screenUpdateBuffer.Add(() => UpdateScreenRange(8f / 9f, 9f / 9f));

    _setupScheduled = new ScheduledAction(Setup, 0.1);

    _scheduler.AddScheduledAction(_setupScheduled);
    _scheduler.AddScheduledAction(UpdateNextScreens, 60);
    _scheduler.AddScheduledAction(WriteDetailedInfo, 1);

    Setup();
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

void Main(string arg, UpdateType updateSource)
{
    _runtimeTracker.AddRuntime();
    _scheduler.Update();
    _runtimeTracker.AddInstructions();
}
#endregion

#region Detailed info
void WriteDetailedInfo()
{
    _detailedInfo.Append($"Whip's Planetary Compass\n(Version {VERSION} - {DATE})\n\n");
    _detailedInfo.Append($"Next block refresh in {Math.Max(0, _setupScheduled.RunInterval - _setupScheduled.TimeSinceLastRun):n0} seconds\n\n");

    bool error = false;
    if (_textSurfaces.Count == 0)
    {
        _detailedInfo.Append($"> ERROR:\n   No text surfaces name tagged\n   \"{_screenNameTag}\" found!\n");
        error = true;
    }
    else
    {
        _detailedInfo.Append($"> INFO:\n   Drawing {_textSurfaces.Count} screen(s).\n");
    }

    if (_taggedControllers.Count == 0)
    {
        _detailedInfo.Append($"> INFO:\n   No ship controllers name tagged\n   \"{_referenceNameTag}\" found.\n   Using all ship controllers...\n");
        if (_allControllers.Count == 0)
        {
            _detailedInfo.Append($"> ERROR:\n   No ship controllers found!\n");
            error = true;
        }
    }
    else
    {
        _detailedInfo.Append($"> INFO:\n   Using name tagged ship\n   controllers...\n");
    }

    if (!error)
    {
        if (!_compass.InGravity)
        {
            _detailedInfo.Append($"> WARN:\n   No natural gravity!\n");
        }
        else
        {
            _detailedInfo.Append($"> INFO:\n   Bearing: {_compass.Bearing:n1}\n");
        }
    }
    
    _detailedInfo.Append(_runtimeTracker.Write());

    string output = _detailedInfo.ToString();
    Echo(output);
    _detailedInfo.Clear();
}
#endregion

#region Compass Work
void UpdateNextScreens()
{
    _screenUpdateBuffer.MoveNext().Invoke();
}

void UpdateScreenRange(float startProportion, float endProportion)
{
    int startInt = (int)Math.Round(startProportion * _textSurfaces.Count);
    int endInt = (int)Math.Round(endProportion * _textSurfaces.Count);

    for (int i = startInt; i < endInt; ++i)
    {
        var surf = _textSurfaces[i];
        _compass.DrawScreen(surf.Surface, _clearSpriteCache, surf.DrawRadialCompass);
    }
}

void ComputeCompassParams()
{
    if (ReferenceList.Count == 0)
        return;

    IMyShipController reference = GetControlledShipController(ReferenceList);
    if (reference == null)
        return;

    Vector3D forward = reference.WorldMatrix.Forward;
    Vector3D gravity = reference.GetNaturalGravity();

    _compass.CalculateParameters(ref forward, ref gravity);
}

public bool IsClosed(IMyTerminalBlock block)
{
    return GridTerminalSystem.GetBlockWithId(block.EntityId) == null;
}

IMyShipController GetControlledShipController(List<IMyShipController> shipControllers)
{
    if (shipControllers.Count == 0)
        return null;

    IMyShipController mainControlled = null;
    IMyShipController controlled = null;
    IMyShipController notClosed = null;

    foreach (IMyShipController b in shipControllers)
    {
        if (IsClosed(b))
            continue;

        if (notClosed == null)
        {
            notClosed = b;
        }

        if (b.IsUnderControl && b.CanControlShip)
        {
            if (controlled == null)
            {
                controlled = b;
            }

            if (b.IsMainCockpit)
            {
                mainControlled = b; // Only one per grid so no null check needed
            }
        }
    }

    if (mainControlled != null)
        return mainControlled;

    if (controlled != null)
        return controlled;

    return notClosed;
}

class Compass
{
    public bool InGravity = false;
    public double Bearing = 0;

    Vector3D _absNorthVec;
    Color _backgroundColor;
    Color _tickColor;
    Color _textColor;
    Color _pipColor;
    Color _textBoxColor;
    bool _drawBearing;

    readonly Program _program;
    readonly Vector2 PIP_SIZE = new Vector2(25f, 25f);
    readonly Vector2 TEXT_BOX_SIZE = new Vector2(FONT_SIZE * BASE_TEXT_HEIGHT_PX * 2.5f, FONT_SIZE * BASE_TEXT_HEIGHT_PX + 4f);
    readonly Vector2 TEXT_BOX_HORIZ_SPACING = new Vector2(FONT_SIZE * BASE_TEXT_HEIGHT_PX * 0.6f, 0);

    const double RAD_TO_DEG = 180.0 / Math.PI;
    const double FOV = 130;
    const double HALF_FOV = FOV * 0.5;
    const int MAJOR_TICK_INTERVAL = 45;
    const int MINOR_TICKS = 3;
    const int MINOR_TICK_INTERVAL = (int)(MAJOR_TICK_INTERVAL / MINOR_TICKS);

    const float FONT_SIZE = 1.8f;
    const float MAJOR_TICK_HEIGHT = 50f;
    const float MINOR_TICK_HEIGHT = MAJOR_TICK_HEIGHT / 2f;
    const float TICK_WIDTH = 6f;
    const float BASE_TEXT_HEIGHT_PX = 28.8f;
    const string FONT = "White";

    readonly Dictionary<int, string> _cardinalDirectionDict = new Dictionary<int, string>()
    {
        { 0,   "N"},
        { 45,  "NE" },
        { 90,  "E" },
        { 135, "SE" },
        { 180, "S" },
        { 225, "SW" },
        { 270, "W" },
        { 315, "NW" },
        { 360, "N" },
    };

    public Compass(Program program, ref CompassConfig compassConfig)
    {
        _program = program;
        UpdateConfigValues(ref compassConfig);
    }

    public void UpdateConfigValues(ref CompassConfig compassConfig)
    {
        _drawBearing = compassConfig.DrawBearing;
        _absNorthVec = compassConfig.AbsNorthVec;
        _backgroundColor = compassConfig.BackgroundColor;
        _tickColor = compassConfig.LineColor;
        _textColor = compassConfig.TextColor;
        _pipColor = compassConfig.PipColor;
        _textBoxColor = compassConfig.TextBoxColor;
    }

    public void CalculateParameters(ref Vector3D forward, ref Vector3D gravity)
    {
        if (Vector3D.IsZero(gravity))
        {
            InGravity = false;
            return;
        }
        InGravity = true;

        Vector3D relativeEastVec = gravity.Cross(_absNorthVec);

        Vector3D relativeNorthVec;
        Vector3D.Cross(ref relativeEastVec, ref gravity, out relativeNorthVec);

        Vector3D forwardProjNorthVec;
        VectorMathRef.Projection(ref forward, ref relativeNorthVec, out forwardProjNorthVec);
        Vector3D forwardProjEastVec;
        VectorMathRef.Projection(ref forward, ref relativeEastVec, out forwardProjEastVec);
        Vector3D forwardProjPlaneVec = forwardProjEastVec + forwardProjNorthVec;

        Bearing = VectorMathRef.AngleBetween(ref forwardProjPlaneVec, ref relativeNorthVec) * RAD_TO_DEG;

        if (Vector3D.Dot(forward, relativeEastVec) < 0)
        {
            Bearing = 360 - Bearing; //because of how the angle is measured 
        }

        if (Bearing >= 359.5)
            Bearing = 0;
    }

    public void DrawScreen(IMyTextSurface surf, bool refreshSpriteCache, bool drawRadialCompass)
    {
        surf.ContentType = ContentType.SCRIPT;
        surf.Script = "";
        surf.ScriptBackgroundColor = _backgroundColor;

        Vector2 textureSize = surf.TextureSize;
        Vector2 screenCenter = textureSize * 0.5f;
        Vector2 viewportSize = surf.SurfaceSize;
        Vector2 scaleVec = viewportSize / 512f;
        float compassHeight = FONT_SIZE * BASE_TEXT_HEIGHT_PX + MAJOR_TICK_HEIGHT + PIP_SIZE.Y + 4f;
        float referenceHeight = drawRadialCompass ? 512f : compassHeight;
        float scale = Math.Min(1, viewportSize.Y / referenceHeight);
        scale = Math.Min(scale, viewportSize.X / 512f);

        using (var frame = surf.DrawFrame())
        {
            if (refreshSpriteCache)
            {
                frame.Add(new MySprite());
            }

            if (drawRadialCompass)
                DrawRadialCompass(frame, ref screenCenter, ref viewportSize, scale);
            else
                DrawHorizontalCompass(frame, ref screenCenter, ref viewportSize, scale);
        }
    }

    void DrawHorizontalCompass(MySpriteDrawFrame frame, ref Vector2 screenCenter, ref Vector2 viewport, float scale)
    {
        double pxPerDeg = viewport.X / FOV; // NOTE: Not affected by scale because I want to fill the entire width

        double lowerAngle = Bearing - HALF_FOV;
        int lowerAngleMinor = (int)(lowerAngle - (lowerAngle % MINOR_TICK_INTERVAL)); // Round up to the nearest minor tick

        double upperAngle = Bearing + HALF_FOV;
        int upperAngleMinor = (int)(upperAngle - (upperAngle % MINOR_TICK_INTERVAL)); // Round down to the nearest minor tick

        int numMinorTicks = (upperAngleMinor - lowerAngleMinor) / MINOR_TICK_INTERVAL;

        Vector2 offsetCenterPos = screenCenter + new Vector2(0, 8f * scale);
        Vector2 majorTickSize = new Vector2(TICK_WIDTH, scale * MAJOR_TICK_HEIGHT);
        Vector2 minorTickSize = new Vector2(TICK_WIDTH, scale * MINOR_TICK_HEIGHT);
        Vector2 minorTickPosOffset = new Vector2(0, scale * (MAJOR_TICK_HEIGHT - MINOR_TICK_HEIGHT) * 0.5f);
        Vector2 majorTextPosOffset = new Vector2(0, -scale * (4f + FONT_SIZE * BASE_TEXT_HEIGHT_PX + 0.5f * MAJOR_TICK_HEIGHT));
        Vector2 pipPosOffset = new Vector2(0, scale * 0.5f * (PIP_SIZE.Y + MAJOR_TICK_HEIGHT));
        Vector2 textBoxPosOffset = majorTextPosOffset + new Vector2(0, 0.5f * scale * FONT_SIZE * BASE_TEXT_HEIGHT_PX);
        Vector2 textBoxTextPos = offsetCenterPos + majorTextPosOffset;

        MySprite pipSprite = MySprite.CreateSprite("Triangle", offsetCenterPos + pipPosOffset, scale * PIP_SIZE);
        pipSprite.Color = _pipColor;
        frame.Add(pipSprite);

        MySprite tickSprite = MySprite.CreateSprite("SquareSimple", offsetCenterPos, Vector2.Zero);
        tickSprite.Color = _tickColor;

        int angle = lowerAngleMinor;
        for (int i = 0; i <= numMinorTicks; ++i)
        {
            double diff = angle - lowerAngle;

            tickSprite.Position = new Vector2((float)(diff * pxPerDeg), offsetCenterPos.Y);
            if (angle % MAJOR_TICK_INTERVAL == 0)
            { // Draw major tick
                tickSprite.Size = majorTickSize;
                int bearingAngle = GetBearingAngle(angle);
                string label = "";
                _cardinalDirectionDict.TryGetValue(bearingAngle, out label);
                MySprite text = MySprite.CreateText(label, FONT, _textColor, scale * FONT_SIZE);
                text.Position = tickSprite.Position + majorTextPosOffset;
                frame.Add(text);
            }
            else
            { // Draw minor tick
                tickSprite.Size = minorTickSize;
                tickSprite.Position += minorTickPosOffset;
            }
            frame.Add(tickSprite);
            angle += MINOR_TICK_INTERVAL;
        }

        if (_drawBearing)
        {
            Vector2 textBoxSize = scale * TEXT_BOX_SIZE;
            Vector2 textHorizOffset = TEXT_BOX_HORIZ_SPACING * scale;
            Vector2 textBoxCenter = offsetCenterPos + textBoxPosOffset;
            MySprite textBox = MySprite.CreateSprite("SquareSimple", textBoxCenter, textBoxSize);
            textBox.Color = _backgroundColor;
            frame.Add(textBox);
            textBox.Data = "AH_TextBox";
            textBox.Color = _textBoxColor;
            frame.Add(textBox);

            string bearingStr = $"{Bearing:000}";
            MySprite digit = MySprite.CreateText(bearingStr.Substring(0, 1), FONT, _textColor, FONT_SIZE * scale);
            digit.Position = textBoxTextPos - textHorizOffset;
            frame.Add(digit);
            digit.Data = bearingStr.Substring(1, 1);
            digit.Position = textBoxTextPos;
            frame.Add(digit);
            digit.Data = bearingStr.Substring(2, 1);
            digit.Position = textBoxTextPos + textHorizOffset;
            frame.Add(digit);
        }
    }

    const float RADIAL_COMPASS_LABEL_RADIUS = 150f;
    readonly Vector2 RADIAL_COMPASS_SIZE = new Vector2(500f, 500f);
    readonly Vector2 RADIAL_COMPASS_MAJOR_CLIP_SIZE = new Vector2(400f, 400f);
    readonly Vector2 RADIAL_COMPASS_MINOR_CLIP_SIZE = new Vector2(450f, 450f);
    readonly Vector2 RADIAL_COMPASS_LINE_SIZE = new Vector2(6f, 500f);
    readonly Vector2 RADIAL_COMPASS_PIP_LOCATION = new Vector2(0f, -190);
    void DrawRadialCompass(MySpriteDrawFrame frame, ref Vector2 screenCenter, ref Vector2 viewport, float scale)
    {
        double angleOffset = -Bearing;

        MySprite line = MySprite.CreateSprite("SquareSimple", screenCenter, scale * RADIAL_COMPASS_LINE_SIZE);
        line.Color = _tickColor;
        for (int angle = 0; angle < 180;  angle += MINOR_TICK_INTERVAL)
        {
            if (angle % MAJOR_TICK_INTERVAL == 0)
            {
                continue;
            }
            float rotation = (float)MathHelper.ToRadians(angle + angleOffset);
            line.RotationOrScale = rotation;
            frame.Add(line);
        }

        MySprite circleClip = MySprite.CreateSprite("Circle", screenCenter, scale * RADIAL_COMPASS_MINOR_CLIP_SIZE);
        circleClip.Color = _backgroundColor;
        frame.Add(circleClip);

        float scaledFontSize = FONT_SIZE * scale;
        Vector2 fontVertOffset = new Vector2(0f, -scaledFontSize * BASE_TEXT_HEIGHT_PX * 0.5f);
        MySprite labelSprite = MySprite.CreateText("", FONT, _textColor, scaledFontSize);

        for (int angle = 0; angle < 180; angle += MAJOR_TICK_INTERVAL)
        {
            float rotation = (float)MathHelper.ToRadians(angle + angleOffset);
            if (angle < 180)
            {
                line.RotationOrScale = rotation;
                frame.Add(line);
            }
        }

        circleClip.Size = scale * RADIAL_COMPASS_MAJOR_CLIP_SIZE;
        frame.Add(circleClip);

        for (int angle = 0; angle < 360; angle += MAJOR_TICK_INTERVAL)
        {
            if (angle % 90 != 0)
                continue;

            float rotation = (float)MathHelper.ToRadians(angle + angleOffset);
            string label = "";
            _cardinalDirectionDict.TryGetValue(angle, out label);
            Vector2 labelOffset = new Vector2(scale * RADIAL_COMPASS_LABEL_RADIUS * MyMath.FastSin(rotation), -scale * RADIAL_COMPASS_LABEL_RADIUS * MyMath.FastCos(rotation));
            labelSprite.Data = label;
            labelSprite.Position = screenCenter + labelOffset + fontVertOffset;
            frame.Add(labelSprite);
        }

        MySprite pipSprite = MySprite.CreateSprite("Triangle", screenCenter + scale * RADIAL_COMPASS_PIP_LOCATION, scale * PIP_SIZE);
        pipSprite.Color = _pipColor;
        frame.Add(pipSprite);

        if (_drawBearing)
        {
            Vector2 textBoxPos = screenCenter + fontVertOffset;
            Vector2 textBoxSize = scale * TEXT_BOX_SIZE;
            Vector2 textHorizOffset = TEXT_BOX_HORIZ_SPACING * scale;
            Vector2 textBoxCenter = screenCenter;
            MySprite textBox = MySprite.CreateSprite("SquareSimple", textBoxCenter, textBoxSize);
            textBox.Color = _backgroundColor;
            frame.Add(textBox);
            textBox.Data = "AH_TextBox";
            textBox.Color = _textBoxColor;
            frame.Add(textBox);

            string bearingStr = $"{Bearing:000}";
            MySprite digit = MySprite.CreateText(bearingStr.Substring(0, 1), FONT, _textColor, FONT_SIZE * scale);
            digit.Position = textBoxPos - textHorizOffset;
            frame.Add(digit);
            digit.Data = bearingStr.Substring(1, 1);
            digit.Position = textBoxPos;
            frame.Add(digit);
            digit.Data = bearingStr.Substring(2, 1);
            digit.Position = textBoxPos + textHorizOffset;
            frame.Add(digit);
        }
    }

    int GetBearingAngle(int angle)
    {
        if (angle < 0)
        {
            return angle + 360;
        }

        if (angle > 360)
        {
            return angle - 360;
        }
        return angle;
    }
}
#endregion

#region Setup
void Setup()
{
    _textSurfaces.Clear();
    _taggedControllers.Clear();
    _allControllers.Clear();
    _clearSpriteCache = !_clearSpriteCache;

    ParseGeneralIni();
    _compass.UpdateConfigValues(ref _compassConfig);

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, CollectFunction);
}

bool CollectFunction(IMyTerminalBlock b)
{
    if (!Me.IsSameConstructAs(b))
    {
        return false;
    }
    
    if (b is IMyTextPanel && (StringExtensions.Contains(b.CustomName, _screenNameTag) || StringExtensions.Contains(b.CustomName, SCREEN_NAME_TAG_COMPAT)))
    {
        AddTextSurfaces(b, _textSurfaces);
        return false;
    }

    if (b is IMyTextSurfaceProvider && StringExtensions.Contains(b.CustomName, _screenNameTag))
    {
        AddTextSurfaces(b, _textSurfaces);
    }

    if (b is IMyShipController)
    {
        var sc = (IMyShipController)b;
        _allControllers.Add(sc);
        if (StringExtensions.Contains(b.CustomName, _referenceNameTag))
        {
            _taggedControllers.Add(sc);
        }
        return false;
    }

    return false;
}

void AddTextSurfaces(IMyTerminalBlock block, List<TextSurfaceConfig> textSurfaces)
{
    var textSurface = block as IMyTextSurface;
    var surfaceProvider = block as IMyTextSurfaceProvider;

    if (textSurface == null && surfaceProvider == null)
        return;

    _textSurfaceIni.Clear();
    bool parsed = _textSurfaceIni.TryParse(block.CustomData);
    if (!parsed && !string.IsNullOrWhiteSpace(block.CustomData))
    {
        _textSurfaceIni.Clear();
        _textSurfaceIni.EndContent = block.CustomData;
    }

    if (textSurface != null)
    {
        var surfConfig = new TextSurfaceConfig(textSurface);

        surfConfig.DrawRadialCompass = _textSurfaceIni.Get(INI_SECTION_APPEARANCE, INI_APPEARANCE_RADIAL).ToBoolean(surfConfig.DrawRadialCompass);
        _textSurfaceIni.Set(INI_SECTION_APPEARANCE, INI_APPEARANCE_RADIAL, surfConfig.DrawRadialCompass);
        
        string iniStr = _textSurfaceIni.ToString();
        if (iniStr != block.CustomData)
            block.CustomData = iniStr;
        
        textSurfaces.Add(surfConfig);
        return;
    }

    int surfaceCount = surfaceProvider.SurfaceCount;
    for (int i = 0; i < surfaceCount; ++i)
    {
        IMyTextSurface surf = surfaceProvider.GetSurface(i);
        
        string appearanceName = string.Format(INI_APPEARANCE_TEMPLATE_RADIAL, i);
        string iniKey = string.Format(INI_TEXT_SURF_TEMPLATE, i);
        bool display = _textSurfaceIni.Get(INI_SECTION_TEXT_SURF, iniKey).ToBoolean(i == 0);
        
        float ratio = surf.SurfaceSize.X / surf.SurfaceSize.Y;
        bool radialDefault = ratio > 1 ? ratio <= 2f : ratio >= 0.5f;
        bool radial = _textSurfaceIni.Get(INI_SECTION_APPEARANCE, appearanceName).ToBoolean(radialDefault);
        if (display)
        {
            var surfConfig = new TextSurfaceConfig(surf);
            textSurfaces.Add(surfConfig);
        }

        _textSurfaceIni.Set(INI_SECTION_TEXT_SURF, iniKey, display);
        _textSurfaceIni.Set(INI_SECTION_APPEARANCE, appearanceName, radial);
    }

    string output = _textSurfaceIni.ToString();
    if (!string.Equals(output, block.CustomData))
        block.CustomData = output;
}
#endregion

#region Ini config
void ParseGeneralIni()
{
    _ini.Clear();
    if (_ini.TryParse(Me.CustomData))
    {
        _compassConfig.DrawBearing = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_DRAW_BEARING).ToBoolean(_compassConfig.DrawBearing);
        _screenNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_SCREEN_NAME).ToString(_screenNameTag);
        _referenceNameTag = _ini.Get(INI_SECTION_GENERAL, INI_GENERAL_REFERENCE_NAME).ToString(_referenceNameTag);
        _compassConfig.AbsNorthVec = MyIniHelper.GetVector3D(INI_SECTION_GENERAL, INI_GENERAL_NORTH_VEC, _ini, _compassConfig.AbsNorthVec);
        _compassConfig.BackgroundColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_BACKGROUND, _ini, _compassConfig.BackgroundColor);
        _compassConfig.LineColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_LINE, _ini, _compassConfig.LineColor);
        _compassConfig.TextColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TEXT, _ini, _compassConfig.TextColor);
        _compassConfig.PipColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_PIP, _ini, _compassConfig.PipColor);
        _compassConfig.TextBoxColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_COLOR_TEXT_BOX, _ini, _compassConfig.TextBoxColor);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        _ini.Clear();
        _ini.EndContent = Me.CustomData;
    }

    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_SCREEN_NAME, _screenNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_REFERENCE_NAME, _referenceNameTag);
    _ini.Set(INI_SECTION_GENERAL, INI_GENERAL_DRAW_BEARING, _compassConfig.DrawBearing);
    MyIniHelper.SetVector3D(INI_SECTION_GENERAL, INI_GENERAL_NORTH_VEC, ref _compassConfig.AbsNorthVec, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_BACKGROUND, _compassConfig.BackgroundColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_LINE, _compassConfig.LineColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TEXT, _compassConfig.TextColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_PIP,_compassConfig.PipColor, _ini);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_COLOR_TEXT_BOX, _compassConfig.TextBoxColor, _ini);

    _ini.SetSectionComment(INI_SECTION_COLORS, INI_COMMENT_COLORS);
    _ini.SetComment(INI_SECTION_GENERAL,INI_GENERAL_NORTH_VEC, INI_COMMENT_NORTH_VEC);

    string output = _ini.ToString();
    if (output != Me.CustomData)
        Me.CustomData = output;
}
#endregion

#region Helper Classes
public static class VectorMathRef
{
    public static void SafeNormalize(ref Vector3D a, out Vector3D aNorm)
    {
        aNorm = Vector3D.Zero;
        if (IsZero(ref a))
            return;
        if (Vector3D.IsUnit(ref a))
            return;
        Vector3D.Normalize(ref a, out aNorm);
    }

    public static bool IsZero(ref Vector3D v, double epsilon = 1e-4)
    {
        if (Math.Abs(v.X) > epsilon) return false;
        if (Math.Abs(v.Y) > epsilon) return false;
        if (Math.Abs(v.Z) > epsilon) return false;
        return true;
    }

    public static void Reflection(ref Vector3D a, ref Vector3D b, out Vector3D result, double rejectionFactor = 1) //reflect a over b
    {
        Vector3D proj, rej;
        Projection(ref a, ref b, out proj);
        Vector3D.Subtract(ref a, ref proj, out rej);
        Vector3D.Multiply(ref rej, rejectionFactor, out rej);
        Vector3D.Subtract(ref proj, ref rej, out result);
    }

    public static void Rejection(ref Vector3D a, ref Vector3D b, out Vector3D result) //reject a on b
    {
        if (IsZero(ref a) || IsZero(ref b))
        {
            result = Vector3D.Zero;
            return;
        }

        Vector3D proj;
        Projection(ref a, ref b, out proj);
        Vector3D.Subtract(ref a, ref proj, out result);
    }

    public static void Projection(ref Vector3D a, ref Vector3D b, out Vector3D result)
    {
        if (IsZero(ref a) || IsZero(ref b))
        {
            result = Vector3D.Zero;
            return;
        }

        double dot;
        if (Vector3D.IsUnit(ref b))
        {
            Vector3D.Dot(ref a, ref b, out dot);
            Vector3D.Multiply(ref b, dot, out result);
            return;
        }

        double lenSq;
        Vector3D.Dot(ref a, ref b, out dot);
        lenSq = b.LengthSquared();
        Vector3D.Multiply(ref b, dot / lenSq, out result);
    }

    public static double ScalarProjection(ref Vector3D a, ref Vector3D b)
    {
        if (IsZero(ref a) || IsZero(ref b))
        {
            return 0;
        }

        double result;
        Vector3D.Dot(ref a, ref b, out result); // Dot prod
        if (Vector3D.IsUnit(ref b))
        {
            return result;
        }

        return result / b.Length(); // Divide by length to normalize b
    }

    public static double AngleBetween(ref Vector3D a, ref Vector3D b)
    {
        if (IsZero(ref a) || IsZero(ref b))
        {
            return 0;
        }

        double cosBtwn = CosBetween(ref a, ref b);
        return Math.Acos(cosBtwn);
    }

    public static double CosBetween(ref Vector3D a, ref Vector3D b)
    {
        if (IsZero(ref a) || IsZero(ref b))
        {
            return 0;
        }
        double dot;
        Vector3D.Dot(ref a, ref b, out dot);
        return MathHelper.Clamp(dot / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);
    }

    public static bool IsDotProductWithinTolerance(ref Vector3D a, ref Vector3D b, double toleranceCos)
    {
        double dot;
        Vector3D.Dot(ref a, ref b, out dot);
        double num = a.LengthSquared() * b.LengthSquared() * toleranceCos * Math.Abs(toleranceCos);
        return Math.Abs(dot) * dot > num;
    }
}

public static class StringExtensions
{
    public static bool Contains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
    {
        return source?.IndexOf(toCheck, comp) >= 0;
    }
}

public class Scheduler
{
    public double CurrentTimeSinceLastRun = 0;

    ScheduledAction _currentlyQueuedAction = null;
    bool _firstRun = true;
    bool _inUpdate = false;

    readonly bool _ignoreFirstRun;
    readonly List<ScheduledAction> _actionsToAdd = new List<ScheduledAction>();
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    readonly Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    readonly Program _program;

    const double RUNTIME_TO_REALTIME = (1.0 / 60.0) / 0.0166666;

    public Scheduler(Program program, bool ignoreFirstRun = false)
    {
        _program = program;
        _ignoreFirstRun = ignoreFirstRun;
    }

    public void Update()
    {
        _inUpdate = true;
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * RUNTIME_TO_REALTIME);

        if (_ignoreFirstRun && _firstRun)
            deltaTime = 0;

        _firstRun = false;
        _actionsToDispose.Clear();
        foreach (ScheduledAction action in _scheduledActions)
        {
            CurrentTimeSinceLastRun = action.TimeSinceLastRun + deltaTime;
            action.Update(deltaTime);
            if (action.JustRan && action.DisposeAfterRun)
            {
                _actionsToDispose.Add(action);
            }
        }

        _scheduledActions.RemoveAll((x) => _actionsToDispose.Contains(x));

        if (_currentlyQueuedAction == null)
        {
            if (_queuedActions.Count != 0)
                _currentlyQueuedAction = _queuedActions.Dequeue();
        }

        if (_currentlyQueuedAction != null)
        {
            _currentlyQueuedAction.Update(deltaTime);
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
            updateInterval = 0.001; // avoids divide by zero
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, true);
        _queuedActions.Enqueue(scheduledAction);
    }

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
    public double RunInterval
    {
        get
        {
            return _runInterval;
        }
        set
        {
            if (value == _runInterval)
                return;

            _runInterval = value < Epsilon ? 0 : value;
            _runFrequency = value == 0 ? double.MaxValue : 1.0 / _runInterval;
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
                RunInterval = double.MaxValue;
            else
                RunInterval = 1.0 / value;
        }
    }

    double _runInterval = -1e9;
    double _runFrequency = -1e9;
    readonly Action _action;

    const double Epsilon = 1e-12;

    public ScheduledAction(Action action, double runFrequency = 0, bool removeAfterRun = false, double timeOffset = 0)
    {
        _action = action;
        RunFrequency = runFrequency; // Implicitly sets RunInterval
        DisposeAfterRun = removeAfterRun;
        TimeSinceLastRun = timeOffset;
    }

    public void Update(double deltaTime)
    {
        TimeSinceLastRun += deltaTime;

        if (TimeSinceLastRun + Epsilon >= RunInterval)
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

public static class MyIniHelper
{
    public static void SetVector3D(string sectionName, string vectorName, ref Vector3D vector, MyIni ini)
    {
        ini.Set(sectionName, vectorName, vector.ToString());
    }

    public static Vector3D GetVector3D(string sectionName, string vectorName, MyIni ini, Vector3D? defaultVector = null)
    {
        var vector = Vector3D.Zero;
        if (Vector3D.TryParse(ini.Get(sectionName, vectorName).ToString(), out vector))
            return vector;
        else if (defaultVector.HasValue)
            return defaultVector.Value;
        return default(Vector3D);
    }

    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini)
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

public class RuntimeTracker
{
    public int Capacity { get; set; }
    public double Sensitivity { get; set; }
    public double MaxRuntime {get; private set;}
    public double MaxInstructions {get; private set;}
    public double AverageRuntime {get; private set;}
    public double AverageInstructions {get; private set;}
    
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
