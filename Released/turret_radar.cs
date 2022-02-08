
#region In-game Script
/*
/ //// / Whip's Turret Based Radar Systems / //// /

HOW DO I USE THIS?

1. Place this script in a programmable block.
2. Place some turrets on your ship.
3. Place a seat on your ship.
4. Place some text panels with "Radar" in their name somewhere.
5. Enjoy!




=================================================
    DO NOT MODIFY VARIABLES IN THE SCRIPT!

 USE THE CUSTOM DATA OF THIS PROGRAMMABLE BLOCK!
=================================================


























HEY! DONT EVEN THINK ABOUT TOUCHING BELOW THIS LINE!

*/

#region Fields
const string VERSION = "34.0.1";
const string DATE = "2022/02/03";

enum TargetRelation : byte { Neutral = 0, Other = 0, Enemy = 1, Friendly = 2, Locked = 4, LargeGrid = 8, SmallGrid = 16, RelationMask = Neutral | Enemy | Friendly, TypeMask = LargeGrid | SmallGrid | Other }

const string IGC_TAG = "IGC_IFF_MSG";

const string INI_SECTION_GENERAL = "Radar - General";
const string INI_RADAR_NAME = "Text surface name tag";
const string INI_REF_NAME = "Optional reference block name";
const string INI_BCAST = "Share own position";
const string INI_NETWORK = "Share targets";
const string INI_USE_RANGE_OVERRIDE = "Use radar range override";
const string INI_RANGE_OVERRIDE = "Radar range override (m)";
const string INI_PROJ_ANGLE = "Radar projection angle in degrees (0 is flat)";
const string INI_DRAW_QUADRANTS = "Draw quadrants";
const string INI_DRAW_RUNNING_SCREEN = "Draw animated title screen";

const string INI_SECTION_COLORS = "Radar - Colors";
const string INI_TITLE_BAR = "Title bar";
const string INI_TEXT = "Text";
const string INI_BACKGROUND = "Background";
const string INI_MSL_LOCK = "Missile lock";
const string INI_RADAR_LINES = "Radar lines";
const string INI_PLANE = "Radar plane";
const string INI_ENEMY = "Enemy icon";
const string INI_ENEMY_ELEVATION = "Enemy elevation";
const string INI_NEUTRAL = "Neutral icon";
const string INI_NEUTRAL_ELEVATION = "Neutral elevation";
const string INI_FRIENDLY = "Friendly icon";
const string INI_FRIENDLY_ELEVATION = "Friendly elevation";

const string INI_SECTION_TEXT_SURF_PROVIDER = "Radar - Text Surface Config";
const string INI_TEXT_SURFACE_TEMPLATE = "Show on screen {0}";

IMyBroadcastListener broadcastListener;

string referenceName = "Reference";
float rangeOverride = 1000;
bool useRangeOverride = false;
bool networkTargets = true;
bool broadcastIFF = true;
bool drawQuadrants = true;
bool drawRunningScreen = true;

Color titleBarColor = new Color(100, 30, 0, 5);
Color backColor = new Color(0, 0, 0, 255);
Color lineColor = new Color(255, 100, 0, 50);
Color planeColor = new Color(100, 30, 0, 5);
Color enemyIconColor = new Color(150, 0, 0, 255);
Color enemyElevationColor = new Color(75, 0, 0, 255);
Color neutralIconColor = new Color(150, 150, 0, 255);
Color neutralElevationColor = new Color(75, 75, 0, 255);
Color allyIconColor = new Color(0, 50, 150, 255);
Color allyElevationColor = new Color(0, 25, 75, 255);
Color textColor = new Color(255, 100, 0, 100);
Color missileLockColor = new Color(255, 100, 0, 255);

float MaxRange
{
    get
    {
        return Math.Max(1, useRangeOverride ? rangeOverride : (turrets.Count == 0 ? rangeOverride : turretMaxRange));
    }
}

List<IMyShipController> Controllers
{
    get
    {
        return taggedControllers.Count > 0 ? taggedControllers : allControllers;
    }
}

string textPanelName = "Radar";
float projectionAngle = 55f;
float turretMaxRange = 800f;

Scheduler scheduler;
RuntimeTracker runtimeTracker;
ScheduledAction grabBlockAction;

Dictionary<long, TargetData> targetDataDict = new Dictionary<long, TargetData>();
Dictionary<long, TargetData> broadcastDict = new Dictionary<long, TargetData>();
List<TurretInterface> turrets = new List<TurretInterface>();
List<IMySensorBlock> sensors = new List<IMySensorBlock>();
List<IMyTextSurface> textSurfaces = new List<IMyTextSurface>();
List<IMyShipController> taggedControllers = new List<IMyShipController>();
List<IMyShipController> allControllers = new List<IMyShipController>();
HashSet<long> myGridIds = new HashSet<long>();
IMyTerminalBlock reference;
IMyShipController lastActiveShipController = null;

const double cycleTime = 1.0 / 60.0;
string lastSetupResult = "";
bool isSetup = false;
bool _clearSpriteCache = false;

readonly CompositeBoundingSphere _compositeBoundingSphere;
readonly RadarSurface radarSurface;
readonly MyIni generalIni = new MyIni();
readonly MyIni textSurfaceIni = new MyIni();
readonly MyCommandLine _commandLine = new MyCommandLine();
readonly RadarRunningScreenManager _runningScreenManager;
#endregion

#region Main Routine
Program()
{
    _runningScreenManager = new RadarRunningScreenManager(VERSION, this);
    _compositeBoundingSphere = new CompositeBoundingSphere(this);

    ParseCustomDataIni();
    GrabBlocks();

    radarSurface = new RadarSurface(titleBarColor, backColor, lineColor, planeColor, textColor, missileLockColor, projectionAngle, MaxRange, drawQuadrants);

    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    runtimeTracker = new RuntimeTracker(this);

    // Scheduler creation
    scheduler = new Scheduler(this);
    grabBlockAction = new ScheduledAction(GrabBlocks, 0.1);
    scheduler.AddScheduledAction(grabBlockAction);
    scheduler.AddScheduledAction(UpdateRadarRange, 1);
    scheduler.AddScheduledAction(PrintDetailedInfo, 1);
    scheduler.AddScheduledAction(DrawRunningScreen, 6);
    scheduler.AddScheduledAction(_runningScreenManager.RestartDraw, 1);

    scheduler.AddQueuedAction(GetTurretTargets, cycleTime);               // cycle 1
    scheduler.AddQueuedAction(radarSurface.SortContacts, cycleTime);      // cycle 2

    float step = 1f / 8f;
    scheduler.AddQueuedAction(() => Draw(0 * step, 1 * step), cycleTime); // cycle 3
    scheduler.AddQueuedAction(() => Draw(1 * step, 2 * step), cycleTime); // cycle 4
    scheduler.AddQueuedAction(() => Draw(2 * step, 3 * step), cycleTime); // cycle 5
    scheduler.AddQueuedAction(() => Draw(3 * step, 4 * step), cycleTime); // cycle 6
    scheduler.AddQueuedAction(() => Draw(4 * step, 5 * step), cycleTime); // cycle 7
    scheduler.AddQueuedAction(() => Draw(5 * step, 6 * step), cycleTime); // cycle 8
    scheduler.AddQueuedAction(() => Draw(6 * step, 7 * step), cycleTime); // cycle 9
    scheduler.AddQueuedAction(() => Draw(7 * step, 8 * step), cycleTime); // cycle 10

    // IGC Register
    broadcastListener = IGC.RegisterBroadcastListener(IGC_TAG);
    broadcastListener.SetMessageCallback(IGC_TAG);
}

void Main(string arg, UpdateType updateSource)
{
    runtimeTracker.AddRuntime();

    if (_commandLine.TryParse(arg))
        HandleArguments();

    scheduler.Update();

    if (arg.Equals(IGC_TAG))
    {
        ProcessNetworkMessage();
    }

    runtimeTracker.AddInstructions();
}

void DrawRunningScreen()
{
    if (drawRunningScreen)
    {
        _runningScreenManager.Draw();
    }
}

void HandleArguments()
{
    int argCount = _commandLine.ArgumentCount;

    if (argCount == 0)
        return;

    switch (_commandLine.Argument(0).ToLowerInvariant())
    {
        case "range":
            if (argCount != 2)
            {
                return;
            }

            float range = 0;
            if (float.TryParse(_commandLine.Argument(1), out range))
            {
                useRangeOverride = true;
                rangeOverride = range;

                UpdateRadarRange();

                generalIni.Clear();
                generalIni.TryParse(Me.CustomData);
                generalIni.Set(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE, rangeOverride);
                generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
                Me.CustomData = generalIni.ToString();
            }
            else if (string.Equals(_commandLine.Argument(1), "default"))
            {
                useRangeOverride = false;

                UpdateRadarRange();

                generalIni.Clear();
                generalIni.TryParse(Me.CustomData);
                generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
                Me.CustomData = generalIni.ToString();
            }
            return;

        default:
            return;
    }
}

void Draw(float startProportion, float endProportion)
{
    int start = (int)(startProportion * textSurfaces.Count);
    int end = (int)(endProportion * textSurfaces.Count);

    for (int i = start; i < end; ++i)
    {
        var textSurface = textSurfaces[i];
        radarSurface.DrawRadar(textSurface, _clearSpriteCache);
    }
}

void PrintDetailedInfo()
{
    Echo($"WMI Radar System Online{RunningSymbol()}\n(Version {VERSION} - {DATE})");
    Echo($"\nNext refresh in {Math.Max(grabBlockAction.RunInterval - grabBlockAction.TimeSinceLastRun, 0):N0} seconds\n");
    Echo($"Range: {MaxRange} m");
    Echo($"Turrets: {turrets.Count}");
    Echo($"Sensors: {sensors.Count}");
    Echo($"Text surfaces: {textSurfaces.Count}");
    Echo($"Ship radius: {_compositeBoundingSphere.Radius:n1} m");
    Echo($"Reference:\n    \"{(reference?.CustomName)}\"");
    Echo($"{lastSetupResult}");
    Echo(runtimeTracker.Write());
}

void UpdateRadarRange()
{
    turretMaxRange = GetMaxTurretRange(turrets);
    radarSurface.Range = MaxRange;
}
#endregion

#region IGC Comms
void ProcessNetworkMessage()
{
    while (broadcastListener.HasPendingMessage)
    {
        var message = broadcastListener.AcceptMessage();
        object messageData = message.Data;
        byte relationship = 0;
        byte type = 0;
        long entityId = 0;
        Vector3D position = default(Vector3D);
        bool targetLock = false;
        
        MyTuple<byte, long, Vector3D, double> myTuple;
        if (messageData is MyTuple<byte, long, Vector3D, byte>) // For backwards compat.
        {
            var payload = (MyTuple<byte, long, Vector3D, byte>)messageData;
            myTuple.Item1 = payload.Item1;
            myTuple.Item2 = payload.Item2;
            myTuple.Item3 = payload.Item3;
        }
        else if (messageData is MyTuple<byte, long, Vector3D, double>) // Item4 is ignored on ingest, it is grid radius
        {
            myTuple = (MyTuple<byte, long, Vector3D, double>)messageData;
        }
        else
        {
            continue;
        }
        
        relationship = (byte)(myTuple.Item1 & (byte)TargetRelation.RelationMask);
        type = (byte)(myTuple.Item1 & (byte)TargetRelation.TypeMask);
        targetLock = (myTuple.Item1 & (byte)TargetRelation.Locked) != 0;
        entityId = myTuple.Item2;
        position = myTuple.Item3;

        if (myGridIds.Contains(entityId))
        {
            if (targetLock)
            {
                radarSurface.RadarLockWarning = true;
            }
            continue;
        }
        
        bool myLock = false;
        if (targetLock && GridTerminalSystem.GetBlockWithId(message.Source) != null)
        {
            myLock = true;
        }

        TargetData targetData;
        if (targetDataDict.TryGetValue(entityId, out targetData))
        {
            targetData.TargetLock |= targetLock;
            targetData.MyLock |= myLock;
            targetData.Type |= (TargetRelation)type;
            if ((byte)targetData.Relation < relationship)
            {
                targetData.Relation = (TargetRelation)relationship;
            }
        }
        else
        {
            targetData.Position = position;
            targetData.TargetLock = targetLock;
            targetData.MyLock = myLock;
            targetData.Relation = (TargetRelation)relationship;
            targetData.Type = (TargetRelation)type;
        }

        targetDataDict[entityId] = targetData;
        
    }
}

void NetworkTargets()
{
    if (broadcastIFF)
    {
        _compositeBoundingSphere.Compute();
        TargetRelation type = _compositeBoundingSphere.LargestGrid.GridSizeEnum == MyCubeSize.Large ? TargetRelation.LargeGrid : TargetRelation.SmallGrid;
        var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)(type | TargetRelation.Friendly), _compositeBoundingSphere.LargestGrid.EntityId, _compositeBoundingSphere.Center, _compositeBoundingSphere.Radius * _compositeBoundingSphere.Radius);
        IGC.SendBroadcastMessage(IGC_TAG, myTuple);
    }

    if (networkTargets)
    {
        foreach (var kvp in broadcastDict)
        {
            var targetData = kvp.Value;
            var myTuple = new MyTuple<byte, long, Vector3D, double>((byte)(targetData.Relation | targetData.Type), kvp.Key, targetData.Position, 0);
            IGC.SendBroadcastMessage(IGC_TAG, myTuple);
        }
    }
}
#endregion

#region Sensor Detection
List<MyDetectedEntityInfo> sensorEntities = new List<MyDetectedEntityInfo>();
void GetSensorTargets()
{
    foreach (var sensor in sensors)
    {
        if (sensor.Closed)
            continue;

        sensorEntities.Clear();
        sensor.DetectedEntities(sensorEntities);
        foreach (var target in sensorEntities)
        {
            AddTargetData(target);
        }
    }
}
#endregion

#region Add Target Info
void AddTargetData(MyDetectedEntityInfo targetInfo)
{
    TargetData targetData;
    targetDataDict.TryGetValue(targetInfo.EntityId, out targetData);

    switch (targetInfo.Relationship)
    {
        case MyRelationsBetweenPlayerAndBlock.Owner:
        case MyRelationsBetweenPlayerAndBlock.Friends:
        case MyRelationsBetweenPlayerAndBlock.FactionShare:
            targetData.Relation |= TargetRelation.Friendly;
            break;
        case MyRelationsBetweenPlayerAndBlock.Enemies:
            targetData.Relation |= TargetRelation.Enemy;
            break;
        default:
            targetData.Relation |= TargetRelation.Neutral;
            break;
    }

    if (targetInfo.Type == MyDetectedEntityType.LargeGrid)
    {
        targetData.Type = TargetRelation.LargeGrid;
    }
    else if (targetInfo.Type == MyDetectedEntityType.SmallGrid)
    {
        targetData.Type = TargetRelation.SmallGrid;
    }
    else
    {
        targetData.Type = TargetRelation.Other;
    }
    targetData.Position = targetInfo.Position;

    targetDataDict[targetInfo.EntityId] = targetData;
    broadcastDict[targetInfo.EntityId] = targetData;
}
#endregion

#region Turret Detection
void GetTurretTargets()
{
    if (!isSetup) //setup error
        return;

    broadcastDict.Clear();
    radarSurface.ClearContacts();

    GetSensorTargets();

    foreach (var block in turrets)
    {
        if (block.Closed)
            continue;

        if (block.HasTarget)
        {
            var target = block.GetTargetedEntity();
            AddTargetData(target);
        }
    }

    // Define reference ship controller
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
            reference = Me;
        }
    }

    if (reference is IMyShipController)
        lastActiveShipController = (IMyShipController)reference;

    foreach (var kvp in targetDataDict)
    {
        if (kvp.Key == Me.CubeGrid.EntityId)
            continue;

        var targetData = kvp.Value;

        Color targetIconColor = enemyIconColor;
        Color targetElevationColor = enemyElevationColor;
        switch (targetData.Relation)
        {
            case TargetRelation.Friendly:
                targetIconColor = allyIconColor;
                targetElevationColor = allyElevationColor;
                break;
            
            case TargetRelation.Neutral:
                targetIconColor = neutralIconColor;
                targetElevationColor = neutralElevationColor;
                break;
        }

        radarSurface.AddContact(
            targetData.Position,
            reference.WorldMatrix,
            targetIconColor,
            targetElevationColor,
            targetData.Relation,
            targetData.Type, 
            targetData.TargetLock,
            targetData.MyLock);
    }
    NetworkTargets();

    targetDataDict.Clear();
    radarSurface.RadarLockWarning = false;
}
#endregion

#region Target Data Struct
struct TargetData
{
    public Vector3D Position;
    public TargetRelation Relation;
    public TargetRelation Type;
    public bool TargetLock;
    public bool MyLock;
}
#endregion

#region Radar Surface
class RadarSurface
{
    float _range = 0f;
    public float Range
    {
        get
        {
            return _range;
        }
        set
        {
            if (value == _range)
                return;
            _range = value;
            PrefixRangeWithMetricUnits(_range, "m", 1, out _outerRange, out _innerRange);
        }
    }
    public bool RadarLockWarning { get; set; }

    public readonly StringBuilder Debug = new StringBuilder();

    const string FONT = "Debug";
    const string RADAR_WARNING_TEXT = "MISSILE LOCK";
    const string ICON_OUT_OF_RANGE = "AH_BoreSight";
    const float TITLE_TEXT_SIZE = 1.5f;
    const float HUD_TEXT_SIZE = 1.3f;
    const float RANGE_TEXT_SIZE = 1.2f;
    const float LOCK_TEXT_SIZE = 1f;
    const float TGT_ELEVATION_LINE_WIDTH = 4f;
    const float RADAR_RANGE_LINE_WIDTH = 8f;
    const float QUADRANT_LINE_WIDTH = 4f;
    const float TITLE_BAR_HEIGHT = 64;
    const float RADAR_WARNING_TEXT_SIZE = 1.5f;
    const float SIZE_TO_PX = 28.8f;

    Color _titleBarColor;
    Color _backColor;
    Color _lineColor;
    Color _quadrantLineColor;
    Color _planeColor;
    Color _textColor;
    Color _targetLockColor;
    float _projectionAngleDeg;
    float _radarProjectionCos;
    float _radarProjectionSin;
    bool _drawQuadrants;
    bool _showRadarWarning = true;
    string _outerRange = "", _innerRange = "";
    Vector2 _quadrantLineDirection;

    Color _radarLockWarningColor = Color.Red;
    Color _textBoxBackgroundColor = new Color(0, 0, 0, 220);

    readonly StringBuilder _textMeasuringSB = new StringBuilder();
    readonly Vector2 DROP_SHADOW_OFFSET = new Vector2(2, 2);
    readonly Vector2 TGT_ICON_SIZE = new Vector2(20f, 20f);
    readonly Vector2 SHIP_ICON_SIZE = new Vector2(32, 16);
    readonly Vector2 TRIANGLE_OFFSET = new Vector2(0, (float)(0.5f - Math.Sqrt(3f)/6f));
    readonly Vector2 BORDER_PADDING = new Vector2(16f, 64f);
    readonly List<TargetInfo> _targetList = new List<TargetInfo>();
    readonly List<TargetInfo> _targetsBelowPlane = new List<TargetInfo>();
    readonly List<TargetInfo> _targetsAbovePlane = new List<TargetInfo>();

    struct TargetInfo
    {
        public Vector3 Position;
        public Vector2 Offset;
        public Color IconColor;
        public Color ElevationColor;
        public string Icon;
        public bool TargetLock;
        public bool MyTargetLock;
        public float Rotation;
        public float Scale;
    }

    public RadarSurface(Color titleBarColor, Color backColor, Color lineColor, Color planeColor, Color textColor, Color targetLockColor, float projectionAngleDeg, float range, bool drawQuadrants)
    {
        UpdateFields(titleBarColor, backColor, lineColor, planeColor, textColor, targetLockColor, projectionAngleDeg, range, drawQuadrants);
        _textMeasuringSB.Append(RADAR_WARNING_TEXT);
    }

    public void UpdateFields(Color titleBarColor, Color backColor, Color lineColor, Color planeColor, Color textColor, Color targetLockColor, float projectionAngleDeg, float range, bool drawQuadrants)
    {
        _titleBarColor = titleBarColor;
        _backColor = backColor;
        _lineColor = lineColor;
        _quadrantLineColor = new Color((byte)(lineColor.R / 2), (byte)(lineColor.G / 2), (byte)(lineColor.B / 2), (byte)(lineColor.A / 2));
        _planeColor = planeColor;
        _textColor = textColor;
        _projectionAngleDeg = projectionAngleDeg;
        _drawQuadrants = drawQuadrants;
        _targetLockColor = targetLockColor;
        Range = range;

        PrefixRangeWithMetricUnits(Range, "m", 2, out _outerRange, out _innerRange);

        var rads = MathHelper.ToRadians(_projectionAngleDeg);
        _radarProjectionCos = (float)Math.Cos(rads);
        _radarProjectionSin = (float)Math.Sin(rads);

        _quadrantLineDirection = new Vector2(0.25f * MathHelper.Sqrt2, 0.25f * MathHelper.Sqrt2 * _radarProjectionCos);
    }

    public void DrawRadarLockWarning(MySpriteDrawFrame frame, IMyTextSurface surface, Vector2 screenCenter, Vector2 screenSize, float scale)
    {
        if (!RadarLockWarning || !_showRadarWarning)
            return;

        float textSize = RADAR_WARNING_TEXT_SIZE * scale;
        Vector2 textBoxSize = surface.MeasureStringInPixels(_textMeasuringSB, "Debug", textSize);
        Vector2 padding = new Vector2(48f, 24f) * scale;
        Vector2 position = screenCenter + new Vector2(0, screenSize.Y * 0.2f);
        Vector2 textPos = position;
        textPos.Y -= textBoxSize.Y * 0.5f;

        // Draw text box bg
        MySprite textBoxBg = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: _textBoxBackgroundColor, size: textBoxSize + padding);
        textBoxBg.Position = position;
        frame.Add(textBoxBg);

        // Draw text box
        MySprite textBox = new MySprite(SpriteType.TEXTURE, "AH_TextBox", color: _radarLockWarningColor, size: textBoxSize + padding);
        textBox.Position = position;
        frame.Add(textBox);

        // Draw text
        MySprite text = MySprite.CreateText(RADAR_WARNING_TEXT, "Debug", _radarLockWarningColor, scale: textSize);
        text.Position = textPos;
        frame.Add(text);
    }
    
    void GetSpriteType(TargetRelation type, out string spriteName, out Vector2 offset, out float scale)
    {
        if ((type & TargetRelation.LargeGrid) != 0)
        {
            spriteName = "SquareSimple";
            offset = Vector2.Zero;
            scale = 1f;
        }
        else if ((type & TargetRelation.SmallGrid) != 0)
        {
            spriteName =  "Triangle";
            offset = TRIANGLE_OFFSET;
            scale = 1.25f;
        }
        else
        {
            spriteName = "Circle";
            offset = Vector2.Zero;
            scale = 1f;
        }
    }

    public void AddContact(Vector3D worldPosition, MatrixD worldMatrix, Color iconColor, Color elevationLineColor, TargetRelation relation, TargetRelation type, bool targetLock, bool myTargetLock)
    {
        Vector3D transformedDirection = Vector3D.TransformNormal(worldPosition - worldMatrix.Translation, Matrix.Transpose(worldMatrix));
        Vector3 position = new Vector3(transformedDirection.X, transformedDirection.Z, transformedDirection.Y);
        bool inRange = position.X * position.X + position.Y * position.Y < Range * Range;
        float angle = 0f;
        string spriteName = "";
        Vector2 offset;
        float scale;
        if (inRange)
        {
            GetSpriteType(type, out spriteName, out offset, out scale);
            position /= Range;
        }
        else
        {
            spriteName = ICON_OUT_OF_RANGE;
            offset = Vector2.Zero;
            scale = 4f;
            var directionFlat = position;
            directionFlat.Z = 0;
            float angleOffset = position.Z > 0 ? MathHelper.Pi : 0f;
            position = Vector3.Normalize(directionFlat);
            angle = angleOffset + MathHelper.PiOver2;
        }

        var targetInfo = new TargetInfo()
        {
            Position = position,
            Offset = offset,
            ElevationColor = elevationLineColor,
            IconColor = iconColor,
            Icon = spriteName,
            TargetLock = targetLock,
            MyTargetLock = myTargetLock,
            Rotation = angle,
            Scale = scale,
        };

        _targetList.Add(targetInfo);
    }

    public void SortContacts()
    {
        _targetsBelowPlane.Clear();
        _targetsAbovePlane.Clear();

        _targetList.Sort((a, b) => (a.Position.Y).CompareTo(b.Position.Y));

        foreach (var target in _targetList)
        {
            if (target.Position.Z >= 0)
                _targetsAbovePlane.Add(target);
            else
                _targetsBelowPlane.Add(target);
        }

        _showRadarWarning = !_showRadarWarning;
    }

    public void ClearContacts()
    {
        _targetList.Clear();
        _targetsAbovePlane.Clear();
        _targetsBelowPlane.Clear();
    }

    /*
    Draws a box that looks like this:
     __    __
    |        |

    |__    __|
    */
    static void DrawBoxCorners(MySpriteDrawFrame frame, Vector2 boxSize, Vector2 centerPos, float lineLength, float lineWidth, Color color)
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

        MySprite sprite;

        // Top left
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxTopLeft + horizontalOffset, rotation: 0, color: color);
        frame.Add(sprite);

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxTopLeft + verticalOffset, rotation: 0, color: color);
        frame.Add(sprite);

        // Top right
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxTopRight + new Vector2(-horizontalOffset.X, horizontalOffset.Y), rotation: 0, color: color);
        frame.Add(sprite);

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxTopRight + new Vector2(-verticalOffset.X, verticalOffset.Y), rotation: 0, color: color);
        frame.Add(sprite);

        // Bottom left
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxBottomLeft + new Vector2(horizontalOffset.X, -horizontalOffset.Y), rotation: 0, color: color);
        frame.Add(sprite);

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxBottomLeft + new Vector2(verticalOffset.X, -verticalOffset.Y), rotation: 0, color: color);
        frame.Add(sprite);

        // Bottom right
        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: horizontalSize, position: boxBottomRight - horizontalOffset, rotation: 0, color: color);
        frame.Add(sprite);

        sprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", size: verticalSize, position: boxBottomRight - verticalOffset, rotation: 0, color: color);
        frame.Add(sprite);
    }

    public void DrawRadar(IMyTextSurface surface, bool clearSpriteCache)
    {
        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";
        surface.ScriptBackgroundColor = _backColor;

        Vector2 surfaceSize = surface.TextureSize;
        Vector2 screenCenter = surfaceSize * 0.5f;
        Vector2 viewportSize = surface.SurfaceSize;
        Vector2 scale = viewportSize / 512f;
        float minScale = Math.Min(scale.X, scale.Y);
        Vector2 viewportCropped = viewportSize - (Vector2.UnitY * (TITLE_BAR_HEIGHT + RANGE_TEXT_SIZE * SIZE_TO_PX) + BORDER_PADDING) * minScale;
        float sideLength;
        if (viewportCropped.X * _radarProjectionCos < viewportCropped.Y)
        {
            sideLength = viewportCropped.X;
        }
        else
        {
            sideLength = viewportCropped.Y / _radarProjectionCos;
        }
        //float sideLength = Math.Min(viewportSize.X, viewportSize.Y - TITLE_BAR_HEIGHT * minScale);

        Vector2 radarCenterPos = screenCenter + Vector2.UnitY * ((TITLE_BAR_HEIGHT - RANGE_TEXT_SIZE * SIZE_TO_PX) * 0.5f * minScale);
        Vector2 radarPlaneSize = new Vector2(sideLength, sideLength * _radarProjectionCos);

        using (var frame = surface.DrawFrame())
        {
            if (clearSpriteCache)
            {
                frame.Add(new MySprite());
            }

            DrawRadarPlaneBackground(frame, radarCenterPos, radarPlaneSize, minScale);

            // Bottom Icons
            foreach (var targetInfo in _targetsBelowPlane)
            {
                DrawTargetIcon(frame, radarCenterPos, radarPlaneSize, targetInfo, minScale);
            }

            // Radar plane
            DrawRadarPlane(frame, viewportSize, screenCenter, radarCenterPos, radarPlaneSize, minScale);

            // Top Icons
            foreach (var targetInfo in _targetsAbovePlane)
            {
                DrawTargetIcon(frame, radarCenterPos, radarPlaneSize, targetInfo, minScale);
            }

            DrawRadarLockWarning(frame, surface, screenCenter, viewportSize, minScale);
        }
    }

    void DrawLineQuadrantSymmetry(MySpriteDrawFrame frame, Vector2 center, Vector2 point1, Vector2 point2, float width, Color color)
    {
        DrawLine(frame, center + point1, center + point2, width, color);
        DrawLine(frame, center - point1, center - point2, width, color);
        point1.X *= -1;
        point2.X *= -1;
        DrawLine(frame, center + point1, center + point2, width, color);
        DrawLine(frame, center - point1, center - point2, width, color);
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

    void DrawRadarPlaneBackground(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 radarPlaneSize, float scale)
    {
        float lineWidth = RADAR_RANGE_LINE_WIDTH * scale;
        
        MySprite sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize, color: _lineColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);
        
        sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize - lineWidth * Vector2.One, color: _backColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);
        
        sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize * 0.5f, color: _lineColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);
        
        sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize * 0.5f - lineWidth * Vector2.One, color: _backColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);

        // Transparent plane circle
        sprite = new MySprite(SpriteType.TEXTURE, "Circle", size: radarPlaneSize, color: _planeColor);
        sprite.Position = screenCenter;
        frame.Add(sprite);
    }

    void DrawRadarPlane(MySpriteDrawFrame frame, Vector2 viewportSize, Vector2 screenCenter, Vector2 radarScreenCenter, Vector2 radarPlaneSize, float scale)
    {
        MySprite sprite;
        Vector2 halfScreenSize = viewportSize * 0.5f;
        float titleBarHeight = TITLE_BAR_HEIGHT * scale;

        sprite = MySprite.CreateSprite("SquareSimple",
            screenCenter + new Vector2(0f, -halfScreenSize.Y + titleBarHeight * 0.5f),
            new Vector2(viewportSize.X, titleBarHeight));
        sprite.Color = _titleBarColor;
        frame.Add(sprite);

        sprite = MySprite.CreateText($"WMI Radar System", FONT, _textColor, scale * TITLE_TEXT_SIZE, TextAlignment.CENTER);
        sprite.Position = screenCenter + new Vector2(0, -halfScreenSize.Y + 4.25f * scale);
        frame.Add(sprite);

        // Ship location
        var iconSize = SHIP_ICON_SIZE * scale;
        sprite = new MySprite(SpriteType.TEXTURE, "Triangle", size: iconSize, color: _lineColor);
        sprite.Position = radarScreenCenter + new Vector2(0f, -0.2f * iconSize.Y);
        frame.Add(sprite);

        Vector2 quadrantLine = radarPlaneSize.X * _quadrantLineDirection;
        // Quadrant lines
        if (_drawQuadrants)
        {
            float lineWidth = QUADRANT_LINE_WIDTH * scale;
            DrawLineQuadrantSymmetry(frame, radarScreenCenter, 0.2f * quadrantLine, 1.0f * quadrantLine, lineWidth, _quadrantLineColor);
        }

        // Draw range text
        float textSize = RANGE_TEXT_SIZE * scale;
        Color rangeColors = new Color(_textColor.R, _textColor.G, _textColor.B, _textColor.A / 2);
        /*
        sprite = MySprite.CreateText($"{_innerRange}", "Debug", rangeColors, textSize, TextAlignment.CENTER);
        sprite.Position = radarScreenCenter + new Vector2(0, radarPlaneSize.Y * -0.25f - textSize * 30f);
        frame.Add(sprite);
        */

        sprite = MySprite.CreateText($"Range: {_outerRange}", "Debug", rangeColors, textSize, TextAlignment.CENTER);
        sprite.Position = radarScreenCenter + new Vector2(0, radarPlaneSize.Y * 0.5f + scale * 4f /*+ textSize * 37f*/ );
        frame.Add(sprite);
    }

    void DrawTargetIcon(MySpriteDrawFrame frame, Vector2 screenCenter, Vector2 radarPlaneSize, TargetInfo targetInfo, float scale)
    {
        Vector3 targetPosPixels = targetInfo.Position * new Vector3(1, _radarProjectionCos, _radarProjectionSin) * radarPlaneSize.X * 0.5f;

        Vector2 targetPosPlane = new Vector2(targetPosPixels.X, targetPosPixels.Y);
        Vector2 iconPos = targetPosPlane - targetPosPixels.Z * Vector2.UnitY;

        RoundVector2(ref iconPos);
        RoundVector2(ref targetPosPlane);

        float elevationLineWidth = Math.Max(1f, TGT_ELEVATION_LINE_WIDTH * scale);
        MySprite elevationSprite = new MySprite(SpriteType.TEXTURE, "SquareSimple", color: targetInfo.ElevationColor, size: new Vector2(elevationLineWidth, targetPosPixels.Z));
        elevationSprite.Position = screenCenter + (iconPos + targetPosPlane) * 0.5f;
        RoundVector2(ref elevationSprite.Position);
        RoundVector2(ref elevationSprite.Size);

        Vector2 iconSize = TGT_ICON_SIZE * scale * targetInfo.Scale;
        MySprite iconSprite = new MySprite(SpriteType.TEXTURE, targetInfo.Icon, color: targetInfo.IconColor, size: iconSize, rotation: targetInfo.Rotation);
        iconSprite.Position = screenCenter + iconPos;
        RoundVector2(ref iconSprite.Position);
        RoundVector2(ref iconSprite.Size);

        MySprite iconShadow = iconSprite;
        iconShadow.Color = Color.Black;
        iconShadow.Size += Vector2.One * 2f * (float)Math.Max(1f, Math.Round(scale * 4f));

        iconSize.Y *= _radarProjectionCos;
        MySprite projectedIconSprite = new MySprite(SpriteType.TEXTURE, "Circle", color: targetInfo.ElevationColor, size: iconSize);
        projectedIconSprite.Position = screenCenter + targetPosPlane;
        RoundVector2(ref projectedIconSprite.Position);
        RoundVector2(ref projectedIconSprite.Size);

        bool showProjectedElevation = Math.Abs(iconPos.Y - targetPosPlane.Y) > iconSize.Y;
        
        Vector2 iconSpriteOffset = targetInfo.Offset * scale * targetInfo.Scale;

        // Changing the order of drawing based on if above or below radar plane
        if (targetPosPixels.Z >= 0)
        {
            iconSprite.Position -= iconSpriteOffset * iconSprite.Size.Value.X;
            iconShadow.Position -= iconSpriteOffset * iconShadow.Size.Value.X;
            
            if (showProjectedElevation)
            {
                frame.Add(projectedIconSprite);
                frame.Add(elevationSprite);
            }
            frame.Add(iconShadow);
            frame.Add(iconSprite);
        }
        else
        {
            iconSprite.RotationOrScale = MathHelper.Pi;
            iconShadow.RotationOrScale = MathHelper.Pi;
            
            iconSprite.Position += iconSpriteOffset * iconSprite.Size.Value.X;
            iconShadow.Position += iconSpriteOffset * iconShadow.Size.Value.X;

            if (showProjectedElevation)
                frame.Add(elevationSprite);
            frame.Add(iconShadow);
            frame.Add(iconSprite);
            if (showProjectedElevation)
                frame.Add(projectedIconSprite);
        }

        if (targetInfo.TargetLock)
        {
            Vector2 targetBoxSize = (TGT_ICON_SIZE + 20) * scale;
            DrawBoxCorners(frame, targetBoxSize, screenCenter + iconPos, 12 * scale, 4 * scale, targetInfo.IconColor);
            
            if (targetInfo.MyTargetLock)
            {
                float lockTextSizeScaled = LOCK_TEXT_SIZE * scale;
                MySprite lockText = new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Alignment = TextAlignment.CENTER,
                    Color = _textColor,
                    Data = "LOCK",
                    FontId = "Debug",
                    Position = screenCenter + iconPos - new Vector2(0, targetBoxSize.X * 0.5f + lockTextSizeScaled * SIZE_TO_PX),
                    RotationOrScale = lockTextSizeScaled,
                    Size = null,
                };
                
                MySprite lockTextShadow = lockText;
                lockTextShadow.Color = _backColor;
                lockTextShadow.Position += DROP_SHADOW_OFFSET;
                
                frame.Add(lockTextShadow);
                frame.Add(lockText);
            }
        }
    }

    void RoundVector2(ref Vector2? vec)
    {
        if (vec.HasValue)
            vec = new Vector2((float)Math.Round(vec.Value.X), (float)Math.Round(vec.Value.Y));
    }

    void RoundVector2(ref Vector2 vec)
    {
        vec.X = (float)Math.Round(vec.X);
        vec.Y = (float)Math.Round(vec.Y);
    }

    string[] _prefixes = new string[]
    {
"Y",
"Z",
"E",
"P",
"T",
"G",
"M",
"k",
    };

    double[] _factors = new double[]
    {
1e24,
1e21,
1e18,
1e15,
1e12,
1e9,
1e6,
1e3,
    };

    void PrefixRangeWithMetricUnits(double num, string unit, int digits, out string numStr, out string halfNumStr)
    {
        string prefix = "";

        for (int i = 0; i < _factors.Length; ++i)
        {
            double factor = _factors[i];

            if (num >= factor)
            {
                prefix = _prefixes[i];
                num /= factor;
                break;
            }
        }

        numStr = (prefix == "" ? num.ToString("n0") : num.ToString($"n{digits}")) + $" {prefix}{unit}";
        num *= 0.5;
        halfNumStr = (prefix == "" ? num.ToString("n0") : num.ToString($"n{digits}")) + $" {prefix}{unit}";
    }
}
#endregion

#region Ini stuff
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

    textSurfaceIni.Clear();
    bool parsed = textSurfaceIni.TryParse(block.CustomData);

    if (!parsed && !string.IsNullOrWhiteSpace(block.CustomData))
    {
        textSurfaceIni.EndContent = block.CustomData;
    }

    int surfaceCount = surfaceProvider.SurfaceCount;
    for (int i = 0; i < surfaceCount; ++i)
    {
        string iniKey = string.Format(INI_TEXT_SURFACE_TEMPLATE, i);
        bool display = textSurfaceIni.Get(INI_SECTION_TEXT_SURF_PROVIDER, iniKey).ToBoolean(i == 0 && !(block is IMyProgrammableBlock));
        if (display)
        {
            textSurfaces.Add(surfaceProvider.GetSurface(i));
        }

        textSurfaceIni.Set(INI_SECTION_TEXT_SURF_PROVIDER, iniKey, display);
    }

    string output = textSurfaceIni.ToString();
    if (!string.Equals(output, block.CustomData))
        block.CustomData = output;
}

void WriteCustomDataIni()
{
    generalIni.Set(INI_SECTION_GENERAL, INI_RADAR_NAME, textPanelName);
    generalIni.Set(INI_SECTION_GENERAL, INI_BCAST, broadcastIFF);
    generalIni.Set(INI_SECTION_GENERAL, INI_NETWORK, networkTargets);
    generalIni.Set(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE, useRangeOverride);
    generalIni.Set(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE, rangeOverride);
    generalIni.Set(INI_SECTION_GENERAL, INI_PROJ_ANGLE, projectionAngle);
    generalIni.Set(INI_SECTION_GENERAL, INI_DRAW_QUADRANTS, drawQuadrants);
    generalIni.Set(INI_SECTION_GENERAL, INI_REF_NAME, referenceName);
    generalIni.Set(INI_SECTION_GENERAL, INI_DRAW_RUNNING_SCREEN, drawRunningScreen);

    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_TITLE_BAR, titleBarColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_TEXT, textColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_BACKGROUND, backColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_RADAR_LINES, lineColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_PLANE, planeColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_ENEMY, enemyIconColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_ENEMY_ELEVATION, enemyElevationColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_NEUTRAL, neutralIconColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_NEUTRAL_ELEVATION, neutralElevationColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_FRIENDLY, allyIconColor, generalIni);
    MyIniHelper.SetColor(INI_SECTION_COLORS, INI_FRIENDLY_ELEVATION, allyElevationColor, generalIni);
    generalIni.SetSectionComment(INI_SECTION_COLORS, "Colors are defined with RGBAlpha color codes where\nvalues can range from 0,0,0,0 [transparent] to 255,255,255,255 [white].");

    string output = generalIni.ToString();
    if (!string.Equals(output, Me.CustomData))
        Me.CustomData = output;
}

void ParseCustomDataIni()
{
    generalIni.Clear();

    if (generalIni.TryParse(Me.CustomData))
    {
        textPanelName = generalIni.Get(INI_SECTION_GENERAL, INI_RADAR_NAME).ToString(textPanelName);
        referenceName = generalIni.Get(INI_SECTION_GENERAL, INI_REF_NAME).ToString(referenceName);
        broadcastIFF = generalIni.Get(INI_SECTION_GENERAL, INI_BCAST).ToBoolean(broadcastIFF);
        networkTargets = generalIni.Get(INI_SECTION_GENERAL, INI_NETWORK).ToBoolean(networkTargets);
        useRangeOverride = generalIni.Get(INI_SECTION_GENERAL, INI_USE_RANGE_OVERRIDE).ToBoolean(useRangeOverride);
        rangeOverride = generalIni.Get(INI_SECTION_GENERAL, INI_RANGE_OVERRIDE).ToSingle(rangeOverride);
        projectionAngle = generalIni.Get(INI_SECTION_GENERAL, INI_PROJ_ANGLE).ToSingle(projectionAngle);
        drawQuadrants = generalIni.Get(INI_SECTION_GENERAL, INI_DRAW_QUADRANTS).ToBoolean(drawQuadrants);
        drawRunningScreen = generalIni.Get(INI_SECTION_GENERAL, INI_DRAW_RUNNING_SCREEN).ToBoolean(drawRunningScreen);

        titleBarColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_TITLE_BAR, generalIni, titleBarColor);
        textColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_TEXT, generalIni, textColor);
        backColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_BACKGROUND, generalIni, backColor);
        lineColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_RADAR_LINES, generalIni, lineColor);
        planeColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_PLANE, generalIni, planeColor);
        enemyIconColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_ENEMY, generalIni, enemyIconColor);
        enemyElevationColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_ENEMY_ELEVATION, generalIni, enemyElevationColor);
        neutralIconColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_NEUTRAL, generalIni, neutralIconColor);
        neutralElevationColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_NEUTRAL_ELEVATION, generalIni, neutralElevationColor);
        allyIconColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_FRIENDLY, generalIni, allyIconColor);
        allyElevationColor = MyIniHelper.GetColor(INI_SECTION_COLORS, INI_FRIENDLY_ELEVATION, generalIni, allyElevationColor);
    }
    else if (!string.IsNullOrWhiteSpace(Me.CustomData))
    {
        generalIni.EndContent = Me.CustomData;
    }

    WriteCustomDataIni();

    if (radarSurface != null)
    {
        radarSurface.UpdateFields(titleBarColor, backColor, lineColor, planeColor, textColor, missileLockColor, projectionAngle, MaxRange, drawQuadrants);
    }
}

public static class MyIniHelper
{
    /// <summary>
    /// Adds a color character to a MyIni object
    /// </summary>
    public static void SetColor(string sectionName, string itemName, Color color, MyIni ini)
    {

        string colorString = string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A);

        ini.Set(sectionName, itemName, colorString);
    }

    /// <summary>
    /// Parses a MyIni for a color character
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

#region General Functions
//Whip's Running Symbol Method v8
//•
int runningSymbolVariant = 0;
int runningSymbolCount = 0;
const int increment = 1;
string[] runningSymbols = new string[] { ".", "..", "...", "....", "...", "..", ".", "" };

string RunningSymbol()
{
    if (runningSymbolCount >= increment)
    {
        runningSymbolCount = 0;
        runningSymbolVariant++;
        if (runningSymbolVariant >= runningSymbols.Length)
            runningSymbolVariant = 0;
    }
    runningSymbolCount++;
    return runningSymbols[runningSymbolVariant];
}

IMyShipController GetControlledShipController(List<IMyShipController> SCs)
{
    foreach (IMyShipController thisController in SCs)
    {
        if (thisController.Closed)
            continue;

        if (thisController.IsUnderControl && thisController.CanControlShip)
            return thisController;
    }

    return null;
}

float GetMaxTurretRange(List<TurretInterface> turrets)
{
    float maxRange = 0;
    foreach (var block in turrets)
    {
        if (block.Closed)
            continue;

        if (!block.IsWorking)
            continue;

        float thisRange = block.Range;
        if (thisRange > maxRange)
        {
            maxRange = thisRange;
        }
    }
    return maxRange;
}

public static bool StringContains(string source, string toCheck, StringComparison comp = StringComparison.OrdinalIgnoreCase)
{
    return source?.IndexOf(toCheck, comp) >= 0;
}
#endregion

#region Block Fetching
bool PopulateLists(IMyTerminalBlock block)
{
    if (!block.IsSameConstructAs(Me))
        return false;

    myGridIds.Add(block.CubeGrid.EntityId);

    if (StringContains(block.CustomName, textPanelName))
    {
        AddTextSurfaces(block, textSurfaces);
    }

    var turret = block as IMyLargeTurretBase;
    if (turret != null)
    {
        turrets.Add(new TurretInterface(turret));
        return false;
    }
    
    var tcb = block as IMyTurretControlBlock;
    if (tcb != null)
    {
        turrets.Add(new TurretInterface(tcb));
        return false;
    }

    var controller = block as IMyShipController;
    if (controller != null)
    {
        allControllers.Add(controller);
        if (StringContains(block.CustomName, referenceName))
            taggedControllers.Add(controller);
        return false;
    }

    var sensor = block as IMySensorBlock;
    if (sensor != null)
    {
        sensors.Add(sensor);
        return false;
    }

    return false;
}

void GrabBlocks()
{
    // This forces sprites to redraw by clearing the cache
    _clearSpriteCache = !_clearSpriteCache;

    myGridIds.Clear();
    sensors.Clear();
    turrets.Clear();
    allControllers.Clear();
    taggedControllers.Clear();
    textSurfaces.Clear();

    _compositeBoundingSphere.FetchCubeGrids();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(null, PopulateLists);

    if (sensors.Count == 0)
        Log.Info($"No sensors found (not an error).");

    if (turrets.Count == 0)
        Log.Warning($"No turrets found. You will only be able to see targets that are broadcast by allies.");

    if (textSurfaces.Count == 0)
        Log.Error($"No text panels or text surface providers with name tag '{textPanelName}' were found.");

    if (allControllers.Count == 0)
        Log.Warning($"No ship controllers were found. Using orientation of this block...");
    else
    {
        if (taggedControllers.Count == 0)
            Log.Info($"No ship controllers named \"{referenceName}\" were found. Using all available ship controllers. (This is NOT an error!)");
        else
            Log.Info($"One or more ship controllers with name tag \"{referenceName}\" were found. Using these to orient the radar.");
    }

    lastSetupResult = Log.Write();

    if (textSurfaces.Count == 0)
        isSetup = false;
    else
    {
        isSetup = true;
        ParseCustomDataIni();
    }
}
#endregion

#region Scheduler
/// <summary>
/// Class for scheduling actions to occur at specific frequencies. Actions can be updated in parallel or in sequence (queued).
/// </summary>
public class Scheduler
{
    readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    readonly List<ScheduledAction> _actionsToDispose = new List<ScheduledAction>();
    Queue<ScheduledAction> _queuedActions = new Queue<ScheduledAction>();
    const double runtimeToRealtime = (1.0 / 60.0) / 0.0166666;
    private readonly Program _program;
    private ScheduledAction _currentlyQueuedAction = null;

    /// <summary>
    /// Constructs a scheduler object with timing based on the runtime of the input program.
    /// </summary>
    /// <param name="program"></param>
    public Scheduler(Program program)
    {
        _program = program;
    }

    /// <summary>
    /// Updates all ScheduledAcions in the schedule and the queue.
    /// </summary>
    public void Update()
    {
        double deltaTime = Math.Max(0, _program.Runtime.TimeSinceLastRun.TotalSeconds * runtimeToRealtime);

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
                // If we should recycle, add it to the end of the queue
                if (!_currentlyQueuedAction.DisposeAfterRun)
                    _queuedActions.Enqueue(_currentlyQueuedAction);

                // Set the queued action to null for the next cycle
                _currentlyQueuedAction = null;
            }
        }
    }

    /// <summary>
    /// Adds an Action to the schedule. All actions are updated each update call.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="updateFrequency"></param>
    /// <param name="disposeAfterRun"></param>
    public void AddScheduledAction(Action action, double updateFrequency, bool disposeAfterRun = false)
    {
        ScheduledAction scheduledAction = new ScheduledAction(action, updateFrequency, disposeAfterRun);
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the schedule. All actions are updated each update call.
    /// </summary>
    /// <param name="scheduledAction"></param>
    public void AddScheduledAction(ScheduledAction scheduledAction)
    {
        _scheduledActions.Add(scheduledAction);
    }

    /// <summary>
    /// Adds an Action to the queue. Queue is FIFO.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="updateInterval"></param>
    /// <param name="disposeAfterRun"></param>
    public void AddQueuedAction(Action action, double updateInterval, bool disposeAfterRun = false)
    {
        if (updateInterval <= 0)
        {
            updateInterval = 0.001; // avoids divide by zero
        }
        ScheduledAction scheduledAction = new ScheduledAction(action, 1.0 / updateInterval, disposeAfterRun);
        _queuedActions.Enqueue(scheduledAction);
    }

    /// <summary>
    /// Adds a ScheduledAction to the queue. Queue is FIFO.
    /// </summary>
    /// <param name="scheduledAction"></param>
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

    private readonly double _runFrequency;
    private readonly Action _action;
    protected bool _justRun = false;

    /// <summary>
    /// Class for scheduling an action to occur at a specified frequency (in Hz).
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="runFrequency">How often to run in Hz</param>
    public ScheduledAction(Action action, double runFrequency, bool removeAfterRun = false)
    {
        _action = action;
        _runFrequency = runFrequency;
        RunInterval = 1.0 / _runFrequency;
        DisposeAfterRun = removeAfterRun;
    }

    public virtual void Update(double deltaTime)
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
        //WriteLine($"Error count: {_errorList.Count}");
        //WriteLine($"Warning count: {_warningList.Count}");
        //WriteLine($"Info count: {_infoList.Count}");

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

        string[] wrappedSplit = content.Split('\n');

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
#endregion

#region Runtime Tracking
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
    public double LastRuntime { get; private set; }
    public double LastInstructions { get; private set; }

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


public class CompositeBoundingSphere
{
    public double Radius
    {
        get
        {
            return _sphere.Radius;
        }
    }

    public Vector3D Center
    {
        get
        {
            return _sphere.Center;
        }
    }

    public IMyCubeGrid LargestGrid = null;

    BoundingSphereD _sphere;

    Program _program;
    HashSet<IMyCubeGrid> _grids = new HashSet<IMyCubeGrid>();
    Vector3D _compositePosLocal = Vector3D.Zero;
    double _compositeRadius = 0;

    public CompositeBoundingSphere(Program program)
    {
        _program = program;
    }

    public void FetchCubeGrids()
    {
        _grids.Clear();
        _grids.Add(_program.Me.CubeGrid);
        LargestGrid = _program.Me.CubeGrid;
        _program.GridTerminalSystem.GetBlocksOfType<IMyMechanicalConnectionBlock>(null, CollectGrids);
        RecomputeCompositeProperties();
    }

    public void Compute(bool fullCompute = false)
    {
        if (fullCompute)
        {
            RecomputeCompositeProperties();
        }
        Vector3D compositePosWorld = _program.Me.GetPosition() + Vector3D.TransformNormal(_compositePosLocal, _program.Me.WorldMatrix);
        _sphere = new BoundingSphereD(compositePosWorld, _compositeRadius);
    }

    void RecomputeCompositeProperties()
    {
        bool first = true;
        Vector3D compositeCenter = Vector3D.Zero;
        double compositeRadius = 0;
        foreach (var g in _grids)
        {
            Vector3D currentCenter = g.WorldVolume.Center;
            double currentRadius = g.WorldVolume.Radius;
            if (first)
            {
                compositeCenter = currentCenter;
                compositeRadius = currentRadius;
                first = false;
                continue;
            }
            Vector3D diff = currentCenter - compositeCenter;
            double diffLen = diff.Normalize();
            double newDiameter = currentRadius + diffLen + compositeRadius;
            double newRadius = 0.5 * newDiameter;
            if (newRadius > compositeRadius)
            {
                double diffScale = (newRadius - compositeRadius);
                compositeRadius = newRadius;
                compositeCenter += diffScale * diff;
            }
        }
        // Convert to local space
        Vector3D directionToCompositeCenter = compositeCenter - _program.Me.GetPosition();
        _compositePosLocal = Vector3D.TransformNormal(directionToCompositeCenter, MatrixD.Transpose(_program.Me.WorldMatrix));
        _compositeRadius = compositeRadius;
    }

    bool CollectGrids(IMyTerminalBlock b)
    {
        if (!b.IsSameConstructAs(_program.Me))
        {
            return false;
        }

        var mech = (IMyMechanicalConnectionBlock)b;
        if (mech.CubeGrid.WorldVolume.Radius > LargestGrid.WorldVolume.Radius)
        {
            LargestGrid = mech.CubeGrid;
        }
        _grids.Add(mech.CubeGrid);
        if (mech.IsAttached)
        {
            _grids.Add(mech.TopGrid);
        }
        return false;
    }
}

#region Running Screen Manager
class RadarRunningScreenManager
{
    readonly Color _topBarColor = new Color(25, 25, 25);
    readonly Color _white = new Color(200, 200, 200);
    readonly Color _grey = new Color(150, 150, 150);
    readonly Color _darkGrey = new Color(100, 100, 100);
    readonly Color _black = Color.Black;

    const TextAlignment Center = TextAlignment.CENTER;
    const SpriteType Texture = SpriteType.TEXTURE;
    const float TitleBarHeightPx = 64f;
    const float TextSize = 1.3f;
    const float BaseTextHeightPx = 37f;
    const float SpriteScale = 1.5f;
    const string Font = "Debug";
    const string TitleFormat = "Whip's Turret Radar - v{0}";
    readonly string _titleText;
    readonly Vector2 _spritePosition = new Vector2(0, 20);

    Program _program;

    int _idx = 0;

    struct AnimationParams
    {
        public readonly float Angle;
        public readonly byte Alpha1;
        public readonly byte Alpha2;
        public readonly byte Alpha3;

        public AnimationParams(float angle, byte alpha1, byte alpha2, byte alpha3)
        {
            Angle = angle;
            Alpha1 = alpha1;
            Alpha2 = alpha2;
            Alpha3 = alpha3;
        }
    }

    AnimationParams[] _animSequence = new AnimationParams[] {
        new AnimationParams(  0f,   0,   0, 140),
        new AnimationParams( 15f,   0,   0, 120),
        new AnimationParams( 30f,   0,   0, 100),
        new AnimationParams( 45f, 255,   0,  80), // contact 1
        new AnimationParams( 60f, 245,   0,  60),
        new AnimationParams( 75f, 220,   0,  40),
        new AnimationParams( 90f, 200,   0,  20),
        new AnimationParams(105f, 180,   0,   0),
        new AnimationParams(120f, 160,   0,   0),
        new AnimationParams(135f, 140, 255,   0), // contact 2
        new AnimationParams(150f, 120, 245,   0),
        new AnimationParams(165f, 100, 220,   0),
        new AnimationParams(180f,  80, 200,   0),
        new AnimationParams(195f,  60, 180,   0),
        new AnimationParams(210f,  40, 160,   0),
        new AnimationParams(225f,  20, 140,   0),
        new AnimationParams(240f,   0, 120,   0),
        new AnimationParams(255f,   0, 100,   0),
        new AnimationParams(270f,   0,  80, 255), // contact 3
        new AnimationParams(285f,   0,  60, 245),
        new AnimationParams(300f,   0,  40, 220),
        new AnimationParams(315f,   0,  20, 200),
        new AnimationParams(330f,   0,   0, 180),
        new AnimationParams(345f,   0,   0, 160),
    };

    bool _clearSpriteCache = false;
    IMyTextSurface _surface = null;

    public RadarRunningScreenManager(string version, Program program)
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
        float angle = MathHelper.ToRadians(anim.Angle);


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
            
            Vector2 pos = _spritePosition * minScale + screenCenter;
            DrawRadarFrame(frame, pos, minScale * SpriteScale);
            DrawContacts(frame, pos, minScale * SpriteScale, anim.Alpha1, anim.Alpha2, anim.Alpha3);
            DrawRadarSweep(frame, pos, minScale * SpriteScale, angle);
            
            DrawTitleBar(_surface, frame, minScale);
        }
    }

    public void RestartDraw()
    {
        _clearSpriteCache = !_clearSpriteCache;
    }

    #region Draw Helper Functions
    void DrawTitleBar(IMyTextSurface surface, MySpriteDrawFrame frame, float scale)
    {
        float titleBarHeight = scale * TitleBarHeightPx;
        Vector2 topLeft = 0.5f * (surface.TextureSize - surface.SurfaceSize);
        Vector2 titleBarSize = new Vector2(surface.TextureSize.X, titleBarHeight);
        Vector2 titleBarPos = topLeft + new Vector2(surface.TextureSize.X * 0.5f, titleBarHeight * 0.5f);
        Vector2 titleBarTextPos = topLeft + new Vector2(surface.TextureSize.X * 0.5f, 0.5f * (titleBarHeight - scale * BaseTextHeightPx));

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

    void SetupDrawSurface(IMyTextSurface surface)
    {
        surface.ScriptBackgroundColor = _black;
        surface.ContentType = ContentType.SCRIPT;
        surface.Script = "";
    }

    void DrawRadarFrame(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f)
    {
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(0f,0f)*scale+centerPos, new Vector2(200f,200f)*scale, _white, null, TextAlignment.CENTER, 0f)); // circle
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(0f,0f)*scale+centerPos, new Vector2(190f,190f)*scale, _black, null, TextAlignment.CENTER, 0f)); // circle inner
    }
    
    void DrawContacts(MySpriteDrawFrame frame, Vector2 centerPos, float scale, byte contact1Alpha, byte contact2Alpha, byte contact3Alpha)
    {
        Color contact1Color, contact2Color, contact3Color; 
        contact1Color = contact2Color = contact3Color = _white;
        contact1Color.A = contact1Alpha;
        contact2Color.A = contact2Alpha;
        contact3Color.A = contact3Alpha;
        
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-30f,30f)*scale+centerPos, new Vector2(10f,10f)*scale, contact1Color, null, TextAlignment.CENTER, 0f)); // contact 1
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(-50f,-50f)*scale+centerPos, new Vector2(10f,10f)*scale, contact2Color, null, TextAlignment.CENTER, 0f)); // contact 2
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(50f,0f)*scale+centerPos, new Vector2(10f,10f)*scale, contact3Color, null, TextAlignment.CENTER, 0f)); // contact 3
    }
    
    void DrawRadarSweep(MySpriteDrawFrame frame, Vector2 centerPos, float scale = 1f, float rotation = 0f)
    {
        float sin = (float)Math.Sin(rotation);
        float cos = (float)Math.Cos(rotation);
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos*9f-sin*44f,sin*9f+cos*44f)*scale+centerPos, new Vector2(10f,90f)*scale, _darkGrey, null, TextAlignment.CENTER, -0.2094f+rotation)); // line trailing 2
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos*5f-sin*45f,sin*5f+cos*45f)*scale+centerPos, new Vector2(10f,90f)*scale, _grey, null, TextAlignment.CENTER, -0.1047f+rotation)); // line trailing 1
        frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", new Vector2(cos*0f-sin*45f,sin*0f+cos*45f)*scale+centerPos, new Vector2(10f,90f)*scale, _white, null, TextAlignment.CENTER, 0f+rotation)); // line
        frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", new Vector2(cos*0f-sin*0f,sin*0f+cos*0f)*scale+centerPos, new Vector2(10f,10f)*scale, _white, null, TextAlignment.CENTER, 0f+rotation)); // center dot
    }

    #endregion
}
#endregion

public class TurretInterface
{
    public IMyTurretControlBlock TCB { get; private set; } = null;
    public IMyLargeTurretBase T { get; private set; } = null;

    List<IMyFunctionalBlock> _tools = new List<IMyFunctionalBlock>();

    private TurretInterface() { }

    public TurretInterface(IMyLargeTurretBase t)
    {
        T = t;
    }

    public TurretInterface(IMyTurretControlBlock tcb)
    {
        TCB = tcb;
        TCB.GetTools(_tools);
    }

    public MyDetectedEntityInfo GetTargetedEntity()
    {
        return T != null ? T.GetTargetedEntity() : TCB.GetTargetedEntity();
    }
    
    public float Range
    {
        get
        {
            return T != null ? T.Range : TCB.Range;
        }
    }

    public bool Closed
    {
        get
        {
            return T != null ? T.Closed : TCB.Closed;
        }
    }

    public bool IsWorking
    {
        get
        {
            return T != null ? T.IsWorking : TCB.IsWorking && (TCB.AzimuthRotor != null || TCB.ElevationRotor != null) && (_tools.Count > 0 || TCB.Camera != null);
        }
    }

    public bool HasTarget
    {
        get
        {
            return T != null ? T.HasTarget : TCB.HasTarget;
        }
    }

    public Vector3D WorldPos
    {
        get
        {
            if (T != null)
                return T.GetPosition();
            var ds = TCB.GetDirectionSource();
            if (ds != null)
                return ds.GetPosition();
            return Vector3D.Zero;
        }
    }
}

#endregion

